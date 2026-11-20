# FORGEBORN — hướng dẫn cho Claude

Game Unity 6.3 LTS (Windows trước). Tiếng Việt trong chat/PR/tài liệu; tên file, lệnh, API giữ nguyên.

## Lệnh đã xác minh
- Test Domain (không cần Unity): `dotnet test build/ForgeBorn.Domain.Tests` — 107 test, .NET SDK 8, ~40 giây (đã chạy 20/11/2026 trên Ubuntu).
- Kiểm `.meta`: `tools/ci/meta-check.sh`; test của nó: `tools/ci/meta-check.test.sh`.
- EditMode và build Windows cần Unity + giấy phép, **không** chạy trong CI; lệnh ở `docs/HANDOVER.md`.

## Quy ước
- Làm trên nhánh `claude/<tên-việc>`, mở PR vào `main`, không push thẳng `main`. CI `ci` (job `domain-tests`, `meta-check`) phải xanh.
- Mỗi asset/thư mục dưới `Assets/` phải đi kèm `.meta` trong cùng commit.
- Mã Domain (`Assets/_ForgeBorn/Scripts/Domain`) không phụ thuộc Unity; thêm test trong `build/ForgeBorn.Domain.Tests`.
- Thay đổi gameplay/giao diện: ghi rõ "đã/chưa kiểm chứng trên máy" trong PR. CI xanh không chứng minh game chơi đúng.
- PR chạm `.github/**`, `CLAUDE.md`, `ProjectSettings/**`, `Packages/manifest.json`: chủ dự án duyệt và merge, không tự merge.
- Không đưa token, mật khẩu, dữ liệu nhạy cảm vào repo.

Bắt đầu đọc: `docs/HANDOVER.md`, `docs/CI_HUONG_DAN.md`.
