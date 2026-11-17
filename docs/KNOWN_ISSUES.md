# Known issues and lessons (as encountered)

Ghi lại các lỗi thật đã gặp khi dựng dự án, kèm cách xử lý. Mục "Đang mở" chưa được giải quyết.

## Đang mở
| Vấn đề | Trạng thái |
|---|---|
| iPhone chưa hiện hình qua Unity Remote 5 | Windows thấy iPhone (USB VID_05AC), dịch vụ Apple đã Running, Editor đã vào Play; chưa có hình trên iPhone. Chưa rõ nguyên nhân. Cần kiểm tra: Editor `Device = Any iOS Device`, app Remote đang mở, iPhone mở khóa. |
| Build APK Android | Dừng ở ~45% (IL2CPP), không có APK. Hạ ưu tiên vì không có máy Android. |
| iOS native / hiệu năng iPhone | Chưa kiểm chứng; cần Mac + Xcode + Apple Developer. |

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
