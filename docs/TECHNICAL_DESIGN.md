# FORGEBORN MOBILE — TECHNICAL DESIGN DOCUMENT (TDD)

**Version:** 3.0 — Mobile production architecture  
**Status:** Implementation specification; not an implemented product  
**Language:** Vietnamese (source code identifiers in English)  
**Platforms:** iPhone (iOS) and Android phones; **native installable game**, not web/PWA  
**Orientation:** Portrait, with safe-area support  
**Project principle:** Quality, responsiveness, and believable manufacturing simulation have priority over one-codebase purity or speed of delivery.

---

## 0. CONTRACT FOR CLAUDE CODE — READ FIRST

You are the implementation engineer. **Create a real playable mobile game**, not a design mock-up, not a video, and not a browser demo.

### Non-negotiable
1. iOS is a **first-class release target**, equally important as Android. Both must be tested on real devices. A working Android build, Editor play mode, or iOS Simulator is **not proof** that the iPhone version works.
2. Prioritize **high-quality 3D graphics, mobile UX, accurate geometry updates, and sustained performance**. Take additional implementation time if necessary. Never simplify away actual deformation/removal of the playable blade.
3. **Shared physical rules, platform-specific optimization**: share the domain/simulation kernel and game rules when mathematically useful; use separate iOS/Android configurations, assets, rendering profiles, build processes, performance tuning, and native plugins as needed. **Two completely independent implementations are not required** and would make parity/fairness harder. If benchmarks demonstrate a platform-specific kernel is necessary, implement it behind a stable interface and prove numerical parity with golden tests.
4. Every claimed milestone requires tangible evidence: tests, in-engine screenshots/video, profiling output, and—where hardware and signing are available—a playable signed test build. Never claim a device build was tested without access to that device.
5. If the host has Windows only, develop and test with Unity Editor / Android, but **set up macOS/Xcode CI or a Mac validation path immediately**. Report iOS build/signing as *unverified* until completed. Do not promise App Store approval.
6. No premium assets, credentials, Apple signing certificates, or backend secrets are to be invented. Generate procedural placeholders only for early function tests, then replace them with licensed production assets.
7. Never rely on visual effects alone to represent metal removal, hammer deformation, overheating, hardening, or weapon damage. Changes must affect saved geometric/material state and evaluation.
8. Keep a working offline vertical slice at every stage. Multiplayer and realistic person-to-person sword fighting are late-stage projects, not prerequisites for the forge simulator.

### Deliverables
- Unity project that can be opened by the pinned Editor version; source and assets in repository.
- iOS Xcode export + documented build/signing workflow and actual on-device build test when possible.
- Android APK for internal testing and AAB export for distribution.
- Playable offline forge scene with touch controls; independent geometry/material systems.
- Reproducible tests for mass conservation, material removal, save/load, geometry integrity, heat model, device input and determinism bounds.
- Technical README, architecture decision records (ADRs), mobile QA reports, profiling captures, clear known limitations.

---

## 1. PRODUCT VISION AND QUALITY BAR

FORGEBORN is a **3D mobile hardcore weapon-forging simulator**. The player shapes a metal blank through heating, hammering, grinding, heat treatment, assembly, inspection, and takes the actual resulting weapon to competitive modes.

### Player fantasy
- Same blank + equipment + time, but different skill → different final weapon.
- Every contact with the wheel takes away real geometry. A mistake is not automatically reversible.
- Hammer strokes plastically deform metal; shape and mass respond to temperature/strength and tools.
- Beautiful-looking weapon can be structurally bad; rough-looking weapon may perform better.
- Inspect → understand the actual geometric/metallurgical tradeoff → learn → forge again.

### Priority order
1. **Correctness & meaningful physical outcomes**.
2. **Satisfying touch-feel** (accurate control, haptic/audio/visual response without obstructing view).
3. **Art direction and 3D lighting/materials**.
4. **Sustained mobile performance and battery/temperature management**.
5. **Gameplay balance and progression**.
6. **Online features**.

### Definition of 'realistic'
This is a **physically informed game approximation**, not certified metallurgical or engineering software. Never infer actual industrial safety, numerical stress limits, or metallurgical phase transition precision from gameplay values. State what is modeled and what is approximated in a debug/technical description.

---

## 2. TARGET PLATFORMS AND ENGINE

