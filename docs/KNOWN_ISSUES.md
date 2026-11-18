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

## Hoãn tới P6 (điện thoại)
Các mục "Đang mở" về iPhone/Android ở đầu file đã hoãn, không còn là việc hiện hành. Sự thật cần nhớ khi quay lại:

| Điều | Hệ quả |
|---|---|
| Unity Remote chạy game trên PC; iPhone chỉ nhận hình nén và gửi cảm ứng | Remote kiểm chứng thao tác và logic, không kiểm chứng FPS, nhiệt, bộ nhớ, shader Metal |
| Windows cần iTunes (Apple Mobile Device) và cáp USB; iPhone không có trong danh sách Device, dùng menu `ForgeBorn > Set Remote iPhone` | Bước cài môi trường |
| Remote 5 không hỗ trợ Input System mới | Giữ Input Manager cũ (`Input.touches`) sau `IPointerSource` |
| iPhone từ bản 11 không có Force Touch | Áp lực mài là nút/phím, lượng tử 5 N, không lấy từ cảm ứng |
| Build iOS cần macOS + Xcode + Apple Developer; build Android IL2CPP từng dừng ở ~45%, chưa có máy Android | Mốc thiết bị thật bị chặn tới khi có phần cứng |

## Mở cho PC
| Vấn đề | Trạng thái |
|---|---|
| Bản .exe 1920x1080 cửa sổ từng treo lúc khởi tạo | Không tái hiện với bản build 2026-11-18 13:37 (`-screen-fullscreen 0`, chụp ảnh thành công, thoát 0). Chưa rõ nguyên nhân cũ; theo dõi |
| Hồ sơ nền tảng chọn `editor-sim-ios` khi chạy bản Windows (nhãn HUD) | Đã xử lý ở P1 (hồ sơ `windows`) |
| UI vẫn là bố cục điện thoại dọc trong khung ngang (nút lớn che lưỡi) | Đã xử lý ở P1 (HUD ngang) |
| Lỗi đã sửa: `Handheld.Vibrate` không có trong bản Windows nên build lỗi CS0103 (`SandboxEffects.cs`) | Bọc `#if UNITY_IOS \|\| UNITY_ANDROID`; PC không rung |
| `manage_build action=build` qua MCP làm mất job, Editor từng dừng | Dùng menu `ForgeBorn > Build Windows` (gọi qua `execute_code` + `delayCall`) |
| Unity không tự nhập file mới tạo ngoài Editor (menu `ForgeBorn/Build Windows` không có) | Gọi `AssetDatabase.ImportAsset(..., ForceUpdate)` rồi `Refresh`, kiểm bằng cách tìm kiểu trong AppDomain |
| Đổi sang không gian màu Linear (2026-11-18) | Đã xử lý: ảnh chuẩn trong `docs/shots/` chụp lại sau Linear, 1280x720, bố cục ngang. Bloom chưa chỉnh riêng; nhìn ảnh thì chấp nhận được, xét lại ở P4 |
