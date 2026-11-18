# FORGEBORN

Game mô phỏng rèn vũ khí (Unity 6.3 LTS). Hiện làm **bản Windows trước**, điện thoại sau.

## Đọc theo thứ tự

1. [`docs/HANDOVER.md`](docs/HANDOVER.md): game đang ở đâu, cách dựng môi trường, bước tiếp theo.
2. [`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md): lộ trình duy nhất (P0 đến P6).
3. [`docs/GAME_DESIGN.md`](docs/GAME_DESIGN.md): thiết kế game.

Tham khảo khi cần: `docs/TECHNICAL_DESIGN.md` (thiết kế gốc, hướng di động), `docs/DEVICE_MATRIX.md` (bằng chứng đã kiểm), `docs/KNOWN_ISSUES.md` (lỗi và hạn chế), `docs/BENCHMARKS.md`, `docs/CREDITS.md`, `docs/adr/`.

## Chạy test không cần Unity

```
dotnet test build/ForgeBorn.Domain.Tests
```