### Primary recommendation
- **Engine:** Unity **6.3 LTS**, pin an exact patch in `ProjectSettings/ProjectVersion.txt` after installation; do not silently upgrade.
- **Language:** C# for gameplay, simulations and tools; Burst + Unity Jobs + NativeArray for hot paths after profiling.
- **Rendering:** URP, physically based metal shaders, ACES-like color grading where supported, controlled bloom, reflection probes and baked lighting where possible.
- **Input:** Unity Input System + custom multitouch coordinator (no mouse-only assumptions).
- **UI:** uGUI or UI Toolkit chosen after a device prototype; use common design tokens and platform-specific safe-area adaptation.
- **Camera:** Cinemachine only if it simplifies scene camera, otherwise custom constrained camera to minimize overhead.
- **Asset streaming:** Addressables when asset count makes it necessary; not required for first prototype.
- **Offline state:** versioned local save with atomic writes and checksum.
- **Online later:** authoritative server with accounts, rooms, matchmaking and validation/replay of forge actions.

### iOS production path
- Target real iPhones, Metal graphics backend, IL2CPP and ARM64.
- Install Unity iOS Build Support; export Xcode project; build/sign with a supported Xcode on macOS (local Mac or suitable managed macOS build service).
- Test via direct device install / TestFlight as appropriate. Developer account/distribution prerequisites must be supplied by project owner.
- Test on at least one older supported iPhone and one recent iPhone; verify notch, Dynamic Island, home indicator and safe area.
- Avoid relying on shader features unavailable on Metal mobile. Validate all shaders and native plugins on actual devices.

### Android production path
- ARM64 primary architecture, IL2CPP for release parity; Android build support installed.
- Build APK for testing, AAB for Google Play. Setup separate quality tiers and Graphics API selection after profiling.
- Test at least one middle-range Android device and one high-end device from different GPU families.

### Device target proposal (to revise after profiling)
- **Recommended tier:** iPhone 13-class or newer; equivalent Android with >= 6 GB RAM.
- **Low supported tier (candidate, not a guarantee):** iPhone 12-class and capable Android with >= 4 GB RAM.
- **Product minimum OS proposal:** iOS 17+ and Android 10+; verify the pinned Unity/SDK/App Store/Google Play build requirements and revise product policy before release.
- **Performance targets:** stable 30 fps on validated lower tier; 60 fps preferred on supported high tier while actively grinding; no major input latency spikes; no thermal collapse in a 15-minute continuous forging soak.
- Adaptive graphics: render scale, shadow quality, reflections, particle count, texture quality, mesh update frequency **without changing simulation quality or PvP results**.

### Why not separate full game engines per OS?
Duplication of forging logic could produce two different physics outcomes and double QA cost. **Use one engine and a shared authoritative game model by default**, but permit platform-specific native/Metal/Vulkan code when profiling proves a real gain. Quality is validated by shipped behavior, not the number of codebases.

---

## 3. GAME MODES AND CORE LOOP

### Offline solo workshop (first shipped mode)
1. Choose training contract, blank and available tools.
2. Heat sections of blank.
3. Hammer into desired rough geometry.
4. Grind blade bevels, contour, tip, and finish; select abrasive wheel.
5. Quench and temper using an intelligible simplified process.
6. Assemble grip/guard/counterweight.
7. Inspect shape and material properties; save weapon.
8. Try weapon in deterministic testing range; compare previous attempts.

### Future multiplayer
- **Forge Duel 1v1**: both receive identical material/tool budget, 3–5-minute abbreviated forging challenge, ranked by shared standardized evaluation.
- **Weapon Trials**: asynchronous comparative cutting/durability/handling simulations; rankings by conditions and category.
- **Weapon Arena**: initially auto-battler with strategic choices; later optionally interactive real-time combat as a separately scoped project.

### Failure and fairness
- Solo training permits checkpoint restore and unrestricted learning.
- Ranked forging is event-logged and non-undoable, with standardized equipment and no pay-to-win stat boosts.
- Avoid making precise touch gestures depend on device DPI or screen size. Interpret actions in **world-space and normalized physical units**, not raw pixel distances.

---

## 4. MOBILE UX DESIGN — PORTRAIT FIRST

### Visual structure
- Upper 65–75%: 3D work area, strong focal subject and unobstructed tool contact zone.
- Upper status strip: current stage, heat state, warnings, pause/settings.
- Bottom action dock: selected tool, tool options, pressure/angle controls, Observe/Work mode, large primary action.
- Side drawer: optional detailed settings; collapsed during precision operations.
- Use Android/iOS safe area; responsive on 19:9–22:9 screens, small phones, and accessibility font sizes.
- No tiny (<44–48 dp approximate) critical touch targets; measure in device-independent physical sizing, not arbitrary pixels.

### Two mutually exclusive interaction modes
**WORK**: strokes/presses modify the weapon. Camera cannot accidentally rotate.  
**INSPECT**: orbit, pan, pinch zoom; weapon cannot be modified.

### Heating interaction
- Drag blank into hot zone / out of furnace; slider for airflow; visual thermal glow with optional thermal camera overlay.
- Heating/cooling progresses by simulation time while active. Show *approximate* color-temperature relationship; never treat color as an accurate thermometer.
- Provide an explicit handoff to anvil.

