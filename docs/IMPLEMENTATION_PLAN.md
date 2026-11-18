# FORGEBORN — Kế hoạch triển khai v3 (PC trước, điện thoại sau)

Ngày đổi: 2026-11-18. **v3 thay thế v2.** Phần v2 (iPhone + Unity Remote) giữ nguyên bên dưới làm lịch sử; các giai đoạn A–E của v2 không còn là lộ trình hiện hành.

## v3. Lý do và lộ trình hiện hành

Không có Android; iPhone qua Unity Remote chỉ cho thấy hình/cảm ứng còn hiệu năng là của PC; build iOS cần Mac + Apple Developer. Bản Windows kiểm chứng được ngay trên máy này (build thật, chuột thật, FPS thật; máy chuẩn thấp: i7-10610U, Intel UHD). `Domain` độc lập nền tảng nên giữ nguyên.

Chốt với người dùng: mục tiêu **bản Windows chơi được, chưa phát hành/Steam**; bố cục **ngang 16:9 thiết kế lại**; điện thoại làm sau cổng P5.

| Giai đoạn | Nội dung | Cổng |
|---|---|---|
| P0 | Chuyển hướng: tài liệu, build target Windows, mốc test xanh | `dotnet test` 63/63, EditMode 33/33 |
| P1 | `IPointerSource` (chuột; cảm ứng cắm sau), phím tắt, UI ngang 1920x1080, menu/tạm dừng/cài đặt, tách HUD khỏi `ForgeSandbox` | Ảnh render 1920x1080 và 1280x720, "INPUT PATH OK" |
| P2 | `ForgeBornBuild.BuildWindows`, chạy .exe, đọc `Player.log`, đo FPS trên iGPU | exe chạy sạch log, số đo ghi `BENCHMARKS.md` |
| P3 | Lưu `Career` (schema 3), tiếng xì, cửa hàng nâng cấp, kiểm giới hạn khối lượng đơn 2–3, tiếng Việt | Test Domain/EditMode cho từng mục |
| P4 | Bóng đổ, mô hình đe/lò, miệng lò sáng, âm thanh thật, tầng chất lượng PC | FPS không tụt dưới ngân sách P2 |
| P5 | Người dùng chơi bản .exe một vòng đủ và báo lại | Người dùng xác nhận |
| P6 | Điện thoại: cảm ứng, layout dọc, safe area, tối ưu iOS/Android, thiết bị thật | Chỉ lên lịch sau P5 |

Nhãn bằng chứng (không trộn): `Domain test` · `Editor` · `Windows build (máy này)` · `Windows máy người dùng` · `Unity Remote (iPhone)` · `iOS native` · `Android build` · `Android máy thật`. Hai nhãn cuối của iOS/Android vẫn là "chưa kiểm chứng".

Đặt trước để P6 không phải làm lại: Domain không đổi; đầu vào qua `IPointerSource`; HUD tách lớp; `PlatformProfile` iOS/Android giữ nguyên.

---

# (Lịch sử) v2 — iPhone + Unity Remote

Thay thế kế hoạch cũ (hướng Android-first). Ngày lập: 2026-11-17.
Nguồn gốc: `TECHNICAL_DESIGN.md` (thiết kế V3) và `CLAUDE_CODE_START.md`.

## 1. Yêu cầu mới (chốt từ người dùng)
- Thiết bị thật duy nhất: **iPhone**. Không có điện thoại Android, không có Mac (đã khảo sát: Windows 11, i7-10610U, Intel UHD).
- Dev loop: **Unity Remote 5** (Editor trên PC, màn hình/cảm ứng từ iPhone).
- Ưu tiên chất lượng game; tối ưu **riêng cho iOS và Android** nhưng chỉ ghi "đạt" khi đã kiểm chứng đúng thiết bị.

