# FORGEBORN: bắt đầu từ đây

Cập nhật: 2026-11-18. File này nói game đang ở đâu và làm gì tiếp. Lộ trình: `IMPLEMENTATION_PLAN.md`. Thiết kế game: `GAME_DESIGN.md`.

## 1. Hiện trạng

| Mục | Giá trị |
|---|---|
| Hướng | **PC (Windows) trước**, điện thoại sau cổng P5 |
| Unity | 6000.3.28f1 (6.3 LTS), build target **Windows64**, backend Mono, không gian màu **Linear** |
| Test | Domain `dotnet test` **63/63**; Unity EditMode **33/33** |
| Giai đoạn | P0 xong; đang ở P0.5 (bộ công cụ và thiết kế) |
| Chưa kiểm chứng | Cảm giác chơi (chuột), bản .exe, hiệu năng, âm thanh nghe thật, mọi thứ trên điện thoại |

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

Gói CoplayDev `com.coplaydev.unity-mcp` v10.0.0 (trong `Packages/manifest.json`). Đã đăng ký phía Claude: `UnityMCP` = `http://127.0.0.1:8080/mcp` (cấu hình người dùng, không nằm trong repo). Cần `uv` (có ở `~/.local/bin`).

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

1. P0.5: tham số `-screenshot`/`-benchmark`, script Blender, bốn mục thiết kế vào `GAME_DESIGN.md`, bộ mô hình 3D đầu tiên.
2. Chụp lại ảnh chuẩn sau khi đổi Linear, chỉnh bloom.
3. P1 trở đi theo `IMPLEMENTATION_PLAN.md`.

Việc cần bạn: P5 (chơi bản .exe) và các quyết định về phát hành/điện thoại.
