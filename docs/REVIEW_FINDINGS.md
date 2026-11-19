# Kết quả review 2026-11-18 (để phiên code xử lý)

**Cập nhật xử lý (2026-11-18):** đã sửa H1, H2, M1-M9, L1-L4, L7-L9; EditMode 56/56 (thêm 3 test), ảnh chụp batchmode đã xem. Còn lại: H3 một phần (bể tôi, bánh mài chưa vào cảnh), L5 (chưa thêm test mức đỉnh hiss), L6 (vô hại, giữ nguyên). M2 chọn cách ghi rõ "khởi động lại để áp dụng" trên nút ngôn ngữ. M3 đổi lời cảnh báo thành "quench gentler". Chưa kiểm trên Blender GUI/Unity GUI: M7/M8 (UV, smooth) chỉ kiểm qua kích thước và ảnh chụp.

Phạm vi: `6ff03e6..2c1957f` (P3.1 đến P3.5) và phần 3D (script Blender, 5 FBX, props, shader). Review chỉ đọc, chưa sửa mã.
Nhãn: **[Đã kiểm]** tôi đối chiếu trực tiếp với mã; **[Chưa kiểm]** do agent review nền báo, chưa đối chiếu. Số dòng có thể lệch, tìm theo tên hàm.
Chưa chạy lại test (Domain 80, EditMode 53 là số do phiên code báo). Chưa mở Unity để xem render.

Cách dùng: làm theo thứ tự ưu tiên bên dưới, mỗi mục có tiêu chí nghiệm thu. Xong mục nào thì đánh dấu và ghi vào `HANDOVER.md`.

## Việc cần làm trước gate P5 (HIGH)

| # | Vấn đề | Vị trí | Trạng thái | Cách sửa gợi ý | Nghiệm thu |
|---|---|---|---|---|---|
| H1 | Nút **Close** của cửa hàng làm game đứng: `OpenShop()` đặt `_paused=true`, `Time.timeScale=0`; nút Close chỉ gọi `ShopScreen.Hide()`, không gọi `ForgeSandbox.CloseShop()` (không bỏ pause, không `SaveCareer()`). Chỉ phím Esc đi đúng đường | `ShopScreen.cs` (constructor, nút Close), `ForgeSandbox.cs` (`OpenShop`, `CloseShop`) | Đã kiểm | Truyền `CloseShop` vào `ShopScreen` (thay `Hide`) | Mở shop, bấm Close: workshop chạy lại, `Time.timeScale==1`, `career.txt` được ghi |
| H2 | Phím tắt (1-6, Tab, `[`, `]`, `-`, `=`) vẫn chạy khi đang ở menu chính, pause, settings, shop vì `HandleDesktopInput()` chạy trước `if (_paused) return;` | `ForgeSandbox.cs` `Update()` (~489-490) | Đã kiểm | Bỏ qua phím tắt khi `_paused` hoặc có menu/shop mở (giữ riêng Esc) | Ở menu chính bấm 4: `_env` và công cụ không đổi |
| H3 | **5 FBX không được dùng ở đâu.** Tra GUID: 0 tham chiếu cho `anvil`, `furnace`, `quench_tank`, `grinder`, `handle`. Game dựng đe/lò bằng `CreatePrimitive`; bể tôi, bánh mài, chuôi không có trong cảnh. `HANDOVER.md:64` ghi "đã nhập vào Unity đúng kích thước và vật liệu" gây hiểu nhầm | `SandboxProps.cs` `Build`; `docs/HANDOVER.md:64` | Đã kiểm | Hoặc dùng FBX thay primitive (xem mục 3D bên dưới), hoặc sửa tài liệu cho đúng. Nên làm cả hai theo thứ tự | Không còn FBX mồ côi, hoặc tài liệu nói rõ "chưa dùng" |

## Trung bình (MEDIUM)

