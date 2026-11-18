# FORGEBORN: bắt đầu từ đây

Cập nhật: 2026-11-18. File này nói game đang ở đâu và làm gì tiếp. Lộ trình: `IMPLEMENTATION_PLAN.md`. Thiết kế game: `GAME_DESIGN.md`.

## 1. Hiện trạng

| Mục | Giá trị |
|---|---|
| Hướng | **PC (Windows) trước**, điện thoại sau cổng P5 |
| Unity | 6000.3.28f1 (6.3 LTS), build target **Windows64**, backend Mono, không gian màu **Linear** |
| Test | Domain `dotnet test` **80/80**; Unity EditMode **53/53** (2026-11-18, sau P3) |
| Giai đoạn | P0, P0.5, P1, **P3 xong về mã và test** (xem §6 "P3"); kế tiếp P4 hoặc cổng P5 (cần bạn chơi) |
| Bản Windows | **Build được và chạy được** (123.9 MB, `build/out/win/ForgeBorn.exe`); đo trên Intel UHD ở 1280x720: nhàn rỗi 4.4 ms, mài 16.0 ms trung bình (p95 22.3 ms). Xem `BENCHMARKS.md` |
| Git | Nhánh `main`, các commit cục bộ chưa push |
| Chưa kiểm chứng | Cảm giác chơi (chuột), giao diện bản PC (đang là bố cục điện thoại kéo ra khung ngang), 1920x1080 (treo lúc khởi động), âm thanh nghe thật, GPU khác, mọi thứ trên điện thoại |
| Lưu ý lỗi thời | Chuỗi UI và hướng dẫn 13 bước trong mã là **tiếng Anh** (nhiều bước nói "Tap"), không phải tiếng Việt |

## 2. Dựng lại môi trường

| Việc | Lệnh / thao tác |
|---|---|
| Unity | Hub: Add > Add project from disk > thư mục repo. Mở từ Explorer hoặc Hub, không từ shell elevated (Unity sẽ chạy admin và treo) |
| Test Domain | `dotnet test build/ForgeBorn.Domain.Tests` |
| Test EditMode (Editor đang mở) | Qua Unity MCP: `run_tests` với `mode: EditMode`, rồi `get_test_job` |
| Test EditMode (Editor đóng) | `Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults build/out/editmode.xml` |
| Render headless | `Unity.exe -batchmode -projectPath . -executeMethod ForgeBorn.EditorTools.ForgeBornCapture.Run` (không `-nographics`). Ra `build/out/shots/*.png` và in "INPUT PATH OK" nếu đường chạm đúng. Ảnh là render GPU PC |

Trên PowerShell dùng `Start-Process -Wait -PassThru` để chờ Unity; gọi trực tiếp bằng `&` sẽ trả về ngay.

## 3. Unity MCP

Gói CoplayDev `com.coplaydev.unity-mcp` v10.0.0 (trong `Packages/manifest.json`; có bản v10.3.0, chưa cập nhật). Cần `uv` (có ở `~/.local/bin`).

Đăng ký phía Claude (một lần, cấu hình người dùng, không nằm trong repo). Bản bàn giao trước ghi "đã đăng ký" là **sai**: trước 2026-11-18 chưa có mục nào. Người dùng đã chạy lệnh sau và `~/.claude.json` giờ có `UnityMCP`:

```
claude mcp add --transport http --scope user UnityMCP http://127.0.0.1:8080/mcp
```

Công cụ `UnityMCP` chỉ hiện trong phiên Claude **mở sau** khi máy chủ Unity đang chạy (hoặc kết nối lại bằng `/mcp`). Nếu phiên mới vẫn không thấy, kiểm `claude mcp list` và xác nhận cổng 8080 đang nghe.

Bật mỗi lần: mở Editor có giao diện, **Window > MCP for Unity > Toggle MCP Window**, ở **Edit mode**, bấm **Start Server**, rồi **Start** ở hộp thoại. Đã kiểm 2026-11-18: đọc Console, chạy test, đổi build target và `execute_code` đều hoạt động.