## 2. Sự thật về Unity Remote (đã tra tài liệu Unity)
| Điều | Hệ quả cho kế hoạch |
|---|---|
| Game vẫn chạy **trên PC**; iPhone chỉ nhận hình nén và gửi cảm ứng/cảm biến về. Unity tự khuyên đo hiệu năng bằng bản build thật. | Remote kiểm chứng **cảm giác thao tác và logic**, **không** kiểm chứng FPS, nhiệt, bộ nhớ, shader Metal, ProMotion. |
| Windows cần **iTunes** (Apple Mobile Device) và cáp USB. | Bước cài môi trường; nếu lỗi thì bảo bạn cài tay. |
| Báo cáo cộng đồng: Remote 5 **không hỗ trợ Input System mới**. | Giữ **Input Manager cũ** (`Input.touches`), không thêm package Input System. Đã đúng với code hiện tại. |
| Chỉ có iPhone ≥ iPhone 11 là phổ biến → **không có 3D Touch/Force Touch**. | Áp lực mài **không** lấy từ cảm ứng. Dùng thanh/nút áp lực trên màn hình (lượng tử 5 N), cùng một lệnh trên mọi thiết bị. |
| Build IPA thật cần macOS + Xcode + Apple Developer. Máy này là Windows. | Mốc "chạy native trên iPhone" bị chặn cho tới khi có Mac (mua/thuê) hoặc dịch vụ build đám mây có chứng chỉ Apple. Xem §6. |

## 3. Nguyên tắc chất lượng
1. Domain độc lập Unity (đã có, 21 test) là nơi đặt mọi mô phỏng; test bằng `dotnet test`, không phụ thuộc thiết bị.
2. Mỗi tuyên bố được gắn nhãn bằng chứng, **không trộn**:
   `Domain test` · `Editor` · `Unity Remote (iPhone)` · `iOS build native` · `Android build` · `Android máy thật`.
3. Số liệu hiệu năng trên PC chỉ là **ước lượng**; ngân sách thật (FPS, ms/frame, nhiệt) chỉ ghi khi đo trên iPhone/Android thật.
4. Tối ưu riêng nền tảng nằm sau một lớp `PlatformProfile`, mặc định an toàn, bật bằng cấu hình, không rải `#if` khắp nơi.

## 4. Kiến trúc nền tảng (mới)
- `ForgeBorn.Unity/Platform/IPlatformProfile`: `TargetFps`, `MeshDetail` (số trạm × chu vi), `ShadowQuality`, `HapticsService`, `SafeArea`, `TextureFormat` gợi ý.
- `IosProfile` (Metal, ASTC, 60/120 Hz ProMotion tùy máy, Dynamic Island/safe area, CoreHaptics qua plugin nhỏ), `AndroidProfile` (Vulkan/GLES3, vibrator, thermal API), `EditorProfile` (mô phỏng iPhone).
- Chọn profile lúc chạy từ `Application.platform`; dữ liệu cấu hình nằm trong ScriptableObject, test được trong Editor.
- Domain không biết nền tảng; chỉ nhận lệnh đã lượng tử (đảm bảo công bằng iOS/Android, so sánh bằng dung sai).

## 5. Lộ trình

### Giai đoạn A — Dev loop Unity Remote (làm ngay, ~1 buổi)
| Bước | Việc | Kiểm chứng |
|---|---|---|
| A1 | Cài iTunes (kiểm tra bản winget/Store phù hợp), cài Unity Remote 5 trên iPhone (bạn làm), cắm cáp, bấm "Tin cậy" | iPhone hiện trong Unity: *Project Settings > Editor > Device = Any iOS Device* |
| A2 | Game view đặt tỉ lệ iPhone dọc (19.5:9); `Compression = JPEG` ban đầu để mượt | Mở scene Sandbox, chạm vào iPhone làm lưỡi rãnh trong Editor |
| A3 | Thay áp lực cảm ứng bằng **thanh áp lực** (UI, lượng tử 5 N); bỏ phụ thuộc `Touch.pressure` | Test EditMode: cùng gesture + cùng thanh → cùng lệnh |
| A4 | Ghi `docs/DEVICE_MATRIX.md` theo nhãn bằng chứng ở §3 | Mục "Unity Remote (iPhone)" = OK chỉ sau khi bạn xác nhận chạm hoạt động |
| A5 | Hoàn tất build APK đang chạy nền (chỉ để kiểm tra pipeline Android, hạ ưu tiên) | Log build; **không** gọi là đã chạy máy thật |