| # | Vấn đề | Vị trí | Trạng thái | Gợi ý |
|---|---|---|---|---|
| M1 | `ContinueGame()` không reset `_order`, `_delivered`, `_resultText`, `_showReport`, `_env` như `StartNewGame()` (có `_delivered=false`). Giao đơn rồi Continue: kiếm tải về bị chấm theo đơn cũ, báo "Already delivered" | `ForgeSandbox.cs` `ContinueGame` (~416) vs `StartNewGame` (~422) | Đã kiểm | Chung hàm reset trạng thái đơn |
| M2 | Đổi ngôn ngữ trong Settings không có hiệu lực ngay: `Loc.Current` chỉ gán một lần ở `Initialize()` (dòng ~200); `MenuScreens` chỉ ghi `settings.Lang`. Nhãn nút đổi, phần còn lại giữ ngôn ngữ cũ tới khi khởi động lại | `MenuScreens.cs` ~149, ~175; `ForgeSandbox.cs` ~200 | Đã kiểm | Gán `Loc.Current` khi đổi và dựng lại text, hoặc ghi rõ "cần khởi động lại" ngay trên nút |
| M3 | Tutorial bảo "try Oil next time" khi lưỡi nứt, nhưng Oil bị khóa tới khi mua ở shop (shop chỉ mở sau lần giao đầu) | `TutorialFlow.cs` (cảnh báo bước quench), `ForgeSandbox.SelectEnv`, `LocVi.cs` | Đã kiểm | Đổi lời cảnh báo hoặc bỏ gợi ý Oil khi chưa sở hữu |
| M4 | Benchmark kéo dọc (x=w*0.5, y từ h*0.42 đến 0.62) dù lưỡi đã nằm ngang; `ForgeBornCapture` đã chuyển sang kéo ngang. Số mài trong `BENCHMARKS.md` có thể không so sánh được với phương pháp cũ | `PlayerAutomation.cs` ~124 | Chưa kiểm | Kéo dọc theo lưỡi như Capture; đo lại và ghi chú đổi phương pháp |
| M5 | Right-drag xoay lưỡi và dịch `_camY` ở mọi công cụ; `ResetView()` chỉ reset tư thế lưỡi, không reset `_camY` | `ForgeSandbox.cs` ~372 | Chưa kiểm | Chỉ cho xoay ở Inspect, hoặc reset cả `_camY` |
| M6 | Texture kéo giãn trên primitive: mỗi Cube Unity có UV 0-1 riêng từng mặt; `AnvilFace` (0.46x0.04 m) dùng tiling 1.5 chung nên thép bị bóp ~11:1; tường gạch tiling 14x14 trên quad 1.6:1 nên gạch giãn ngang | `SandboxProps.cs` `Build` | Đã kiểm (suy từ mã, chưa xem render) | Tiling theo kích thước thật (tường 22.4x14), hoặc UV triplanar theo mét trong `ForgeBornProp.shader` |
| M7 | UV trong FBX không có mật độ texel theo mét: `create_cube` unwrap từng mặt 0-1 bất kể kích thước; `quad` không có UV | `tools/blender/forge_models.py` (`box`, `quad`, `frustum`) | Đã kiểm (suy từ mã) | Unwrap/box-project theo mét khi dựng lại mô hình |
| M8 | Tô phẳng toàn bộ: `use_smooth = False` cho mọi mặt nên bánh mài (32 cạnh), sừng đe (20), chuôi (14), cầu hiện từng mảng phẳng | `forge_models.py` `Builder.finish` | Đã kiểm (suy từ mã) | Smooth cho cylinder/sphere, flat cho box |
| M9 | Z-fighting trong `make_quench_tank`: đáy `L x W` có mặt ngoài trùng mặt phẳng với 4 vách | `forge_models.py` `make_quench_tank` | Đã kiểm (suy từ mã) | Thu đáy vào trong hoặc bỏ phần trùng |

## Thấp (LOW)

