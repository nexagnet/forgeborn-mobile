using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ForgeBorn.Domain.Commands;
using ForgeBorn.Domain.Geometry;
using ForgeBorn.Domain.Material;
using ForgeBorn.Domain.Persistence;
using ForgeBorn.Domain.Session;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ForgeBorn.UnityLayer
{
    public enum InteractionMode { Work, Inspect }

    /// <summary>
    /// M1 geometry bench: one tapered blank, finger grinding (WORK) or rotating the part (INSPECT), mass meter.
    /// Touch positions become blade-local rays in meters; the Domain never sees pixels. Everything is built from code
    /// so the scene is just an empty bootstrap.
    /// </summary>
    public sealed class ForgeSandbox : MonoBehaviour
    {
        private const int Stations = 192;
        private const int Perimeter = 48;
        private const double BladeLengthM = 0.8;
        private const double WheelRpm = 3000.0;
        private const float MinPressureN = 20f, MaxPressureN = 150f, PressureStepN = 10f;
        private float _pressureN = 80f; // on-screen pressure control: iPhones since the 11 have no force touch

        private Camera _cam;
        private Transform _bladeRoot;
        private MeshFilter _meshFilter;
        private ForgeSession _session;
        private BladeMeshBuilder _builder;
        private double _startMassKg;
        private long _nextCommandId = 1;

        private InteractionMode _mode = InteractionMode.Work;
        private readonly AbrasiveWheel[] _wheels = { AbrasiveWheel.Coarse, AbrasiveWheel.Medium, AbrasiveWheel.Fine };
        private int _wheelIndex = 1;

        private bool _hasPrev;
        private GrindSample _prev;
        private Vector2 _lastInspectPos;
        private bool _hasInspectPrev;

        private Text _massText, _statusText, _modeLabel, _wheelLabel;
        private readonly Stopwatch _sw = new Stopwatch();
        private string _status = "";

        private static string SavePath => Path.Combine(Application.persistentDataPath, "weapon_slot1.fbws");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindFirstObjectByType<ForgeSandbox>() == null)
                new GameObject("ForgeSandbox").AddComponent<ForgeSandbox>();
        }

        private void Start()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            BuildScene();
            NewWeapon();
            BuildHud();
            RefreshHud();
        }

        // ---------------------------------------------------------------- scene

        private void BuildScene()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                _cam = go.AddComponent<Camera>();
            }
            _cam.orthographic = true;
            _cam.orthographicSize = 0.55f;
            _cam.transform.position = new Vector3(0f, 0f, -2f);
            _cam.transform.rotation = Quaternion.identity;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 10f;

            var lightGo = new GameObject("Key Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -25f, 0f);

            var root = new GameObject("Blade");
            _bladeRoot = root.transform;
            ResetView();
            _meshFilter = root.AddComponent<MeshFilter>();
            var mr = root.AddComponent<MeshRenderer>();
            mr.sharedMaterial = LoadBladeMaterial();
        }

        private static Material LoadBladeMaterial()
        {
            var m = Resources.Load<Material>("BladeMat");
            if (m != null) return m;
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            var mat = new Material(sh);
            mat.color = new Color(0.62f, 0.64f, 0.68f);
            return mat;
        }

        private void ResetView()
        {
            // Blade length (+X local) points up on the portrait screen; flat face toward the camera.
            _bladeRoot.SetPositionAndRotation(new Vector3(0f, -(float)BladeLengthM * 0.5f, 0f), Quaternion.Euler(0f, 0f, 90f));
        }

        private void NewWeapon()
        {
            BladeProfile blade = BladeFactory.CreateTapered(
                BladeLengthM,
                x => 0.025 - 0.010 * x / BladeLengthM,
                x => 0.004 - 0.002 * x / BladeLengthM,
                Stations, Perimeter);
            AttachSession(new ForgeSession(blade, MaterialData.SteelBasicV1));
            _startMassKg = _session.MassKg;
        }

        private void AttachSession(ForgeSession s)
        {
            _session = s;
            _builder = new BladeMeshBuilder(s.Profile);
            _meshFilter.sharedMesh = _builder.Mesh;
        }

        // ---------------------------------------------------------------- update / input

        private void Update()
        {
            Vector2 pos;
            int fingerId;
            bool down = TryGetPointer(out pos, out fingerId);

            if (down && IsOverUi(fingerId)) down = false;

            if (!down)
            {
                _hasPrev = false;
                _hasInspectPrev = false;
            }
            else if (_mode == InteractionMode.Work)
            {
                WorkStep(pos, _pressureN);
            }
            else
            {
                InspectStep(pos);
            }
            RefreshHud();
        }

        private static bool TryGetPointer(out Vector2 pos, out int fingerId)
        {
            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                pos = t.position;
                fingerId = t.fingerId;
                return t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled;
            }
            pos = Input.mousePosition;
            fingerId = -1;
            return Input.GetMouseButton(0);
        }

        private static bool IsOverUi(int fingerId)
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(fingerId);
        }

        private void WorkStep(Vector2 screenPos, float pressureN)
        {
            Ray r = _cam.ScreenPointToRay(screenPos);
            Vector3 o = _bladeRoot.InverseTransformPoint(r.origin);
            Vector3 d = _bladeRoot.InverseTransformDirection(r.direction);
            // Quantize pressure (5 N steps) so the same gesture is the same command on every device.
            double pq = System.Math.Round(pressureN / 5.0) * 5.0;
            double dt = System.Math.Min(Time.deltaTime, 0.05);
            var cur = new GrindSample(new Vec3d(o.x, o.y, o.z), new Vec3d(d.x, d.y, d.z), pq, dt);

            if (_hasPrev)
            {
                var a = new GrindSample(_prev.RayOrigin, _prev.RayDirection, _prev.PressureN, 0.0);
                var samples = new List<GrindSample>(2) { a, cur };
                var cmd = new GrindStrokeCommand(_nextCommandId++, Time.timeAsDouble, _wheels[_wheelIndex], WheelRpm, samples);
                _sw.Restart();
                GrindResult res = _session.ApplyGrind(cmd);
                _sw.Stop();
                if (!res.Accepted) _status = "rejected: " + res.Reason;
                else
                {
                    if (res.LastStation >= 0) _builder.Rebuild(res.FirstStation, res.LastStation);
                    _status = res.ContactSubsteps > 0 ? "grind " + _sw.Elapsed.TotalMilliseconds.ToString("F2") + " ms" : "no contact";
                }
            }
            _prev = cur;
            _hasPrev = true;
        }

        private void InspectStep(Vector2 pos)
        {
            if (_hasInspectPrev)
            {
                Vector2 delta = pos - _lastInspectPos;
                _bladeRoot.Rotate(_bladeRoot.TransformDirection(Vector3.right), -delta.x * 0.4f, Space.World); // roll about blade length
                _bladeRoot.Rotate(Vector3.right, delta.y * 0.4f, Space.World);
            }
            _lastInspectPos = pos;
            _hasInspectPrev = true;
        }

        // ---------------------------------------------------------------- HUD

        private void BuildHud()
        {
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            float dpScale = Mathf.Max(1f, (Screen.dpi > 0f ? Screen.dpi : 160f) / 160f);
            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; // 1 canvas unit = 1 dp
            scaler.scaleFactor = dpScale;

            var safe = new GameObject("SafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
            safe.SetParent(canvasGo.transform, false);
            Rect sa = Screen.safeArea;
            safe.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            safe.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;

            _massText = MakeText(safe, "Mass", 20, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -76), new Vector2(-12, -12));
            _statusText = MakeText(safe, "Status", 13, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(1, 0), new Vector2(12, 156), new Vector2(-12, 184));

            // Four >= 56 dp high buttons, each a quarter of the width.
            _modeLabel = MakeButton(safe, 0, "WORK", ToggleMode);
            _wheelLabel = MakeButton(safe, 1, "Wheel", CycleWheel);
            MakeButton(safe, 2, "Save", Save);
            MakeButton(safe, 3, "Load", Load);
            MakeButton(safe, 0, "Press -", () => _pressureN = Mathf.Max(MinPressureN, _pressureN - PressureStepN), 1);
            MakeButton(safe, 1, "Press +", () => _pressureN = Mathf.Min(MaxPressureN, _pressureN + PressureStepN), 1);
        }

        private static Text MakeText(RectTransform parent, string name, int size, TextAnchor anchor, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Color.white;
            t.raycastTarget = false;
            return t;
        }

        private static Text MakeButton(RectTransform parent, int slot, string label, UnityEngine.Events.UnityAction onClick, int row = 0)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(slot * 0.25f, 0f);
            rt.anchorMax = new Vector2((slot + 1) * 0.25f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            float y = 12f + row * 68f;
            rt.offsetMin = new Vector2(4f, y);
            rt.offsetMax = new Vector2(-4f, y + 60f);
            go.GetComponent<Image>().color = new Color(0.25f, 0.27f, 0.32f, 0.95f);
            go.GetComponent<Button>().onClick.AddListener(onClick);
            Text t = MakeText(rt, "Label", 15, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            t.text = label;
            return t;
        }

        private void RefreshHud()
        {
            if (_massText == null) return;
            double mass = _session.MassKg;
            double removed = _startMassKg - mass;
            _massText.text = "Mass " + mass.ToString("F3") + " kg\nRemoved " + (removed * 1000.0).ToString("F1") + " g";
            _statusText.text = _status;
            _modeLabel.text = _mode == InteractionMode.Work ? "WORK" : "INSPECT";
            _wheelLabel.text = _wheels[_wheelIndex].Id.Replace("wheel_", "").Replace("_v1", "");
        }

        private void ToggleMode()
        {
            _mode = _mode == InteractionMode.Work ? InteractionMode.Inspect : InteractionMode.Work;
            _hasPrev = false;
            if (_mode == InteractionMode.Work) ResetView(); // grinding is done on the face-on view
        }

        private void CycleWheel() { _wheelIndex = (_wheelIndex + 1) % _wheels.Length; }

        private void Save()
        {
            try
            {
                WeaponFile.SaveAtomic(SavePath, _session.Capture());
                _status = "saved";
            }
            catch (System.Exception e) { _status = "save failed: " + e.Message; }
        }

        private void Load()
        {
            WeaponSnapshot snap;
            string err;
            if (WeaponFile.TryLoadWithRecovery(SavePath, out snap, out err))
            {
                AttachSession(ForgeSession.FromSnapshot(snap));
                _status = "loaded";
            }
            else _status = "load failed: " + err;
        }
    }
}
