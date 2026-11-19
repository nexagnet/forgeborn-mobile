# FORGEBORN: bắt đầu từ đây

Cập nhật: 2026-11-18. File này nói game đang ở đâu và làm gì tiếp. Lộ trình: `IMPLEMENTATION_PLAN.md`. Thiết kế game: `GAME_DESIGN.md`.

## 1. Hiện trạng

| Mục | Giá trị |
|---|---|
| Hướng | **PC (Windows) trước**, điện thoại sau cổng P5 |
| Unity | 6000.3.28f1 (6.3 LTS), build target **Windows64**, backend Mono, không gian màu **Linear** |
| Test | Domain `dotnet test` **80/80**; Unity EditMode **53/53** (2026-11-18, sau P3) |
| Giai đoạn | P0, P0.5, P1, **P3 xong về mã và test** (xem §6 "P3"); kế tiếp P4 hoặc cổng P5 (cần bạn chơi) |
| Bản Windows | **Build được và chạy được** (123.9 MB, `build/out/win/ForgeBorn.exe`); đo trên Intel UHD ở 1280x720: nhàn rỗi 4.4 ms, mài 16.0 ms trung bình (p95 22.3 ms). Xem `BENCHMARKS.md` |
| Git | Nhánh `main`, các commit cục bộ chưa push |
| Chưa kiểm chứng | Cảm giác chơi (chuột), giao diện bản PC (đang là bố cục điện thoại kéo ra khung ngang), 1920x1080 (treo lúc khởi động), âm thanh nghe thật, GPU khác, mọi thứ trên điện thoại |
| Lưu ý lỗi thời | Chuỗi UI và hướng dẫn 13 bước trong mã là **tiếng Anh** (nhiều bước nói "Tap"), không phải tiếng Việt |

## 2. Dựng lại môi trường

| Việc | Lệnh / thao tác |
|---|---|
| Unity | Hub: Add > Add project from disk > thư mục repo. Mở từ Explorer hoặc Hub, không từ shell elevated (Unity sẽ chạy admin và treo) |
| Test Domain | `dotnet test build/ForgeBorn.Domain.Tests` |
| Test EditMode (Editor đang mở) | Qua Unity MCP: `run_tests` với `mode: EditMode`, rồi `get_test_job` |
| Test EditMode (Editor đóng) | `Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults build/out/editmode.xml` |
| Render headless | `Unity.exe -batchmode -projectPath . -executeMethod ForgeBorn.EditorTools.ForgeBornCapture.Run` (không `-nographics`). Ra `build/out/shots/*.png` và in "INPUT PATH OK" nếu đường chạm đúng. Ảnh là render GPU PC |

Trên PowerShell dùng `Start-Process -Wait -PassThru` để chờ Unity; gọi trực tiếp bằng `&` sẽ trả về ngay.

## 3. Unity MCP

Gói CoplayDev `com.coplaydev.unity-mcp` v10.0.0 (trong `Packages/manifest.json`; có bản v10.3.0, chưa cập nhật). Cần `uv` (có ở `~/.local/bin`).

Đăng ký phía Claude (một lần, cấu hình người dùng, không nằm trong repo). Bản bàn giao trước ghi "đã đăng ký" là **sai**: trước 2026-11-18 chưa có mục nào. Người dùng đã chạy lệnh sau và `~/.claude.json` giờ có `UnityMCP`:

```
claude mcp add --transport http --scope user UnityMCP http://127.0.0.1:8080/mcp
```

Công cụ `UnityMCP` chỉ hiện trong phiên Claude **mở sau** khi máy chủ Unity đang chạy (hoặc kết nối lại bằng `/mcp`). Nếu phiên mới vẫn không thấy, kiểm `claude mcp list` và xác nhận cổng 8080 đang nghe.

