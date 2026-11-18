# FORGEBORN: lộ trình (v3, PC trước, điện thoại sau)

Cập nhật: 2026-11-18. Đây là **lộ trình duy nhất**. Hiện trạng và cách dựng môi trường: `HANDOVER.md`. Thiết kế game: `GAME_DESIGN.md`.

## Vì sao PC trước

Không có Android; iPhone qua Unity Remote chỉ cho thấy hình và cảm ứng còn hiệu năng là của PC; build iOS cần Mac + Apple Developer. Bản Windows kiểm chứng được ngay trên máy này: build .exe thật, chuột thật, FPS thật (máy chuẩn thấp: i7-10610U, Intel UHD). `Domain` độc lập nền tảng nên giữ nguyên.

Chốt với người dùng: mục tiêu là **bản Windows chơi được, chưa phát hành/Steam**; bố cục **ngang 16:9 thiết kế lại**; điện thoại làm sau cổng P5.

## Nhãn bằng chứng (không trộn)

`Domain test` · `Editor` · `Windows build (máy này)` · `Windows máy người dùng` · `Unity Remote (iPhone)` · `iOS native` · `Android build` · `Android máy thật`. Số đo hiệu năng chỉ ghi "đạt" khi đo đúng loại thiết bị đó. Các nhãn iOS/Android vẫn là "chưa kiểm chứng".

## Tổng quan

| GĐ | Nội dung | Cổng | Trạng thái |
|---|---|---|---|
| P0 | Chuyển hướng: tài liệu, build target Windows64, không gian màu Linear | `dotnet test` 63/63, EditMode 33/33 | **Xong** (commit `98bf216`, `49afe19`) |
| P0.5 | Bộ công cụ và thiết kế: tham số `-screenshot`/`-benchmark`, script Blender, thiết kế UI ngang + art bible + danh sách âm thanh + bảng cân bằng, bộ mô hình 3D đầu tiên | Tài liệu nằm trong `GAME_DESIGN.md`, mô hình nhập được vào Unity | Đang làm: thiết kế (§13–§16), script Blender, 5 FBX và `PlayerAutomation.cs` đã có; chưa kiểm nhập FBX vào Unity và chưa chạy thử `-screenshot`/`-benchmark` vì Unity đang mất giấy phép |
| P1 | `IPointerSource` (chuột; cảm ứng cắm sau), phím tắt, UI ngang 1920x1080, menu/tạm dừng/cài đặt, tách HUD khỏi `ForgeSandbox` | Ảnh render 1920x1080 và 1280x720, "INPUT PATH OK" | Chưa |
| P2 | `ForgeBornBuild.BuildWindows`, chạy .exe, đọc `Player.log`, đo FPS trên iGPU | .exe chạy sạch log, số đo vào `BENCHMARKS.md` | Chưa |
| P3 | Lưu `Career` (schema 3), tiếng xì, cửa hàng nâng cấp, kiểm giới hạn khối lượng đơn 2–3, tiếng Việt | Test Domain/EditMode cho từng mục | Chưa |
| P4 | Bóng đổ, mô hình đe/lò, miệng lò sáng, âm thanh thật, tầng chất lượng PC | FPS không tụt dưới ngân sách P2 | Chưa |
| P5 | **Cổng PC:** bạn chơi bản .exe một vòng đủ và báo lại | Bạn xác nhận | Chưa |
| P6 | Điện thoại (xem cuối) | Chỉ lên lịch sau P5 | Hoãn |

## P0.5. Bộ công cụ và thiết kế

**Công cụ**
- Thêm tham số dòng lệnh `-screenshot <file>` và `-benchmark` cho bản .exe (dựa trên `ForgeBornCapture` đã có ở `Assets/_ForgeBorn/Scripts/Editor/ForgeBornCapture.cs`).
- Script Blender chạy không giao diện: `"C:\Program Files\Blender Foundation\Blender 5.3\blender.exe" -b -P tools/blender/<tên>.py`, xuất FBX vào `Assets/_ForgeBorn/Art/Models`.
- Font: `LegacyRuntime.ttf` đã đủ ký tự tiếng Việt (kiểm bằng Unity: thiếu 0). Không cần font mới; chỉ chụp ảnh xác nhận hiển thị.
- Chưa có: RenderDoc, PresentMon, Windows Performance Toolkit. Dùng Unity Profiler qua MCP (`manage_profiler`) và bộ đếm FPS tự viết.

**Thiết kế** (bốn mục mới vào cuối `GAME_DESIGN.md`, không tạo file mới)
1. Bố cục UI ngang 16:9 từng màn hình: xưởng, bảng đơn, giao hàng, cửa hàng, menu, cài đặt.
2. Art bible: bảng màu, ánh sáng, vật liệu, kiểu chữ.
3. Danh sách âm thanh (mài, búa, xì, lò, môi trường, UI) và nguồn.
4. Bảng cân bằng kinh tế và đường cong độ khó; hướng dẫn viết lại cho chuột/phím.

**Mô hình 3D đầu tiên** (Blender, script): đe, lò rèn, bể tôi, bánh mài, chuôi và cán. Mô hình tự làm, ghi vào `CREDITS.md`.

## P1. Đầu vào và UI cho PC

