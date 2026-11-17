# FORGEBORN MOBILE: Game Design

Tài liệu thiết kế làm nền cho việc dựng cảnh, HUD và vòng chơi. Không chờ duyệt: đây là thiết kế đang áp dụng, sửa khi chơi thử thấy sai.
Nhãn bằng chứng giữ nguyên như `DEVICE_MATRIX.md`: mọi điều về cảm giác chơi, hiệu năng điện thoại, haptics, âm thanh thật đều **chưa kiểm chứng** cho tới khi có thiết bị.

## 1. Trụ cột (theo thứ tự ưu tiên)

1. **Hình khối là thật.** Mài lấy đi khối lượng thật, búa dồn kim loại sang ngang, uốn không đổi thể tích. Người chơi thấy và đo được kết quả (gram, mm, HRC).
2. **Nhiệt luyện là một kỹ năng, không phải thanh tiến độ.** Nhiệt độ, tôi, ram, nứt đều từ mô hình luyện kim đơn giản đã có test. Sai thì lưỡi cong, nứt, mềm.
3. **Hardcore nhưng công bằng.** Không có thang điểm giấu: báo cáo nói rõ vì sao điểm thấp (góc lưỡi, độ thẳng, xoắn, độ cứng, độ dai, cân bằng).
4. **Ngắn, lặp lại được, cảm giác tay.** Một lần rèn 8 đến 15 phút, một lệnh cho một cử chỉ, tay thấy ngay hồi đáp (tia lửa, tiếng, rung).

## 2. Vòng chơi chính

```
Nhận đơn  ->  Chọn phôi  ->  Nung (lò)  ->  Rèn búa (đe)  ->  Nung/rèn lặp
          ->  Mài (bánh mài)  ->  Tôi (nước/dầu)  ->  Ram (lò ram)  ->  Kiểm tra  ->  Giao, nhận tiền, nâng cấp xưởng
```

Mỗi trạm là một chế độ của cùng một cảnh, chuyển bằng thanh tab dưới cùng. Lưỡi luôn là vật ở giữa màn hình.

| Trạm | Hành động | Hệ thống Domain đã có | Phản hồi cảm giác |
|---|---|---|---|
| Lò rèn | Đưa lưỡi vào lò, chọn tốc độ thời gian x1/x10/x60/x600 | ThermalSolver (Furnace) | Lưỡi phát sáng theo nhiệt độ, miệng lò rực, bloom |
| Đe | Chạm là một nhát búa, giữ lực chọn năng lượng; mặt búa phẳng hoặc nhọn | HammerSolver, nguội thì nứt | Tia lửa, tiếng búa, rung; lưỡi phình ngang |
| Bánh mài | Kéo ngón tay trên lưỡi để mài, áp lực 5 N một nấc | GrindSolver | Tia lửa, tiếng mài theo áp lực, khối lượng giảm |
| Bể tôi | Nhúng nước, dầu hoặc muối | ThermalSolver (Water/Oil/Brine) | Hơi nước, tiếng xì, tốc độ nguội quyết định nứt hay cứng |
| Lò ram | Giữ nhiệt thấp, thời gian dài | ThermalSolver (Oven), TemperP | Màu oxit rơm đến xanh |
| Kiểm tra | Xem báo cáo, xoay, phóng to cạnh | BladeEvaluator | Bảng điểm có lý do |

## 3. Bố cục cảnh (ảnh chụp dọc 9:19.5)

- Camera hiện tại trực giao, nhìn thẳng mặt lưỡi (dễ chạm chính xác). Các lớp theo trục Z: lưỡi (z=0), đe (z=0.5), lò (z=1.2), tường gạch (z=2.5).
- Lưỡi nằm dọc ở giữa. Đe ngay dưới phần giữa lưỡi, lò rèn bên trái, tường gạch phía sau, ánh lò ấm từ trái, ánh sáng lạnh từ cửa sổ bên phải (HDRI).
- Vùng bàn tay (dưới 33% màn hình) chỉ chứa nút. Mọi vật quan trọng nằm trên đó.
- Giai đoạn sau: chuyển sang camera phối cảnh có xoay nhẹ khi sang trạm; cần giữ nguyên đường chạm (ray camera) đã có test.

## 4. Chạm và điều khiển