**Cổng A:** kéo ngón tay trên iPhone → rãnh xuất hiện trong Editor và khối lượng giảm đúng; mọi mục khác ghi "chưa kiểm chứng".

### Giai đoạn B — Chất lượng vật lý lõi (Domain, không cần thiết bị)
| Bước | Nội dung | Test |
|---|---|---|
| B1 | Thêm **centerline + xoắn** mỗi trạm (offset Y/Z, góc) vào `BladeProfile`, lưu trữ schema 2 có migrate từ schema 1 | Thể tích giải tích với lưỡi cong; round-trip; đọc được file schema 1 |
| B2 | **Búa (M2)**: lệnh `HammerStrike`, bảo toàn thể tích ≤ 1% sau 100 nhát, giao dịch + hoàn tác khi ring tự cắt | Property test 100 nhát ngẫu nhiên |
| B3 | Nhiệt/tôi/ram (M3): trường nhiệt theo trạm, độ cứng, rủi ro nứt; xác định, có seed | Test bảo toàn năng lượng gần đúng, tái lập |
| B4 | Chuẩn hóa đánh giá lưỡi (thẳng, đối xứng, vát, khối lượng, COM) | Bộ test vàng (golden) |
| B5 | Microbenchmark solver trên PC để đặt ngân sách (**ước lượng**, nhãn rõ) | Báo cáo ms/stroke, cảnh báo nếu vượt ngân sách giả định |

**Cổng B:** `dotnet test` xanh toàn bộ; không có đường code làm thể tích tăng ngoài búa có kiểm soát.

**Trạng thái Giai đoạn B (2026-11-18):** B1–B5 đã code, `dotnet test` 54/54 xanh, benchmark ở `docs/BENCHMARKS.md` (ước lượng PC).
Chưa commit; chờ review độc lập (`/code-review`). Ghi chú mô hình: búa bảo toàn thể tích theo từng trạm (nở ngang, chưa kéo dài theo X);
lưỡi cong/xoắn không đổi thể tích (mặt cắt luôn vuông góc X); tôi dùng nhiệt dung gộp theo trạm + dẫn nhiệt dọc, chưa mô hình cong vênh khi tôi.
Schema lưu là 2 (đọc được schema 1).

**Trạng thái Giai đoạn C (2026-11-18):** code xong và biên dịch được, 18/18 EditMode (headless). **Chưa xem bằng mắt** (xem DEVICE_MATRIX) và chưa chạy qua Unity Remote.
Đã có: mesh theo centerline/xoắn + màu nhiệt (phát sáng HDR), màu oxit khi ram, vết nứt; shader thép `ForgeBorn/BladeSteel` (vệt mài, không bóng đổ);
tia lửa (giới hạn hạt theo profile); âm thanh thủ tục (mài/búa); haptics qua `IHapticsService` (chỉ `Handheld.Vibrate`, chưa kiểm chứng, không phải CoreHaptics);
UI: GRIND/HAMMER/INSPECT, môi trường Air/Furnace/Water/Oil/Oven, tốc độ thời gian x1..x600, Zoom, báo cáo lưỡi, bar vát sẵn.
`PlatformProfile` (iOS/Android/Editor-giả-lập) là cấu hình mặc định, **chưa đo trên máy**. Đã khắc phục sau đó: audit/Id lệnh có giới hạn, thermal rẻ hơn (500 bước/lệnh ngoài tôi), shader có pass bóng/độ sâu, đường cảm ứng→hình học kiểm chứng bằng điểm chạm giả, test âm thanh. Còn thiếu: cảm ứng thật trên iPhone, haptics thật, thermal chạy nền (chỉ làm nếu đo thấy cần).

