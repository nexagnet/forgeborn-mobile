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

- Camera phối cảnh FOV 35 độ, lệch ngang nhẹ để thấy chiều sâu; mặt lưỡi vẫn hướng về camera (tia chạm dùng ScreenPointToRay nên đúng với phối cảnh). Các lớp theo trục Z: lưỡi (z=0), đe (z=0.5), lò (z=1.2), tường gạch (z=2.5).
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

- **PC (Windows) làm trước** (xem `IMPLEMENTATION_PLAN.md`): bố cục ngang 16:9, chuột và bàn phím. Điện thoại làm sau cổng P5.
- Tối ưu theo `PlatformProfile`: tầng chất lượng, số hạt tia lửa, bloom, độ phân giải texture. Mục tiêu PC thấp nhất là Intel UHD.
- Điện thoại (hoãn): haptics iOS cần CoreHaptics qua build native (cần Mac); Android chưa có thiết bị nên không khẳng định gì.

> Các mục §3, §4 và §12 bên dưới viết cho màn hình dọc cảm ứng. Bản ngang cho PC sẽ được thiết kế lại ở P0.5 (mục UI ngang cuối file này).

## 9. Cột mốc thiết kế

| Mốc | Nội dung | Trạng thái |
|---|---|---|
| D1 | Cảnh xưởng: tường, đe, lò, HDRI, bloom | Đang làm (ảnh render headless) |
| D2 | Thanh tab trạm và HUD theo trạm | Tiếp theo |
| D3 | Đơn hàng + chấm điểm theo spec: 3 đơn (học việc, thợ, thợ cả), nút Order và Deliver, báo cáo lý do, tiền trong bộ nhớ (chưa lưu, chưa có cửa hàng). Giới hạn khối lượng 1500 g và 1450 g là số giả định chưa kiểm tra có đạt được bằng mài | Một phần (Domain có test, UI chỉ thấy ảnh render) |
| D4 | Camera phối cảnh và hơi nước khi tôi (nước trắng, dầu khói sẫm; chỉ hình ảnh, không đổi mô hình nhiệt). Xong ở mức ảnh render headless; tiếng xì và rung chưa có | Xong phần hình |
| D5 | Kinh tế, nâng cấp, lưu hồ sơ | Chưa |
| D6 | Âm thanh thật, haptics native, thử trên thiết bị | Một phần: có mẫu tiếng búa thật; còn lại cần phần cứng |

## 10. Định vị so với game cùng thể loại

Tra cứu nhanh ngày 2026-11-18 từ trang cửa hàng và tổng hợp đánh giá (nguồn không đồng đều, một số là trang tổng hợp tự động nên chỉ mang tính tham khảo, chưa phải khảo sát):

- Game rèn trên mobile (Blade Forge 3D, Smithing Master, Forge Master, Forge Ahead) thiên về arcade/idle. Lời phàn nàn lặp lại nhiều nhất: quảng cáo xen ngang, tiến trình bị kéo dài để ép xem quảng cáo hoặc trả tiền, kinh tế bị làm yếu sau cập nhật.
- Không tìm thấy game mobile nào mô hình hóa nhiệt luyện và khối lượng thật. Khoảng trống này là chỗ để khác biệt, nhưng cũng có thể là dấu hiệu thị trường nhỏ; chưa có số liệu để kết luận.

Hệ quả cho thiết kế:

1. Không quảng cáo xen ngang, không chờ bằng thời gian để ép trả tiền, không mua bằng tiền thật trong bản đầu.
2. Tiến trình đến từ kỹ năng (đơn khó hơn cần kiểm soát nhiệt và hình khối chính xác hơn), không từ việc cày.
3. Điểm khác biệt phải **thấy được trong 60 giây đầu**: nung lưỡi, một nhát búa làm lưỡi phình ngang, mài thấy gram giảm. Hướng dẫn 13 bước đã dẫn thẳng vào đó.

## 11. Phiên chơi, độ khó, kinh tế (số khởi điểm, chưa chơi thử)

Mọi con số dưới đây là **giả định để có cái điều chỉnh**, không phải kết quả đo.