- Một ngón: tác dụng của trạm hiện tại (mài kéo, búa chạm, kiểm tra xoay).
- Hai ngón: phóng to, xoay lưỡi (chế độ kiểm tra).
- Áp lực là nút Press -/+ vì điện thoại không đo lực chạm đáng tin; lượng tử hóa 5 N.
- Thời gian: x1 để nung có cảm giác, x600 để ram nhanh; hộp thoại không chặn việc chơi.

## 5. Hệ thống tiến trình (kế hoạch, chưa code)

- **Đơn hàng:** khách đưa BladeSpec (góc lưỡi, độ dày, độ cứng mục tiêu, khối lượng tối đa). Điểm là BladeReport so với spec. Hạng F đến S.
- **Kinh tế:** tiền thưởng theo hạng và độ khó; trừ chi phí phôi và nhiên liệu. Không có mua bằng tiền thật trong thiết kế này.
- **Nâng cấp xưởng:** lò nóng nhanh hơn, đe cho mặt búa tốt, bánh mài mịn hơn, bể dầu. Nâng cấp mở thêm vùng làm được, không chỉ tăng chỉ số.
- **Phôi và vật liệu:** thép 1080-like trước; các loại thép khác khi có dữ liệu vật liệu có kiểm chứng.
- **Lưu:** định dạng FBWS schema 2 đã có (SHA-256); thêm hồ sơ xưởng và danh sách đơn ở schema 3.

## 6. Thất bại và rủi ro (tạo chiều sâu)

| Lỗi | Nguyên nhân người chơi gây ra | Kết quả |
|---|---|---|
| Nứt khi búa | Búa dưới nhiệt độ rèn | Damage tăng, vết nứt tối trên lưỡi, điểm độ dai giảm |
| Nứt khi tôi | Nguội quá nhanh ở nước/muối với phôi dày | Damage >= 1, lưỡi hỏng |
| Quá nhiệt | Nung trên ngưỡng cháy | Mất chất lượng, báo cáo ghi rõ |
| Mềm | Không tôi hoặc ram quá nhiệt | HRC thấp |
| Cong, xoắn | Búa lệch | Điểm thẳng/xoắn giảm; khung mặt cắt đã mô hình hóa |

## 7. Phong cách hình ảnh và âm thanh

- Xưởng rèn tối, ánh lửa ấm là nguồn sáng chính, tương phản cao, thép là ngôi sao: phản xạ HDRI, vân xước, màu oxit khi ram.
- Tài sản CC0: HDRI Poly Haven `boiler_room`, texture ambientCG `Metal038` (đe) và `Bricks075A` (tường, lò). Mọi thứ còn lại tạo bằng code.
- Bloom và tonemapping ACES ở tầng chất lượng Medium trở lên; tầng Low bỏ bloom.
- Âm thanh hiện là thuật toán (nhiễu lọc, tiếng clank); thay bằng mẫu thật CC0 ở giai đoạn sau, giữ mức âm theo áp lực/tốc độ.
- HUD tối giản: khối lượng, nhiệt độ, HRC, áp lực. Báo cáo ở panel riêng.

## 8. Phân nền tảng

- iPhone là thiết bị phát triển duy nhất (Unity Remote 5): chỉ thấy hình, hiệu năng thấy là của PC.
- Tối ưu theo `PlatformProfile`: tầng chất lượng, số hạt tia lửa, bloom, độ phân giải texture (mobile giới hạn 1024 px, ASTC 6x6).
- Haptics: iOS cần CoreHaptics qua build native (cần Mac); hiện là placeholder.
- Android: cùng mã, chưa có thiết bị nên không khẳng định gì.

## 9. Cột mốc thiết kế

| Mốc | Nội dung | Trạng thái |
|---|---|---|
| D1 | Cảnh xưởng: tường, đe, lò, HDRI, bloom | Đang làm (ảnh render headless) |
| D2 | Thanh tab trạm và HUD theo trạm | Tiếp theo |
| D3 | Đơn hàng + chấm điểm theo spec | Chưa |
| D4 | Camera phối cảnh, hiệu ứng tôi (hơi nước) | Chưa |
| D5 | Kinh tế, nâng cấp, lưu hồ sơ | Chưa |
| D6 | Âm thanh thật, haptics native, thử trên thiết bị | Cần phần cứng |