Bật mỗi lần: mở Editor có giao diện, **Window > MCP for Unity > Toggle MCP Window**, ở **Edit mode**, bấm **Start Server**, rồi **Start** ở hộp thoại. Đã kiểm 2026-11-18: đọc Console, chạy test, đổi build target và `execute_code` đều hoạt động.

Lưu ý: công cụ chỉ hiện trong Claude nếu phiên mở **sau** khi server chạy. Khi chưa hiện, gọi thẳng JSON-RPC tới `http://127.0.0.1:8080/mcp` (initialize, rồi `tools/call`). Đổi build target hoặc biên dịch lại làm Editor nạp lại assembly nên MCP mất kết nối tạm; chờ vài chục giây rồi gọi lại.

## 4. Đã có

- **Domain** (`Assets/_ForgeBorn/Scripts/Domain`, độc lập Unity): hình học lưỡi (`BladeProfile`, `BladeFactory`, `BladeRaycaster`, centerline và xoắn), lệnh mài và búa (`GrindSolver`, `HammerSolver`), nhiệt luyện (`ThermalSolver`), đánh giá (`BladeEvaluator`), đơn hàng (`Orders`), phiên có giao dịch và hoàn tác (`ForgeSession`), lưu FBWS schema 2 (SHA-256, ghi nguyên tử, khôi phục `.bak`).
- **Unity**: cảnh xưởng dựng bằng mã (HDRI, đe, lò, tường gạch, bloom, camera phối cảnh FOV 35), mesh lưỡi theo Domain, màu nhiệt/oxit/vết nứt, tia lửa, hơi nước khi tôi, tiếng búa thật, hướng dẫn 13 bước, nút Order/Deliver, báo cáo có lý do.
- **Công cụ**: `ForgeBornCapture` (render headless và chạm giả), `PlatformProfile` iOS/Android/Editor (đã cấu hình, chưa đo trên máy).

## 5. Giới hạn đã biết của mô hình

- Mô hình mài là mô hình gameplay đã hiệu chỉnh, không phải phương trình mài thật. Chưa có mòn bánh mài.
- Raycast bước 0.25 mm: chi tiết mỏng hơn có thể bị lỡ.
- Búa bảo toàn thể tích theo từng trạm (nở ngang, chưa kéo dài theo X); lưỡi cong/xoắn không đổi thể tích; tôi dùng nhiệt dung gộp theo trạm, chưa mô hình cong vênh.
- Phát lại lệnh cho kết quả giống hệt trên một máy; chênh lệch giữa các nền tảng chưa kiểm, phải dùng dung sai.
- Giới hạn khối lượng 1500 g / 1450 g của đơn 2 và 3 là số giả định chưa kiểm có đạt được bằng mài.

## 6. Bước tiếp theo

### P0.5 đã xong
- Thiết kế: `GAME_DESIGN.md` §13–§16 (UI ngang 16:9, art bible, danh sách âm thanh, bảng kinh tế).
- `tools/blender/forge_models.py`: chạy `"C:\Program Files\Blender Foundation\Blender 5.3\blender.exe" -b -P tools/blender/forge_models.py -- <thư mục xuất> [--preview]`. Xuất 5 FBX (đe, lò, bể tôi, bánh mài, chuôi), có tô mượt cho mặt cong, UV theo mét, và tự kiểm kích thước/số tam giác (`EXPECTED`; lệch thì script thoát lỗi). FBX nằm ở `Assets/_ForgeBorn/Resources/Art/Models` (để nạp lúc chạy). **Đang dùng trong cảnh:** đe, lò (`SandboxProps.Place`, thu nhỏ cho khớp khung nhìn 0,68 m) và chuôi (`SandboxProps.AttachHandle`, gắn vào lưỡi). **Chưa dùng:** bể tôi và bánh mài (chưa có chỗ trong bố cục HUD). Unity lật trục X khi nhập nên mô hình xoay 180° quanh Y.
- `PlayerAutomation.cs`: `-fb-screenshot <file> [-fb-wait 3]`, `-fb-benchmark <giây> [-fb-out file]` cho bản .exe. Biên dịch sạch, **chưa chạy**.
- `ForgeBornBuild.cs`: menu `ForgeBorn > Build Windows`, ra `build/out/win/ForgeBorn.exe` và `build/out/win_build.txt`. Mono (không có IL2CPP cho Windows).