Lưu ý: công cụ chỉ hiện trong Claude nếu phiên mở **sau** khi server chạy. Khi chưa hiện, gọi thẳng JSON-RPC tới `http://127.0.0.1:8080/mcp` (initialize, rồi `tools/call`). Đổi build target hoặc biên dịch lại làm Editor nạp lại assembly nên MCP mất kết nối tạm; chờ vài chục giây rồi gọi lại.

## 4. Đã có

- **Domain** (`Assets/_ForgeBorn/Scripts/Domain`, độc lập Unity): hình học lưỡi (`BladeProfile`, `BladeFactory`, `BladeRaycaster`, centerline và xoắn), lệnh mài và búa (`GrindSolver`, `HammerSolver`), nhiệt luyện (`ThermalSolver`), đánh giá (`BladeEvaluator`), đơn hàng (`Orders`), phiên có giao dịch và hoàn tác (`ForgeSession`), lưu FBWS schema 2 (SHA-256, ghi nguyên tử, khôi phục `.bak`).
- **Unity**: cảnh xưởng dựng bằng mã (HDRI, đe, lò, tường gạch, bloom, camera phối cảnh FOV 35), mesh lưỡi theo Domain, màu nhiệt/oxit/vết nứt, tia lửa, hơi nước khi tôi, tiếng búa thật, hướng dẫn 13 bước, nút Order/Deliver, báo cáo có lý do.
- **Công cụ**: `ForgeBornCapture` (render headless và chạm giả), `PlatformProfile` iOS/Android/Editor (đã cấu hình, chưa đo trên máy).

## 5. Giới hạn đã biết của mô hình

- Mô hình mài là mô hình gameplay đã hiệu chỉnh, không phải phương trình mài thật. Chưa có mòn bánh mài.
- Raycast bước 0.25 mm: chi tiết mỏng hơn có thể bị lỡ.
- Búa bảo toàn thể tích theo từng trạm (nở ngang, chưa kéo dài theo X); lưỡi cong/xoắn không đổi thể tích; tôi dùng nhiệt dung gộp theo trạm, chưa mô hình cong vênh.
- Phát lại lệnh cho kết quả giống hệt trên một máy; chênh lệch giữa các nền tảng chưa kiểm, phải dùng dung sai.
- Giới hạn khối lượng 1500 g / 1450 g của đơn 2 và 3 là số giả định chưa kiểm có đạt được bằng mài.

## 6. Bước tiếp theo

### P0.5 đã xong
- Thiết kế: `GAME_DESIGN.md` §13–§16 (UI ngang 16:9, art bible, danh sách âm thanh, bảng kinh tế).
- `tools/blender/forge_models.py`: chạy `"C:\Program Files\Blender Foundation\Blender 5.3\blender.exe" -b -P tools/blender/forge_models.py -- <thư mục xuất> [--preview]`. Xuất 5 FBX (đe, lò, bể tôi, bánh mài, chuôi), mô hình còn **thô và vuông**. Năm file nằm ở `Assets/_ForgeBorn/Art/Models`, đã nhập vào Unity đúng kích thước và vật liệu.
- `PlayerAutomation.cs`: `-fb-screenshot <file> [-fb-wait 3]`, `-fb-benchmark <giây> [-fb-out file]` cho bản .exe. Biên dịch sạch, **chưa chạy**.
- `ForgeBornBuild.cs`: menu `ForgeBorn > Build Windows`, ra `build/out/win/ForgeBorn.exe` và `build/out/win_build.txt`. Mono (không có IL2CPP cho Windows).

