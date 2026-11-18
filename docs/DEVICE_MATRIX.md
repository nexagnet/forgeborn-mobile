# Device / evidence matrix

Only facts observed on this project are marked OK. Evidence labels are never merged.

| Evidence | Status | Detail |
|---|---|---|
| Domain test (`dotnet test`, headless) | OK | 63/63 on .NET 8 (2026-11-18), Windows 11 (frame/twist, hammer, thermal, evaluation goldens, schema 2). Not yet independently reviewed |
| Domain microbenchmark (PC estimate) | Measured on PC only | See `docs/BENCHMARKS.md`; default detail grind frame ~2.6 ms on i7-10610U. Not device performance |
| Unity EditMode tests | OK | 33/33 headless (gồm 5 test âm thanh, 6 test hướng dẫn người chơi, có một test chạy ngưỡng hướng dẫn trên mô hình nhiệt thật, 4 test hơi nước tôi) (`Unity.exe -batchmode -runTests`) on 6000.3.28f1: mesh vs Domain incl. bent/twisted blade, heat glow/oxide/crack colours, platform profile, sandbox rules, report text |
| Editor-render (headless, PC Intel UHD, 1280x720 or 1920x1080 landscape since 2026-11-18; the description in the next column still mentions the retired 540x1170 iPhone layout and the 44.5 g figure from it) | Reviewed on the 9 renders in `docs/shots/` | `ForgeBornCapture` renders scripted states without the Editor window: heat glow gradient, hammer bulge, tempering oxide tint, report panel, HUD layout at iPhone dp scale, sparks. It also drives a synthetic finger drag and screen hammer blow through the real camera-ray input path (44.5 g removed, hit=true) and exits non-zero if that breaks. Real touch/haptics NOT covered. PC GPU image, not an iPhone image |
| Windows build (máy này) | **Built and run; look and feel not reviewed** | 2026-11-18: build Mono 123.9 MB, 0 lỗi; chạy .exe, chụp ảnh `-fb-screenshot` và đo `-fb-benchmark` ở 1280x720 (xem `BENCHMARKS.md`: nhàn rỗi 4.4 ms, mài 16.0 ms trung bình, p95 22.3 ms, trên Intel UHD). Giao diện vẫn là bố cục điện thoại kéo ra khung ngang (nút lớn che lưỡi) và nhãn hồ sơ ghi `editor-sim-ios`. 1920x1080 treo lúc khởi động (màn hình chỉ 1536x864). Máy build, không phải máy người dùng |
| Windows máy người dùng (chơi thật) | **Not verified** | Cổng P5 của kế hoạch v3; chỉ ghi OK sau khi người dùng chơi bản .exe và xác nhận |
| Unity Remote 5 (iPhone) | Image streaming OK (user-reported); touch/grinding **not yet confirmed** | Working setup: build target iOS, iOS module loaded, Store iTunes, `ForgeBorn > Set Remote iPhone` (device is not listed in the dropdown). Shows PC performance, not iPhone |
| iOS native build / iPhone performance | **Not verified** | Needs Mac + Xcode + Apple Developer |
| Android build | Not verified | IL2CPP build started and was stopped at ~45% (deprioritised); no APK produced |
| Android device | **Not verified** | No device available |

Notes
- Unity Remote streams the Editor's Game view; FPS, thermals, memory and Metal behaviour seen through it are the PC's, not the iPhone's.
- Pressure is an on-screen control (Press -/+), quantised to 5 N, not touch force.
