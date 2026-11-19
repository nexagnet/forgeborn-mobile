# Domain microbenchmarks (B5)

**Evidence label: PC estimate (Domain, Release, .NET 8, Windows 11, Intel i7-10610U, Unity Editor open in the background).**
These are NOT iPhone or Android numbers. A phone can be several times slower or faster; thermal throttling is not captured.
Nothing here may be quoted as device performance. Real budgets are set only after Phase E measurements on hardware.

Reproduce: `dotnet run -c Release --project build/ForgeBorn.Bench`

Assumed budget used by the tool: 4 ms median per per-frame operation on this PC (a 16.7 ms frame at 60 fps with several times of
headroom for a slower phone). It is an assumption, not a measurement.

## Results (median ms, one run; timings on this machine are noisy, p95 in the tool output)

| Operation | S=129, P=32 | S=257, P=48 (default) | S=513, P=64 |
|---|---|---|---|
| Grind frame (2 samples, 20 mm fast swipe in 16 ms) | 0.96 | 2.57 | 7.96 (over budget) |
| Grind stroke (60 samples, 0.12 m), informational | 6.06 | 11.99 | 36.50 |
| Hammer blow | 0.15 | 0.34 | 1.14 |
| Evaluate blade | 0.57 | 0.37 | 0.73 |
| Thermal 10 s water quench (one-shot) | 16 | 23 | 48 |
| Thermal 1 h temper (one-shot) | 170 | 293 | 946 |
| Serialize + verify (save, one-shot) | 2.0 | 5.5 | 11.4 |

## What the benchmark found and fixed
1. A grind frame cost 5.6 ms at the default detail because the raycaster rescanned every ring vertex for bounds on each substep
   and the grind kernel ran a full O(P^2) self-intersection test per station per substep.
2. Fixes (behaviour preserved, tests green): bounds computed once per stroke (`BladeRaycaster.ComputeBounds`, valid because grinding
   only removes metal) and a self-intersection test limited to edges touching moved vertices
   (`StationSelfIntersectsTouching`, tested equal to the full test on 400 random perturbations).
3. Result at default detail: 5.6 ms -> 2.6 ms per fast-swipe frame.

## Reading these numbers honestly
- 513 x 64 is over the assumed budget; it stays a "high" tier candidate for Phase D, not the default.
- A 1 h temper at high detail takes about a second on the PC; the game must run long thermal commands as a time skip, not per frame.
- Substep size is the main remaining lever for grind cost (`MaxStepM` 0.5 mm was chosen for accuracy: 1.5 mm gave a 9.6 % error).
  A platform profile may relax it only with a measured accuracy test.

## Windows player (frame time), 2026-11-18

**Evidence label: Windows build (this PC): standalone Mono player, Direct3D11, Intel UHD Graphics (shared memory), i7-10610U, 16 GB, quality "Ultra", windowed 1280x720.**
Low-end baseline only. Nothing here says how the game runs on a stronger GPU, a phone or another screen size.

Reproduce: build with menu `ForgeBorn > Build Windows`, then
`build\out\win\ForgeBorn.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -fb-benchmark 8 -fb-out build\out\bench.json`.
vSync and the frame cap are switched off by the tool. Phases: 3 s idle, then 8 s of grinding (2 substeps per frame, mesh rebuild included).

| Phase | Frames | Avg ms | p95 ms | Max ms | FPS (avg) |
|---|---|---|---|---|---|
| Idle | 685 | 4.39 | 5.97 | 10.15 | 228 |
| Grind load | 500 | 16.03 | 22.31 | 54.09 | 62 |

What this says: at 1280x720 on this iGPU, grinding sits right at the 60 fps budget (16.7 ms) with hitches up to 54 ms. The phone-style UI stretched into a landscape window is part of the cost, so the number is a pessimistic starting point, not a verdict.

Not measured / known gaps:
- 1920x1080 windowed: the run hung at start-up and was killed (this screen is only 1536x864); cause not investigated.
- Thermal ticks (x600), the Low/Medium/High quality tiers, and any GPU other than Intel UHD.
- An earlier run showed exactly 16.7 ms per idle frame because `ForgeSandbox.Initialize` re-applied its 60 fps target after the tool had removed the cap; fixed by removing the cap after warm-up. Only the corrected run is quoted above.

## Windows build after the review fixes (2026-11-18, baseline for P4 shadows)

`build\out\win\ForgeBorn.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -fb-benchmark 8`, Intel UHD (i7-10610U), D3D11, quality Ultra, vsync off, one run.
Scene now has the FBX anvil, furnace and handle. The grind stroke changed (review M4): it now runs along the blade like `ForgeBornCapture`, so these grind numbers are **not comparable** with the table above.

| Phase | Frames | Avg ms | p95 ms | Max ms | FPS (avg) |
|---|---|---|---|---|---|
| idle | 605 | 4.96 | 7.03 | 9.68 | 201.6 |
| grind | 447 | 17.90 | 24.13 | 86.50 | 55.9 |

Mass removed 199 g. Single run on a noisy machine: treat as a floor, not a spec.
