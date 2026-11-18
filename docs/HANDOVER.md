# Bàn giao FORGEBORN MOBILE (2026-11-18)

Điểm dừng của phiên làm việc này, để người hoặc phiên Claude sau tiếp tục mà không cần lịch sử hội thoại.

## 1. Dựng lại môi trường

| Việc | Lệnh / thao tác |
|---|---|
| Unity | 6000.3.28f1 (6.3 LTS). Thêm project vào Hub: Add > Add project from disk > thư mục repo |
| Test Domain | `dotnet test build/ForgeBorn.Domain.Tests` (63/63 lúc bàn giao) |
| Test Unity EditMode | `Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults build/out/editmode.xml` (33/33). Editor phải đóng |
| Render headless | `Unity.exe -batchmode -projectPath . -executeMethod ForgeBorn.EditorTools.ForgeBornCapture.Run` (không `-nographics`). Ra `build/out/shots/*.png`, in "INPUT PATH OK" nếu đường chạm đúng. Ảnh là render GPU PC, không phải ảnh thiết bị |
| Chơi thử | Unity Remote 5 trên iPhone + Play trong Editor |

Trên PowerShell dùng `Start-Process -Wait -PassThru` để chờ Unity; gọi trực tiếp bằng `&` sẽ trả về ngay.

## 2. Unity MCP (chưa kiểm chứng đầu cuối)

Gói `com.gamelovers.mcp-unity` đã nằm trong `Packages/manifest.json`. Server Node build bằng script, `.mcp.json` đăng ký nó với Claude Code bằng đường dẫn tương đối:

```powershell
powershell -ExecutionPolicy Bypass -File tools/setup-unity-mcp.ps1
```

Rồi mở project bằng Unity Editor có giao diện (cổng 8090, `ProjectSettings/McpUnitySettings.json` do Unity tạo, đã gitignore) và khởi động Claude Code từ thư mục repo. `claude mcp list` đã báo "Connected" khi đăng ký, nhưng trong phiên bàn giao công cụ không hiện ra (server thêm giữa phiên không được nhận), nên chưa có lần gọi công cụ thật nào. Việc đầu tiên của phiên sau: xác nhận các công cụ `unity` xuất hiện và đọc được console của Editor.

## 3. Đã làm

- Domain: mô hình lưỡi, nhiệt luyện, búa, mài, đánh giá, lưu FBWS schema 2, đơn hàng (`Domain/Orders`).
- Cảnh: HDRI, đe, lò, tường gạch, bloom, camera phối cảnh FOV 35, hơi nước khi tôi, tiếng búa thật.
- Hướng dẫn 13 bước, nút Order/Deliver, báo cáo có lý do. Nguồn tài sản ở `docs/CREDITS.md`. Thiết kế ở `docs/GAME_DESIGN.md`. Trạng thái bằng chứng ở `docs/DEVICE_MATRIX.md`.

## 4. Chưa làm (theo thứ tự đề xuất)

1. Lưu tiền và hồ sơ (`Career`, hiện chỉ trong bộ nhớ). Đã định: thêm Serialize/Parse dạng dòng trong Domain có test, lưu cạnh `weapon_slot1.fbws`, gọi sau mỗi `Deliver`.
2. Tiếng xì khi tôi (hiệu ứng hình đã có, âm thanh chưa).
3. Cửa hàng nâng cấp (lò nhanh, mặt búa nhọn, bể dầu, bánh mài mịn), mở sau đơn đầu.
4. Kiểm tra giới hạn khối lượng 1500 g / 1450 g của đơn 2 và 3 có đạt được bằng mài; chỉnh số kinh tế sau khi chơi thử.
5. Tiếng Việt: font mặc định chưa kiểm tra dấu; chuỗi đang cứng trong code.
6. Mô hình 3D thật cho đe và lò (chưa tìm được nguồn CC0 tải thẳng), miệng lò sáng theo trạng thái lò.
7. Camera Cinemachine, Input System, Localization: đã cân nhắc, chưa làm vì lợi ích nhỏ so với rủi ro (camera cố định, đường chạm đang có test).

## 5. Chưa kiểm chứng

Cảm ứng thật, hiệu năng điện thoại, haptics, âm thanh nghe thật, bloom trên thiết bị, toàn bộ cảm giác chơi. Không có Mac: build iOS cần Mac đám mây + tài khoản Apple Developer (chưa thử). Ba file `ProjectSettings` (GraphicsSettings, ProjectSettings, QualitySettings) đổi do chuyển target iOS, chưa commit.
