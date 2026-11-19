# Nghiên cứu: vì sao khó giữ đúng góc mài

Kết quả đo bằng test Domain (`build/ForgeBorn.Domain.Tests/GameplayAidsTests.cs`), phôi thon, đá vừa, tia nhìn thẳng mặt lưỡi, tốc độ 0,25 m/s. Góc là góc kẹp mép (độ) mà `BladeEvaluator` đo.

## Kết luận ngắn

1. **Người chơi không điều khiển được góc vát.** Góc phụ thuộc vào khoảng cách con trỏ so với mép, nhưng không đơn điệu và không trơn: lệch 1 mm có thể đổi 3° thành 19°, hoặc không ra mép vát nào. Trên màn hình 1080p ở độ phóng mặc định, 1 mm ≈ 1,6 px.
2. **Mài đúng trên đường mép không tạo mép vát** (0 mm: không có vát ở cả ba loại đá).
3. **Cùng một đường mài thẳng cho góc khác nhau dọc lưỡi** (2° đến 36° trong một lượt), vì phôi thon làm khoảng cách tới mép đổi theo x, còn vòng mặt cắt chỉ có 48 điểm (cách nhau khoảng 2,5 mm).
4. **Tăng độ phân giải không chữa được.** Thử 96 và 192 điểm: vẫn nhảy loạn, lại chậm hơn 3 đến 8 lần.
5. **Độ lệch tốt nhất đổi theo lực ấn.** Lực game là 20 đến 150 N, mặc định 80 N; các số đầu tiên đo ở 200 N nên không dùng được.

## Số liệu (một lượt, góc trung bình các trạm đo được mép vát; "-" = không có mép vát)

Khoảng cách dưới mép (mm): 3 / 4 / 4,5 / 5 / 5,5 / 6 / 6,5 / 7 / 7,5 / 8

| Lực | Đá | Góc |
|---|---|---|
| 80 N | thô | 40 / 7 / 7 / 10 / 5 / 14 / - / 8 / 7 / 3 |
| 80 N | vừa | 40 / 6 / 11 / 15 / 3 / 2 / - / 2 / 1 / 4 |
| 80 N | mịn | 6 / 8 / 7 / 3 / - / - / - / - / 1 / - |
| 150 N | thô | 42 / 4 / 4 / 12 / 15 / 10 / 1 / 18 / 3 / 5 |
| 150 N | vừa | - / 6 / 6 / 9 / 18 / 7 / 19 / 1 / 1 / - |
| 150 N | mịn | 5 / 24 / 23 / 9 / - / - / - / - / 4 / - |

Độ phân giải vòng (đá vừa, 200 N, số trạm có mép vát trên 96 trạm giữa lưỡi và góc trung bình):

| Điểm trên vòng | 3 mm | 4 mm | 5 mm | 6 mm | 7 mm |
|---|---|---|---|---|---|
| 48 | 71/96, 33° | 28/96, 3° | 96/96, 5° | 41/96, 10° | 70/96, 15° |
| 96 | 1/96 | 96/96, 29° | 3/96 | 72/96, 7° | 3/96 |
| 192 | 96/96, 16° | 96/96, 11° | 96/96, 21° | 81/96, 10° | 7/96 |

## Vì sao (nguyên nhân gốc, trước khi sửa)

Ngoài đời, **góc vát bằng góc cầm lưỡi so với đá**: người thợ nghiêng lưỡi 20° thì mép ra 20°, không phụ thuộc chuyện tay lệch 1 mm. Trước khi sửa, game **không có tham số góc nào**. `GrindSolver` đẩy các đỉnh vào trong theo pháp tuyến bề mặt tại chỗ chạm, với dạng cos² rộng bằng bán kính đá, nên hình mép chỉ là hệ quả của vị trí con trỏ và độ sâu mỗi nhát. `BladeEvaluator` lại đo góc bằng hai điểm cách đầu mép 2 mm và 6 mm, nên rất nhạy.

Hệ quả phụ: mài một mặt làm tâm mặt cắt lệch, và báo cáo coi đó là "cong" (`Straightness` đo tâm mặt cắt). Cần sửa cùng đợt.

## Đã sửa: mài theo góc đặt (2026-11-18)

Lệnh mài mang thêm `BevelAngleDeg` (góc kẹp mép, 10° đến 40° theo nấc 5°; 0 = hành vi cũ). Khi con trỏ nằm trong 12 mm tính từ đầu mép, `GrindSolver` kéo từng đỉnh vòng mặt cắt về một mặt phẳng nghiêng θ/2 neo ở đầu mép (nửa dày đầu mép 0,15 mm). Mỗi nhát chỉ tiến gần mặt phẳng đó một bước theo tốc độ gỡ kim loại như cũ, và chỉ gỡ, không đắp. Xa mép (mài mặt để bớt khối lượng) vẫn dùng rãnh cos² cũ.

Hệ quả: **góc cuối không còn phụ thuộc vị trí tay, lực ấn hay loại đá**; những thứ đó chỉ quyết định cần mấy nhát. Phải mài cả hai mặt mới ra đủ góc (một mặt ra khoảng một nửa), nên game có nút Lật (phím F). Phím Q/E hoặc nút "Góc" đổi nấc. Mở đơn hàng thì tự chọn nấc gần góc yêu cầu nhất.

Kết quả đo (test `GrindFacetTests`, phôi 3 mm, đá thô, hội tụ):

| Đặt | Đo được |
|---|---|
| 10° / 20° / 30° / 40° | 9,8° / 20,0° / 29,6° / 37,5° (mặt vát dốc ngắn hơn đoạn đo nên đọc thấp một chút) |
| Tay lệch 0,5 / 2 / 4 / 6 mm (đặt 20°) | 19,9° / 20,0° / 19,6° / 19,7° |
| Lực 40 N / 150 N (đặt 20°) | 19,7° / 19,7° |
| Một mặt (đặt 20°) | 10,1° |
| Phôi thon dày của game, đá thô 150 N, 5 vòng hai mặt | 20,0° |
| Qua đường chạm thật trong Unity (capture), 4 vòng | 20,0° (từ 11,8° vòng 2, 18,3° vòng 3) |

Số nhát cần: 4 đến 5 vòng hai mặt với đá thô ở 150 N; đá vừa ở 80 N chậm hơn nhiều (một nhát hạ đầu mép khoảng 0,2 mm, mà phôi dày cần hạ khoảng 2,7 mm mỗi mặt). Đây là tốc độ mài sẵn có của game; nên chỉnh theo cảm giác khi chơi thật.

Sửa kèm theo ở `BladeEvaluator`:
- Góc mép đo ở 1 mm và 3 mm sau đầu mép (trước đây 2 mm và 6 mm, vượt quá đầu mặt vát ngắn).
- Độ cong đo theo tâm khung bao mặt cắt thay vì tâm diện tích, nên mài một mặt không còn bị coi là "cong".
- Thêm `BevelAsymmetry` để nhắc "mới mài một mặt, hãy lật lưỡi".

Đường ray ở trợ giúp Mạnh giờ chỉ giữ con trỏ cách mép 2 mm, vì vị trí không còn quyết định góc.

## Chưa làm

- Cân tốc độ mài theo cảm giác chơi (đá mặc định đang là đá vừa, chậm cho phôi dày).
- Mài một mặt vẫn đọc ra khoảng nửa góc; đơn hàng chấm theo góc kẹp mép nên người chơi phải làm đủ hai mặt (đã có gợi ý).