### P0.5 đã kiểm chứng ngày 2026-11-18
- Build Windows thành công qua menu `ForgeBorn > Build Windows` (gọi qua MCP: `execute_code` với `EditorApplication.delayCall += () => EditorApplication.ExecuteMenuItem("ForgeBorn/Build Windows")`, rồi đọc `build/out/win_build.txt`). **Không** dùng `manage_build action=build`: job bị mất và Editor từng dừng. Lần đầu build lỗi vì `Handheld.Vibrate` không có trên Windows; đã bọc `#if UNITY_IOS || UNITY_ANDROID`.
- `-fb-screenshot` và `-fb-benchmark` chạy đúng. Ảnh chụp từ .exe (`build/out/shot_1280x720.png`, không nằm trong git) cho thấy giao diện điện thoại dọc bị kéo ra khung ngang: nút lớn che lưỡi, HUD ghi `[editor-sim-ios]`, toàn bộ chữ tiếng Anh. Đó là việc của P1.
- `-fb-benchmark` từng bị giới hạn 60 khung vì `ForgeSandbox.Initialize` đặt lại `targetFrameRate`; đã sửa trong `PlayerAutomation.cs`.

### P1 xong (2026-11-18)
Có và test EditMode xanh (44/44): `PointerSource.cs` (chuột/cảm ứng), `DesktopKeys.cs` (1–6, Tab, `[` `]`, `-` `=`, Esc; Ctrl+con lăn đổi áp lực; con lăn zoom; chuột phải kéo xoay), HUD ngang (`CanvasScaler` 1920x1080: cột trái, thanh dưới, bảng thông số), lưỡi nằm ngang, `SandboxProps` xếp lại cho khung ngang, hồ sơ `windows` (hết nhãn `[editor-sim-ios]` trên .exe), `HudWidgets.cs` + `MenuScreens.cs` (menu chính, tạm dừng, cài đặt) tách khỏi `ForgeSandbox`, `GameSettings.cs` (cỡ cửa sổ, toàn màn hình, tầng chất lượng, âm lượng, lưu `PlayerPrefs` khóa `fb.settings`; tầng chất lượng áp dụng sau khi khởi động lại), cửa sổ 1280x720 resize được, chạy nền. Test khói `SandboxBuildsAndMainMenuPausesAndResumes` dựng cả sandbox. `ForgeBornCapture` chụp 1920x1080 hoặc `-fb-size 1280x720` (ảnh chuẩn mới trong `docs/shots/`, 1280x720, Linear).
Đã xem bằng ảnh: 1920x1080 và 1280x720 (batchmode, GPU máy này), menu chính, cài đặt và workshop trong Play mode (Editor, game view 880x373).
**Chưa kiểm chứng:** phím/chuột/Fullscreen/Quit trên bản .exe thật; cảm giác chơi bằng chuột (cổng P5); `build/out/win/ForgeBorn.exe` hiện có được build TRƯỚC P1 nên chưa có HUD ngang và menu, phải build lại (`ForgeBorn > Build Windows`).
Việc để lại: chỉnh màu khối lò (mặt trước trùng màu tường, chỉ thấy mặt bên như thanh dọc; P4). Chuỗi UI vẫn tiếng Anh (P3).
Unity MCP: bật tùy chọn "Auto-Start on Editor Load" của gói (EditorPrefs `MCPForUnity.AutoStartOnLoad`), nên máy chủ tự chạy khi mở Editor, không cần bấm Start Server. Chụp màn hình trong Play mode: `manage_camera action=screenshot` (ra `Assets/Screenshots/`, xóa sau khi xem, đừng commit). Chụp batchmode vẫn cần Editor đóng. Phiên MCP có thể mất sau khi máy chủ khởi động lại ("Session not found"): mở phiên mới.