### Giai đoạn C — Chất lượng hình ảnh và cảm giác (Editor + Remote)
- Mesh: tách cạnh sắc (vertex đôi theo góc), pháp tuyến đúng cho vát; chỉ dựng lại đoạn bẩn bằng `Mesh.AllocateWritableMeshData` + Burst/Jobs nếu đo thấy cần.
- Shader URP thép: vệt mài theo hướng (anisotropic), nhiệt màu (temper colors) từ trường nhiệt, tia lửa (VFX nhẹ, giới hạn hạt theo profile).
- Cảm giác: âm thanh mài theo áp lực/tốc độ, haptic (iOS CoreHaptics / Android Vibrator) đi qua `HapticsService`, camera INSPECT mượt, chế độ WORK/INSPECT rõ ràng, nút ≥ 48 dp/44 pt.
- Mỗi hiệu ứng có mức chất lượng (High/Med/Low) trong profile; ảnh chụp Editor làm bằng chứng hình ảnh (nhãn `Editor`).

### Giai đoạn D — Tối ưu riêng iOS / Android (cấu hình sẵn, kiểm chứng theo thiết bị)
| iOS | Android |
|---|---|
| Metal, ASTC, 60 Hz mặc định; 120 Hz ProMotion là tùy chọn có kiểm tra nhiệt | Vulkan ưu tiên, GLES3 dự phòng; ASTC |
| Safe area + Dynamic Island, CoreHaptics | Thermal API, Adaptive Performance (tùy chọn), vibrator |
| Giới hạn bộ nhớ theo iPhone cũ nhất hỗ trợ (đặt sau khi đo) | Phân tầng thiết bị (low/mid/high) theo RAM/GPU |
Trạng thái mỗi mục: **Đã cấu hình** (code + test Editor) ≠ **Đã đo** (cần máy thật). Không gộp.

### Giai đoạn E — Cổng thiết bị thật (bị chặn bởi phần cứng/tài khoản)
- **iOS native**: cần macOS + Xcode + Apple Developer (cài lên máy cá nhân được với tài khoản miễn phí nhưng hạn 7 ngày; phát hành cần gói trả phí). Lựa chọn: thuê Mac đám mây, hoặc dịch vụ build đám mây, hoặc mượn Mac. **Cần bạn quyết định** (xem §7).
- **Android máy thật**: chưa có thiết bị → trạng thái giữ "chưa kiểm chứng"; có thể kiểm tra chức năng bằng emulator (không đại diện hiệu năng).
- M6 (bản offline hoàn chỉnh) **không** được báo xong cho tới khi cả hai nền tảng có bằng chứng máy thật (đúng yêu cầu thiết kế gốc).

## 6. Rủi ro
| Rủi ro | Mức | Xử lý |
|---|---|---|
| Remote nén hình, độ trễ → cảm giác sai so với native | Cao | Dùng Remote chỉ cho thao tác; cảm giác/tốc độ chốt bằng build native khi có Mac |
| iTunes/driver Apple lỗi trên Windows | Trung bình | Tự cài; lỗi thì hướng dẫn cài tay; thử cáp/cổng khác |
| Remote không hỗ trợ Input System, API cũ bị đánh dấu lỗi thời | Trung bình | Khóa Input cũ sau lớp `IPointerSource` để thay thế sau này |
| Không có Mac → không đo được hiệu năng/nhiệt iOS thật | Cao | Ngân sách ước lượng gắn nhãn; thuê Mac sớm ở Giai đoạn C |
| Tối ưu "mù" cho Android không máy thật | Cao | Chỉ cấu hình an toàn, ghi chưa kiểm chứng |
| Lưỡi tự cắt/độ chính xác với hammer | Trung bình | Giao dịch hoàn tác, back-off đã có |

## 7. Quyết định cần bạn xác nhận
1. Cho phép mình cài **iTunes** lên PC (kèm driver Apple) để Remote nhận iPhone?
2. Có kế hoạch tiếp cận Mac (thuê đám mây / mượn / dịch vụ build) và tài khoản Apple Developer không, hay giữ tới khi cần?
3. Áp lực mài bằng **thanh trượt trên màn hình** (vì iPhone mới không có cảm ứng lực) có chấp nhận được?

## 8. Việc đổi tài liệu khi duyệt
Cập nhật `IMPLEMENTATION_STATUS.md` (nhãn bằng chứng), `README.md`, thêm `docs/adr/ADR-002-iphone-remote-first.md`, tạo `DEVICE_MATRIX.md`.