### Hammering interaction
- Tap on visible blank to select target contact; drag a handle or long-press to set energy, release to strike.
- Alternate one-handed accessibility control with stepwise precise aiming + power button.
- Pinned small gizmo indicates tool footprint/contact angle; brief haptic response on impact.
- Rotate blank using separate intentional control, not a gesture overlapping a strike.
- Player can choose flat/rounded/peen hammer with different footprint kernels.

### Grinding interaction: THE SIGNATURE MECHANIC
- Choose wheel type (coarse, medium, fine, profiled, polish) + RPM range/operational mode.
- In WORK mode, finger path sets grind trajectory on the exposed visible surface; raycasting maps finger to 3D contact; pressure and angle set via adjacent controls or second thumb.
- Contact only changes geometry when brush projects onto valid material, within reachable tool angle and with actual surface intersection.
- Show contact patch, emitted sparks, drag audio and subtle haptics as feedback **derived from computed removal**.
- When finger lifts, stroke ends; no invisible further grinding. Provide precise slow-control zoom with contact-locked camera.
- INSPECT shows false-color thickness, asymmetry, deviations from desired outline, local heat and tool marks.
- Grinding never adds material. Polishing can smooth surface micro-roughness but cannot repair large geometric damage.

### Quench/temper interaction
- Show current workpiece temperature and environment, allow a limited selection of media and process timing for gameplay.
- Use a user-friendly sim with hardening/toughness/distortion tradeoff; avoid false claims about industrial heat treatment.

### Assembly interaction
- Choose handle/guard/pommel; snap/align components; balance updates immediately.

### Haptics and sound
- iOS haptics through adapter wrapping supported native haptic APIs where useful; Android vibration/haptics through Android adapter; fallback to no haptics.
- Spatial metal contact, machine motor, sizzling, quench, ambient furnace. Separate tracks for dynamic mix; no infinite repetitive loud spark sound.
- Settings: reduced motion, reduce flashing, haptics off, sound and high contrast, left-hand layout.

---

## 5. SIMULATION ARCHITECTURE

### Separation of responsibilities
```
Touch Gestures
  -> ForgeCommand (normalized world-space, timestamped, validated)
  -> Simulation (geometry + thermal + material + tool state)
  -> Geometry/Material State + Integrity Checks
  -> Mesh Rebuild (incremental, render only)
  -> VFX/Audio/Haptics (derived from simulation result)
  -> Evaluator / Save / Future Network
```

A **single source of truth** is `WeaponState`. Never infer the physical state by scraping the visual Unity Mesh. Rendering is a projection of simulation state.

### Coordinate system and units
- SI internally: meters, kilograms, seconds, Kelvin / Celsius explicitly converted; force newtons, energy joules, stresses pascals when applicable.
- Blade local axis: +X length (tang to tip), Y width, Z thickness; world transform stored separately.
- Simulation timestep: fixed or explicit command delta, with upper bounds; render frame rate may differ. Device-independent actions.
- Keep design dimensions from JSON / ScriptableObjects; test benchmark blade ~0.65–0.8 m long.

### Interfaces (illustrative contracts; may refine in actual source)
```csharp
public interface IWeaponGeometry {
    double VolumeM3 { get; }
    Vector3 CenterOfMassLocalM { get; }
    GeometryChange ApplyHammer(in HammerCommand command, in MaterialField material);
    GeometryChange ApplyGrind(in GrindCommand command, in AbrasiveWheel wheel);
    bool Validate(out string reason);
    GeometrySnapshot Capture();
}

public interface IThermalSimulation {
    void Step(double deltaSeconds, in HeatBoundary boundary);
    float SampleTemperatureK(Vector3 localPoint);
}

public interface IWeaponEvaluator {
    WeaponMetrics Evaluate(in WeaponState state, in TestScenario test);
}

public interface IPlatformFeedback {
    void OnHammerImpact(float strength01);
    void OnGrindContact(float intensity01);
}
```

**Do not implement direct unsafe mutations in MonoBehaviour UI handlers.** Commands queue to simulation, are validated and applied as a transaction; update state version and audit log after success.

---

## 6. REAL GEOMETRIC DEFORMATION & MATERIAL REMOVAL

### Strategy: quality-first, benchmarked representations
**Phase A (functional, mobile-friendly):** cross-section solid model: axial rings with a perimeter sampled around each cross-section; asymmetric geometry, local shape, edge and tip adjustments; surface interpolation and local detail layers. This permits actual movement/removal of vertices and mass calculation at controlled cost. Aim initial 192–256 longitudinal stations × 32–64 perimeter samples. **These are initial profiling hypotheses, not hard quality limits.**

