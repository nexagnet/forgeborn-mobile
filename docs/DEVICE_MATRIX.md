# Device / evidence matrix

Only facts observed on this project are marked OK. Evidence labels are never merged.

| Evidence | Status | Detail |
|---|---|---|
| Domain test (`dotnet test`, headless) | OK | 21/21 on .NET 8, Windows 11 |
| Unity EditMode tests | OK | 2/2 (mesh volume vs Domain, winding, grinding updates mesh) on Unity 6000.3.28f1 |
| Editor (Windows, Intel UHD) | Setup compiles; not visually reviewed | No screenshots recorded yet |
| Unity Remote 5 (iPhone) | **Not verified** | Apple Mobile Device Support installed; iPhone not yet seen by the PC; touch not confirmed |
| iOS native build / iPhone performance | **Not verified** | Needs Mac + Xcode + Apple Developer |
| Android build | Not verified | IL2CPP build started and was stopped at ~45% (deprioritised); no APK produced |
| Android device | **Not verified** | No device available |

Notes
- Unity Remote streams the Editor's Game view; FPS, thermals, memory and Metal behaviour seen through it are the PC's, not the iPhone's.
- Pressure is an on-screen control (Press -/+), quantised to 5 N, not touch force.
