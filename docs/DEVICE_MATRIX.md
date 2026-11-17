# Device / evidence matrix

Only facts observed on this project are marked OK. Evidence labels are never merged.

| Evidence | Status | Detail |
|---|---|---|
| Domain test (`dotnet test`, headless) | OK | 54/54 on .NET 8, Windows 11 (frame/twist, hammer, thermal, evaluation goldens, schema 2). Not yet independently reviewed |
| Domain microbenchmark (PC estimate) | Measured on PC only | See `docs/BENCHMARKS.md`; default detail grind frame ~2.6 ms on i7-10610U. Not device performance |
| Unity EditMode tests | OK | 18/18 headless (`Unity.exe -batchmode -runTests`) on 6000.3.28f1: mesh vs Domain incl. bent/twisted blade, heat glow/oxide/crack colours, platform profile, sandbox rules, report text |
| Editor (Windows, Intel UHD) | Compiles; shaders import without errors; **not visually reviewed** | Phase C scene (steel shader, sparks, tool UI) never looked at: the Editor window rendered blank in the last session. No screenshots exist |
| Unity Remote 5 (iPhone) | Image streaming OK (user-reported); touch/grinding **not yet confirmed** | Working setup: build target iOS, iOS module loaded, Store iTunes, `ForgeBorn > Set Remote iPhone` (device is not listed in the dropdown). Shows PC performance, not iPhone |
| iOS native build / iPhone performance | **Not verified** | Needs Mac + Xcode + Apple Developer |
| Android build | Not verified | IL2CPP build started and was stopped at ~45% (deprioritised); no APK produced |
| Android device | **Not verified** | No device available |

Notes
- Unity Remote streams the Editor's Game view; FPS, thermals, memory and Metal behaviour seen through it are the PC's, not the iPhone's.
- Pressure is an on-screen control (Press -/+), quantised to 5 N, not touch force.