**Phase B (only if needed):** hybrid local high-resolution SDF / adaptive volumetric patches for arbitrary grooves, complex freeform details, and cleanly removed tip chunks. Use chunked meshing and measured memory budgets. Do not force 3D voxel everywhere if mobile quality/regression tests favor profile geometry.

- Visual bevels must correspond to geometry, not normal-map-only tricks.
- If a feature is finer than physical resolution, label as cosmetic and do not use it to cheat physical metrics.
- No global per-frame `MeshCollider` rebuild. Analytical collision with the blade model / ray-broadphase and local contact refinement; mesh/collider refresh only at controlled safe points.
- Rebuild only dirty sections; pool buffers; avoid per-stroke managed allocations and GC spikes.
- Use indexed triangle topology consistently, validate manifoldness / no negative thickness / normals winding.

### 6.1 Hammer deformation (physical approximation)
Inputs: impact center, contact normal, energy, tool footprint, local T, effective flow stress, strain/volume state.

Algorithm:
1. Reject no-contact, invalid direction, empty material, and temperature state outside allowed gameplay limits; do not fabricate an impact.
2. Compute deposited plastic work capped to avoid singular deformations; relate to local temperature-dependent flow stress.
3. Apply a **bounded, smooth spatial indentation/displacement kernel** across contact patch, with neighbor material flow and constrained boundary conditions.
4. Correct volume deviation locally by distributing lateral/axial displacement in physically plausible directions. Log numerical correction, not invisible mass creation.
5. Update geometry, local strain/work hardening proxy, temperature change and surface condition.
6. Validate topology, center of mass and volume, then commit atomically. If invalid, reduce step and retry / reject gracefully with telemetry.

**Hard invariant:** hammering (without lost fragments or scale) approximately conserves metal volume. Define tolerance against numerical integration baseline; prototype target <= 1% total drift after scripted 100-stroke test. If modeling scale loss explicitly, account for lost mass separately.

### 6.2 Grind removal
Inputs: contact path in local 3D, wheel profile, normal, pressure, speed, dwell time, abrasive wear, material hardness/temperature.

Algorithm:
1. Cast touch ray, calculate valid contact patch from wheel/metal intersection; reject if no contact.
2. Subdivide path by **world-space distance and dt bounds**, not screen pixels; ensure fast swipes cannot tunnel through features or skip contact.
3. Estimate a bounded removal depth/volume using a monotonic wear model (calibrated gameplay coefficients, inspired by contact/abrasion mechanics; not a claim of an industrial grinding equation).
4. Subtract material in contacted local surface region; clamp to actual remaining thickness/volume. Track removed mass as chips/dust; never allow negative material.
5. Estimate local heat generation and tool wear; grinding heat may harm material state.
6. Recompute affected surfaces, volume/mass, edge metrics and normals, trigger VFX with intensity proportional to actual removal.

**Hard invariants:**
- Grind without contact → `removedVolume == 0`.
- Grind with valid pressure/dwell → `removedVolume > 0` (until material is gone/invalid).
- No grinding action may increase weapon volume or mass.
- Removed metal mass must equal geometric mass difference within numerical tolerance.
- Grinding can cut through a blade, weaken or destroy it.
- Undo is allowed only in Training Mode, from captured state checkpoint.

### 6.3 Bevel and sharpness
- Store local edge wedge angle, bevel symmetry, edge radius proxy, thickness behind edge, roughness, nicks.
- Sharpness is **not** just sharpening time. Extremely thin edge may cut well but chip more easily; thick edge tolerates impacts.
- Derive game metrics from sampled edge cross-sections and material field, with bounded normalized transformation.

### 6.4 Material volume & mass
For density `rho`, mass = integrated geometry volume × `rho` (plus assembly component masses). Use a closed triangle mesh volume integral or sectional integration. Compare independent implementations in tests (e.g. analytic rectangular blade, tapered blade).

### 6.5 Rendering work budget
- CPU simulation update and mesh upload decoupled from render cadence.
- Partial rebuild and batching before full mesh replacement. Queue recent contact marks if mesh visual update is throttled, but **physical state must apply on command arrival**.
- Low-cost geometry collision query from simulation profile; no expensive GPU readback in gameplay path.

---

## 7. THERMAL & MATERIAL MODEL

### Thermal field
- Coarse field aligned with metal sections/cells (do not use a single global temperature).
- Simplified thermal equation: heat diffusion + external furnace/air/water/oil boundary exchange + grinding heat/impact heat where applicable.
- Use stable explicit time step if CFL constraints met or robust implicit/coarse integration for stability. Clamp to plausible finite ranges; validate conservation trend in isolated test.
- UI: glow and heat map from thermal field, progressive cooling; avoid flicker and exact-sounding scientific precision.

### Material field
Per sampled area: density, specific heat, thermal conductivity, effective flow stress, hardness proxy, toughness proxy, residual stress proxy, local defect/damage scalar, phase/heat treatment state (simplified).