### P3 xong về mã và test (2026-11-18)
- **Career lưu được:** `Career.Serialize/Parse` (dòng `career1;money=N;best=id:G,...;up=...`, không ném lỗi, bỏ qua khóa lạ), ghi nguyên tử `career.txt` cạnh `weapon_slot1.fbws` sau mỗi `Deliver` và khi đóng cửa hàng; đọc ở `Initialize()`. Không lưu khi batch, `-fb-screenshot`/`-fb-benchmark` hoặc ngoài Play mode (cờ `_persistCareer`), để ảnh chụp và test không đụng hồ sơ người chơi.
- **Cửa hàng:** `Shop.cs` (Domain) + `ShopScreen.cs` (nút "Shop" ở cột trái, mở sau đơn giao đầu tiên, Esc đóng). Bốn nâng cấp: đá mài mịn 60, bể dầu 80, mặt búa nhọn 90, lò mạnh hơn 120 (`ThermalEnvironment.FastFurnace`, h 100 thay 60). Chưa mua thì đá mịn/mặt nhọn/Dầu bị khóa; giá chỉnh theo thu nhập đơn 1 (net 80 ở hạng B).
- **Giới hạn khối lượng đơn 2 và 3:** `BalanceTests` cho thấy cả hai phôi trong game (1.549 kg và 1.688 kg) nặng hơn 1.50/1.45 kg lúc đầu, và một lượt mài mặt đủ đưa cả hai xuống dưới giới hạn mà không phá lưỡi. Không cần chỉnh số.
- **Tiếng xì:** `SandboxAudio.SetHiss` (nhiễu qua lọc cao, theo tốc độ hơi nước); có test. Chưa nghe thử bằng loa thật; đây là âm tổng hợp, âm thật là việc của P4.
- **Tiếng Việt:** `Loc.cs` + `LocVi.cs` (khóa là chính câu tiếng Anh; thiếu bản dịch thì hiện tiếng Anh). Domain trả lý do chấm đơn dạng mẫu + tham số (`OrderResult.Parts`) để dịch mà giữ số. Cài đặt có nút ngôn ngữ (tự động theo hệ thống, đổi có hiệu lực sau khi khởi động lại). Test kiểm đủ bản dịch cho mọi bước hướng dẫn, đơn hàng, nâng cấp, mẫu lý do; font mặc định có đủ ký tự có dấu. Chữ hiển thị đã xem trong Play mode (ảnh Editor). Hướng dẫn đã viết lại theo chuột/phím ("Bấm", "Giữ chuột trái và kéo").
- **Chưa làm / chưa kiểm chứng:** bản .exe chưa build lại sau P3 (bản gần nhất `build/out/win` là sau P1); chuỗi trong `PlayerAutomation`, nhãn Editor và bản ghi log vẫn tiếng Anh; chỉ có bản dịch của tôi, chưa người bản ngữ rà (nhất là thuật ngữ rèn); mua nâng cấp và khóa/mở mới chạy qua test Domain và ảnh cửa hàng, chưa chơi thật bằng chuột.

### Còn dở
1. Bản .exe 1920x1080 từng treo lúc khởi động; bản build sau P1 chạy được ở cả 1280x720 và 1920x1080 (chụp ảnh, thoát 0), chưa rõ nguyên nhân cũ.
2. Phím, chuột, Toàn màn hình, Thoát chưa thử tay trên .exe; build lại .exe sau P3 rồi thử.

