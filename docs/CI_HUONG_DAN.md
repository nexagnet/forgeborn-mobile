# CI và review tự động (1 trang)

## Luồng một PR
1. Làm trên nhánh `claude/<việc>`, mở PR vào `main`. PR **Draft** không bị review AI.
2. CI `ci` tự chạy, gồm hai job:
   - `domain-tests`: `dotnet test build/ForgeBorn.Domain.Tests` (107 test, không cần Unity).
   - `meta-check`: mọi file/thư mục dưới `Assets/` có `.meta`, không có `.meta` mồ côi (`.meta` của thư mục rỗng chỉ cảnh báo).
3. Khi `ci` xanh và PR ở trạng thái Ready, Reviewer AI (caller: `.github/workflows/nexagnet-claude-reviewer.yml`, cần secret `CLAUDE_CODE_OAUTH_TOKEN` của repo) (chỉ đọc) tự chạy và comment `NEXAGNET_REVIEWER PASS` hoặc `REQUEST_CHANGES` kèm HEAD và run CI. Commit mới thì kết quả cũ không còn dùng được.
4. Chủ dự án merge. Bot tự merge chưa bật (cần GitHub App merge và ruleset do chủ tạo).

## Chạy tại máy
```
dotnet test build/ForgeBorn.Domain.Tests
tools/ci/meta-check.sh
tools/ci/meta-check.test.sh
```

## CI KHÔNG chứng minh
EditMode (62 test), build Windows và chơi thật không nằm trong CI. PR đổi gameplay hoặc giao diện phải ghi "đã/chưa kiểm chứng trên máy". CI xanh + Reviewer PASS không có nghĩa game chơi đúng.

## PR chạm đường dẫn nhạy cảm
`.github/**`, `CLAUDE.md`, `.claude/**`, `.mcp.json`, `.env*`, `infra/`, `deploy/`: Reviewer từ chối tự động; chủ xem và merge tay.

## Reviewer bị lỗi/đỏ thì sao
Xem tab Actions, workflow "Nexagnet Reviewer caller": dòng `REVIEWER_GATE_BLOCKED: <lý do>` nói rõ thiếu gì (CI chưa xanh, job thiếu, PR Draft thì bỏ qua êm, tác giả chưa nằm trong danh sách tin cậy...).

## Bot tự merge
Sau khi `ci` xanh và Reviewer PASS đúng HEAD, bot (`nexagnet-claude-autonomy.yml`) tự squash merge PR Ready. Bot không merge khi: PR là Draft, có nhãn `needs-human`, chạm `.github/**` hay đường dẫn nhạy cảm của dự án (`ProjectSettings/**`, `Packages/manifest.json`, `Packages/packages-lock.json`), hoặc HEAD đổi sau lần review. Khi đó PR chỉ nhận comment `READY_FOR_MANUAL_MERGE` hoặc `AUTO_MERGE_STOPPED`; chủ xem và merge tay.

Ghi chú thử bot (ca Draft/needs-human/HEAD cũ); PR sẽ đóng, không merge.