### Forging and heat treatment
- Hot forging: resistance decreases with temperature in defined ranges, with property table / curve data.
- Quench: rate influences hardening potential and risk of distortion/cracking.
- Temper: trade some hardness for toughness after quench, bounded by steel class and previous process.
- Do **not** simulate microscopic martensite grains as literal particles in MVP. Use calibrated coarse phase fractions / response curves and clearly mark as model approximation.
- Support multiple alloys through material data, not hardcoded switch statements.

### Damage & defects
- Track grinding burn, edge thinning, cracks, bend, residual stress, chatter marks (visual vs structurally consequential differentiated).
- Failure risk computed from load case + local section + toughness/hardness + defects, not random blanket stat penalty.
- Avoid deterministic guarantee of fracture without validated load thresholds; clearly represent it as game model.

---

## 8. TOOLING AND SHOP SYSTEM

`ToolDefinition` data (ScriptableObject + serialized canonical identifiers):
- Hammer: head geometry, footprint kernel, mass, tool efficiency, permitted actions.
- Grinding wheel: profile radius, grit, contact footprint, effective removal coefficient, wear, heat generation coefficient, speed envelope.
- Furnace: heat transfer curves, air control, hot zone.
- Quench bath: cooling response characteristics, capacity/contamination simplified.
- Inspection tools: unlock measurements, precision and uncertainty.
- Handle/guard/pommel: mass, position, attachment constraints, optional durability.

Allow wheel selection to matter: rough removal faster but harder to control; fine finishing improves roughness but low removal; polishing cannot restore lost metal.

---

## 9. WEAPON EVALUATION — DERIVED, REPRODUCIBLE

Do **not** assign arbitrary level-based damage bonuses.

Raw physical/game parameters:
- Blade mass `kg`, overall length `m`, center of mass `m`, inertia about grip `kg*m^2`.
- Edge profile by station, effective sharpness proxy, curvature/symmetry, maximum edge damage.
- Local hardness/toughness proxies, bulk stiffness proxy, defects and thermal damage.
- Assembly balance, grip size, mechanical constraints.

Derived gameplay metrics:
- `handling`: from inertia and balance relative to target weapon class.
- `cutting`: edge geometry + swing speed assumption + test target type + material properties.
- `durability`: weakest vulnerable zones + defect distribution + impact test case.
- `piercing`: tip geometry + rigidity + point loading.
- `deflection/defense`: geometry and stiffness in standardized scenario.

Raw metrics must be visible in developer diagnostics and used for balancing. Player-facing scores (0–100) are **context-dependent mappings**, with category-specific benchmarks. Do not use a single universal power score to pick PvP winners. E.g. small blade excels at fast cuts, heavy blade excels under different assumptions.

Weapon inspection:
- 3D heatmaps: thickness, thermal damage, bending, balance, estimated weak spots.
- UI reveals measurements depending on inspection instrument, not hidden paywall.
- Explain causes with event reference: e.g. “left bevel at 72% of length became too thin during grinding stroke 48”.

---

## 10. GRAPHICS, AUDIO, ART QUALITY

### Visual target
- High-fidelity **small workshop scene** rather than huge open world: detailed anvil, furnace, sparks, rotating wheel, forging tools, convincing materials.
- Directional key lighting + warm emissive metal with controlled HDR/bloom; metal reflection via reflection probes and material roughness.
- Real geometric silhouette and bevel specular reflections; clean close-up shaders.
- Particle effects tied to contact energy/material removal; cheap particle tiers for slower devices.
- High-quality sound layers for hammer, grinding pitch/contact, ambient forge, quench and UI.

### Asset acceptance
- No default cubes/spheres as the **final** player-visible art. Procedural placeholders are valid during geometry prototypes only.
- Every texture/model used must have provenance and license record.
- Avoid photo-real claims until actual high-quality assets, animation, lighting and on-device video are demonstrated.
- Mobile texture compression target: iOS ASTC; Android ASTC for supporting devices with appropriate fallback; validate actual device compatibility.

---

## 11. PHYSICS / RESPONSIVENESS / DEVICE PERFORMANCE

### Threading rules
- Unity objects on main thread, pure simulation jobs off-main where safe.
- Burst Jobs and NativeCollections after a measurable bottleneck; no unmanaged or native plugin architecture before proof.
- No per-contact `new Mesh`, `GetComponent`, large array allocation, `Instantiate` particles or collider rebuild loops.
- Fixed-point or bounded quantized commands for replay fairness where required; **do not assume floating-point bitwise identical between iOS and Android**.

