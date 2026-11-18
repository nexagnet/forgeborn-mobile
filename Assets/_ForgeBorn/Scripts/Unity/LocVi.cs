using System.Collections.Generic;

namespace ForgeBorn.UnityLayer
{
    /// <summary>
    /// Vietnamese table for Loc. Keys are the exact English source strings used in code (templates keep their {0} placeholders).
    /// Smith terms: tôi = quench (dipping hot steel in liquid), ram = temper, mài = grind, rèn = forge, phôi = blank, lưỡi = blade.
    /// LocTests checks that every tutorial step, order, upgrade and order-result reason has an entry.
    /// </summary>
    internal static class LocVi
    {
        public static Dictionary<string, string> Build()
        {
            var d = new Dictionary<string, string>();
            void A(string en, string vi) { d[en] = vi; }

            // ---- workshop buttons and labels
            A("GRIND", "MÀI"); A("HAMMER", "BÚA"); A("INSPECT", "XEM");
            A("Wheel", "Đá mài");
            A("Air", "Không khí"); A("Furnace", "Lò rèn"); A("Water", "Nước"); A("Oil", "Dầu"); A("Oven", "Lò ram");
            A("Zoom", "Phóng"); A("Shop", "Cửa hàng");
            A("Press -", "Lực -"); A("Press +", "Lực +");
            A("Order", "Đơn"); A("Deliver", "Giao"); A("Report", "Báo cáo"); A("Guide", "Hướng dẫn");
            A("Save", "Lưu"); A("Load", "Tải"); A("Next", "Tiếp"); A("Skip", "Bỏ qua");
            A("coarse", "thô"); A("medium", "vừa"); A("fine", "mịn"); A("flat", "phẳng"); A("peen", "nhọn");
            A("Anvil", "Đe"); A("Grinder", "Máy mài"); A("Quench", "Tôi"); A("Temper", "Ram"); A("Inspect", "Xem");

            // ---- readout
            A("Mass", "Khối lượng"); A("removed", "đã mài"); A("Temp", "Nhiệt"); A("CRACKED", "NỨT");
            A("Energy", "Năng lượng"); A("Pressure", "Áp lực"); A("Free play", "Chơi tự do");
            A("Order:", "Đơn:"); A("max", "tối đa"); A("Money", "Tiền");

            // ---- status line
            A("station {0}: {1}", "trạm {0}: {1}");
            A("grind {0} ms", "mài {0} ms"); A("no contact", "không chạm");
            A("rejected: {0}", "bị từ chối: {0}"); A("missed the blade", "trượt lưỡi");
            A("hammer: no deformation ({0})", "búa: không biến dạng ({0})");
            A("COLD hammering at {0}: dent {1} mm, crack risk +{2}", "BÚA NGUỘI ở {0}: móp {1} mm, nguy cơ nứt +{2}");
            A("hammer at {0}: dent {1} mm, {2} ms", "búa ở {0}: móp {1} mm, {2} ms");
            A("thermal rejected: {0}", "nhiệt bị từ chối: {0}");
            A("CRACKED during {0}!", "NỨT khi {0}!");
            A("environment: {0}", "môi trường: {0}");
            A("Oil bath is locked: buy it in the Shop (after your first delivery).", "Bể dầu đang khóa: mua ở Cửa hàng (sau đơn giao đầu tiên).");
            A("The shop opens after your first delivered order.", "Cửa hàng mở sau đơn giao đầu tiên.");
            A("Order: {0}. {1}", "Đơn: {0}. {1}");
            A("ORDER {0} for {1}\n{2}\nPays up to {3}, blank costs {4}. Needs score {5}+.", "ĐƠN {0} cho {1}\n{2}\nTrả tối đa {3}, phôi giá {4}. Cần từ {5} điểm.");
            A("Pick an order first (Order button).", "Hãy chọn đơn trước (nút Đơn).");
            A("Already delivered. Press Order for the next one.", "Đã giao rồi. Bấm Đơn để lấy đơn tiếp.");
            A("DELIVERED  grade {0}  paid {1}  (net {2})", "ĐÃ GIAO  hạng {0}  nhận {1}  (thu chi {2})");
            A("NOT ACCEPTED  net {0}", "KHÔNG NHẬN  thu chi {0}");
            A("Delivered: grade {0}", "Đã giao: hạng {0}"); A("Rejected", "Bị từ chối");
            A("new tapered blank", "phôi thon mới"); A("new beveled bar", "thanh vát mới");
            A("saved", "đã lưu"); A("save failed: {0}", "lưu lỗi: {0}"); A("loaded", "đã tải"); A("load failed: {0}", "tải lỗi: {0}");

            // ---- shop
            A("SHOP", "CỬA HÀNG"); A("SHOP    Money {0}", "CỬA HÀNG    Tiền {0}"); A("Close", "Đóng"); A("{0}  (owned)", "{0}  (đã có)");
            A("Bought: {0}", "Đã mua: {0}"); A("Not enough money for {0}", "Không đủ tiền mua {0}");
            A("You already own {0}", "Bạn đã có {0}"); A("Deliver an order first to open the shop", "Hãy giao một đơn trước để mở cửa hàng");
            A("Fine grinding wheel", "Đá mài mịn"); A("Oil quench bath", "Bể tôi dầu");
            A("Peen hammer face", "Mặt búa nhọn"); A("Hotter furnace", "Lò rèn mạnh hơn");
            A("Removes little metal per pass: precise finishing.", "Mỗi lượt mài lấy ít kim loại: hoàn thiện chính xác.");
            A("Cools slower than water: less risk of cracking.", "Nguội chậm hơn nước: ít nguy cơ nứt.");
            A("Narrow hammer face that stretches metal along the blade.", "Mặt búa hẹp, kéo dài kim loại dọc theo lưỡi.");
            A("Heats the blade faster.", "Nung lưỡi nhanh hơn.");

            // ---- menus
            A("Continue", "Tiếp tục"); A("New game", "Chơi mới"); A("Settings", "Cài đặt"); A("Quit", "Thoát");
            A("PAUSED", "TẠM DỪNG"); A("Resume", "Chơi tiếp"); A("Main menu", "Menu chính"); A("SETTINGS", "CÀI ĐẶT");
            A("Window size: {0} x {1}", "Cỡ cửa sổ: {0} x {1}"); A("Fullscreen: {0}", "Toàn màn hình: {0}");
            A("On", "Bật"); A("Off", "Tắt"); A("Quality: {0}", "Chất lượng: {0}");
            A("Auto", "Tự động"); A("Low", "Thấp"); A("Medium", "Trung bình"); A("High", "Cao");
            A("Volume -", "Âm lượng -"); A("Volume +", "Âm lượng +"); A("Back", "Quay lại");
            A("Language: {0}", "Ngôn ngữ: {0}");
            A("Quality and language take effect after restart.", "Chất lượng và ngôn ngữ có hiệu lực sau khi khởi động lại.");

            // ---- report panel
            A("Grade {0}   Score {1}   {2}", "Hạng {0}   Điểm {1}   {2}"); A("PASS", "ĐẠT"); A("not passed", "chưa đạt");
            A("BROKEN: blade is cut through", "GÃY: lưỡi bị cắt đứt"); A("CRACKED at {0} stations", "NỨT ở {0} trạm");
            A("Mass {0} kg   Length {1} mm", "Khối lượng {0} kg   Dài {1} mm");
            A("Balance {0}% from tang  (score {1})", "Cân bằng {0}% tính từ đuôi  (điểm {1})");
            A("Straight {0} mm dev  ({1})", "Độ thẳng: lệch {0} mm  ({1})");
            A("Twist {0} deg  ({1})", "Xoắn {0} độ  ({1})");
            A("Symmetry {0}", "Đối xứng {0}");
            A("Edge {0} deg, {1} mm at 0.5 mm  ({2})", "Lưỡi {0} độ, dày {1} mm ở 0.5 mm  ({2})");
            A("Edge: no bevel ground yet", "Lưỡi: chưa mài vát");
            A("Hardness {0} HRC  ({1})", "Độ cứng {0} HRC  ({1})");
            A("Toughness {0}", "Độ dai {0}");

            // ---- order result reasons (Domain templates)
            A("The blade is cut through: lost.", "Lưỡi bị cắt đứt: hỏng.");
            A("The blade cracked ({0} stations): lost. Quench gentler or temper sooner.", "Lưỡi bị nứt ({0} trạm): hỏng. Lần sau tôi nhẹ hơn hoặc ram sớm hơn.");
            A("Too heavy: {0} g, the client wants at most {1} g.", "Quá nặng: {0} g, khách chỉ muốn tối đa {1} g.");
            A("Score {0} is below the {1} this client requires.", "Điểm {0} thấp hơn mức {1} mà khách này yêu cầu.");
            A("No edge ground: grind a bevel on the +Y side.", "Chưa mài lưỡi: hãy mài vát ở cạnh +Y.");
            A("Edge is {0} deg and {1} mm thick 0.5 mm behind the tip; the client wants {2} deg and thin.", "Lưỡi {0} độ và dày {1} mm ở cách mũi 0.5 mm; khách muốn {2} độ và mỏng.");
            A("Hardness is {0} HRC, the client wants about {1}.", "Độ cứng {0} HRC, khách muốn khoảng {1}.");
            A("Bent by {0} mm along its length.", "Cong {0} mm dọc theo lưỡi.");
            A("Twisted by {0} deg.", "Xoắn {0} độ.");
            A("Uneven faces: one side is thicker.", "Hai mặt không đều: một bên dày hơn.");
            A("Too brittle: temper it to gain toughness.", "Quá giòn: hãy ram để tăng độ dai.");
            A("Balance point is {0}% from the tang, the client wants about {1}%.", "Điểm cân bằng cách đuôi {0}%, khách muốn khoảng {1}%.");

            // ---- orders
            A("Village guard", "Lính gác làng"); A("Plain utility blade", "Dao dùng việc thường");
            A("Any honest edge will do. Keep it straight and do not crack it.", "Lưỡi nào chắc tay cũng được. Giữ cho thẳng và đừng làm nứt.");
            A("Traveling merchant", "Thương nhân lữ hành"); A("Sharp working knife", "Dao làm việc sắc");
            A("Needs a keen, hard edge. Harden it properly, then temper so it does not chip.", "Cần lưỡi sắc và cứng. Hãy tôi đúng cách, rồi ram để lưỡi không bị mẻ.");
            A("Master swordsman", "Kiếm sư"); A("Duelling blade", "Kiếm đấu");
            A("Thin keen edge, near-perfect straightness, light in the hand.", "Lưỡi mỏng và sắc, thẳng gần như hoàn hảo, nhẹ tay.");

            // ---- tutorial
            A("Welcome to the forge", "Chào mừng đến xưởng rèn");
            A("You will turn a plain steel bar into a blade. Every step changes the real metal: heat, shape, hardness.", "Bạn sẽ biến một thanh thép trơn thành lưỡi dao. Mỗi bước đều thay đổi kim loại thật: nhiệt, hình dạng, độ cứng.");
            A("Click Next. You can Skip this guide at any time.", "Bấm Tiếp. Bạn có thể Bỏ qua hướng dẫn bất cứ lúc nào.");
            A("1. Light the furnace", "1. Nhóm lò");
            A("Cold steel is hard to shape and cracks when hammered. Hot steel flows like soft clay.", "Thép nguội khó tạo hình và nứt khi bị đập. Thép nóng mềm như đất sét.");
            A("Click Furnace to put the blade in the fire.", "Bấm Lò rèn để cho lưỡi vào lửa.");
            A("2. Speed up time", "2. Tua nhanh thời gian");
            A("Heating takes minutes in real life. Time scale lets you wait without waiting.", "Ngoài đời nung mất vài phút. Tốc độ thời gian giúp bạn khỏi phải chờ.");
            A("Click the x1 button until it reads x60.", "Bấm nút x1 cho đến khi hiện x60.");
            A("3. Heat to bright orange", "3. Nung đến màu cam sáng");
            A("Above about 830 C (1100 K) steel is soft enough to forge. Colour tells you the temperature: dull red, orange, yellow.", "Trên khoảng 830 C (1100 K) thép đủ mềm để rèn. Màu cho biết nhiệt độ: đỏ sẫm, cam, vàng.");
            A("Wait until the blade glows orange. Do not overheat: past 1500 K steel is ruined.", "Chờ đến khi lưỡi sáng màu cam. Đừng nung quá: trên 1500 K thép bị hỏng.");
            A("4. Pick up the hammer", "4. Cầm búa lên");
            A("The hammer pushes metal sideways: the blade gets wider where you strike, without losing any mass.", "Búa đẩy kim loại sang hai bên: lưỡi rộng ra ở chỗ bạn đập, không mất chút khối lượng nào.");
            A("Click the tool button (top left) until it reads HAMMER.", "Bấm nút dụng cụ (góc trên trái) cho đến khi hiện BÚA.");
            A("5. Strike while it is hot", "5. Đập khi còn nóng");
            A("Hammer only while the steel glows. Below 777 C (1050 K) it is too cold: it dents and cracks.", "Chỉ đập khi thép còn sáng. Dưới 777 C (1050 K) là quá nguội: thép móp và nứt.");
            A("Click the blade 3 times. If it goes dark, return to the Furnace first.", "Bấm vào lưỡi 3 lần. Nếu nó tối đi, hãy quay lại Lò rèn trước.");
            A("Too cold to forge! Put it back in the Furnace.", "Quá nguội để rèn! Cho lại vào Lò rèn.");
            A("6. Switch to the grinder", "6. Chuyển sang máy mài");
            A("Grinding removes metal for good. It is how you cut the edge bevel and the final shape.", "Mài lấy đi kim loại vĩnh viễn. Đây là cách tạo mặt vát của lưỡi và hình dạng cuối.");
            A("Click the tool button until it reads GRIND.", "Bấm nút dụng cụ cho đến khi hiện MÀI.");
            A("7. Grind an edge", "7. Mài lưỡi");
            A("Mass leaves with every pass; more pressure cuts faster but heats and roughens the edge. Watch the removed grams.", "Mỗi lượt mài đều lấy đi khối lượng; ép mạnh thì cắt nhanh hơn nhưng nóng và làm nhám lưỡi. Hãy theo dõi số gam đã mài.");
            A("Hold the left mouse button and drag along the blade edge to remove at least 3 g.", "Giữ chuột trái và kéo dọc theo cạnh lưỡi để mài đi ít nhất 3 g.");
            A("8. Heat again for hardening", "8. Nung lại để tôi cứng");
            A("To harden steel you first heat it bright orange, so its structure changes (austenite). Hot steel that is cooled fast becomes hard.", "Để tôi cứng thép, trước hết nung cam sáng để cấu trúc đổi (austenit). Thép nóng làm nguội nhanh sẽ cứng.");
            A("Click Furnace and wait until the blade glows orange again.", "Bấm Lò rèn và chờ cho lưỡi sáng màu cam trở lại.");
            A("9. Quench in water", "9. Tôi trong nước");
            A("Fast cooling locks the steel into hard martensite (HRC goes up to about 60). Too fast on a thick blade and it cracks.", "Làm nguội nhanh khóa thép thành martensit cứng (HRC lên khoảng 60). Quá nhanh trên lưỡi dày thì nứt.");
            A("Click Water and let the blade cool until HRC reads 45 or more.", "Bấm Nước và để lưỡi nguội cho đến khi HRC từ 45 trở lên.");
            A("The blade cracked. Press Order for a new blade, or try Oil next time.", "Lưỡi bị nứt. Bấm Đơn để lấy phôi mới, hoặc lần sau thử Dầu.");
            A("10. Temper in the oven", "10. Ram trong lò");
            A("Fresh martensite is brittle like glass. Gentle reheating trades a little hardness for toughness; the surface colour shows how far you went.", "Martensit mới tôi giòn như thủy tinh. Nung lại nhẹ đổi bớt độ cứng lấy độ dai; màu bề mặt cho biết bạn đã ram đến đâu.");
            A("Click Oven and raise the time scale to x600 until the blade turns straw yellow.", "Bấm Lò ram và tăng tốc thời gian lên x600 cho đến khi lưỡi chuyển vàng rơm.");
            A("11. Read the report", "11. Đọc báo cáo");
            A("The report scores edge angle, straightness, twist, hardness, toughness and balance, and tells you why.", "Báo cáo chấm góc lưỡi, độ thẳng, độ xoắn, độ cứng, độ dai và cân bằng, và cho biết lý do.");
            A("Click Report.", "Bấm Báo cáo.");
            A("You made a blade", "Bạn đã làm xong một lưỡi dao");
            A("That is the whole loop: heat, hammer, grind, harden, temper, judge.", "Đó là cả chu trình: nung, rèn, mài, tôi, ram, chấm.");
            A("Click Next to play freely, or press Order to get a new blade.", "Bấm Tiếp để chơi tự do, hoặc bấm Đơn để lấy phôi mới.");
            return d;
        }
    }
}
