# Device / evidence matrix

Only facts observed on this project are marked OK. Evidence labels are never merged.

| Evidence | Status | Detail |
|---|---|---|
| Domain test (`dotnet test`, headless) | OK | 54/54 on .NET 8, Windows 11 (frame/twist, hammer, thermal, evaluation goldens, schema 2). Not yet independently reviewed |
| Domain microbenchmark (PC estimate) | Measured on PC only | See `docs/BENCHMARKS.md`; default detail grind frame ~2.6 ms on i7-10610U. Not device performance |
| Unity EditMode tests | OK | 23/23 headless (gồm 5 test bộ đệm âm thanh) (`Unity.exe -batchmode -runTests`) on 6000.3.28f1: mesh vs Domain incl. bent/twisted blade, heat glow/oxide/crack colours, platform profile, sandbox rules, report text |
| Editor-render (headless, PC Intel UHD, 540x1170) | Reviewed on 4 renders in `docs/shots/` | `ForgeBornCapture` renders scripted states without the Editor window: heat glow gradient, hammer bulge, tempering oxide tint, report panel, HUD layout at iPhone dp scale, sparks. It also drives a synthetic finger drag and screen hammer blow through the real camera-ray input path (44.5 g removed, hit=true) and exits non-zero if that breaks. Real touch/haptics NOT covered. PC GPU image, not an iPhone image |
| Unity Remote 5 (iPhone) | Image streaming OK (user-reported); touch/grinding **not yet confirmed** | Working setup: build target iOS, iOS module loaded, Store iTunes, `ForgeBorn > Set Remote iPhone` (device is not listed in the dropdown). Shows PC performance, not iPhone |
| iOS native build / iPhone performance | **Not verified** | Needs Mac + Xcode + Apple Developer |
| Android build | Not verified | IL2CPP build started and was stopped at ~45% (deprioritised); no APK produced |
| Android device | **Not verified** | No device available |

Notes
- Unity Remote streams the Editor's Game view; FPS, thermals, memory and Metal behaviour seen through it are the PC's, not the iPhone's.
- Pressure is an on-screen control (Press -/+), quantised to 5 N, not touch force.