- Một đơn: 8 đến 15 phút. Ba mức: Học việc (spec rộng, một chỉ tiêu quan trọng), Thợ (spec chặt hơn, hai chỉ tiêu), Thợ cả (spec chặt, thép khó hơn).
- Đơn đưa spec và hạn mức vật liệu. Điểm = trung bình có trọng số các tiêu chí của BladeReport so với spec; hạng theo `BladeEvaluator` đã có: S từ 92, A 85, B 75, C 65, D 50, dưới 50 là F. Mỗi đơn có điểm tối thiểu riêng (55, 65, 75); dưới mức đó, hoặc nặng quá hạn mức, khách từ chối.
- Tiền thưởng = giá cơ bản theo mức × hệ số hạng (F 0, D 0.4, C 0.7, B 1, A 1.3, S 1.6), trừ phôi và nhiên liệu. Hỏng (nứt, quá nhiệt) thì mất phôi, không bị phạt thêm.
- Nâng cấp mở thêm khả năng chứ không chỉ cộng chỉ số: lò nóng nhanh hơn, mặt búa nhọn, bể dầu, bánh mài mịn.

## 12. Luồng màn hình

1. Xưởng (màn chính, cảnh hiện tại): lưỡi ở giữa, thanh trạm và nút ở vùng ngón cái.
2. Bảng đơn: danh sách đơn có spec và tiền thưởng; chọn một để nhận phôi.
3. Rèn: sandbox hiện tại; góc trên hiện spec rút gọn và tiến độ từng tiêu chí.
4. Giao hàng: báo cáo có lý do từng điểm, hạng, tiền thưởng; nút "Thử lại" và "Đơn tiếp".
5. Cửa hàng nâng cấp: chỉ mở sau đơn đầu.

---

# Thiết kế cho bản PC (P0.5, bản nháp đầu tiên)

Các mục §13–§16 là thiết kế cho bản Windows. Chúng thay cho §3, §4 và §12 khi làm P1. Mọi con số là giả định để chỉnh sau khi chơi thử (P5), không phải kết quả đo.

## 13. Bố cục UI ngang 16:9

Khung tham chiếu 1920x1080, co giãn theo `CanvasScaler` (ScaleWithScreenSize). Lề an toàn 3% mỗi cạnh. Chữ nhỏ nhất 16 px ở 1080p. Hỗ trợ 16:10 (co khung giữa) và ultrawide (chỉ nới hai bên, không kéo giãn lưỡi).

```
+--------------------------------------------------------------------------+
| ĐƠN: tên, khách, tiêu chí rút gọn            Tiền        [Cài đặt] [Esc] |  thanh trên (72 px)
+----------+------------------------------------------+--------------------+
| TRẠM     |                                          | THÔNG SỐ           |
| 1 Lò     |                                          | Khối lượng  g      |
| 2 Đe     |          LƯỠI Ở GIỮA (khu thao tác)      | Nhiệt độ    °C     |
| 3 Mài    |                                          | Độ cứng     HRC    |
| 4 Tôi    |                                          | Áp lực      N      |
| 5 Ram    |                                          | Tốc độ  x1..x600   |
| 6 Kiểm   |                                          | Báo cáo (có lý do) |
+----------+------------------------------------------+--------------------+
| Gợi ý thao tác theo trạm              [Nhận đơn]  [Giao hàng]  [Zoom]    |  thanh dưới (88 px)
+--------------------------------------------------------------------------+
```

| Màn hình | Nội dung chính | Điều hướng |
|---|---|---|
| Menu chính | Tiếp tục, Chơi mới, Cài đặt, Thoát | Chuột, Enter/Esc |
| Xưởng | Bố cục ở trên; lưỡi ở giữa, trạm bên trái, thông số bên phải | Phím 1–6 đổi trạm |
| Bảng đơn | Danh sách đơn: khách, tiêu chí, tiền thưởng, giới hạn khối lượng | Chọn một đơn để nhận phôi |
| Giao hàng | Hạng, điểm từng tiêu chí kèm lý do, tiền thưởng trừ chi phí phôi | Nút "Thử lại", "Đơn tiếp" |
| Cửa hàng | Nâng cấp xưởng, mở sau đơn đầu | Quay lại bảng đơn |
| Cài đặt | Độ phân giải, toàn màn hình, tầng chất lượng, âm lượng, phím | Lưu khi thoát |
| Tạm dừng | Tiếp tục, Cài đặt, Về menu | Esc |

Điều khiển chuột và phím (thay cho cảm ứng):

| Việc | Chuột/phím |
|---|---|
| Mài | Giữ chuột trái và kéo trên lưỡi |
| Búa | Nhấp chuột trái; giữ lâu hơn để tăng năng lượng |
| Xoay/zoom lưỡi (kiểm tra) | Chuột phải kéo để xoay, con lăn để zoom |
| Áp lực mài | `[` giảm, `]` tăng (nấc 5 N), thanh trên màn hình |
| Tốc độ thời gian | `-` / `=` (x1, x10, x60, x600) |
| Đổi trạm | `1`–`6`, hoặc `Tab` sang trạm kế |
| Tạm dừng | `Esc` |