### Initial profiling targets (must be measured)
| Metric | Lower tier target | Upper tier target |
|---|---:|---:|
| FPS during continuous grind | >=30 sustained | 60 preferred |
| Frame time p95 | <=33 ms | <=16.7 ms |
| Grind physical response | Within next sim tick | Within next sim tick |
| Geometry visual response | <=100 ms (95th) | <=50 ms (95th) |
| 15 min forge stress | No crash/thermal collapse | No crash/thermal collapse |
| Touch input | No unintended camera motion in WORK | Same |

These are **design targets** to be updated after hardware measurements. Always report device name, OS version, resolution, render scale, quality tier, FPS p50/p95, memory, battery/thermal symptoms, and build hash.

### Quality tiers
- Keep **same simulation command/geometry resolution** for competitive game fairness; adjust only art/render effects and potentially visual update cadence.
- If geometric simulation resolution must differ for severe hardware constraints, establish a platform-independent canonical state/evaluator and prove statistically equivalent outcomes in regression corpus; otherwise disallow ranked modes on that tier.

---

## 12. SAVE FORMAT & VERSIONING

Use a versioned compact binary snapshot or JSON-plus-binary blobs:
```json
{
  "schemaVersion": 1,
  "weaponId": "uuid",
  "materialId": "steel_basic_v1",
  "geometryEncoding": "cross_section_v1",
  "geometryBlob": "binary reference",
  "thermalFieldBlob": "binary reference",
  "materialFieldBlob": "binary reference",
  "assembly": [],
  "toolState": {},
  "qualityModelVersion": "1.0.0",
  "forgeActionLogRef": "path or id",
  "timestampUtc": "ISO-8601",
  "checksum": "SHA-256"
}
```
- Atomic save (temp file -> verify -> replace); corruption recovery from last valid snapshot.
- Separate mutable local training records from server-approved ranked weapon identities.
- Migration tests for old schema; NEVER parse untrusted blobs without size bounds and integrity checks.
- Offline play must not require login or network.

---

## 13. MULTIPLAYER ARCHITECTURE (AFTER OFFLINE VERTICAL SLICE)

### Forge Duel protocol
- Matchmaker groups users by forging skill rating, latency/region and fair equipment category; server controls initial seed/material/tool budget, deadline and scoring model version.
- Clients send normalized high-level **intent commands**, not arbitrary end-state score or mesh.
- Use fixed/quantized coordinates, fixed step where possible, bounded deltas and rate limits; server validates/replays authoritative forging actions.
- Do not promise bitwise cross-architecture determinism. Server runs authoritative canonical simulation (same rules/version) and reconciles divergence.
- Preserve compact logs for replay/dispute. Disconnect policy, clock sync, retry/duplicate action IDs and anti-cheat checks.

### Early backend scope
- Authentication (guest/login later), matchmaker, lobby, room lifecycle, score submission/replay verification, immutable result record and seasonal leaderboard.
- No real-money staking, gambling or unfair paid stat advantage.
- A single-player game does not need a server; defer infra until solo is fun and benchmarked.

### Arena
- Weapon Trial is easier to validate than real-time sword fighting. Implement standardized deterministic target simulations first; postpone character animation, hit detection and live PvP netcode.

---

## 14. UNITY REPOSITORY LAYOUT

```text
FORGEBORN/
├── README.md
├── docs/
│   ├── TECHNICAL_DESIGN.md
│   ├── ROADMAP.md
│   ├── IOS_BUILD.md
│   ├── ANDROID_BUILD.md
│   ├── PERFORMANCE_BUDGET.md
│   ├── ART_GUIDE.md
│   ├── KNOWN_LIMITATIONS.md
│   └── adr/
├── Assets/
│   ├── _ForgeBorn/
│   │   ├── Art/{Models,Materials,Textures,VFX,Audio}/
│   │   ├── Data/{Materials,Tools,Contracts}/
│   │   ├── Prefabs/{Workshop,Tools,UI,Weapons}/
│   │   ├── Scenes/{Bootstrap,Workshop,Inspection,Trials}/
│   │   ├── Scripts/
│   │   │   ├── Domain/{Geometry,Thermal,Material,Evaluation,Commands}/
│   │   │   ├── Application/{ForgeSession,Save,Input,Progression}/
│   │   │   ├── Presentation/{Render,Camera,UI,VFX,Audio}/
│   │   │   ├── Platform/{Common,IOS,Android}/
│   │   │   ├── Networking/
│   │   │   └── Editor/{Generators,Build}/
│   │   └── Tests/{EditMode,PlayMode,Device,Fixtures}/
├── Packages/manifest.json
├── ProjectSettings/
├── .gitignore
└── .github/workflows/ (only if CI provider supports actual licensed Unity builds)
```

Use assembly definitions to keep `Domain` independent from UnityEngine UI/scene classes, where feasible. Avoid huge "GameManager" god-objects and hidden static state.

---

## 15. MILESTONES / IMPLEMENTATION ORDER