### Sau đó
P4 (bóng đổ, mô hình đe/lò, âm thanh thật, tầng chất lượng) hoặc, nếu bạn muốn, đi thẳng cổng P5: build lại .exe (`ForgeBorn/Build Windows` qua `execute_code` + `delayCall`) rồi bạn chơi thử. Đổi build target hoặc biên dịch lại làm Editor nạp lại assembly nên MCP mất kết nối tạm vài chục giây. File mới tạo ngoài Editor có thể không được nhập: gọi `AssetDatabase.ImportAsset(path, ForceUpdate)` rồi `Refresh` và kiểm tra kiểu đã có.

### Cạm bẫy đã gặp (tránh lặp lại)
- **Giấy phép Unity** có thể hết hạn giữa phiên: Editor tự thoát, log ghi "No ULF license found". Cần người dùng đăng nhập lại Unity Hub (tôi không nhập thông tin đăng nhập).
- Hook GateGuard bắt nêu người gọi, hàm bị ảnh hưởng, dữ liệu và lời người dùng trước lần ghi/sửa đầu tiên của mỗi file; nêu xong rồi thử lại cùng thao tác.
- Máy chỉ có Intel UHD: mọi số FPS là mốc thấp, không khẳng định cho GPU khác.

Việc cần bạn: P5 (chơi bản .exe), đăng nhập lại Unity Hub khi hết hạn, và các quyết định về phát hành/điện thoại.

### P4: tình trạng sau lượt review (2026-11-18)
- **Xong:** đe/lò/chuôi FBX trong cảnh; miệng lò sáng theo trạng thái (`SandboxProps.SetFurnaceGlow`, 0.2 khi không nung, 1 khi chọn Lò). Chưa xem hiệu ứng đổi sáng khi chơi thật (ảnh chụp batchmode không chạy `Update`).
- **Bóng đổ: chưa làm, và tiền đề trong kế hoạch sai.** Chỉ `ForgeBornBladeSteel.shader` có pass `ShadowCaster`; `ForgeBornProp.shader` không có pass đổ bóng và **không shader nào nhận bóng** (không lấy `_MainLightShadowCoord`). Bật bóng nghĩa là viết phần nhận bóng cho Prop shader + bật bóng ở URP asset, rồi đo lại FPS với mốc ở `BENCHMARKS.md` (mài 17.9 ms avg trên Intel UHD). Nên đo trước khi giữ lại.
- **Âm thanh CC0 (chưa nhập, chưa nghe):** workflow tìm và kiểm tra trang giấy phép bằng công cụ fetch (bản tóm tắt, không phải HTML thô). Mài: Freesound `shelbyshark/501170` "Bench Grinder Metal Bar" (41.8 s, CC0, cần cắt đoạn lặp), `PlumForestPodcast/519671` "Sharpening Blade - Long Blade" (14.5 s), `Kinoton/347446` "Angle grinder on/off grinding" (41.3 s). Tôi: BigSoundBank `quenching-cooling-1..5` (s1694-s1698, Joseph SARDIN, CC0, 1-3 s). Trước khi nhập: nghe thử (có lời/nhạc nền không), xác nhận lại giấy phép trên trang, rồi ghi vào `CREDITS.md`. Freesound thường cần đăng nhập để tải.
- **Chưa vào cảnh:** bể tôi và bánh mài (chưa có chỗ trong bố cục HUD). Tầng Low cho iGPU chưa đo.
- **Cổng P5:** `build/out/win/ForgeBorn.exe` đã build lại sau các sửa này (2026-11-18, 124 MB, 0 lỗi). Có thể chơi thử.