## 14. Art bible

Xưởng rèn tối, ánh lửa ấm là nguồn sáng chính, thép là ngôi sao. Không gian màu Linear; bloom và ACES ở tầng Medium trở lên.

| Vai trò | Giá trị gợi ý |
|---|---|
| Nền xưởng | `#120C0A` (nâu đen ấm) |
| Gạch/lò | `#3A1F18` đến `#6B3326` |
| Thép nguội | `#6B7078`, độ nhám 0.35–0.5 |
| Lưỡi nóng | dải `#7A1A0E` (đỏ thẫm) → `#FF7A1A` (cam) → `#FFC35A` (vàng); phát sáng HDR |
| Ánh sáng chính | ấm, từ lò bên trái; ánh phụ lạnh `#7FA6C8` từ cửa sổ bên phải (HDRI `boiler_room`) |
| Nền bảng UI | `#1A1412` độ mờ 85% |
| Chữ | `#EDE3D6`; tiêu đề nhấn `#F2A33A`; cảnh báo `#D9503B`; đạt `#6FB36B` |

Quy tắc: tương phản chữ/nền tối thiểu 4.5:1; không dùng màu làm tín hiệu duy nhất (kèm biểu tượng hoặc chữ) để người mù màu vẫn đọc được độ nóng; mô hình tự làm trong Blender (đe, lò, bể tôi, bánh mài, chuôi) dùng chung bảng màu này; ảnh chụp từng bản dựng so với bảng này trước khi gọi là xong.

## 15. Danh sách âm thanh

Hiện có 1 mẫu thật (tiếng búa); phần còn lại là thuật toán. Nguồn mới phải là tự tạo (REAPER, ffmpeg) hoặc CC0, ghi vào `CREDITS.md`.

| Âm thanh | Hiện trạng | Kế hoạch |
|---|---|---|
| Mài (vòng lặp, cao độ và âm lượng theo áp lực và tốc độ) | Thuật toán | Giữ cơ chế, thay chất liệu bằng mẫu thật |
| Búa (3 biến thể theo nhiệt độ lưỡi) | 1 mẫu thật | Thêm 2 biến thể; nguội thì tiếng "chát" |
| Xì khi tôi (nước, dầu) | Chưa có | Làm ở P3 (hơi nước hình ảnh đã có) |
| Lửa lò (nền, to dần khi lưỡi trong lò) | Chưa có | P4 |
| Không khí xưởng (nền nhẹ) | Chưa có | P4 |
| Giao diện (nhấp, xác nhận, lỗi) | Chưa có | P1–P4 |
| Nhạc nền | Chưa có | Tùy chọn, sau P5 |

Định dạng: WAV 44.1 kHz; âm lượng mặc định thấp, thanh trượt riêng cho hiệu ứng và nhạc ở màn Cài đặt.

## 16. Cân bằng kinh tế và độ khó (số hiện tại, trong `Orders.cs`)

| Đơn | Khách | Phôi | Tiền cơ bản | Điểm tối thiểu | Giới hạn khối lượng | Tiêu chí chính |
|---|---|---|---|---|---|---|
| o1 | Lính làng | 20 | 100 | 55 | không | góc 25° ±15°, 55 HRC ±15, thẳng ≤ 6 mm |
| o2 | Lái buôn | 30 | 180 | 65 | 1500 g | góc 20° ±10°, 58 HRC ±12, thẳng ≤ 4 mm, xoắn ≤ 3° |
| o3 | Kiếm sĩ | 50 | 320 | 75 | 1450 g | góc 16° ±6°, 60 HRC ±8, thẳng ≤ 3 mm, xoắn ≤ 2°, cân bằng 0.15 |

Tiền thưởng = tiền cơ bản × hệ số hạng (S 1.6, A 1.3, B 1.0, C 0.7, D 0.4, F 0), trừ chi phí phôi.

Việc cần kiểm trong P3, theo thứ tự:
1. Mô phỏng mài trong Domain để biết giới hạn 1500 g và 1450 g có đạt được không (hiện chưa kiểm).
2. Đo thời gian chơi một đơn bằng người chơi thật ở P5; mục tiêu 8–15 phút.
3. Đường cong: đơn 2 và 3 chỉ cần kỹ năng cao hơn, không cần cày tiền; nâng cấp mở thêm khả năng (§11).
4. Viết lại hướng dẫn 13 bước cho chuột/phím và dịch sang tiếng Việt: lời hướng dẫn hiện là **tiếng Anh**, nói "Tap" (chạm) ở nhiều bước; mọi chuỗi UI trong mã cũng là tiếng Anh.
