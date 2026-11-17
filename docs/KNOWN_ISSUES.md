# Known issues and lessons (as encountered)

Ghi lại các lỗi thật đã gặp khi dựng dự án, kèm cách xử lý. Mục "Đang mở" chưa được giải quyết.

## Đang mở
| Vấn đề | Trạng thái |
|---|---|
| Chạm/mài trên iPhone qua Remote | Hình đã lên iPhone (người dùng báo). Chưa xác nhận chạm và mài hoạt động. |
| Dropdown Unity Remote Device không liệt kê iPhone | Chỉ có None/Any Android Device dù target iOS. Cách đang dùng: menu `ForgeBorn > Set Remote iPhone` (đặt `Any iOS Device`). Chưa rõ vì sao UI không liệt kê; trên UI giá trị hiện là None. |
| Unity mở bằng quyền admin báo "Administrator Privileges Detected" | Shell của Claude đang elevated nên Unity kế thừa. Bấm "Restart as Standard User"; mở Unity từ shell thường. |
| Build APK Android | Dừng ở ~45% (IL2CPP), không có APK. Hạ ưu tiên vì không có máy Android. |
| iOS native / hiệu năng iPhone | Chưa kiểm chứng; cần Mac + Xcode + Apple Developer. |
| Giao diện Phase C chỉ xem qua ảnh render headless | `docs/shots/` (tạo bằng `ForgeBornCapture`, lệnh trong file nguồn). Tia lửa đã xem (ảnh 07); đường "điểm chạm trên màn hình -> ray camera -> lệnh Domain" đã chạy thật bằng điểm chạm giả (mài 44.5 g, búa trúng). Chưa kiểm chứng: cảm ứng thật trên iPhone, âm thanh nghe thực tế, haptics. Layout Editor cũ đã đổi tên `.bak` (UserSettings/Layouts) để tránh cửa sổ trắng. Lưỡi hiển thị rất mảnh (50 mm trên 800 mm): dùng nút Zoom. |
| Unity chạy quyền admin treo/đóng băng khi tôi điều khiển | Editor admin hiện hộp thoại "Administrator Privileges" mỗi lần focus, chặn biên dịch; bấm Restart qua Hub không chạy được (project không có trong Hub). Mở Editor bằng Explorer/Hub (không từ shell elevated). Chạy test headless: `Unity.exe -batchmode -nographics -runTests -testPlatform EditMode` (cần đóng Editor). |
| Thermal tick ở x600 | Đã giảm: `StepsPerCommand` 500 cho Air/Oven/Furnace (2000 chỉ cho tôi nước/dầu), test cùng độ cứng trong ±2 HRC. Chi phí thật trên điện thoại vẫn chưa đo. |
| Audit và danh sách Id lệnh | Đã xử lý: giới hạn 4096 Id gần nhất và ~2048 mục audit (có đếm `AuditDropped`), có test phiên dài. Id cũ hơn cửa sổ không còn bị chặn trùng (chấp nhận, ghi trong mã). |
| Shader thiếu pass bóng/độ sâu | Đã thêm ShadowCaster và DepthOnly tự chứa; biên dịch sạch. Chưa bật bóng trong cảnh (đèn không đổ bóng). |

## Môi trường Windows / Unity
| Lỗi | Nguyên nhân | Xử lý |
|---|---|---|
| Unity Hub không có `C:\Program Files\Unity Hub` | Hub cài dạng MSIX (winget) | Tìm bằng `Get-AppxPackage`. Hub 4.0.1 không có lệnh `editors`. |
| Script cài Unity CLI bị chặn | Bộ phân loại quyền từ chối chạy script tải về | Không vượt qua; người dùng cài Unity 6.3 LTS bằng tay qua Hub. |
| `tar` báo "Cannot connect to C: resolve failed" | tar trong Git Bash hiểu `C:` là host | Sao chép file về đường dẫn tương đối rồi giải nén. |
| Không đặt được `Set-Service`/`Start-Service` (Access is denied) | Shell không có quyền admin; "toàn quyền cho Claude" không phải admin Windows | Cần người dùng bấm UAC. `Start-Process -Verb RunAs -Command "..."` chạy ngầm không ghi được kết quả; dùng **file .ps1** với `-File` thì chạy được. |
| Apple Mobile Device Service ở trạng thái Stopped/Manual | Gói winget chỉ cài driver, không tự chạy dịch vụ | Chạy elevated: `Set-Service ... Automatic; Start-Service ...` (đã Running, Automatic). |
| Build Android khóa project | Một project Unity chỉ mở bởi một tiến trình | Dừng build nền trước khi chạy test/Editor. |
| `ArgumentOutOfRangeException` trong `SearchDatabase` lúc mở Editor | Lỗi lập chỉ mục Search của Editor lần đầu | Vô hại với dự án, bỏ qua. |
| Cài đặt Unity Remote không nằm trong repo | `EditorSettings.unityRemote*` lưu ở EditorPrefs theo máy | Đặt lại bằng `ForgeBornSetup.Run` trên mỗi máy. |

## Quy trình / công cụ
| Lỗi | Xử lý |
|---|---|
| Hook GateGuard chặn lần Bash/Write đầu và mỗi file mới | Nêu sự thật yêu cầu (người gọi, file trùng, trích lời người dùng) rồi thử lại cùng thao tác. |
| Commit đầu chứa email cá nhân trước khi repo public | Đổi author sang địa chỉ noreply, force-push trước khi chuyển public. Luôn kiểm tra `git config user.email` trước khi commit. |

## Domain (đã sửa, có test)
| Lỗi | Cách sửa |
|---|---|
| Test FastSwipe lệch 9.6% (>5%) | Giảm `MaxStepM` từ 1.5 mm xuống 0.5 mm. |
| Test MultipleSwipes tạo vòng tự cắt (rollback giao dịch) | Thêm `StationSelfIntersects` và back-off nửa bước tối đa 6 lần; nếu vẫn lỗi thì hoàn tác trạm đó. |
