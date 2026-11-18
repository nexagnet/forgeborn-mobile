using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ForgeBorn.Domain.Commands;
using ForgeBorn.Domain.Evaluation;
using ForgeBorn.Domain.Geometry;
using ForgeBorn.Domain.Material;
using ForgeBorn.Domain.Persistence;
using ForgeBorn.Domain.Session;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ForgeBorn.UnityLayer
{
    public enum ToolMode { Grind, Hammer, Inspect }

    /// <summary>
    /// Forging bench: finger grinding, hammer blows, heat / quench / temper, blade report.
    /// Touch positions become blade-local rays in meters; the Domain never sees pixels. Everything is built from code
    /// so the scene is just an empty bootstrap. Pressure is an on-screen control (iPhones since the 11 have no force touch).
    /// </summary>
    public sealed class ForgeSandbox : MonoBehaviour
    {
        private const int Stations = 192;
        private const int Perimeter = 48;
        private const double BladeLengthM = 0.8;
        private const double WheelRpm = 3000.0;
        private const float PressureStepN = 10f;
        private const float ThermalTickS = 0.25f;
        private static readonly float[] ZoomSizes = { 0.78f, 0.39f, 0.2f };
        private const float BaseCamY = -0.15f; // keeps the blade between the info text (top) and the button rows (bottom)

        private float _pressureN = 80f;

        private PlatformProfile _profile;
        private Camera _cam;
        private Transform _bladeRoot;
        private MeshFilter _meshFilter;
        private ForgeSession _session;
        private BladeMeshBuilder _builder;
        private double _startMassKg;
        private long _nextCommandId = 1;
        private int _blankIndex;

        private ToolMode _tool = ToolMode.Grind;
        private readonly AbrasiveWheel[] _wheels = { AbrasiveWheel.Coarse, AbrasiveWheel.Medium, AbrasiveWheel.Fine };
        private readonly HammerFace[] _faces = { HammerFace.Flat, HammerFace.Peen };
        private int _wheelIndex = 1;
        private int _faceIndex;

        private ForgeEnv _env = ForgeEnv.Air;
        private int _speedIndex;
        private float _thermalAcc;
        private int _zoomIndex;
        private float _camY = BaseCamY;

        private bool _hasPrev;
        private GrindSample _prev;
        private Vector2 _lastScreenPos;
        private bool _hasScreenPrev;
        private bool _wasDown;
        private float _lastStrikeTime = -10f;

        private SparkEffect _sparks;
        private SteamEffect _steam;
        private readonly Vector3[] _steamPoints = new Vector3[12];
        private SandboxAudio _audio;
        private IHapticsService _haptics;

        private Text _infoText, _statusText, _toolLabel, _optionLabel, _speedLabel, _reportText;
        private Text[] _envLabels;
        private GameObject _reportPanel;
        private bool _showReport;
        private float _hudTimer;
        private readonly Stopwatch _sw = new Stopwatch();
        private string _status = "";

        // Guided first forge (see TutorialFlow).
        private readonly TutorialFlow _tutorial = new TutorialFlow();
        private int _hammerHits;
        private GameObject _tutPanel;
        private Text _tutStep, _tutTitle, _tutBody, _tutWarn;
        private Text _speedBtn, _reportBtn, _nextBtn;
        private float _pulse;

        private static string SavePath => Path.Combine(Application.persistentDataPath, "weapon_slot1.fbws");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindFirstObjectByType<ForgeSandbox>() == null)
                new GameObject("ForgeSandbox").AddComponent<ForgeSandbox>();
        }

        private bool _initialized;

        private void Start()
        {
            Initialize();
        }

        // ---------------------------------------------------------------- capture / test hooks (used by the headless screenshot tool)

        public ForgeSession Session => _session;

        public HammerResult LastHammer { get; private set; }

        /// <summary>
        /// Drives the real input path with synthetic screen points (camera ray -> blade space -> Domain command), so the
        /// touch-to-geometry pipeline can be verified without a device. Returns the mass removed in kg.
        /// </summary>
        public double CaptureGrindDrag(Vector2 from, Vector2 to, int steps, float frameDt)
        {
            double before = _session.MassKg;
            _hasPrev = false;
            _hasScreenPrev = false;
            for (int i = 0; i <= steps; i++) GrindStep(Vector2.Lerp(from, to, i / (float)System.Math.Max(1, steps)), frameDt);
            _hasPrev = false;
            _hasScreenPrev = false;
            return before - _session.MassKg;
        }

        public void CaptureHammerScreen(Vector2 pos)
        {
            _lastStrikeTime = -10f;
            HammerStrike(pos);
        }

        public void CaptureSetTool(ToolMode tool) { _tool = tool; }

        public void CaptureSimulateSparks(float seconds) { _sparks.SimulateForCapture(seconds); }

        public void CaptureClearSteam() { _steam.Clear(); }

        /// <summary>Headless capture: emit one dt of steam for the current bath/temperature, then advance the particles.</summary>
        public void CaptureSimulateSteam(float seconds)
        {
            const int Steps = 8;
            for (int i = 0; i < Steps; i++)
            {
                TickSteam(seconds / Steps);
                _steam.SimulateForCapture(seconds / Steps);
            }
        }

        /// <summary>Runs the thermal model for simSeconds in the given environment and refreshes visuals (no real-time wait).</summary>
        public void CaptureThermal(ForgeEnv env, double simSeconds)
        {
            ThermalResult r = _session.ApplyThermal(new ThermalCommand(0, SandboxRules.EnvFor(env), simSeconds));
            _env = env;
            _status = r.Accepted ? "thermal " + env + " " + SandboxRules.F(simSeconds, 0) + " s" : "thermal rejected: " + r.Reason;
            _builder.RefreshState();
        }

        /// <summary>One hammer blow at a blade-local point on the top face (x along the blade, y across), straight down in blade space.</summary>
        public HammerResult CaptureHammer(double x, double y, double energyJ)
        {
            var cmd = new HammerStrikeCommand(_nextCommandId++, 0.0, new Vec3d(x, y, 1.0), new Vec3d(0, 0, -1), energyJ, _faces[_faceIndex]);
            HammerResult res = _session.ApplyHammer(cmd);
            if (res.StationsTouched > 0) { _builder.Rebuild(res.FirstStation, res.LastStation); _builder.RefreshState(); }
            return res;
        }

        public void CaptureShowReport(bool show, ToolMode tool)
        {
            _showReport = show;
            _reportPanel.SetActive(show);
            _tool = tool;
        }

        /// <summary>Forces the HUD to redraw now.</summary>
        public void CaptureRefreshHud()
        {
            RefreshHud();
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _profile = PlatformProfile.Current;
            Application.targetFrameRate = _profile.TargetFps;
            Screen.orientation = ScreenOrientation.Portrait;
            BuildScene();
            NewWeapon();
            BuildEffects();
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
            if (FindFirstObjectByType<AudioListener>() == null) _cam.gameObject.AddComponent<AudioListener>();
            _cam.orthographic = false;
            _cam.fieldOfView = SandboxEnvironment.FovDeg;
            PoseCamera();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 10f;

            var lightGo = new GameObject("Key Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -25f, 0f);
            SandboxEnvironment.Apply(_cam, _profile.Tier);

            var root = new GameObject("Blade");
            _bladeRoot = root.transform;
            ResetView();
            _meshFilter = root.AddComponent<MeshFilter>();
            var mr = root.AddComponent<MeshRenderer>();
            mr.sharedMaterial = CreateBladeMaterial();
        }

        private Material CreateBladeMaterial()
        {
            Shader sh = Resources.Load<Shader>("ForgeBornBladeSteel");
            if (sh != null && sh.isSupported)
            {
                var m = new Material(sh);
                m.SetFloat("_Brush", _profile.BrushedSteel ? 0.6f : 0f);
                if (RenderSettings.customReflectionTexture != null) m.SetTexture("_EnvCube", RenderSettings.customReflectionTexture);
                return m;
            }
            var legacy = Resources.Load<Material>("BladeMat");
            if (legacy != null) return legacy;
            Shader fallback = Shader.Find("Universal Render Pipeline/Lit");
            if (fallback == null) fallback = Shader.Find("Standard");
            return new Material(fallback) { color = new Color(0.62f, 0.64f, 0.68f) };
        }

        private void PoseCamera()
        {
            SandboxEnvironment.PoseCamera(_cam.transform, ZoomSizes[_zoomIndex], _camY);
        }

        private void ResetView()
        {
            // Blade length (+X local) points up on the portrait screen; flat face toward the camera.
            _bladeRoot.SetPositionAndRotation(new Vector3(0f, -(float)BladeLengthM * 0.5f, 0f), Quaternion.Euler(0f, 0f, 90f));
        }

        private void NewWeapon()
        {
            BladeProfile blade;
            if (_blankIndex == 0)
            {
                blade = BladeFactory.CreateTapered(BladeLengthM, x => 0.025 - 0.010 * x / BladeLengthM,
                    x => 0.004 - 0.002 * x / BladeLengthM, Stations, Perimeter);
            }
            else
            {
                // Single-edged practice bar with a ready bevel on the +Y edge (kink lands on a ring vertex).
                blade = BladeFactory.CreateBeveled(BladeLengthM, 0.05, 0.006, 0.0125, 0.001, Stations, Perimeter);
            }
            AttachSession(new ForgeSession(blade, MaterialData.SteelBasicV1));
            _startMassKg = _session.MassKg;
        }

        private void AttachSession(ForgeSession s)
        {
            _session = s;
            _builder = new BladeMeshBuilder(s.Profile);
            _meshFilter.sharedMesh = _builder.Mesh;
            _builder.RefreshState();
        }

        private void BuildEffects()
        {
            var sparkShader = Resources.Load<Shader>("ForgeBornSpark");
            var go = new GameObject("Sparks");
            _sparks = go.AddComponent<SparkEffect>();
            _sparks.Setup(sparkShader != null ? new Material(sparkShader) : null, _profile.MaxSparkParticles);

            var steamShader = Resources.Load<Shader>("ForgeBornSteam");
            _steam = new GameObject("Steam").AddComponent<SteamEffect>();
            _steam.Setup(steamShader != null ? new Material(steamShader) : null, Mathf.Max(48, _profile.MaxSparkParticles));

            var audioGo = new GameObject("Audio");
            _audio = audioGo.AddComponent<SandboxAudio>();
            _audio.Setup();

            _haptics = _profile.UseHandheldHaptics ? (IHapticsService)new HandheldHaptics() : new NullHaptics();
        }

        // ---------------------------------------------------------------- update / input

        private void Update()
        {
            Vector2 pos;
            int fingerId;
            bool down = TryGetPointer(out pos, out fingerId);
            if (down && IsOverUi(fingerId)) down = false;

            bool pressedNow = down && !_wasDown;
            _wasDown = down;

            if (!down)
            {
                _hasPrev = false;
                _hasScreenPrev = false;
                _audio.SetGrind(0f, 0f);
            }
            else if (_tool == ToolMode.Grind)
            {
                GrindStep(pos, Time.deltaTime);
            }
            else if (_tool == ToolMode.Hammer)
            {
                _audio.SetGrind(0f, 0f);
                if (pressedNow) HammerStrike(pos);
            }
            else
            {
                _audio.SetGrind(0f, 0f);
                InspectStep(pos);
            }

            TickThermal();
            TickSteam(Time.deltaTime);
            _pulse += Time.unscaledDeltaTime;

            _hudTimer -= Time.unscaledDeltaTime;
            if (_hudTimer <= 0f)
            {
                _hudTimer = 0.1f;
                RefreshHud();
            }
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

        private void ScreenToBladeRay(Vector2 screenPos, out Vector3 o, out Vector3 d)
        {
            Ray r = _cam.ScreenPointToRay(screenPos);
            o = _bladeRoot.InverseTransformPoint(r.origin);
            d = _bladeRoot.InverseTransformDirection(r.direction);
        }

        private Vector3? HitWorld(Vector3 o, Vector3 d)
        {
            BladeHit hit;
            if (!BladeRaycaster.Raycast(_session.Profile, new Vec3d(o.x, o.y, o.z), new Vec3d(d.x, d.y, d.z), 5.0, out hit)) return null;
            return _bladeRoot.TransformPoint(new Vector3((float)hit.X, (float)hit.PointYZ.X, (float)hit.PointYZ.Y));
        }

        // ---------------------------------------------------------------- grinding

        private void GrindStep(Vector2 screenPos, float frameDt)
        {
            Vector3 o, d;
            ScreenToBladeRay(screenPos, out o, out d);
            double pq = SandboxRules.QuantizePressure(_pressureN);
            double dt = System.Math.Min(frameDt, 0.05);
            var cur = new GrindSample(new Vec3d(o.x, o.y, o.z), new Vec3d(d.x, d.y, d.z), pq, dt);

            float speed = _hasScreenPrev ? (screenPos - _lastScreenPos).magnitude / Mathf.Max(frameDt, 1e-3f) / Mathf.Max(1, _cam.pixelHeight) : 0f;
            _lastScreenPos = screenPos;
            _hasScreenPrev = true;

            bool contact = false;
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
                    contact = res.ContactSubsteps > 0;
                    _status = contact ? "grind " + SandboxRules.F(_sw.Elapsed.TotalMilliseconds, 2) + " ms" : "no contact";
                }
            }
            _prev = cur;
            _hasPrev = true;

            if (contact)
            {
                float load = Mathf.InverseLerp((float)SandboxRules.MinPressureN, (float)SandboxRules.MaxPressureN, _pressureN);
                _audio.SetGrind(0.35f + 0.65f * load, Mathf.Clamp01(0.3f * load + speed * 0.5f));
                Vector3? w = HitWorld(o, d);
                if (w.HasValue && _profile.MaxSparkParticles > 0)
                {
                    float hot = Mathf.InverseLerp(700f, 1500f, (float)SandboxRules.MaxTempK(_session.Profile));
                    int count = Mathf.RoundToInt(Mathf.Lerp(1f, 4f, load) * (_profile.Tier == QualityTier.Low ? 0.5f : 1f));
                    _sparks.Emit(w.Value, Vector3.down + Vector3.right * (speed > 0.05f ? 0.5f : 0f), count,
                        Color.Lerp(new Color(1f, 0.8f, 0.4f), new Color(1f, 0.5f, 0.15f), hot));
                }
            }
            else
            {
                _audio.SetGrind(0f, 0f);
            }
        }

        // ---------------------------------------------------------------- hammering

        private void HammerStrike(Vector2 screenPos)
        {
            if (Time.time - _lastStrikeTime < 0.2f) return;
            _lastStrikeTime = Time.time;

            Vector3 o, d;
            ScreenToBladeRay(screenPos, out o, out d);
            double energy = SandboxRules.HammerEnergyJ(_pressureN);
            var cmd = new HammerStrikeCommand(_nextCommandId++, Time.timeAsDouble, new Vec3d(o.x, o.y, o.z),
                new Vec3d(d.x, d.y, d.z), energy, _faces[_faceIndex]);

            _sw.Restart();
            HammerResult res = _session.ApplyHammer(cmd);
            _sw.Stop();
            LastHammer = res;

            if (!res.Accepted) { _status = "rejected: " + res.Reason; return; }
            if (!res.Hit) { _status = "missed the blade"; return; }
            if (res.StationsTouched > 0 && !res.Cold) _hammerHits++;

            if (res.StationsTouched > 0)
            {
                _builder.Rebuild(res.FirstStation, res.LastStation);
                _builder.RefreshState(); // cold-work damage shows immediately
            }

            float e01 = Mathf.InverseLerp((float)SandboxRules.MinPressureN, (float)SandboxRules.MaxPressureN, (float)energy);
            float hot01 = Mathf.InverseLerp(700f, 1500f, (float)res.HitTempK);
            _audio.Impact(e01, hot01);
            _haptics.Impact(0.4f + 0.6f * e01);

            Vector3? w = HitWorld(o, d);
            if (w.HasValue)
            {
                int n = Mathf.RoundToInt(Mathf.Lerp(6f, 20f, e01) * (_profile.Tier == QualityTier.Low ? 0.5f : 1f));
                _sparks.Emit(w.Value, Vector3.up, n, Color.Lerp(new Color(1f, 0.85f, 0.5f), new Color(1f, 0.5f, 0.1f), hot01));
            }

            string temp = SandboxRules.F(res.HitTempK - 273.15, 0) + " C";
            if (res.StationsTouched == 0) _status = "hammer: no deformation (" + temp + ")";
            else if (res.Cold) _status = "COLD hammering at " + temp + ": dent " + SandboxRules.F(res.DepthM * 1000.0, 2) + " mm, crack risk +" + SandboxRules.F(res.DamageAdded, 2);
            else _status = "hammer at " + temp + ": dent " + SandboxRules.F(res.DepthM * 1000.0, 2) + " mm, " + SandboxRules.F(_sw.Elapsed.TotalMilliseconds, 2) + " ms";
        }

        // ---------------------------------------------------------------- inspect

        private void InspectStep(Vector2 pos)
        {
            if (_hasScreenPrev)
            {
                Vector2 delta = pos - _lastScreenPos;
                _bladeRoot.Rotate(_bladeRoot.TransformDirection(Vector3.right), -delta.x * 0.4f, Space.World); // roll about blade length
                _camY = Mathf.Clamp(_camY - delta.y * ZoomSizes[_zoomIndex] * 2f / Screen.height, -0.6f, 0.6f);
                PoseCamera();
            }
            _lastScreenPos = pos;
            _hasScreenPrev = true;
        }

        // ---------------------------------------------------------------- thermal

        private void TickThermal()
        {
            _thermalAcc += Time.deltaTime;
            if (_thermalAcc < ThermalTickS) return;
            double dt = _thermalAcc * SandboxRules.TimeScales[_speedIndex];
            _thermalAcc = 0f;

            ThermalEnvironment env = SandboxRules.EnvFor(_env);
            if (!SandboxRules.NeedsTick(_session.Profile, env, _env == ForgeEnv.Oven)) return;

            // Quenches need fine steps (the cooling rate at the nose decides hardness); air/oven/furnace tolerate coarse ones.
            _session.Thermal.StepsPerCommand = (_env == ForgeEnv.Water || _env == ForgeEnv.Oil) ? 2000.0 : 500.0;
            ThermalResult r = _session.ApplyThermal(new ThermalCommand(0, env, dt));
            if (!r.Accepted) { _status = "thermal rejected: " + r.Reason; return; }
            _builder.RefreshState();
            if (r.NewlyCrackedStations > 0)
            {
                _status = "CRACKED during " + env.Id + "!";
                _haptics.Impact(1f);
            }
        }

        /// <summary>Steam rises from the hot stations of a blade sitting in a liquid bath. Visual only.</summary>
        private void TickSteam(float dt)
        {
            if (_steam == null || dt <= 0f) return;
            float rate = SteamModel.PuffsPerSecond(SandboxRules.MaxTempK(_session.Profile), _env);
            if (rate <= 0f) return;
            if (_profile.Tier == QualityTier.Low) rate *= 0.5f;

            BladeProfile p = _session.Profile;
            int n = 0;
            int stride = Mathf.Max(1, p.StationCount / _steamPoints.Length);
            for (int s = 0; s < p.StationCount && n < _steamPoints.Length; s += stride)
            {
                if (p.Severed[s] || p.TempK[s] <= SteamModel.ColdK) continue;
                _steamPoints[n++] = _bladeRoot.TransformPoint(new Vector3((float)p.X[s], 0f, 0f));
            }
            _steam.Emit(_steamPoints, n, rate, dt, SteamModel.Tint(_env));
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

            _infoText = MakeText(safe, "Info", 17, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -100), new Vector2(-12, -12));
            _statusText = MakeText(safe, "Status", 13, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(1, 0), new Vector2(12, 4 * 68 + 14), new Vector2(-12, 4 * 68 + 44));

            // Row 0: tool / option / save / load. Row 1: pressure, zoom, report. Row 2: environments. Row 3: speed, new blank.
            _toolLabel = MakeButton(safe, 0, 4, 0, "GRIND", CycleTool);
            _optionLabel = MakeButton(safe, 1, 4, 0, "Wheel", CycleOption);
            MakeButton(safe, 2, 4, 0, "Save", Save);
            MakeButton(safe, 3, 4, 0, "Load", Load);

            MakeButton(safe, 0, 4, 1, "Press -", () => _pressureN = Mathf.Max((float)SandboxRules.MinPressureN, _pressureN - PressureStepN));
            MakeButton(safe, 1, 4, 1, "Press +", () => _pressureN = Mathf.Min((float)SandboxRules.MaxPressureN, _pressureN + PressureStepN));
            MakeButton(safe, 2, 4, 1, "Zoom", CycleZoom);
            _reportBtn = MakeButton(safe, 3, 4, 1, "Report", ToggleReport);

            _envLabels = new Text[5];
            string[] names = { "Air", "Furnace", "Water", "Oil", "Oven" };
            for (int i = 0; i < 5; i++)
            {
                int idx = i;
                _envLabels[i] = MakeButton(safe, i, 5, 2, names[i], () => { _env = (ForgeEnv)idx; _status = "environment: " + names[idx]; });
            }

            _speedLabel = MakeButton(safe, 0, 4, 3, "x1", CycleSpeed);
            _speedBtn = _speedLabel;
            MakeButton(safe, 1, 4, 3, "New blank", NewBlank);
            MakeButton(safe, 2, 4, 3, "Reset view", () => { ResetView(); _camY = BaseCamY; PoseCamera(); });
            MakeButton(safe, 3, 4, 3, "Guide", () => { _tutorial.Restart(); _hammerHits = 0; RefreshHud(); });
            BuildTutorialPanel(safe);

            // Report panel (does not block touches).
            _reportPanel = new GameObject("ReportPanel", typeof(RectTransform), typeof(Image));
            var prt = _reportPanel.GetComponent<RectTransform>();
            prt.SetParent(safe, false);
            prt.anchorMin = new Vector2(0.04f, 0.5f);
            prt.anchorMax = new Vector2(0.96f, 0.7f);
            prt.offsetMin = prt.offsetMax = Vector2.zero;
            var img = _reportPanel.GetComponent<Image>();
            img.color = new Color(0.05f, 0.06f, 0.08f, 0.7f);
            img.raycastTarget = false;
            _reportText = MakeText(prt, "ReportText", 13, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(10, 8), new Vector2(-10, -8));
            _reportPanel.SetActive(false);
        }

        private void BuildTutorialPanel(RectTransform safe)
        {
            _tutPanel = new GameObject("TutorialPanel", typeof(RectTransform), typeof(Image));
            var rt = _tutPanel.GetComponent<RectTransform>();
            rt.SetParent(safe, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(8f, -226f);
            rt.offsetMax = new Vector2(-8f, -82f);
            var img = _tutPanel.GetComponent<Image>();
            img.color = new Color(0.07f, 0.06f, 0.05f, 0.86f);
            img.raycastTarget = false;

            _tutStep = MakeText(rt, "Step", 12, TextAnchor.UpperRight, Vector2.zero, Vector2.one, new Vector2(8, 6), new Vector2(-8, -6));
            _tutStep.color = new Color(0.7f, 0.7f, 0.7f);
            _tutTitle = MakeText(rt, "Title", 16, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(10, 6), new Vector2(-70, -6));
            _tutTitle.color = new Color(1f, 0.72f, 0.3f);
            _tutBody = MakeText(rt, "Body", 13, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(10, 40), new Vector2(-10, -28));
            _tutWarn = MakeText(rt, "Warn", 13, TextAnchor.LowerLeft, Vector2.zero, Vector2.one, new Vector2(10, 6), new Vector2(-150, -6));
            _tutWarn.color = new Color(1f, 0.4f, 0.3f);

            // Next and Skip live on the panel (buttons block touches only where they are).
            _nextBtn = MakePanelButton(rt, "Next", 1f, () => _tutorial.Acknowledge());
            MakePanelButton(rt, "Skip", 0f, () => { _tutorial.Skip(); RefreshHud(); });
        }

        private static Text MakePanelButton(RectTransform panel, string label, float slot, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Tut_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(panel, false);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(66f, 30f);
            rt.anchoredPosition = new Vector2(-8f - slot * 72f, 6f);
            go.GetComponent<Image>().color = new Color(0.3f, 0.32f, 0.38f, 0.95f);
            go.GetComponent<Button>().onClick.AddListener(onClick);
            Text t = MakeText(rt, "Label", 14, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            t.text = label;
            return t;
        }

        private TutorialSnapshot MakeTutorialSnapshot()
        {
            BladeProfile p = _session.Profile;
            int mid = p.StationCount / 2;
            return new TutorialSnapshot
            {
                MaxTempK = SandboxRules.MaxTempK(p),
                Hrc = _session.Material.HardnessHrc(p.Martensite[mid], p.TemperP[mid]),
                TemperP = p.TemperP[mid],
                RemovedG = (_startMassKg - _session.MassKg) * 1000.0,
                HammerHits = _hammerHits,
                Tool = _tool,
                Env = _env,
                TimeScale = SandboxRules.TimeScales[_speedIndex],
                ReportOpen = _showReport,
                Cracked = SandboxRules.AnyCracked(p),
            };
        }

        private Text TargetLabel(TutorialTarget t)
        {
            switch (t)
            {
                case TutorialTarget.EnvFurnace: return _envLabels[(int)ForgeEnv.Furnace];
                case TutorialTarget.EnvWater: return _envLabels[(int)ForgeEnv.Water];
                case TutorialTarget.EnvOven: return _envLabels[(int)ForgeEnv.Oven];
                case TutorialTarget.Speed: return _speedBtn;
                case TutorialTarget.ToolButton: return _toolLabel;
                case TutorialTarget.Report: return _reportBtn;
                case TutorialTarget.Next: return _nextBtn;
                default: return null;
            }
        }

        private static readonly Color ButtonIdle = new Color(0.25f, 0.27f, 0.32f, 0.95f);

        private void UpdateTutorial()
        {
            if (_tutPanel == null) return;
            TutorialSnapshot snap = MakeTutorialSnapshot();
            _tutorial.Tick(snap);
            // Clear all highlights, then light the current target.
            foreach (Text l in new[] { _toolLabel, _speedBtn, _reportBtn })
                if (l != null) l.transform.parent.GetComponent<Image>().color = ButtonIdle;
            foreach (Text l in _envLabels) l.transform.parent.GetComponent<Image>().color = ButtonIdle;

            bool show = !_tutorial.Finished;
            _tutPanel.SetActive(show);
            if (!show) return;
            TutorialStep st = _tutorial.Current;
            _tutStep.text = (_tutorial.Index + 1) + " / " + _tutorial.Count;
            _tutTitle.text = st.Title;
            _tutBody.text = st.Why + "\n" + st.Action;
            _tutWarn.text = _tutorial.Warning(snap) ?? "";
            _nextBtn.transform.parent.gameObject.SetActive(st.NeedsAck);
            Text target = TargetLabel(st.Target);
            if (target != null && target.transform.parent.gameObject.activeSelf && st.Target != TutorialTarget.Next)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(_pulse * 5f);
                target.transform.parent.GetComponent<Image>().color = Color.Lerp(ButtonIdle, new Color(0.95f, 0.55f, 0.12f, 1f), 0.45f + 0.4f * k);
            }
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

        private static Text MakeButton(RectTransform parent, int slot, int slots, int row, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(slot / (float)slots, 0f);
            rt.anchorMax = new Vector2((slot + 1) / (float)slots, 0f);
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
            if (_infoText == null || _session == null) return;
            BladeProfile p = _session.Profile;
            double mass = _session.MassKg;
            double removed = _startMassKg - mass;
            int mid = p.StationCount / 2;
            double hrc = _session.Material.HardnessHrc(p.Martensite[mid], p.TemperP[mid]);
            double tmax = SandboxRules.MaxTempK(p);
            string flag = SandboxRules.AnyCracked(p) ? "   CRACKED" : "";
            _infoText.text = "Mass " + SandboxRules.F(mass, 3) + " kg   removed " + SandboxRules.F(removed * 1000.0, 1) + " g\n"
                + "Temp " + SandboxRules.F(tmax - 273.15, 0) + " C   HRC " + SandboxRules.F(hrc, 0) + flag + "\n"
                + (_tool == ToolMode.Hammer ? "Energy " : "Pressure ") + SandboxRules.F(_pressureN, 0) + (_tool == ToolMode.Hammer ? " J" : " N")
                + "   " + _env + "  x" + SandboxRules.F(SandboxRules.TimeScales[_speedIndex], 0)
                + (_profile.Simulated ? "   [" + _profile.Name + "]" : "");
            _statusText.text = _status;
            _toolLabel.text = _tool.ToString().ToUpperInvariant();
            _optionLabel.text = _tool == ToolMode.Hammer ? _faces[_faceIndex].Id.Replace("hammer_", "").Replace("_v1", "")
                : _tool == ToolMode.Grind ? _wheels[_wheelIndex].Id.Replace("wheel_", "").Replace("_v1", "") : "-";
            _speedLabel.text = "x" + SandboxRules.F(SandboxRules.TimeScales[_speedIndex], 0);
            for (int i = 0; i < _envLabels.Length; i++)
                _envLabels[i].color = (int)_env == i ? new Color(1f, 0.7f, 0.25f) : Color.white;
            if (_showReport) _reportText.text = SandboxRules.FormatReport(_session.Evaluate(null));
            UpdateTutorial();
        }

        private void CycleTool()
        {
            _tool = (ToolMode)(((int)_tool + 1) % 3);
            _hasPrev = false;
            _hasScreenPrev = false;
            if (_tool != ToolMode.Inspect) ResetView(); // grinding and hammering are done on the face-on view
        }

        private void CycleOption()
        {
            if (_tool == ToolMode.Grind) _wheelIndex = (_wheelIndex + 1) % _wheels.Length;
            else if (_tool == ToolMode.Hammer) _faceIndex = (_faceIndex + 1) % _faces.Length;
        }

        private void CycleSpeed() { _speedIndex = (_speedIndex + 1) % SandboxRules.TimeScales.Length; }

        private void CycleZoom()
        {
            _zoomIndex = (_zoomIndex + 1) % ZoomSizes.Length;
            PoseCamera();
        }

        private void ToggleReport()
        {
            _showReport = !_showReport;
            _reportPanel.SetActive(_showReport);
            _hudTimer = 0f;
        }

        private void NewBlank()
        {
            _blankIndex = (_blankIndex + 1) % 2;
            NewWeapon();
            _env = ForgeEnv.Air;
            _status = _blankIndex == 0 ? "new tapered blank" : "new beveled bar";
        }

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
                _startMassKg = _session.MassKg;
                _status = "loaded";
            }
            else _status = "load failed: " + err;
        }
    }
}