| # | Vấn đề | Vị trí | Trạng thái |
|---|---|---|---|
| L1 | `Loc.F` không bảo vệ: bản dịch sai số placeholder sẽ ném `FormatException` giữa lúc chơi. `LocTests` chỉ kiểm 6 mẫu, không phải toàn bảng | `Loc.cs`, `LocTests.cs` | Đã kiểm. Gợi ý: try/catch trả về English và thêm test quét toàn bảng `LocVi` |
| L2 | `Career.Parse` nhận tiền âm từ `career.txt` (văn bản thường, sửa tay được) | `Orders.cs` `Career.Parse` | Đã kiểm. Kẹp `Money >= 0` |
| L3 | `OrderResult.Add/Insert` dựng chuỗi bằng `string.Format` rồi UI `Loc.F` format lại cùng template: nếu sau này template hoặc Title/Client chứa `{` hoặc `}` sẽ ném lỗi | `Orders.cs` | Chưa kiểm sâu. Escape hoặc kiểm bằng test |
| L4 | Trùng hằng số: `SandboxProps.ViewHalfHeightM` với `ForgeSandbox.ZoomSizes[0]` (0.34); công thức spark count trong `PlatformProfile.WithTier()` với nhánh Android của `ForPlatform()` | `SandboxProps.cs`, `PlatformProfile.cs` | Chưa kiểm. Dùng một hằng chung |
| L5 | Hiss tôi nước cộng thêm biên độ tới ~0.35 lên mài và búa; chưa kiểm khả năng clip khi cả ba cùng chạy | `SandboxEffects.cs` `Render` | Chưa kiểm. Thêm test mức đỉnh |
| L6 | 3 `new Material(...)` trong `SandboxProps.Build` không bị hủy | `SandboxProps.cs` | Vô hại nếu `Build` gọi một lần |
| L7 | `forge_models.py` dùng `Material.use_nodes` (sẽ bị gỡ ở Blender 6.0, đã có cảnh báo); `main()` chạy ngay cấp module, không có `if __name__ == "__main__"` | `forge_models.py` | Đã kiểm |
| L8 | Không có kiểm chứng tự động kích thước/số tam giác của mô hình; script chỉ in console | `forge_models.py` | Đã kiểm |
| L9 | FBX bật `importCameras`/`importLights` dù không có camera/đèn | `Art/Models/*.fbx.meta` | Đã kiểm |

## Đã kiểm chứng tốt (đừng làm lại)

- Chạy lại `forge_models.py` trong Blender 5.3 cho FBX **giống hệt từng byte** bản đã commit (17004, 17500, 20556, 19868, 17756 byte). Quy trình tái tạo ổn định.
- Kích thước thực: đe 0.93x0.28x0.38 m (124 tam giác), lò 0.9x0.8x1.5 m (74), bể tôi 0.8x0.41x0.4 m (110), bánh mài 0.44x0.37x0.37 m (252), chuôi 0.27x0.18x0.05 m (288). Gốc tọa độ trên sàn; trục chuôi khớp trục lưỡi.
- Hướng sừng đe (+X, ngang mặt đe), trục xi lanh, vị trí chuôi/miếng chắn tại `x=0` đã tính tay: đúng.
- Import texture đúng: màu sRGB, normal map đúng kiểu, roughness không sRGB. `ForgeBornProp.shader` có DepthOnly hợp lệ.
- `Career` lưu bằng file tạm rồi đổi tên, có bắt lỗi; `Career.Parse` không ném lỗi; cờ `_persistCareer` chặn ghi file khi benchmark, chụp ảnh, batch.

## Gợi ý cho phần 3D (có Blender MCP, mở phiên mới)

Mục tiêu giải quyết H3, M6-M9. Nguyên tắc: không đoán, nhìn kết quả.
1. Mở Blender có giao diện (addon `blender_mcp` tự khởi động server ở cổng 9876; MCP `blender` đăng ký ở cấp người dùng).
2. Dựng lại 5 mô hình có bevel, smooth theo nhóm, UV theo mét; chụp viewport để xem, sửa, chụp lại. Xuất FBX vào `Assets/_ForgeBorn/Art/Models`.
3. Thay primitive trong `SandboxProps.Build` bằng các FBX, gán `ForgeBornProp.shader`, kiểm bằng ảnh chụp (`ForgeBornCapture`, 1280x720) và so với `docs/shots/`.
4. Thêm test/số liệu: kích thước và số tam giác mỗi mô hình.
5. Ghi nguồn tài sản mới vào `CREDITS.md` nếu dùng thêm Poly Haven/Sketchfab.

## Ghi chú công cụ (tình trạng máy lúc review)

- Đã cài: Blender MCP (`uvx mcp-for-blender`, addon `blender_mcp` đã bật trong Blender 5.3), Audacity, Serena (cài lại với Python 3.12), `blender` đã vào PATH người dùng.
- Cần quyền admin, chưa cài: Krita, RenderDoc (`winget install KDE.Krita BaldurKarlsson.RenderDoc` trong PowerShell chạy bằng "Run as administrator").
- GitHub MCP lỗi vì biến môi trường `GITHUB_PERSONAL_ACCESS_TOKEN` chưa đặt; đã dùng `gh` CLI thay.
- Gói Unity chưa thêm: Cinemachine, Recorder, Profiler (cần sửa `Packages/manifest.json`, để phiên code làm).
- Phần cứng đo duy nhất là Intel UHD 1 GB, 15.6 GB RAM: mọi số FPS chỉ là mốc thấp.
