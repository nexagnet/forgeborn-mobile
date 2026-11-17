# Implementation status

Last updated: 2026-11-17. Only facts verified on the dev PC are marked done.

## Platform status (never conflate these)

| Item | Status |
|---|---|
| Domain kernel, `dotnet test` (headless) | **OK** — 21/21 tests pass (.NET 8.0.428, Windows 11) |
| Unity Editor 6000.3.28f1 | Installed; setup + 2 EditMode tests pass (see DEVICE_MATRIX.md) |
| Unity Remote 5 on iPhone | **Not verified** — iPhone not yet connected/confirmed |
| Android build / device | Not verified (build stopped, no device) |
| iOS Xcode export / iPhone | Not verified (no Mac, Xcode, signing, device) |

## Milestones

| Milestone | Status | Evidence / blocker |
|---|---|---|
| M0 feasibility gate | **Partly blocked** | Editor OK; device proof needs iPhone via Remote (pending) and a Mac for native iOS |
| M1 geometry bench — Domain part | **Done (headless)** | Tests below |
| M1 geometry bench — Unity part (mesh, touch, scene) | **Code done, device-unverified** | `BladeMeshBuilder`, `ForgeSandbox`, on-screen pressure; EditMode 2/2 |
| M2 hammer | Not started | |
| M3 heat / quench / temper | Not started | |
| M4–M6 | Not started | |

## What exists (Assets/_ForgeBorn/Scripts/Domain)

- `Geometry`: `Vec2d/Vec3d`, `BladeProfile` (stations x perimeter rings), `BladeFactory`, `BladeRaycaster` (analytic, no MeshCollider).
- `Commands`: `AbrasiveWheel`, `GrindStrokeCommand`, `GrindSolver` (distance/time-bounded substeps, monotonic removal,
  min-thickness clamp, through-cut = `Severed` station, self-intersection back-off).
- `Session`: `ForgeSession` (transactional apply with exact rollback, duplicate command ids, audit log, snapshots).
- `Persistence`: `WeaponSerializer` (schema 1, SHA-256, size-bounded) and `WeaponFile` (atomic save, `.bak` recovery).

## Verified by tests (build/ForgeBorn.Domain.Tests)

Analytic volume (rect, tapered), mass = volume x density, center of mass, ray hits/misses, no-contact removes nothing,
constant contact strictly reduces mass and removed mass equals geometric difference, tool lift stops removal, 40 random
strokes keep geometry valid and volume non-increasing, fast swipe equals dense stroke (within 5%), through-cut,
invalid command rejected with state untouched, duplicate id rejected, replay gives identical geometry, finer wheel removes less,
save/load round trip, corruption detected, interrupted-write recovery.

## Known limitations / design deviations

- Removal model is a calibrated gameplay model (not a grinding equation). No heat, wheel wear, bevel metrics, hammer yet.
- Raycast steps at 0.25 mm: features thinner than that can be missed.
- Replay equality is exact on one machine; cross-platform (iOS vs Android) parity is not tested and must use tolerances.
- Centerline/bend per station (needed for M2) is not in the data model yet.
- Placeholder/procedural only; no art, audio, shaders, touch UI.

## Next blockers / tasks

1. User installs Unity 6.3 LTS with Android Build Support (see README).
2. Then: Unity project settings, `BladeMeshBuilder`, touch input (WORK/INSPECT), HUD mass meter, APK build, `docs/DEVICE_MATRIX.md`.
