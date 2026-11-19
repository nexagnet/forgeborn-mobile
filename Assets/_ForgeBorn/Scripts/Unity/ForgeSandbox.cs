using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ForgeBorn.Domain.Commands;
using ForgeBorn.Domain.Evaluation;
using ForgeBorn.Domain.Geometry;
using ForgeBorn.Domain.Material;
using ForgeBorn.Domain.Orders;
using ForgeBorn.Domain.Persistence;
using ForgeBorn.Domain.Session;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static ForgeBorn.UnityLayer.HudWidgets;

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
        // Half-heights (m) of the view at the blade plane. 16:9 landscape: 0.34 shows ~1.2 m wide, the 0.8 m blade fills two thirds
        // and clears the station column (left) and the readout panel (right).
        private static readonly float[] ZoomSizes = { SandboxProps.ViewHalfHeightM, 0.17f, 0.09f };
        private const float BaseCamY = 0f;

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
        // Orders (D3): Free play when _order is null.
        private Career _career = new Career();
        /// <summary>False for capture, benchmark, batch and edit-mode test runs so they never touch the player's career file.</summary>
        private bool _persistCareer;
        private ShopScreen _shop;
        private Order _order;
        private int _orderIndex = -1;
        private bool _delivered;
        private string _resultText;
        private Text _orderBtn, _deliverBtn;
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

        private static string CareerPath => Path.Combine(Application.persistentDataPath, "career.txt");
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
            // The main menu is for a person starting the game: not for the headless capture, the batch runs or the -fb-* automation.
            if (!Application.isBatchMode && !AutomationRequested()) ShowMainMenu();
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
            _coldArmTime = Time.time; // the capture strikes a cold blade on purpose: skip the "click again" confirmation
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
            if (_prevTemps == null || _prevTemps.Length != _session.Profile.StationCount) _prevTemps = new double[_session.Profile.StationCount];
            System.Array.Copy(_session.Profile.TempK, _prevTemps, _prevTemps.Length);
            ThermalResult r = _session.ApplyThermal(new ThermalCommand(0, SandboxRules.EnvFor(env), simSeconds));
            if (r.Accepted && (env == ForgeEnv.Water || env == ForgeEnv.Oil) && simSeconds > 0.0)
            {
                _quench = QuenchMonitor.Assess(_session.Profile, _session.Material, _prevTemps, simSeconds);
                _quenchStamp = Time.unscaledTime;
            }
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
            _settings = GameSettings.Load();
            if (Application.isPlaying) Loc.Current = _settings.ResolveLanguage();
            _persistCareer = !Application.isBatchMode && !AutomationRequested() && Application.isPlaying;
            if (_persistCareer) LoadCareer();
            // ProjectSettings/TimeManager can be saved with timeScale 0 by an Editor session that was paused at the menu;
            // every player would then start frozen. The main menu pauses again right after this when it is wanted.
            Time.timeScale = 1f;
            // First time ever: the guide cannot be skipped until it has been completed once (stored in PlayerPrefs).
            _tutorial.Mandatory = _persistCareer && PlayerPrefs.GetInt(TutorialFlow.DonePrefsKey, 0) == 0;
            _profile = PlatformProfile.Current;
            Application.targetFrameRate = _profile.TargetFps;
            if (PlayerPrefs.HasKey(GameSettings.PrefsKey)) _settings.Apply(); // only once the player has saved settings: do not fight the window default
#if UNITY_IOS || UNITY_ANDROID
            // The HUD is laid out for a landscape 16:9 screen (left station column, readouts on the right): hold the phone sideways.
            Screen.autorotateToPortrait = Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
