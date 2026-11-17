# FORGEBORN MOBILE — Kế hoạch triển khai (bản của Claude)

Nguồn: `FORGEBORN_MOBILE_TECHNICAL_DESIGN_V3.md` và `CLAUDE_CODE_START_FORGEBORN_MOBILE.md`.
Ngày lập: 2026-11-17.

## 1. Khảo sát PC (2026-11-17)

| Hạng mục | Thực tế | Đánh giá |
|---|---|---|
| OS | Windows 11 Pro 10.0.26200 | Không build được iOS trên máy này |
| CPU / RAM | i7-10610U, 15,6 GB | Đủ cho Editor, hơi chậm |
| GPU | Intel UHD (tích hợp) | Editor chạy được ở chất lượng thấp, không đại diện cho mobile |
| Ổ C: | 25,7 GB trống lúc khảo sát (user xác nhận đủ chỗ) | Chấp nhận được |
| Unity Hub | Đã cài (MSIX, 4.0.1) | Hub bản này không có lệnh `editors`, cần Unity CLI hoặc cài Editor thủ công |
| Unity Editor 6.3 LTS | **Chưa cài** | Chặn M0/M1 phần engine |
| .NET SDK | 8.0.428, đã cài | Dùng cho `dotnet test` |
| Android SDK / adb / thiết bị | Chưa có | Unity Android module sẽ kèm SDK/NDK/JDK |
| Mac / Xcode / iPhone | Không có | iOS = *unverified* |
| git, gh, node, python | Có; gh đã đăng nhập, có quyền org `nexagnet` | OK |

## 2. Đánh giá 2 tài liệu thiết kế

**Đúng, giữ nguyên:** `WeaponState` là nguồn sự thật duy nhất; mô hình mặt cắt dọc lưỡi cho Phase A; đơn vị SI;
gate theo từng milestone; không báo iOS xong khi chưa chạy trên iPhone; tách WORK/INSPECT.

**Cần sửa / làm rõ:**
1. File START yêu cầu tạo project ngay, nhưng máy chưa có Unity, Android SDK, Mac. M0 không thể đạt gate ở đây; phần Domain vẫn làm được.
2. Interface §5 dùng `Vector3` của UnityEngine, trái với §14 (Domain độc lập Unity). Dùng kiểu vector double riêng (`Vec2d`, `Vec3d`).
3. Mô hình mặt cắt thiếu đường tâm (centerline) cho từng trạm, nên không biểu diễn được uốn cong/lệch do đập (M2). Thêm offset và xoắn cho mỗi trạm.
4. "Mài xuyên lưỡi" (§6.2) chưa có quy tắc topology. Quy ước: trạm bị mài xuyên toàn bộ chiều rộng thì đánh dấu `Severed`, diện tích bằng 0; khối lượng mất vẫn tính bằng chênh lệch hình học.
5. Ngân sách 192–256 trạm × 32–64 điểm chu vi là giả thuyết, cần benchmark trên điện thoại thật.
6. Mốc iOS 17+/Android 10+, bản Unity 6.3 LTS và yêu cầu store cần đối chiếu lại với tài liệu chính thức khi cài.
7. Công bằng iOS/Android: lệnh nên được lượng tử hóa; trạng thái nội bộ so sánh bằng dung sai, không so bit.

## 3. Kế hoạch code

### Nguyên tắc
- Domain thuần C# (`netstandard2.1`, C# 9, không UnityEngine). Dùng chung cho Unity qua asmdef và cho `dotnet test`.
- Mọi thay đổi hình học đi qua lệnh (`ForgeCommand`) → kiểm tra → giao dịch (hoàn tác nếu sai) → nhật ký.
- Trạng thái thật ghi trong `docs/IMPLEMENTATION_STATUS.md`, tách `Editor OK / Android device OK / iOS Xcode OK / iPhone OK`.

### Giai đoạn 0 — Môi trường
- [x] .NET SDK 8
- [x] Unity Hub
- [ ] Unity 6.3 LTS + Android Build Support (cài thủ công hoặc qua Unity CLI)
- [x] Repo git, `.gitignore`
- [ ] Repo GitHub `nexagnet/forgeborn-mobile`

### Giai đoạn 1 — Domain kernel (không cần Unity)
| Bước | Nội dung | Kiểm chứng |
|---|---|---|
| 1.1 | `Vec2d`, `Vec3d`, `BladeProfile` (mảng trạm: x, vòng điểm chu vi) | Thể tích lưỡi chữ nhật/thon có đáp án giải tích |
| 1.2 | `WeaponState`, `MaterialData`, khối lượng = thể tích × khối lượng riêng | `Mass_Analytic_*` |
| 1.3 | Hàng đợi lệnh + giao dịch + nhật ký | Lệnh bị từ chối không đổi trạng thái |
| 1.4 | `ApplyGrind`: raycast giải tích, nội suy đường theo khoảng cách thế giới, mô hình mòn đơn điệu | Bộ test Grind ở §16 |
| 1.5 | Kiểm tra toàn vẹn (NaN, độ dày âm, tự cắt) | `Mesh_NoNaN_*` |
| 1.6 | Lưu/đọc nhị phân có SHA-256, ghi nguyên tử, khôi phục từ `.bak` | `Save_Atomic_*` |

**Cổng:** `dotnet test` xanh; thể tích/khối lượng giảm đúng bằng phần đã mài; mài không tiếp xúc thì `removedVolume == 0`.

### Giai đoạn 2 — Unity M0/M1 (cần Unity)
1. Project URP dọc, scene thử nghiệm, sinh bằng script Editor.
2. `BladeMeshBuilder` dựng mesh từ `WeaponState` (Mesh.AllocateWritableMeshData), chỉ dựng lại đoạn bẩn.
3. Điều phối cảm ứng WORK/INSPECT, raycast giải tích (không dùng `MeshCollider`), đơn vị mét.
4. HUD: đồng hồ khối lượng, safe area, nút ≥ 48 dp.
5. Build APK debug, ghi `docs/DEVICE_MATRIX.md`; iOS ghi rõ *chưa kiểm chứng*.

**Cổng M1:** kéo ngón tay tạo được rãnh đo được và khối lượng lưu lại giảm đúng.

### Giai đoạn 3 — M2 đến M6 (theo tài liệu)
M2 Hammer (bảo toàn thể tích ≤ 1% sau 100 nhát, thêm centerline) → M3 nhiệt/tôi/ram → M4 UX và nghệ thuật →
M5 đánh giá/lưu/hợp đồng → M6 bản offline hoàn chỉnh (cần cả Android và iPhone thật; thiếu một thì M6 chưa xong).

## 4. Rủi ro

| Rủi ro | Mức | Xử lý |
|---|---|---|
| Chưa có Unity Editor | Cao | Cài thủ công; làm Domain trước |
| Không có thiết bị Android/iPhone | Cao | Ghi *chưa kiểm chứng* trong DEVICE_MATRIX |
| Không có Mac/Xcode/tài khoản Apple | Cao, chặn M6 | Mac từ xa hoặc dịch vụ build macOS |
| Hammer làm mesh không hợp lệ | Trung bình | Giao dịch hoàn tác, giảm bước, thử lại |
| Hiệu năng Editor trên Intel UHD | Trung bình | Số liệu hiệu năng lấy từ điện thoại |