### Phản hồi chơi thử P5 (2026-11-18) và các sửa
- Phản hồi: chạy trọn vòng không lỗi; khó điều khiển (búa khó nhắm, mài khó giữ đường); camera không lia linh hoạt; muốn giao diện tiếng Việt.
- **Tiếng Việt mặc định:** `GameSettings.ResolveLanguage()` trả tiếng Việt khi `lang=-1` (trước đây chỉ khi hệ điều hành là tiếng Việt). Người đã chọn English (`lang=0`) vẫn giữ. Đã xem ảnh chụp từ `.exe`.
- **Camera:** chuột phải hoặc chuột giữa kéo lia (X và Y) ở mọi công cụ; phím mũi tên lia khi giữ; xoay lưỡi chỉ còn ở chế độ Xem (chuột trái). Đổi công cụ Mài/Búa vẫn reset góc nhìn.
- **Trợ giúp ngắm** (`AimAssist.cs`, chỉ hiển thị, không đổi mô hình mài/búa): vòng theo chuột, xanh khi trên lưỡi, đỏ khi ngoài; ở Mài có thêm đường vàng dọc mép +Y. **Chưa kiểm khi chơi thật**: cảm giác có đủ giúp hay không là việc của lượt chơi kế.

### Cải tiến cách chơi (2026-11-18, theo đề xuất sau phản hồi P5)
- **Vòng ngắm theo nhiệt độ (Búa):** cam = đủ nóng để rèn (>= `ForgeMinTempK`), xanh dương = nguội (đập có thể nứt), đỏ = trượt lưỡi. Mài: xanh = trên lưỡi.
- **Bảng "Mục tiêu"** (góc phải): kiểm tra trực tiếp theo đơn đang làm, cập nhật 2 lần/giây: mép, độ cứng, cong, xoắn, khối lượng (nếu đơn có giới hạn). Xanh = đạt (điểm thành phần >= 0.6, cùng ngưỡng với lý do khi giao), cam = chưa. Đã xem ảnh `09_order_goals_checklist`. Hiển thị góc mép hiện tại so với mục tiêu nằm ở dòng "Mép".
- **Hoàn tác Ctrl+Z:** chụp snapshot trước mỗi nhát búa và mỗi lần bấm chuột để mài, tối đa 20; đổi phôi/tải save thì xóa. **Chưa thử khi chơi thật** (không có test tự động vì nằm trong MonoBehaviour).
- **Chưa làm:** (3) khóa góc mài theo nấc: chưa nắm cách góc vát hình thành từ đường kéo nên không thêm mò; (6) chỉnh độ khó đơn 2 và 3: cần số liệu từ lượt chơi.

### Giai đoạn 0 đến 2 và 4a của kế hoạch cải tiến cách chơi (2026-11-18)
- **Nhật ký lượt chơi (giai đoạn 0):** mỗi lần giao hàng ghi một dòng vào `playlog.csv` cạnh file save (`Application.persistentDataPath`). Chỉ nằm trên máy, không gửi đi. Cột: đơn, đã giao, hạng, điểm, góc mép, HRC, cong, xoắn, khối lượng, nứt, lãi ròng, giây, số nhát búa, nhát trượt, nhát nguội, số lần hoàn tác, số lần mài (`PlayStats`, `PlayLog` trong `Domain/Evaluation/PlayStats.cs`). Chưa có dòng nào vì chưa chơi bản này.
- **Trợ giúp ngắm 3 nấc (giai đoạn 1):** Cài đặt, nút "Trợ giúp": Tắt / Nhẹ (mặc định) / Mạnh. Nhẹ: vòng ngắm có kích thước đúng vùng tiếp xúc của đá hoặc mặt búa, đường dẫn mài ở khoảng cách dưới mép theo từng loại đá, gợi ý. Mạnh: thêm hút con trỏ (trong 28 px) vào đường dẫn khi mài. Búa: bấm lên chỗ nguội thì lần đầu chỉ cảnh báo, bấm lại trong 1,5 giây mới đập; nhát không trúng thì không tốn bước hoàn tác.
- **Kết quả nghiên cứu mài: xem `GRIND_ANGLE_RESEARCH.md`.** Mô hình mài **không có góc điều khiển được**: góc mép nhảy loạn theo lệch 1 mm và theo lực ấn. Đường ray chỉ giữ tay trên một đường có cắt ra mép vát, **không bảo đảm góc 20°**. Cách chữa thật là thêm tham số góc mài vào `GrindSolver` (cần quyết định vì đổi mô hình).
- **Thông tin trước khi hành động (giai đoạn 2):** bảng bên phải gồm "Bước tiếp" (`NextStepAdvisor`), dòng tốc độ nguội khi tôi (`QuenchMonitor`: quá chậm / cứng một phần / cứng hoàn toàn / nguy cơ nứt), dự báo ram ở 250 °C (10 phút, 1 giờ, 4 giờ; `TemperForecast`, khớp với lò trong 3 HRC) và nhiệt độ rèn khi chọn Búa. Tắt khi trợ giúp = Tắt.
- **Phát hiện vật lý:** tôi dầu không làm thép này cứng (0 martensit, 22 HRC); chỉ nước cứng (62 HRC). Mài một mặt bị báo là "cong". Chi tiết ở `MODEL_FIDELITY.md`.
- **Nhãn bằng chứng (4a):** `MODEL_FIDELITY.md`. Chưa mô hình nào có nhãn "số liệu thật" vì chưa có nguồn.
- **Test:** Domain 96/96 (`GameplayAidsTests` mới, 15 test), Unity EditMode 59/59.