### P0.5 đã kiểm chứng ngày 2026-11-18
- Build Windows thành công qua menu `ForgeBorn > Build Windows` (gọi qua MCP: `execute_code` với `EditorApplication.delayCall += () => EditorApplication.ExecuteMenuItem("ForgeBorn/Build Windows")`, rồi đọc `build/out/win_build.txt`). **Không** dùng `manage_build action=build`: job bị mất và Editor từng dừng. Lần đầu build lỗi vì `Handheld.Vibrate` không có trên Windows; đã bọc `#if UNITY_IOS || UNITY_ANDROID`.
- `-fb-screenshot` và `-fb-benchmark` chạy đúng. Ảnh chụp từ .exe (`build/out/shot_1280x720.png`, không nằm trong git) cho thấy giao diện điện thoại dọc bị kéo ra khung ngang: nút lớn che lưỡi, HUD ghi `[editor-sim-ios]`, toàn bộ chữ tiếng Anh. Đó là việc của P1.
- `-fb-benchmark` từng bị giới hạn 60 khung vì `ForgeSandbox.Initialize` đặt lại `targetFrameRate`; đã sửa trong `PlayerAutomation.cs`.

### P1 xong (2026-11-18)
Có và test EditMode xanh (44/44): `PointerSource.cs` (chuột/cảm ứng), `DesktopKeys.cs` (1–6, Tab, `[` `]`, `-` `=`, Esc; Ctrl+con lăn đổi áp lực; con lăn zoom; chuột phải kéo xoay), HUD ngang (`CanvasScaler` 1920x1080: cột trái, thanh dưới, bảng thông số), lưỡi nằm ngang, `SandboxProps` xếp lại cho khung ngang, hồ sơ `windows` (hết nhãn `[editor-sim-ios]` trên .exe), `HudWidgets.cs` + `MenuScreens.cs` (menu chính, tạm dừng, cài đặt) tách khỏi `ForgeSandbox`, `GameSettings.cs` (cỡ cửa sổ, toàn màn hình, tầng chất lượng, âm lượng, lưu `PlayerPrefs` khóa `fb.settings`; tầng chất lượng áp dụng sau khi khởi động lại), cửa sổ 1280x720 resize được, chạy nền. Test khói `SandboxBuildsAndMainMenuPausesAndResumes` dựng cả sandbox. `ForgeBornCapture` chụp 1920x1080 hoặc `-fb-size 1280x720` (ảnh chuẩn mới trong `docs/shots/`, 1280x720, Linear).
Đã xem bằng ảnh: 1920x1080 và 1280x720 (batchmode, GPU máy này), menu chính, cài đặt và workshop trong Play mode (Editor, game view 880x373).
**Chưa kiểm chứng:** phím/chuột/Fullscreen/Quit trên bản .exe thật; cảm giác chơi bằng chuột (cổng P5); `build/out/win/ForgeBorn.exe` hiện có được build TRƯỚC P1 nên chưa có HUD ngang và menu, phải build lại (`ForgeBorn > Build Windows`).
Việc để lại: chỉnh màu khối lò (mặt trước trùng màu tường, chỉ thấy mặt bên như thanh dọc; P4). Chuỗi UI vẫn tiếng Anh (P3).
Unity MCP: bật tùy chọn "Auto-Start on Editor Load" của gói (EditorPrefs `MCPForUnity.AutoStartOnLoad`), nên máy chủ tự chạy khi mở Editor, không cần bấm Start Server. Chụp màn hình trong Play mode: `manage_camera action=screenshot` (ra `Assets/Screenshots/`, xóa sau khi xem, đừng commit). Chụp batchmode vẫn cần Editor đóng. Phiên MCP có thể mất sau khi máy chủ khởi động lại ("Session not found"): mở phiên mới.

