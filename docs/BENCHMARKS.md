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