### Bản Android (2026-11-18)
- Lệnh: `Unity.exe -batchmode -nographics -projectPath . -executeMethod ForgeBorn.EditorTools.ForgeBornBuild.BuildAndroid` (Editor phải đóng) hoặc menu `ForgeBorn > Build Android`. Ra `build/out/android/ForgeBorn.apk`, báo cáo `build/out/android_build.txt`. IL2CPP, ARM64, màn hình ngang, ký bằng khóa debug (đủ để cài trực tiếp, không đủ để lên Google Play).
- Trên điện thoại: bật "Cài ứng dụng không rõ nguồn" cho trình quản lý file, chép file `.apk` sang máy rồi mở nó; hoặc `adb install -r ForgeBorn.apk`.
- Đã sửa cho cảm ứng: bỏ ép màn hình dọc (giao diện thiết kế ngang), thêm nút "Hoàn tác" (không có Ctrl+Z trên cảm ứng).
- **Chưa kiểm trên máy thật** (đây là lần đầu có APK). Chưa có: lia camera bằng cảm ứng (lia chỉ có chuột phải/giữa và phím mũi tên), bóng, âm thanh CC0; hiệu năng chưa đo trên Android; vòng ngắm ẩn khi cảm ứng.

### Mài theo góc đặt (2026-11-18)
- Mô hình: xem `GRIND_ANGLE_RESEARCH.md` (mục "Đã sửa") và `MODEL_FIDELITY.md`. Gần mép, `GrindSolver` cắt về mặt phẳng nghiêng theo góc đặt; góc cuối không phụ thuộc tay, lực hay loại đá.
- Điều khiển: nút "Góc 20°" (cột trái) hoặc Q/E đổi nấc 10° đến 40°; nút "Lật" hoặc F lật lưỡi để mài mặt kia (cả hai mặt mới đủ góc; một mặt ra khoảng nửa). Mở đơn hàng tự chọn nấc gần nhất. Dòng "Bước tiếp" nhắc lật khi mới mài một mặt.
- Chấm điểm đổi: góc mép đo ở 1 và 3 mm sau đầu mép; độ cong đo theo tâm khung bao. Có thể làm điểm các blade cũ khác đi chút.
- Test: Domain 107/107 (`GrindFacetTests` mới, 12 test), Unity EditMode 59/59, capture "INPUT PATH OK" gồm kiểm góc 20° qua đường chạm thật (hội tụ 20,0° sau 4 vòng).
- **Chưa kiểm khi chơi thật:** cảm giác số nhát (đá vừa 80 N chậm cho phôi dày; đá thô 150 N nhanh), vị trí nút "Góc" và "Lật" trên điện thoại.