### P3 xong về mã và test (2026-11-18)
- **Career lưu được:** `Career.Serialize/Parse` (dòng `career1;money=N;best=id:G,...;up=...`, không ném lỗi, bỏ qua khóa lạ), ghi nguyên tử `career.txt` cạnh `weapon_slot1.fbws` sau mỗi `Deliver` và khi đóng cửa hàng; đọc ở `Initialize()`. Không lưu khi batch, `-fb-screenshot`/`-fb-benchmark` hoặc ngoài Play mode (cờ `_persistCareer`), để ảnh chụp và test không đụng hồ sơ người chơi.
- **Cửa hàng:** `Shop.cs` (Domain) + `ShopScreen.cs` (nút "Shop" ở cột trái, mở sau đơn giao đầu tiên, Esc đóng). Bốn nâng cấp: đá mài mịn 60, bể dầu 80, mặt búa nhọn 90, lò mạnh hơn 120 (`ThermalEnvironment.FastFurnace`, h 100 thay 60). Chưa mua thì đá mịn/mặt nhọn/Dầu bị khóa; giá chỉnh theo thu nhập đơn 1 (net 80 ở hạng B).
- **Giới hạn khối lượng đơn 2 và 3:** `BalanceTests` cho thấy cả hai phôi trong game (1.549 kg và 1.688 kg) nặng hơn 1.50/1.45 kg lúc đầu, và một lượt mài mặt đủ đưa cả hai xuống dưới giới hạn mà không phá lưỡi. Không cần chỉnh số.
- **Tiếng xì:** `SandboxAudio.SetHiss` (nhiễu qua lọc cao, theo tốc độ hơi nước); có test. Chưa nghe thử bằng loa thật; đây là âm tổng hợp, âm thật là việc của P4.
- **Tiếng Việt:** `Loc.cs` + `LocVi.cs` (khóa là chính câu tiếng Anh; thiếu bản dịch thì hiện tiếng Anh). Domain trả lý do chấm đơn dạng mẫu + tham số (`OrderResult.Parts`) để dịch mà giữ số. Cài đặt có nút ngôn ngữ (tự động theo hệ thống, đổi có hiệu lực sau khi khởi động lại). Test kiểm đủ bản dịch cho mọi bước hướng dẫn, đơn hàng, nâng cấp, mẫu lý do; font mặc định có đủ ký tự có dấu. Chữ hiển thị đã xem trong Play mode (ảnh Editor). Hướng dẫn đã viết lại theo chuột/phím ("Bấm", "Giữ chuột trái và kéo").
- **Chưa làm / chưa kiểm chứng:** bản .exe chưa build lại sau P3 (bản gần nhất `build/out/win` là sau P1); chuỗi trong `PlayerAutomation`, nhãn Editor và bản ghi log vẫn tiếng Anh; chỉ có bản dịch của tôi, chưa người bản ngữ rà (nhất là thuật ngữ rèn); mua nâng cấp và khóa/mở mới chạy qua test Domain và ảnh cửa hàng, chưa chơi thật bằng chuột.

### Còn dở
1. Bản .exe 1920x1080 từng treo lúc khởi động; bản build sau P1 chạy được ở cả 1280x720 và 1920x1080 (chụp ảnh, thoát 0), chưa rõ nguyên nhân cũ.
2. Phím, chuột, Toàn màn hình, Thoát chưa thử tay trên .exe; build lại .exe sau P3 rồi thử.

### Sau đó
P4 (bóng đổ, mô hình đe/lò, âm thanh thật, tầng chất lượng) hoặc, nếu bạn muốn, đi thẳng cổng P5: build lại .exe (`ForgeBorn/Build Windows` qua `execute_code` + `delayCall`) rồi bạn chơi thử. Đổi build target hoặc biên dịch lại làm Editor nạp lại assembly nên MCP mất kết nối tạm vài chục giây. File mới tạo ngoài Editor có thể không được nhập: gọi `AssetDatabase.ImportAsset(path, ForceUpdate)` rồi `Refresh` và kiểm tra kiểu đã có.

### Cạm bẫy đã gặp (tránh lặp lại)
- **Giấy phép Unity** có thể hết hạn giữa phiên: Editor tự thoát, log ghi "No ULF license found". Cần người dùng đăng nhập lại Unity Hub (tôi không nhập thông tin đăng nhập).
- Hook GateGuard bắt nêu người gọi, hàm bị ảnh hưởng, dữ liệu và lời người dùng trước lần ghi/sửa đầu tiên của mỗi file; nêu xong rồi thử lại cùng thao tác.
- Máy chỉ có Intel UHD: mọi số FPS là mốc thấp, không khẳng định cho GPU khác.

Việc cần bạn: P5 (chơi bản .exe), đăng nhập lại Unity Hub khi hết hạn, và các quyết định về phát hành/điện thoại.