### M0 — iOS & Android feasibility gate (FIRST)
- Create Unity 6.3 LTS URP project; pin Editor patch and dependencies.
- Build portrait one-room test scene with touch orbit and dynamic mesh edit.
- Export Android APK; export iOS Xcode project. Configure signing workflow, install on an iPhone if Mac/signing/device exists.
- Record actual status separately: `Editor OK`, `Android device OK`, `iOS Xcode build OK`, `iPhone device OK`; do not conflate.
- Test Metal shader compatibility, safe areas, texture compression, IL2CPP startup, native device input.
- Deliver evidence in `docs/DEVICE_MATRIX.md` and identify missing hardware/credentials explicitly.
- **Gate:** No iOS-only incompatible system may be selected without replacement/test plan.

### M1 — Geometry test bench
- Single blank in 3D, debug gizmos and mass meter.
- `ApplyGrind` removes actual surface geometry on finger drag; section highlighting and tool visualization.
- Cross-section/profile representation and 3D mesh rebuild from canonical state.
- Tests: valid removal, no-contact, mass conservation under no-op, topology, cross-platform command replay tolerance.
- No flashy particles necessary yet.
- **Gate:** player can grind a visually measurable groove and the saved weapon loses correct mass.

### M2 — Hammer
- Target, power and tool footprint; localized deformation with approximate volume preservation.
- Heat-dependent resistance. Multi-hit test, bend/asymmetry, undo for training only.
- **Gate:** position and energy materially change geometry, volume drift within declared bounds, invalid topology never committed.

### M3 — Heat + quench + temper
- Local temperature distribution, furnace interaction, cooling curves, material property transitions and regression tests.
- **Gate:** different valid heating/temper histories of same shape produce different evaluated properties; thermal sim stable across time step changes.

### M4 — Mobile production UX / aesthetics
- Real workshop, tool models, carefully art-directed metal shaders, reflection, VFX, audio and haptics.
- 2-mode input, accessible large touch targets, left-handed mode and mobile device testing.
- **Gate:** 10 consecutive tool operations without unintended camera manipulation; stable first 15-minute stress pass on validation devices.

### M5 — Evaluation, inspect, save and solo game
- Inspect heatmaps, raw metrics and bounded player metrics, local save/reload, 3 practice contracts and weapon trials.
- **Gate:** save/load retains geometry and physical state; weapon trials reflect expected tradeoffs rather than arbitrary scores.

### M6 — Release-grade offline mobile vertical slice
- Device matrix extended, crashes and thermal regressions addressed, refined audio/art, tutorial and onboarding.
- Both Android and iOS builds validated on physical devices; if one unavailable, **M6 remains incomplete**.
- Capture gameplay from actual phones, not Editor only.

### M7 — Online Forge Duel (optional later milestone)
- Back-end authority, fair-match recipe, network state/replay, anti-cheat, latency tolerance, reconnection, scoring.
- **Gate:** parity of canonical evaluation and stable matches under network loss simulations.

### M8 — Arena / marketplace (future)
- Detailed separate design and QA plan; do not start until M0–M6 success.

---

## 16. AUTOMATED TEST PLAN

### Geometry tests
- `Grind_NoContact_DoesNotChangeMeshOrMass`
- `Grind_ConstantContact_MassStrictlyDecreases`
- `Grind_ToolLift_StopsRemoval`
- `Grind_MultipleSwipes_NoNegativeThickness`
- `Grind_FastSwipe_InterpolatesContinuousPath`
- `Hammer_100Impacts_VolumeDriftUnderTolerance`
- `Hammer_ImpactLocation_ChangesAffectedSection`
- `Hammer_HotVsCold_DifferentDeformationAtSameEnergy`
- `Mesh_NoNaN_NoSelfIntersection_NoInvertedNormals` (within supported representation constraints)
- `Mesh_SavedReloaded_GeometryEquivalent`

### Thermal / material tests
- `Heat_Furnace_GradualTemperatureRise`
- `Cool_Ambient_SensibleTemperatureDecay`
- `Heat_NoSource_NoRunawayEnergy`
- `QuenchVsTemper_ChangesHardnessAndToughnessTradeoff`
- `GrindingHeat_LocalizedDamageModel`

### Game / mobile tests
- `Touch_WorkMode_DoesNotMoveCamera`
- `Touch_InspectMode_DoesNotModifyWeapon`
- `Touch_SameNormalizedStroke_AcrossDPI_EquivalentGeometry`
- `Save_Atomic_WriteInterrupted_RecoversPreviousVersion`
- `Replay_SameSeedAndCommands_EquivalentWithinTolerance`
- `UI_SafeArea_iPhoneNotch_And_AndroidNavigationBar`
- `PauseResume_BackgroundForeground_NoCorruption`
- `IOS_IL2CPP_RealDeviceSmokeTest` (manual/CI device requirement; skip with explicit reason only)
- `Android_RealDeviceSmokeTest`

