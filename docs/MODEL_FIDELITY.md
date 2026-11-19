# Mức tin cậy của từng mô hình (giai đoạn 4a)

Mỗi mô hình có một nhãn bằng chứng:

- **Số liệu thật**: hệ số lấy từ nguồn công bố, có ghi tên nguồn.
- **Dạng thật, hệ số hiệu chỉnh**: công thức có cơ sở vật lý, nhưng hệ số chỉnh cho dễ chơi.
- **Hiệu chỉnh cho gameplay**: hành vi được chọn để chơi vui, không đối chiếu số liệu.
- **Giả định**: con số tạm, chưa có dữ liệu.

Hiện **chưa có mô hình nào mang nhãn "số liệu thật"**: dự án chưa có ASM Handbook hay datasheet, và mình không tự bịa số. Các dải trong `GameplayAidsTests` (nhóm `PublishedRanges_*`) là giá trị thường gặp trong giáo trình, dùng làm dải dung sai, **nguồn chưa được xác nhận**. Cần bạn cấp tài liệu hoặc cho phép tra nguồn công khai rồi ghi tên nguồn vào đây.

| Mô hình | Mã | Nhãn | Ghi chú đã kiểm |
|---|---|---|---|
| Truyền nhiệt | `ThermalSolver` | Dạng thật, hệ số hiệu chỉnh | Bảo toàn năng lượng có test; hệ số truyền nhiệt từng môi trường là giá trị chỉnh |
| Chuyển pha, martensit | `ThermalSolver.PhaseStep` | Hiệu chỉnh cho gameplay | Ngưỡng 120 K/s (cứng hoàn toàn), 130 K/s (nứt) rất hẹp; một loại thép duy nhất |
| Độ cứng, ram | `MaterialData.HardnessHrc`, Hollomon-Jaffe | Dạng thật, hệ số hiệu chỉnh | Tôi đủ nhanh: 62 HRC (đạt ≥ 60). Ram 200 °C 1 giờ: 60 HRC (giảm 2). Ram 400 °C 1 giờ: 44 HRC (có thể thấp hơn giá trị giáo trình cho thép tương tự, cần đối chiếu) |
| Ứng suất chảy nóng | `MaterialData.FlowStressPa` | Hiệu chỉnh cho gameplay | Bảng 6 điểm, nội suy log |
| Búa | `HammerSolver` | Hiệu chỉnh cho gameplay | Bảo toàn thể tích theo trạm; **không kéo dài lưỡi**; ngưỡng nguội 1050 K cứng, không có vùng chuyển |
| Mài | `GrindSolver` (mặt vát theo góc đặt), `BladeEvaluator.EdgeAt` | Dạng thật, hệ số hiệu chỉnh | Gần mép: cắt về mặt phẳng nghiêng θ/2 neo ở đầu mép, nên góc ra đúng góc đặt (`GrindFacetTests`). Tốc độ gỡ kim loại và vùng 12 mm là hiệu chỉnh. Xa mép: rãnh cos² cũ, hiệu chỉnh cho gameplay |
| Chấm điểm | `BladeEvaluator` | Hiệu chỉnh cho gameplay | Góc mép đo ở 1 mm và 3 mm sau đầu mép; độ cong đo theo tâm khung bao (đã sửa lỗi mài một mặt bị coi là cong) |
| Giới hạn khối lượng đơn | `OrderBook` | Giả định | Chỉ có test tới được bằng một lượt mài (`BalanceTests`) |
| Chỉ báo tôi, gợi ý, dự báo ram | `ProcessAdvisor` | Hiệu chỉnh cho gameplay | Dùng cùng ngưỡng với bộ giải; dự báo ram khớp với lò trong sai số 3 HRC |

## Điều test đã phát hiện

1. **Dầu không tôi được thép này**: tôi dầu 30 giây từ 1400 K cho 0 martensit, 22 HRC (nước: 100 %, 62 HRC). Về vật lý hợp lý với thép carbon tôi nước, nhưng nâng cấp "bể dầu" trong cửa hàng hiện không có tác dụng gì có lợi.
2. **Góc mài không điều khiển được bằng tay**: đã sửa bằng mài theo góc đặt, xem `GRIND_ANGLE_RESEARCH.md`.
3. **Mài một mặt từng bị đọc là cong**: đã sửa (độ cong đo theo tâm khung bao).

## Các bước rèn thật mà game chưa có

| Bước | Trạng thái |
|---|---|
| Búa kéo dài lưỡi | Chưa có (chỉ nong rộng) |
| Ủ, làm chuẩn tổ chức | Chưa có |
| Cong vênh khi tôi, nắn thẳng | Chưa có |
| Mòn đá mài, sửa đá | Chưa có |
| Tôi phân vùng (đất sét) | Chưa có; martensit chỉ lưu theo trạm, không theo vị trí quanh mặt cắt |
| Góc mài | Có (góc đặt 10° đến 40°, lật lưỡi để mài mặt kia) |

Thứ tự đề xuất khi làm tiếp: rà vai trò của dầu (hiện không tôi được), rồi ủ và mòn đá. Kéo dài, cong vênh và tôi phân vùng chỉ nên làm nếu mục tiêu là mô phỏng nghiêm túc.

## Đối chiếu ngoài

Chưa làm. Cần thợ rèn thật, video hoặc sách xem lại thứ tự và hệ quả từng bước. Việc này không tự làm được.