#endif
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
            SandboxProps.AttachHandle(_bladeRoot, RenderSettings.customReflectionTexture); // grip and guard follow the blade
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
            SandboxEnvironment.PoseCamera(_cam.transform, ZoomSizes[_zoomIndex], _camY, _camX);
        }

        private float _camX;

        /// <summary>Pan the camera by a screen-pixel drag or a world step (metres at the blade plane); clamped so the blade stays in view.</summary>
        private void PanCamera(float dxM, float dyM)
        {
            _camX = Mathf.Clamp(_camX + dxM, -0.7f, 0.7f);
            _camY = Mathf.Clamp(_camY + dyM, -0.6f, 0.6f);
            PoseCamera();
        }

        private void ResetView()
        {
            _flipped = false;
            // Landscape: blade length (+X local) runs left to right across the screen, centred; flat face toward the camera.
            _bladeRoot.SetPositionAndRotation(new Vector3(-(float)BladeLengthM * 0.5f, 0f, 0f), Quaternion.identity);
            if (_camY != BaseCamY || _camX != 0f)
            {
                _camY = BaseCamY;
                _camX = 0f;
                if (_cam != null) PoseCamera();
            }
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
            _stats.Reset();
            // A fresh blade while the guide is running: go back so no step is left unreachable (e.g. quenching a new cold blank).
            if (!_tutorial.Finished && _tutorial.Index > _tutorial.IndexOf("order"))
            {
                _tutorial.RewindTo(_order != null ? "furnace" : "order");
                _hammerHits = 0;
                _flipCount = 0;
            }
        }

        private void AttachSession(ForgeSession s)
        {
            _session = s;
            if (!_undoing) _undo.Clear(); // a new blade or a loaded save: old snapshots no longer belong to it
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

        private Station _station = Station.Grinder;
        private bool _paused;
        private float _furnaceGlow = 0.2f; // 0.2 = banked fire, 1 = the blade is in the furnace
        private bool _rmbWasDown;
        private Vector2 _rmbLast;

        public bool Paused => _paused;

        /// <summary>Keyboard shortcuts, wheel and right-drag (GAME_DESIGN §13). Ignored on touch-only devices where no keys arrive.</summary>
        private void HandleDesktopInput()
        {
            foreach (KeyCode k in HandledKeys)
            {
                if (!Input.GetKeyDown(k)) continue;
                KeyAction a = DesktopKeys.FromKey(k);
                if (_paused && a != KeyAction.Pause) continue; // main menu, pause, settings and shop only react to Esc
                switch (a)
                {
                    case KeyAction.PressureDown: _pressureN = DesktopKeys.StepPressure(_pressureN, -1, (float)SandboxRules.MinPressureN, (float)SandboxRules.MaxPressureN); break;
                    case KeyAction.PressureUp: _pressureN = DesktopKeys.StepPressure(_pressureN, 1, (float)SandboxRules.MinPressureN, (float)SandboxRules.MaxPressureN); break;
                    case KeyAction.SpeedDown: _speedIndex = DesktopKeys.StepIndex(_speedIndex, -1, SandboxRules.TimeScales.Length); break;
                    case KeyAction.SpeedUp: _speedIndex = DesktopKeys.StepIndex(_speedIndex, 1, SandboxRules.TimeScales.Length); break;
                    case KeyAction.NextStation: GoStation(DesktopKeys.Next(_station)); break;
                    case KeyAction.Pause: OnEscape(); break;
                    default:
                        Station s;
                        if (DesktopKeys.TryStation(a, out s)) GoStation(s);
                        break;
                }
                _hudTimer = 0f;
            }

            if (_paused) { _rmbWasDown = false; return; }

            if (Input.GetKeyDown(KeyCode.Z) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))) Undo();
            if (Input.GetKeyDown(KeyCode.Q)) StepAngle(-1);
            if (Input.GetKeyDown(KeyCode.E)) StepAngle(1);
            if (Input.GetKeyDown(KeyCode.F)) FlipBlade();

            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f && !IsOverUi(-1))
            {
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                    _pressureN = DesktopKeys.StepPressure(_pressureN, wheel > 0f ? 1 : -1, (float)SandboxRules.MinPressureN, (float)SandboxRules.MaxPressureN);
                else
                {
                    _zoomIndex = DesktopKeys.StepIndex(_zoomIndex, wheel > 0f ? 1 : -1, ZoomSizes.Length);
                    PoseCamera();
                }
                _hudTimer = 0f;
            }

            // Right or middle drag pans the camera in every tool ("grab the workshop"); the blade is only rolled by Inspect's left drag.
            bool rmb = (Input.GetMouseButton(1) || Input.GetMouseButton(2)) && !IsOverUi(-1);
            Vector2 mp = Input.mousePosition;
            if (rmb && _rmbWasDown)
            {
                Vector2 delta = mp - _rmbLast;
                float perPx = ZoomSizes[_zoomIndex] * 2f / Screen.height; // metres at the blade plane per screen pixel
                PanCamera(-delta.x * perPx, -delta.y * perPx);
            }

            // Arrow keys pan too (held), so the view can be steered without the mouse.
            float ax = (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            float ay = (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            if (ax != 0f || ay != 0f)
            {
                float step = ZoomSizes[_zoomIndex] * 1.2f * Time.unscaledDeltaTime;
                PanCamera(ax * step, ay * step);
            }
            _rmbWasDown = rmb;
            _rmbLast = mp;
        }

        private static readonly KeyCode[] HandledKeys =
        {
            KeyCode.LeftBracket, KeyCode.RightBracket, KeyCode.Minus, KeyCode.KeypadMinus, KeyCode.Equals, KeyCode.KeypadPlus,
            KeyCode.Tab, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Escape,
        };

        private MenuScreens _menus;
        // Loaded in Initialize(): PlayerPrefs may not be called from a field initializer (Unity throws).
        private GameSettings _settings;

        /// <summary>Esc: inside a menu go back (settings) or resume (pause); the main menu ignores it; in the workshop open the pause menu.</summary>
        private void OnEscape()
        {
            if (_menus == null) { SetPaused(!_paused); return; }
            if (_shop != null && _shop.IsOpen) { CloseShop(); return; }
            if (_menus.MainOpen) return;
            if (_menus.AnyOpen) _menus.Back(); else SetPaused(true);
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
            Time.timeScale = paused ? 0f : 1f;
            if (_menus != null) { if (paused) _menus.ShowPause(); else _menus.HideAll(); }
            if (paused && _audio != null) { _audio.SetGrind(0f, 0f); _audio.SetHiss(0f); }
            _hasPrev = false;
            _hasScreenPrev = false;
        }

        public void ShowMainMenu()
        {
            _paused = true;
            Time.timeScale = 0f;
            if (_audio != null) { _audio.SetGrind(0f, 0f); _audio.SetHiss(0f); }
            _menus.ShowMain();
        }

        private void ContinueGame()
        {
            Load();
            ResetOrderState();
            SetPaused(false);
        }

        private void ResetOrderState()
        {
            _order = null;
            _orderIndex = -1;
            _delivered = false;
            _resultText = null;
            _showReport = false;
            _env = ForgeEnv.Air;
            if (_reportPanel != null) _reportPanel.SetActive(false);
        }

        private void StartNewGame()
        {
            ResetOrderState();
            NewWeapon();
            _tutorial.Restart();
            _hammerHits = 0;
            _flipCount = 0;
            _status = "";
            SetPaused(false);
        }

        private static bool AutomationRequested()
        {
            foreach (string a in System.Environment.GetCommandLineArgs())
                if (a.StartsWith("-fb-screenshot", System.StringComparison.OrdinalIgnoreCase) || a.StartsWith("-fb-benchmark", System.StringComparison.OrdinalIgnoreCase) || a.StartsWith("-fb-frames", System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private void BuildMenus(RectTransform safe)
        {
            _menus = new MenuScreens(safe, _settings, new MenuScreens.Actions
            {
                Resume = () => SetPaused(false),
                Continue = ContinueGame,
                NewGame = StartNewGame,
                Quit = QuitGame,
                HasSave = () => File.Exists(SavePath),
            });
        }

        private void BuildShop(RectTransform safe)
        {
            _shop = new ShopScreen(safe, () => _career, s => _status = s, () => { SaveCareer(); RefreshHud(); }, CloseShop);
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void GoStation(Station s)
        {
            _station = s;
            ToolMode? tool = DesktopKeys.ToolFor(s);
            if (tool.HasValue && tool.Value != _tool)
            {
                _tool = tool.Value;
                _hasPrev = false;
                _hasScreenPrev = false;
                if (_tool != ToolMode.Inspect) ResetView();
            }
            ForgeEnv? env = DesktopKeys.EnvFor(s);
            if (env.HasValue) _env = env.Value;
            _status = Loc.F("station {0}: {1}", (int)s + 1, Loc.T(s.ToString()));
        }

        private void Update()
        {
            HandleDesktopInput();
            if (_paused) { if (_aim != null) _aim.Hide(); return; }
            UpdateAim();
            UpdateGoals();
            UpdateAdvice();
            if (_order != null && !_delivered) _stats.AddTime(Time.unscaledDeltaTime);
            _furnaceGlow = Mathf.MoveTowards(_furnaceGlow, _env == ForgeEnv.Furnace ? 1f : 0.2f, Time.deltaTime * 1.5f);
            SandboxProps.SetFurnaceGlow(_furnaceGlow);
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
                if (pressedNow) { PushUndo(); _stats.OnGrindStroke(); }
                GrindStep(pos, Time.deltaTime);
            }
            else if (_tool == ToolMode.Hammer)
            {
                _audio.SetGrind(0f, 0f);
                if (pressedNow) HammerStrike(pos); // pushes its own undo step, only when a blow really lands
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

        private AimAssist _aim;

        /// <summary>Blade temperature (K) at position x along the blade, interpolated between stations.</summary>
        private double TempAtX(double x)
        {
            BladeProfile p = _session.Profile;
            int n = p.StationCount;
            if (x <= p.X[0]) return p.TempK[0];
            if (x >= p.X[n - 1]) return p.TempK[n - 1];
            int lo = 0, hi = n - 1;
            while (hi - lo > 1) { int mid = (lo + hi) >> 1; if (p.X[mid] <= x) lo = mid; else hi = mid; }
            double t = (x - p.X[lo]) / System.Math.Max(1e-9, p.X[hi] - p.X[lo]);
            return p.TempK[lo] + (p.TempK[hi] - p.TempK[lo]) * t;
        }

        // ---------------------------------------------------------------- assists (plan phases 1 and 2)

        // Grind angle (included edge angle, degrees) and the blade being turned over to reach the other face.
        private static readonly double[] GrindAngles = { 10.0, 15.0, 20.0, 25.0, 30.0, 35.0, 40.0 };
        private int _angleIndex = 2;
        private bool _flipped;
        private int _flipCount;       // flips since the blade was made (the tutorial waits for the first one)
        private Text _flipLabel, _skipBtn;
        private Text _angleLabel;

        private double GrindAngle { get { return GrindAngles[_angleIndex]; } }

        private void StepAngle(int dir)
        {
            _angleIndex = (_angleIndex + dir + GrindAngles.Length) % GrindAngles.Length;
            _status = Loc.F("grind angle: {0} deg", SandboxRules.F(GrindAngle, 0));
            _hudTimer = 0f;
            _adviceEval = 0f;
        }

        /// <summary>Picks the notch nearest to an order's target edge angle, so a new order starts at a sensible setting.</summary>
        private void SelectAngleNear(double deg)
        {
            int best = 0;
            for (int i = 1; i < GrindAngles.Length; i++)
                if (System.Math.Abs(GrindAngles[i] - deg) < System.Math.Abs(GrindAngles[best] - deg)) best = i;
            _angleIndex = best;
        }

        /// <summary>Turns the blade over (about its length) so the other face points at the wheel. Switching tools restores the normal view.</summary>
        private void FlipBlade()
        {
            _bladeRoot.Rotate(Vector3.right, 180f, Space.World);
            _flipped = !_flipped;
            _flipCount++;
            _hasPrev = false;
            _hasScreenPrev = false;
            _status = Loc.T(_flipped ? "Blade turned over: other face up" : "Blade turned back");
        }

        public void CaptureSetGrindAngle(double deg) { SelectAngleNear(deg); }
        public void CaptureNewWeapon() { NewWeapon(); }
        public void CaptureSetupGrind(int wheelIndex, float pressureN) { _wheelIndex = wheelIndex; _pressureN = pressureN; }

        /// <summary>Screen position (pixels of the camera's target) of the +Y edge at blade position x, offsetM below the tip.</summary>
        public Vector2 CaptureEdgeScreen(double x, double offsetM)
        {
            return _cam.WorldToScreenPoint(_bladeRoot.TransformPoint(new Vector3((float)x, (float)(EdgeHalfWidthM(x) - offsetM), 0f)));
        }
        public void CaptureFlip() { FlipBlade(); }

        private readonly PlayStats _stats = new PlayStats();
        private float _coldArmTime = -10f;
        private QuenchReading _quench;
        private float _quenchStamp = -10f;
        private double[] _prevTemps;
        private Text _adviceText;
        private Text _hintText;   // one line of mouse/key hints for the tool in hand (nothing on screen used to name Q/E, F, the wheel or right-drag)
        public const string HintGrind = "GRIND: hold left mouse, drag along the edge  |  Q/E angle  |  F flip  |  Ctrl+wheel force  |  right-drag to pan  |  wheel to zoom";
        public const string HintHammer = "HAMMER: left-click the blade while it glows orange  |  Ctrl+wheel strike force  |  right-drag to pan  |  wheel to zoom  |  key 3: grinder";
        public const string HintInspect = "VIEW: hold left mouse and drag to turn the blade  |  right-drag to pan  |  wheel to zoom  |  key 2: hammer, key 3: grinder";
        private float _adviceTimer, _adviceEval;
        private string _adviceCache = "";

        private int AssistLevel { get { return _settings != null ? _settings.Assist : 1; } }

        private static string PlayLogPath { get { return Path.Combine(Application.persistentDataPath, "playlog.csv"); } }

        /// <summary>Screen ends of the grind rail: the edge line moved down by the wheel's rail distance (see GrindRail).</summary>
        private bool RailEnds(out Vector3 a, out Vector3 b)
        {
            float off = (float)GrindRail.OffsetM(_wheels[_wheelIndex]);
            a = _cam.WorldToScreenPoint(_bladeRoot.TransformPoint(new Vector3(0f, (float)EdgeHalfWidthM(0.0) - off, 0f)));
            b = _cam.WorldToScreenPoint(_bladeRoot.TransformPoint(new Vector3((float)BladeLengthM, (float)EdgeHalfWidthM(BladeLengthM) - off, 0f)));
            return a.z > 0f && b.z > 0f;
        }

        /// <summary>Strong assist: while grinding, a pointer within 28 px of the rail is held on it (the player still sets position along the blade, speed and pressure).</summary>
        private Vector2 AssistedPos(Vector2 raw)
        {
            if (AssistLevel < 2 || _tool != ToolMode.Grind || _cam == null) return raw;
            Vector3 a, b;
            if (!RailEnds(out a, out b) || Mathf.Abs(b.x - a.x) < 1f) return raw;
            float u = (raw.x - a.x) / (b.x - a.x);
            if (u < -0.02f || u > 1.02f) return raw;
            float y = a.y + (b.y - a.y) * u;
            return Mathf.Abs(raw.y - y) <= 28f ? new Vector2(raw.x, y) : raw;
        }

        private void UpdateAdvice()
        {
            if (_adviceText == null) return;
            _adviceTimer -= Time.unscaledDeltaTime;
            if (_adviceTimer > 0f) return;
            _adviceTimer = 0.2f;
            if (AssistLevel == 0 || _delivered || _paused) { _adviceText.text = ""; return; }
            _adviceEval -= 0.2f;
            if (_adviceEval <= 0f) { _adviceEval = 0.5f; _adviceCache = BuildAdvice(); }
            string q = QuenchLine();
            _adviceText.text = (q.Length > 0 ? q + "\n" : "") + _adviceCache;
        }

        private string QuenchLine()
        {
            if (!(_env == ForgeEnv.Water || _env == ForgeEnv.Oil) || Time.unscaledTime - _quenchStamp > 1.5f) return "";
            string rate = SandboxRules.F(_quench.MinRateKPerS, 0);
            switch (_quench.State)
            {
                case QuenchState.TooSlow: return "<color=#9EC4FF>" + Loc.F("Quench {0} K/s: too slow, it will not harden", rate) + "</color>";
                case QuenchState.Partial: return "<color=#FFD070>" + Loc.F("Quench {0} K/s: only partly hardening", rate) + "</color>";
                case QuenchState.Full: return "<color=#8CFF8C>" + Loc.F("Quench {0} K/s: hardening fully", rate) + "</color>";
                case QuenchState.CrackRisk: return "<color=#FF7060>" + Loc.F("Quench {0} K/s: CRACK RISK, take it out of the bath", rate) + "</color>";
                default: return "";
            }
        }

        private string BuildAdvice()
        {
            BladeSpec spec = _order != null ? _order.Spec : new BladeSpec();
            BladeReport r = _session.Evaluate(spec);
            Advice a = NextStepAdvisor.Suggest(_session.Profile, _session.Material, r, spec, _order != null ? _order.MaxMassKg : double.MaxValue);
            string col = a.Level == AdviceLevel.Danger ? "#FF7060" : a.Level == AdviceLevel.Warn ? "#FFC060" : a.Level == AdviceLevel.Good ? "#8CFF8C" : "#E8E8E8";
            var sb = new System.Text.StringBuilder();
            sb.Append("<color=").Append(col).Append(">").Append(Loc.T("Next:")).Append(" ").Append(Loc.F(a.Template, (object[])a.Args)).Append("</color>");

            BladeProfile p = _session.Profile;
            int mid = p.StationCount / 2;
            if (p.Martensite[mid] >= 0.5)
            {
                MaterialData m = _session.Material;
                sb.Append("\n").Append(Loc.F("Temper {0} C: 10 min {1}, 1 h {2}, 4 h {3} HRC (aim {4})", SandboxRules.F(SandboxRules.OvenK - 273.15, 0),
                    SandboxRules.F(TemperForecast.HrcAfter(m, p.Martensite[mid], p.TemperP[mid], SandboxRules.OvenK, 1.0 / 6.0), 0),
                    SandboxRules.F(TemperForecast.HrcAfter(m, p.Martensite[mid], p.TemperP[mid], SandboxRules.OvenK, 1.0), 0),
                    SandboxRules.F(TemperForecast.HrcAfter(m, p.Martensite[mid], p.TemperP[mid], SandboxRules.OvenK, 4.0), 0),
                    SandboxRules.F(spec.TargetEdgeHrc, 0)));
            }
            if (_tool == ToolMode.Hammer)
                sb.Append("\n").Append(Loc.F("Forging heat: {0} C and up (blue ring = too cold)", SandboxRules.F(_session.Material.ForgeMinTempK - 273.15, 0)));
            return sb.ToString();
        }

        private void WritePlayLog(OrderResult res, BladeReport rep)
        {
            if (!_persistCareer || _order == null) return;
            try
            {
                bool fresh = !File.Exists(PlayLogPath);
                using (var w = new StreamWriter(PlayLogPath, true))
                {
                    if (fresh) w.WriteLine(PlayLog.Header);
                    w.WriteLine(PlayLog.Row(_order.Id, res.Delivered, res.Net, rep, _stats));
                }
            }
            catch (System.Exception e) { UnityEngine.Debug.LogWarning("Play log failed: " + e.Message); }
        }

        // ---------------------------------------------------------------- order goals (live checklist)

        private Text _goalText;
        private float _goalTimer;

        private static string Mark(bool ok, string s) { return ok ? "<color=#8CFF8C>" + s + "</color>" : "<color=#FFB070>" + s + "</color>"; }

        /// <summary>Live checklist of what the current order still needs, refreshed twice a second (Evaluate walks every station).</summary>
        private void UpdateGoals()
        {
            if (_goalText == null) return;
            _goalTimer -= Time.unscaledDeltaTime;
            if (_goalTimer > 0f) return;
            _goalTimer = 0.5f;
            if (_order == null || _delivered) { _goalText.text = ""; return; }

            BladeReport r = _session.Evaluate(_order.Spec);
            BladeSpec sp = _order.Spec;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(Loc.T("Goals"));
            sb.AppendLine(!r.HasBevel
                ? Mark(false, Loc.F("Edge: not ground yet (aim {0} deg)", SandboxRules.F(sp.TargetEdgeAngleDeg, 0)))
                : Mark(r.EdgeScore >= 0.6, Loc.F("Edge {0} deg (aim {1} +-{2})", SandboxRules.F(r.EdgeAngleDeg, 0), SandboxRules.F(sp.TargetEdgeAngleDeg, 0), SandboxRules.F(sp.EdgeAngleTolDeg, 0))));
            sb.AppendLine(Mark(r.HardnessScore >= 0.6, Loc.F("Hardness {0} HRC (aim {1} +-{2})", SandboxRules.F(r.MeanHrc, 0), SandboxRules.F(sp.TargetEdgeHrc, 0), SandboxRules.F(sp.HrcTol, 0))));
            sb.AppendLine(Mark(r.StraightnessScore >= 0.6, Loc.F("Bend {0} mm (max {1})", SandboxRules.F(r.StraightnessDevM * 1000.0, 1), SandboxRules.F(sp.MaxStraightDevM * 1000.0, 1))));
            sb.AppendLine(Mark(r.TwistScore >= 0.6, Loc.F("Twist {0} deg (max {1})", SandboxRules.F(r.TwistRangeDeg, 1), SandboxRules.F(sp.MaxTwistDeg, 1))));
            if (_order.MaxMassKg < double.MaxValue)
                sb.AppendLine(Mark(r.MassKg <= _order.MaxMassKg, Loc.F("Mass {0} g (max {1})", SandboxRules.F(r.MassKg * 1000.0, 0), SandboxRules.F(_order.MaxMassKg * 1000.0, 0))));
            _goalText.text = sb.ToString();
        }

        // ---------------------------------------------------------------- undo (Ctrl+Z)

        private const int MaxUndo = 20;
        private readonly List<WeaponSnapshot> _undo = new List<WeaponSnapshot>();
        private bool _undoing;

        private void PushUndo()
        {
            _undo.Add(_session.Capture());
            if (_undo.Count > MaxUndo) _undo.RemoveAt(0);
        }

        private void Undo()
        {
            if (_undo.Count == 0) { _status = Loc.T("Nothing to undo"); return; }
            WeaponSnapshot snap = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            _stats.OnUndo();
            _undoing = true;
            AttachSession(ForgeSession.FromSnapshot(snap));
            _undoing = false;
            _hasPrev = false;
            _hasScreenPrev = false;
            _goalTimer = 0f;
            _status = Loc.F("Undone ({0} left)", _undo.Count);
        }

        /// <summary>Half-width (m) of the +Y edge at blade position x for the current blank (matches NewWeapon).</summary>
        private double EdgeHalfWidthM(double x)
        {
            return _blankIndex == 0 ? 0.025 - 0.010 * x / BladeLengthM : 0.025;
        }

        /// <summary>Aiming aid for Grind and Hammer: ring on the mouse (green over the blade) and, for Grind, a line along the edge to follow.</summary>
        private void UpdateAim()
        {
            if (_aim == null) return;
            bool aiming = (_tool == ToolMode.Grind || _tool == ToolMode.Hammer) && Input.touchCount == 0;
            Vector2 mp = Input.mousePosition;
            if (AssistLevel == 0 || !aiming || IsOverUi(-1) || mp.x < 0f || mp.y < 0f || mp.x > Screen.width || mp.y > Screen.height) { _aim.Hide(); return; }

            mp = AssistedPos(mp);
            Vector3 o, d;
            ScreenToBladeRay(mp, out o, out d);
            BladeHit bh;
            bool onBlade = BladeRaycaster.Raycast(_session.Profile, new Vec3d(o.x, o.y, o.z), new Vec3d(d.x, d.y, d.z), 5.0, out bh);
            AimAssist.AimState st = !onBlade ? AimAssist.AimState.Miss : AimAssist.AimState.OnBlade;
            if (onBlade && _tool == ToolMode.Hammer)
                st = TempAtX(bh.X) < _session.Material.ForgeMinTempK ? AimAssist.AimState.Cold : AimAssist.AimState.Hot;
            // Ring size = the contact footprint (wheel or hammer face) at the blade plane.
            float ppm = _cam.pixelHeight / (2f * ZoomSizes[_zoomIndex]);
            float halfLen = _tool == ToolMode.Hammer ? (float)_faces[_faceIndex].HalfLengthM : (float)_wheels[_wheelIndex].FootprintHalfLengthM;
            _aim.SetRingSize(Mathf.Clamp(2f * halfLen * ppm, 22f, 220f));
            _aim.ShowRing(mp, st);

            Vector3 ra, rb;
            if (_tool == ToolMode.Grind && RailEnds(out ra, out rb)) _aim.ShowGuide(ra, rb);
            else _aim.HideGuide();
        }

        private readonly IPointerSource _pointer = CompositePointerSource.Default();

        private bool TryGetPointer(out Vector2 pos, out int fingerId)
        {
            PointerState s;
            _pointer.TryGet(out s);
            pos = s.Position;
            fingerId = s.PointerId;
            return s.Down;
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
            screenPos = AssistedPos(screenPos);
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
                var cmd = new GrindStrokeCommand(_nextCommandId++, Time.timeAsDouble, _wheels[_wheelIndex], WheelRpm, samples, GrindAngle);
                _sw.Restart();
                GrindResult res = _session.ApplyGrind(cmd);
                _sw.Stop();
                if (!res.Accepted) _status = Loc.F("rejected: {0}", res.Reason);
                else
                {
                    if (res.LastStation >= 0) _builder.Rebuild(res.FirstStation, res.LastStation);
                    contact = res.ContactSubsteps > 0;
                    _status = contact ? (ShowTimings ? Loc.F("grind {0} ms", SandboxRules.F(_sw.Elapsed.TotalMilliseconds, 2)) : "") : Loc.T("no contact");
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

            if (AssistLevel > 0)
            {
                BladeHit coldHit;
                if (BladeRaycaster.Raycast(_session.Profile, cmd.RayOrigin, cmd.RayDirection, 5.0, out coldHit)
                    && TempAtX(coldHit.X) < _session.Material.ForgeMinTempK && Time.time - _coldArmTime > 1.5f)
                {
                    _coldArmTime = Time.time;
                    _lastStrikeTime = -10f; // the warning is not a blow
                    _status = Loc.T("Cold metal: click again within 1.5 s to strike anyway.");
                    return;
                }
            }
            PushUndo();
            _sw.Restart();
            HammerResult res = _session.ApplyHammer(cmd);
            _sw.Stop();
            LastHammer = res;

            if (res.Accepted) _stats.OnStrike(res.Hit, res.Cold);
            if (!res.Accepted) { _status = Loc.F("rejected: {0}", res.Reason); return; }
            if (!res.Hit) { _undo.RemoveAt(_undo.Count - 1); _status = Loc.T("missed the blade"); return; }
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
            if (res.StationsTouched == 0) _status = Loc.F("hammer: no deformation ({0})", temp);
            else if (res.Cold) _status = Loc.F("COLD hammering at {0}: dent {1} mm, crack risk +{2}", temp, SandboxRules.F(res.DepthM * 1000.0, 2), SandboxRules.F(res.DamageAdded, 2));
            else if (ShowTimings) _status = Loc.F("hammer at {0}: dent {1} mm, {2} ms", temp, SandboxRules.F(res.DepthM * 1000.0, 2), SandboxRules.F(_sw.Elapsed.TotalMilliseconds, 2));
            else _status = Loc.F("hammer at {0}: dent {1} mm", temp, SandboxRules.F(res.DepthM * 1000.0, 2));
        }

        /// <summary>Solver timings are for developers: players do not need "5.8 ms" in the status line.</summary>
        private static bool ShowTimings { get { return Application.isEditor || UnityEngine.Debug.isDebugBuild; } }

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

            ThermalEnvironment env = SandboxRules.EnvFor(_env, _career.Owns(Upgrade.HotFurnace));
            if (!SandboxRules.NeedsTick(_session.Profile, env, _env == ForgeEnv.Oven)) return;

            // Quenches need fine steps (the cooling rate at the nose decides hardness); air/oven/furnace tolerate coarse ones.
            _session.Thermal.StepsPerCommand = (_env == ForgeEnv.Water || _env == ForgeEnv.Oil) ? 2000.0 : 500.0;
            if (_prevTemps == null || _prevTemps.Length != _session.Profile.StationCount) _prevTemps = new double[_session.Profile.StationCount];
            System.Array.Copy(_session.Profile.TempK, _prevTemps, _prevTemps.Length);
            ThermalResult r = _session.ApplyThermal(new ThermalCommand(0, env, dt));
            if (r.Accepted && (_env == ForgeEnv.Water || _env == ForgeEnv.Oil))
            {
                _quench = QuenchMonitor.Assess(_session.Profile, _session.Material, _prevTemps, dt);
                if (_quench.State != QuenchState.Idle) _quenchStamp = Time.unscaledTime;
            }
            if (!r.Accepted) { _status = Loc.F("thermal rejected: {0}", r.Reason); return; }
            _builder.RefreshState();
            if (r.NewlyCrackedStations > 0)
            {
                _status = Loc.F("CRACKED during {0}!", env.Id);
                _haptics.Impact(1f);
            }
        }

        /// <summary>Steam rises from the hot stations of a blade sitting in a liquid bath. Visual only.</summary>
        private void TickSteam(float dt)
        {
            if (_steam == null || dt <= 0f) return;
            float rate = SteamModel.PuffsPerSecond(SandboxRules.MaxTempK(_session.Profile), _env);
            if (_audio != null) _audio.SetHiss(Mathf.Clamp01(rate / SteamModel.WaterPuffsPerSecond));
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

            // Landscape 16:9 reference 1920x1080 (GAME_DESIGN §13). Matching half width / half height keeps 4:3 windows
            // and ultrawide usable; the blade is never stretched because only the HUD scales.
            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var safe = new GameObject("SafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
            safe.SetParent(canvasGo.transform, false);
            Rect sa = Screen.safeArea;
            safe.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            safe.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;

            // Readout panel top right, status line above the bottom bar.
            _infoText = MakeText(safe, "Info", 27, TextAnchor.UpperLeft, new Vector2(0.71f, 0.82f), new Vector2(0.995f, 0.99f), Vector2.zero, Vector2.zero);
            _statusText = MakeText(safe, "Status", 26, TextAnchor.LowerLeft, new Vector2(0.135f, 0.10f), new Vector2(0.995f, 0.15f), Vector2.zero, Vector2.zero);

            // Left column: tool, option, the five environments (stations 1-5), zoom.
            const float colX0 = 0.005f, colX1 = 0.125f, rowH = 0.085f, rowStep = 0.095f, top = 0.99f;
            int row = 0;
            Vector2 ColMin(int r) { return new Vector2(colX0, top - r * rowStep - rowH); }
            Vector2 ColMax(int r) { return new Vector2(colX1, top - r * rowStep); }
            _toolLabel = MakeButton(safe, Loc.T("GRIND"), ColMin(row), ColMax(row), CycleTool); row++;
            _optionLabel = MakeButton(safe, Loc.T("Wheel"), ColMin(row), ColMax(row), CycleOption); row++;
            _envLabels = new Text[5];
            string[] names = { "Air", "Furnace", "Water", "Oil", "Oven" };
            for (int i = 0; i < 5; i++)
            {
                int idx = i;
                _envLabels[i] = MakeButton(safe, Loc.T(names[i]), ColMin(row), ColMax(row), () => SelectEnv(idx, names[idx]));
                row++;
            }
            MakeButton(safe, Loc.T("Zoom"), ColMin(row), ColMax(row), CycleZoom); row++;
            MakeButton(safe, Loc.T("Shop"), ColMin(row), ColMax(row), OpenShop); row++;
            _angleLabel = MakeButton(safe, "", ColMin(row), ColMax(row), () => StepAngle(1));

            // Bottom bar, nine buttons across.
            const float barX0 = 0.135f, barW = 0.078f, barY0 = 0.01f, barY1 = 0.095f;
            int slot = 0;
            Vector2 BarMin() { return new Vector2(barX0 + slot * barW, barY0); }
            Vector2 BarMax() { return new Vector2(barX0 + (slot + 1) * barW, barY1); }
            MakeButton(safe, Loc.T("Press -"), BarMin(), BarMax(), () => _pressureN = DesktopKeys.StepPressure(_pressureN, -2, (float)SandboxRules.MinPressureN, (float)SandboxRules.MaxPressureN)); slot++;
            MakeButton(safe, Loc.T("Press +"), BarMin(), BarMax(), () => _pressureN = DesktopKeys.StepPressure(_pressureN, 2, (float)SandboxRules.MinPressureN, (float)SandboxRules.MaxPressureN)); slot++;
            _speedLabel = MakeButton(safe, "x1", BarMin(), BarMax(), CycleSpeed); slot++;
            _speedBtn = _speedLabel;
            _orderBtn = MakeButton(safe, Loc.T("Order"), BarMin(), BarMax(), CycleOrder); slot++;
            _deliverBtn = MakeButton(safe, Loc.T("Deliver"), BarMin(), BarMax(), Deliver); slot++;
            _reportBtn = MakeButton(safe, Loc.T("Report"), BarMin(), BarMax(), ToggleReport); slot++;
            _flipLabel = MakeButton(safe, Loc.T("Flip") + " [F]", BarMin(), BarMax(), FlipBlade); slot++;
            MakeButton(safe, Loc.T("Undo"), BarMin(), BarMax(), Undo); slot++; // touch screens have no Ctrl+Z
            MakeButton(safe, Loc.T("Guide"), BarMin(), BarMax(), () => { _tutorial.Restart(); _hammerHits = 0; _flipCount = 0; RefreshHud(); }); slot++;
            MakeButton(safe, Loc.T("Save"), BarMin(), BarMax(), Save); slot++;
            MakeButton(safe, Loc.T("Load"), BarMin(), BarMax(), Load);
            BuildTutorialPanel(safe);

            // Report panel (does not block the pointer): below the blade, between the station column and the readout.
            _reportPanel = new GameObject("ReportPanel", typeof(RectTransform), typeof(Image));
            var prt = _reportPanel.GetComponent<RectTransform>();
            prt.SetParent(safe, false);
            prt.anchorMin = new Vector2(0.14f, 0.16f);
            prt.anchorMax = new Vector2(0.70f, 0.40f);
            prt.offsetMin = prt.offsetMax = Vector2.zero;
            var img = _reportPanel.GetComponent<Image>();
            img.color = new Color(0.05f, 0.06f, 0.08f, 0.78f);
            img.raycastTarget = false;
            _reportText = MakeText(prt, "ReportText", 24, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(14, 10), new Vector2(-14, -10));
            _reportPanel.SetActive(false);
            _aim = new AimAssist(safe);
            _goalText = MakeText(safe, "Goals", 27, TextAnchor.UpperRight, new Vector2(0.58f, 0.56f), new Vector2(0.99f, 0.80f), Vector2.zero, Vector2.zero);
            _goalText.raycastTarget = false; // must not swallow grind/hammer clicks
            _goalText.supportRichText = true;
            _adviceText = MakeText(safe, "Advice", 26, TextAnchor.UpperRight, new Vector2(0.45f, 0.15f), new Vector2(0.99f, 0.32f), Vector2.zero, Vector2.zero);
            _adviceText.raycastTarget = false;
            _adviceText.supportRichText = true;
            _hintText = MakeText(safe, "Hint", 24, TextAnchor.LowerLeft, new Vector2(0.135f, 0.155f), new Vector2(0.995f, 0.205f), Vector2.zero, Vector2.zero);
            _hintText.raycastTarget = false;
            _hintText.color = new Color(0.62f, 0.85f, 1f);
            BuildShop(safe);
            BuildMenus(safe); // last child, so the menus draw over everything

        }

        private void BuildTutorialPanel(RectTransform safe)
        {
            _tutPanel = new GameObject("TutorialPanel", typeof(RectTransform), typeof(Image));
            var rt = _tutPanel.GetComponent<RectTransform>();
            rt.SetParent(safe, false);
            rt.anchorMin = new Vector2(0.135f, 0.78f);
            rt.anchorMax = new Vector2(0.69f, 0.99f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = _tutPanel.GetComponent<Image>();
            img.color = new Color(0.07f, 0.06f, 0.05f, 0.86f);
            img.raycastTarget = false;

            // Large type: this panel is the one thing a first-time player must read (74-27).
            _tutStep = MakeText(rt, "Step", 22, TextAnchor.UpperRight, Vector2.zero, Vector2.one, new Vector2(12, 8), new Vector2(-12, -8));
            _tutStep.color = new Color(0.78f, 0.78f, 0.78f);
            _tutTitle = MakeText(rt, "Title", 34, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(16, 8), new Vector2(-130, -8));
            _tutTitle.color = new Color(1f, 0.72f, 0.3f);
            _tutBody = MakeText(rt, "Body", 26, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(16, 64), new Vector2(-16, -56));
            _tutBody.supportRichText = true;
            _tutWarn = MakeText(rt, "Warn", 24, TextAnchor.LowerLeft, Vector2.zero, Vector2.one, new Vector2(16, 8), new Vector2(-260, -8));
            _tutWarn.color = new Color(1f, 0.4f, 0.3f);

            // Next and Skip live on the panel (buttons block touches only where they are). Skip is hidden while the guide is mandatory.
            _nextBtn = MakePanelButton(rt, Loc.T("Next"), 1f, () => _tutorial.Acknowledge());
            _skipBtn = MakePanelButton(rt, Loc.T("Skip"), 0f, () => { _tutorial.Skip(); RefreshHud(); });
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
                HasOrder = _order != null,
                Delivered = _delivered,
                FlipCount = _flipCount,
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
                case TutorialTarget.Order: return _orderBtn;
                case TutorialTarget.Deliver: return _deliverBtn;
                case TutorialTarget.Flip: return _flipLabel;
                default: return null;
            }
        }

        private static readonly Color ButtonIdle = new Color(0.25f, 0.27f, 0.32f, 0.95f);

        private void UpdateTutorial()
        {
            if (_tutPanel == null) return;
            TutorialSnapshot snap = MakeTutorialSnapshot();
            if (!_tutFrozen) _tutorial.Tick(snap);
            if (_tutorial.Finished && _tutorial.Mandatory)
            {
                // Completed the required first run: never force it again.
                _tutorial.Mandatory = false;
                if (_persistCareer) { PlayerPrefs.SetInt(TutorialFlow.DonePrefsKey, 1); PlayerPrefs.Save(); }
            }
            // Clear all highlights, then light the current target.
            foreach (Text l in new[] { _toolLabel, _speedBtn, _reportBtn, _orderBtn, _deliverBtn, _flipLabel })
                if (l != null) l.transform.parent.GetComponent<Image>().color = ButtonIdle;
            foreach (Text l in _envLabels) l.transform.parent.GetComponent<Image>().color = ButtonIdle;

            bool show = !_tutorial.Finished;
            _tutPanel.SetActive(show);
            if (!show) return;
            TutorialStep st = _tutorial.Current;
            _tutStep.text = (_tutorial.Index + 1) + " / " + _tutorial.Count;
            _tutTitle.text = Loc.T(st.Title);
            _tutBody.text = Loc.T(st.Why) + "\n" + Loc.T(st.Action);
            _tutWarn.text = Loc.T(_tutorial.Warning(snap) ?? "");
            _nextBtn.transform.parent.gameObject.SetActive(st.NeedsAck);
            _skipBtn.transform.parent.gameObject.SetActive(!_tutorial.Mandatory);
            Text target = TargetLabel(st.Target);
            if (target != null && target.transform.parent.gameObject.activeSelf && st.Target != TutorialTarget.Next)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(_pulse * 5f);
                target.transform.parent.GetComponent<Image>().color = Color.Lerp(ButtonIdle, new Color(0.95f, 0.55f, 0.12f, 1f), 0.45f + 0.4f * k);
            }
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
            string flag = SandboxRules.AnyCracked(p) ? "   " + Loc.T("CRACKED") : "";
            _infoText.text = Loc.T("Mass") + " " + SandboxRules.F(mass, 3) + " kg   " + Loc.T("removed") + " " + SandboxRules.F(removed * 1000.0, 1) + " g\n"
                + Loc.T("Temp") + " " + SandboxRules.F(tmax - 273.15, 0) + " C   HRC " + SandboxRules.F(hrc, 0) + flag + "\n"
                + Loc.T(_tool == ToolMode.Hammer ? "Energy" : "Pressure") + " " + SandboxRules.F(_pressureN, 0) + (_tool == ToolMode.Hammer ? " J" : " N")
                + "   " + Loc.T(_env.ToString()) + "  x" + SandboxRules.F(SandboxRules.TimeScales[_speedIndex], 0)
                + (_profile.Simulated ? "   [" + _profile.Name + "]" : "");
            _statusText.text = _status;
            if (_hintText != null)
                _hintText.text = Loc.T(_tool == ToolMode.Hammer ? HintHammer : _tool == ToolMode.Grind ? HintGrind : HintInspect);
            if (_angleLabel != null) _angleLabel.text = Loc.F("Angle {0} deg", SandboxRules.F(GrindAngle, 0));
            _toolLabel.text = Loc.T(_tool.ToString().ToUpperInvariant());
            _optionLabel.text = _tool == ToolMode.Hammer ? Loc.T(_faces[_faceIndex].Id.Replace("hammer_", "").Replace("_v1", ""))
                : _tool == ToolMode.Grind ? Loc.T(_wheels[_wheelIndex].Id.Replace("wheel_", "").Replace("_v1", "")) : "-";
            _speedLabel.text = "x" + SandboxRules.F(SandboxRules.TimeScales[_speedIndex], 0);
            for (int i = 0; i < _envLabels.Length; i++)
                _envLabels[i].color = (int)_env == i ? new Color(1f, 0.7f, 0.25f) : Color.white;
            if (_showReport)
                _reportText.text = (_resultText != null ? _resultText + "\n\n" : "")
                    + SandboxRules.FormatReport(_session.Evaluate(_order != null ? _order.Spec : null));
            _infoText.text += "\n" + (_order == null ? Loc.T("Free play") : Loc.T("Order:") + " " + Loc.T(_order.Title) + (_order.MaxMassKg < double.MaxValue ? " (" + Loc.T("max") + " " + SandboxRules.F(_order.MaxMassKg * 1000.0, 0) + " g)" : ""))
                + "   " + Loc.T("Money") + " " + _career.Money;
            UpdateTutorial();
        }

        private void CycleTool()
        {
            _tool = (ToolMode)(((int)_tool + 1) % 3);
            _hasPrev = false;
            _hasScreenPrev = false;
            if (_tool != ToolMode.Inspect) ResetView(); // grinding and hammering are done on the face-on view
        }

        private bool WheelAllowed(int i) { return i != 2 || _career.Owns(Upgrade.FineWheel); }

        private bool FaceAllowed(int i) { return i != 1 || _career.Owns(Upgrade.PeenFace); }

        /// <summary>Next unlocked wheel or hammer face. The coarse wheel and flat face are always owned, so the loop ends.</summary>
        private void CycleOption()
        {
            if (_tool == ToolMode.Grind)
            {
                do _wheelIndex = (_wheelIndex + 1) % _wheels.Length; while (!WheelAllowed(_wheelIndex));
            }
            else if (_tool == ToolMode.Hammer)
            {
                do _faceIndex = (_faceIndex + 1) % _faces.Length; while (!FaceAllowed(_faceIndex));
            }
        }

        private void SelectEnv(int idx, string name)
        {
            if ((ForgeEnv)idx == ForgeEnv.Oil && !_career.Owns(Upgrade.OilBath)) { _status = Loc.T("Oil bath is locked: buy it in the Shop (after your first delivery)."); return; }
            _env = (ForgeEnv)idx;
            _status = Loc.F("environment: {0}", Loc.T(name));
        }

        private void OpenShop()
        {
            if (_paused) return;
            if (!_career.ShopOpen) { _status = Loc.T("The shop opens after your first delivered order."); return; }
            _paused = true;
            Time.timeScale = 0f;
            if (_audio != null) { _audio.SetGrind(0f, 0f); _audio.SetHiss(0f); }
            _hasPrev = false;
            _hasScreenPrev = false;
            _shop.Show();
        }

        private void CloseShop()
        {
            _shop.Hide();
            _paused = false;
            Time.timeScale = 1f;
            SaveCareer();
            RefreshHud();
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

        /// <summary>Cycles Free play, then each unlocked order. Starting one gives a fresh blank.</summary>
        private void CycleOrder()
        {
            IReadOnlyList<Order> all = OrderBook.All;
            int next = _orderIndex + 1;
            while (next < all.Count && !_career.IsUnlocked(all[next])) next++;
            _orderIndex = next >= all.Count ? -1 : next;
            _order = _orderIndex < 0 ? null : all[_orderIndex];
            _delivered = false;
            _resultText = null;
            if (_order == null) { NewBlank(); return; }
            NewWeapon();
            SelectAngleNear(_order.Spec.TargetEdgeAngleDeg);
            _env = ForgeEnv.Air;
            _status = Loc.F("Order: {0}. {1}", Loc.T(_order.Client), Loc.T(_order.Brief));
            _resultText = Loc.F("ORDER {0} for {1}\n{2}\nPays up to {3}, blank costs {4}. Needs score {5}+.", Loc.T(_order.Title), Loc.T(_order.Client), Loc.T(_order.Brief),
                (long)System.Math.Round(_order.BasePay * OrderBook.GradeFactor("S")), _order.BlankCost, SandboxRules.F(_order.Spec.PassScore, 0));
            if (!_showReport) ToggleReport();
        }

        /// <summary>Hands the blade to the client: scored against the order, paid or rejected, with reasons.</summary>
        private void Deliver()
        {
            if (_order == null) { _status = Loc.T("Pick an order first (Order button)."); return; }
            if (_delivered) { _status = Loc.T("Already delivered. Press Order for the next one."); return; }
            BladeReport rep = _session.Evaluate(_order.Spec);
            OrderResult res = OrderBook.Settle(_order, rep);
            _career.Apply(_order, res);
            SaveCareer();
            WritePlayLog(res, rep);
            _delivered = true;
            var sb = new System.Text.StringBuilder();
            sb.Append(res.Delivered ? Loc.F("DELIVERED  grade {0}  paid {1}  (net {2})", res.Grade, res.Payout, res.Net) : Loc.F("NOT ACCEPTED  net {0}", res.Net));
            foreach (ReasonPart part in res.Parts) sb.Append("\n- ").Append(Loc.F(part.Template, (object[])part.Args));
            _resultText = sb.ToString();
            _status = res.Delivered ? Loc.F("Delivered: grade {0}", res.Grade) : Loc.T("Rejected");
            if (!_showReport) ToggleReport();
            _hudTimer = 0f;
        }

        public void CaptureSetOrder(int index)
        {
            _orderIndex = index;
            _order = OrderBook.All[index];
            _delivered = false;
            _resultText = null;
        }

        public void CaptureDeliver() { Deliver(); RefreshHud(); }

        /// <summary>Automation hook (PlayerAutomation -fb-scenario forge): pick the station and time scale as a player would.</summary>
        public void AutomationSetEnvAndSpeed(ForgeEnv env, double timeScale)
        {
            _env = env;
            int best = 0;
            for (int i = 1; i < SandboxRules.TimeScales.Length; i++)
                if (System.Math.Abs(SandboxRules.TimeScales[i] - timeScale) < System.Math.Abs(SandboxRules.TimeScales[best] - timeScale)) best = i;
            _speedIndex = best;
            _hudTimer = 0f;
        }

        private bool _tutFrozen; // capture only: keep the shown tutorial step even if its goal is already met

        /// <summary>Screenshot hook: shows tutorial step <paramref name="id"/> and stops it advancing.</summary>
        public void CaptureTutorialStep(string id, bool mandatory)
        {
            _tutFrozen = true;
            _tutorial.Mandatory = mandatory;
            _tutorial.JumpTo(id);
        }
        public void CaptureRefreshGoals() { _goalTimer = 0f; UpdateGoals(); }
        public void CaptureRefreshAdvice() { _adviceTimer = 0f; _adviceEval = 0f; UpdateAdvice(); }
        public string CaptureAdviceText() { return _adviceText != null ? _adviceText.text : ""; }

        private void NewBlank()
        {
            _blankIndex = (_blankIndex + 1) % 2;
            NewWeapon();
            _env = ForgeEnv.Air;
            _status = Loc.T(_blankIndex == 0 ? "new tapered blank" : "new beveled bar");
        }

        private void SaveCareer()
        {
            if (!_persistCareer) return;
            try
            {
                string tmp = CareerPath + ".tmp";
                File.WriteAllText(tmp, _career.Serialize());
                if (File.Exists(CareerPath)) File.Delete(CareerPath);
                File.Move(tmp, CareerPath);
            }
            catch (System.Exception e) { UnityEngine.Debug.LogWarning("Career save failed: " + e.Message); }
        }

        private void LoadCareer()
        {
            try { if (File.Exists(CareerPath)) _career = Career.Parse(File.ReadAllText(CareerPath)); }
            catch (System.Exception e) { UnityEngine.Debug.LogWarning("Career load failed: " + e.Message); }
        }

        private void Save()
        {
            try
            {
                WeaponFile.SaveAtomic(SavePath, _session.Capture());
                _status = Loc.T("saved");
            }
            catch (System.Exception e) { _status = Loc.F("save failed: {0}", e.Message); }
        }

        private void Load()
        {
            WeaponSnapshot snap;
            string err;
            if (WeaponFile.TryLoadWithRecovery(SavePath, out snap, out err))
            {
                AttachSession(ForgeSession.FromSnapshot(snap));
                _startMassKg = _session.MassKg;
                _status = Loc.T("loaded");
            }
            else _status = Loc.F("load failed: {0}", err);
        }
    }
}
