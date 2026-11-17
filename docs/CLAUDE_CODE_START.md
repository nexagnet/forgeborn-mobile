# Yêu cầu cho Claude Code — FORGEBORN MOBILE

Đọc TOÀN BỘ file `FORGEBORN_MOBILE_TECHNICAL_DESIGN_V3.md` và triển khai dự án game di động theo đó.

**Ưu tiên số 1:** Chất lượng trên **iOS và Android thực tế**, không phải chạy một mã nguồn duy nhất bằng mọi giá. Dùng Unity 6.3 LTS, chia phần mô phỏng lõi và tối ưu riêng từng nền tảng nếu có cơ sở benchmark. **Không tạo web game.**

Tự kiểm tra OS, Unity, modules, Mac/Xcode/iOS signing, Android SDK và thiết bị. Bắt đầu M0 (kiểm chứng build và thiết bị hai nền tảng) rồi M1 (đường mài thực sự trừ vật liệu, thay mesh, giảm khối lượng). Chủ động quyết định chi tiết kỹ thuật nhỏ, không dừng ở bản kế hoạch. Tiếp tục các milestone sau khi đạt tiêu chí nghiệm thu.

Yêu cầu không thể bỏ qua:
- Khi mài, hình học và khối lượng thực sự thay đổi; đập phải biến dạng với mô hình gần bảo toàn thể tích.
- Phân tách WORK/INSPECT bằng cảm ứng; điều khiển màn hình dọc, safe areas và multitouch rõ ràng.
- Không giả vờ đã build/test trên iPhone nếu thiếu Mac/Xcode/thiết bị/chữ ký. Ghi rõ trạng thái iOS và Android riêng.
- Có test tự động, cập nhật tài liệu tiến độ, build script và video/screenshot hoặc profiling thực tế khi có thể.
- Không lấy multiplayer hoặc đồ họa đẹp làm cái cớ bỏ qua vật lý lõi; nhưng cũng không tuyên bố hoàn tất chất lượng hình ảnh nếu còn placeholder.

Ngay sau khi đọc tài liệu: tạo repository/dự án Unity, cấu trúc mã nguồn, scene thử nghiệm và bắt đầu lập trình, không chỉ trả lời bằng kế hoạch.