### Measurement & fixtures
- Store golden input commands and expected geometry checksum / volumes / material metrics, version them with model version.
- Numerical tolerances documented; detect threshold regressions, not flaky exact float equality.
- Compare cold/warm start, multitouch, force interruption, thermal throttling, rotated device (game must remain portrait), low battery mode and offline operation.

---

## 17. RISKS AND ENGINEERING DECISIONS

| Risk | Mitigation / stop rule |
|---|---|
| Deformation creates non-manifold mesh | Constrained geometry representation, transactional validation, rollback on illegal geometry |
| Real-time volumetric carving is too expensive | Start swept-section solid, local patch updates; benchmark local SDF alternative before switching |
| Visual illusion replaces simulation | Acceptance requires mass delta + saved geometry + stress result |
| High-end visual effects cause hot devices | Device thermal soak, adaptive render tiers, mesh update scheduling |
| iOS cannot be built on developer Windows machine | Use Mac / managed macOS build service; report as blocker, never claim iOS completed |
| Android/Apple simulation differs | Canonical command quantization, shared kernel or server-authoritative execution and golden tests |
| Complex metallurgy is scientifically misleading | Physically informed simplified model, transparent assumptions, tuning with credible reference data |
| Pay-to-win ruins fair matchmaking | Standardized ranked materials/tools and cosmetics-only monetization |
| Scope explosion (open world, sword combat, server) | Hard gates M0–M6 before multiplayer |
| Third-party art license problem | Provenance sheet and royalty/license checking |

---

## 18. DEVELOPMENT OPERATION RULES FOR CLAUDE CODE

1. **Inspect local environment**: OS, Unity Editor availability/version, platform modules, Xcode/Mac access, Android SDK/device, asset packages, repository structure. Do not assume external capabilities.
2. Create `docs/IMPLEMENTATION_STATUS.md` with all milestones and **actual** status; continue until tooling or objective blocker.
3. Create runnable Unity scenes and scripts; use Editor automation to generate consistent scenes/settings if editing Unity YAML by hand is fragile.
4. Build and run EditMode tests at every domain change; PlayMode tests and device smoke at each milestone.
5. When a feature requires artistic assets not available, use a temporary labeled primitive/asset **but do not mark visual quality completed**. Identify required assets and licensing.
6. Keep strong boundaries between canonical physics/geometry data and renderer. Use source control commits at stable milestones where configured; never destroy existing user work.
7. Run Unity batchmode compile/tests where available. For iOS, validate IL2CPP build with supported macOS/Xcode environment; do not confuse exporting Xcode project with successfully running on iPhone.
8. Report: repository path, installed version, completed features, proof artifacts, automated test counts, device matrix, benchmark results, exact next blocker, and next implementation task.
9. If only one platform can be tested, label release readiness **blocked** until the other passes. Never change the requirement silently.
10. Prioritize physically correct geometry/removal > advanced shaders > feature count; do not substitute procedural animation for geometry.

---

## 19. FIRST TASK TO EXECUTE

1. Read this document thoroughly and inspect the current machine.
2. Produce a **short ADR** confirming Unity 6.3 LTS + shared simulation + platform-specific rendering/build adapters (or prove a better approach with benchmark evidence). Do not stall implementation in endless debate.
3. Initialize the project and baseline build for both platforms.
4. Implement M0 and M1 immediately, including command-based simulation and a real grindable 3D blade.
5. Run tests and provide an in-game interactive result. Never claim iPhone support has been verified unless the iOS build has actually run on an iPhone.
6. Continue milestones in order; checkpoint reports should state evidence and blockers, not just completion percentage.

---

## 20. REFERENCES — OFFICIAL ENGINE DOCUMENTATION

- Unity 6 versions and official support policy: https://unity.com/releases/unity-6/support
- Unity 6.3 LTS announcement: https://unity.com/blog/unity-6-3-lts-is-now-available
- Unity iOS environment setup: https://docs.unity3d.com/2023.2/Documentation/Manual/ios-environment-setup.html
- Unity iOS build process (Unity exports Xcode project, Xcode builds app): https://docs.unity3d.com/2022.3/Documentation/Manual/iphone-BuildProcess.html
- Mesh writable data via Jobs: https://docs.unity3d.com/ScriptReference/Mesh.AllocateWritableMeshData.html
- Apply mesh data: https://docs.unity3d.com/ScriptReference/Mesh.ApplyAndDisposeWritableMeshData.html
- Unity Android build settings (APK/AAB): https://docs.unity3d.com/2023.2/Documentation/Manual/android-build-settings.html

**END OF TECHNICAL DESIGN.**