- Tách `IPointerSource` khỏi `ForgeSandbox.cs` (đọc cảm ứng/chuột ở khoảng dòng 352-361). Mài = giữ chuột trái kéo; búa = nhấp; xoay/zoom = chuột phải/con lăn.
- Áp lực mài: phím `[` `]`, con lăn và thanh trên màn hình; vẫn lượng tử 5 N, một lệnh Domain cho mọi thiết bị.
- Phím tắt: chuyển trạm, tốc độ thời gian x1..x600, Tab đổi chế độ, Esc tạm dừng.
- Bỏ khóa dọc (`Screen.orientation`); `CanvasScaler` ScaleWithScreenSize tham chiếu 1920x1080 (thay `dpScale/390`); tách dựng HUD khỏi `ForgeSandbox` để sau này có layout dọc.
- Bật `resizableWindow` và `runInBackground`; hỗ trợ tỉ lệ 16:10 và ultrawide cơ bản; vSync theo cài đặt.
- Menu chính, tạm dừng, cài đặt (độ phân giải, toàn màn hình, tầng chất lượng, âm lượng).
- Kiểm: EditMode cho `IPointerSource` + ánh xạ phím; `ForgeBornCapture` chụp 1920x1080 và 1280x720; đường chạm giả vẫn in "INPUT PATH OK". Chụp lại ảnh chuẩn sau khi đổi Linear và chỉnh bloom.

## P2. Pipeline build Windows

- `ForgeBornBuild.BuildWindows` ra `build/out/win/ForgeBorn.exe`, gọi được qua `-executeMethod` và qua MCP. Backend Mono (máy chưa có IL2CPP cho Windows).
- Chạy .exe thật, đọc `Player.log`, chụp ảnh, đo ms mỗi khung ở mốc tải nặng nhất (mài chi tiết cao, x600). Ghi vào `BENCHMARKS.md` với nhãn **Windows build (máy này, iGPU)** và nêu rõ là mốc thấp.

## P3. Hoàn thiện vòng chơi

1. Lưu hồ sơ `Career`: Serialize/Parse dạng dòng trong Domain có test, lưu cạnh `weapon_slot1.fbws`, gọi sau mỗi `Deliver`; schema 3 theo `GAME_DESIGN.md` §5.
2. Tiếng xì khi tôi (hơi nước đã có).
3. Cửa hàng nâng cấp (lò nhanh, mặt búa nhọn, bể dầu, bánh mài mịn), mở sau đơn đầu.
4. Test Domain mô phỏng mài để kiểm tra giới hạn 1500 g / 1450 g của đơn 2 và 3 có đạt được; chỉnh số kinh tế.
5. Tiếng Việt: toàn bộ chuỗi UI và hướng dẫn trong mã hiện là **tiếng Anh**; đưa ra bảng chuỗi, dịch sang tiếng Việt, test hiển thị (font mặc định đã đủ ký tự có dấu).

Dùng TDD cho phần Domain.

## P4. Hình ảnh và âm thanh

Bật bóng đổ (shader đã có ShadowCaster/DepthOnly); đưa mô hình đe/lò thật vào cảnh; miệng lò sáng theo trạng thái lò; mẫu âm thanh thật cho mài và xì; tầng chất lượng PC (Low/Med/High) với Low chạy được trên iGPU, số hạt và bloom theo tầng.

## P5. Cổng PC (cần bạn)

Bạn chơi bản .exe một vòng đủ (nhận đơn, nung, búa, mài, tôi, ram, giao, mua nâng cấp, lưu/đọc) và báo cảm giác, độ khó, lỗi. Nên thử thêm ít nhất một cấu hình màn hình khác nếu có. Chỉnh kinh tế/độ khó theo phản hồi. Chỉ sau cổng này mới gọi là "chơi được trên PC".

## P6. Điện thoại (sau P5)

Đặt trước để không phải làm lại: Domain không đổi; đầu vào qua `IPointerSource`; HUD tách lớp; `PlatformProfile` iOS/Android giữ nguyên.

Khi bắt đầu: thêm nguồn cảm ứng, layout dọc, safe area, haptics; tối ưu riêng iOS/Android. Cổng thiết bị thật cần Mac + Xcode + Apple Developer cho iOS và một máy Android; quyết định lúc đó. Các sự thật về Unity Remote nằm ở `KNOWN_ISSUES.md`, mục "Hoãn tới P6".

## Rủi ro

| Rủi ro | Mức | Xử lý |
|---|---|---|
| Tách HUD làm hỏng đường chạm đã có test | Trung bình | Giữ `ForgeBornCapture` xanh sau mỗi bước; commit nhỏ |
| iGPU chậm với mesh/bloom | Trung bình | Đo ở P2 trước khi thêm hiệu ứng P4; tầng Low |
| Đổi Linear làm lệch màu và ánh sáng | Trung bình | Chụp lại ảnh chuẩn, chỉnh bloom trước P1 |
| Mô hình 3D tự làm trông thô | Thấp | Làm từng cái, so ảnh với art bible |
| Chỉ thiết kế ngang nên phần dọc làm lại ở P6 | Chấp nhận | Tách HUD để giảm chi phí |

## Luật giữ nguyên

Nhãn bằng chứng không trộn; không tuyên bố hiệu năng hay cảm giác khi chưa đo/chưa chơi; commit bằng email noreply; không push khi chưa được yêu cầu; không quảng cáo hoặc chờ ép trả tiền trong thiết kế.
