# Database của Vị Nhà

Hệ thống chạy được trên hai loại database, chọn ở `appsettings.json`:

| Provider | Khi nào dùng | Cấu hình |
|---|---|---|
| `Sqlite` (mặc định) | Chạy thử, một máy, không cần cài gì | file `vinha.db` |
| `PostgreSql` | Dùng thật cho 4 chi nhánh, nhiều máy cùng truy cập | `docker compose up -d` rồi đổi provider |

## Chạy PostgreSQL
1. `docker compose up -d` (có thêm Adminer ở http://localhost:8081 để xem bảng).
2. Trong `appsettings.json` đổi `"Provider": "Sqlite"` thành `"PostgreSql"`
   (hoặc `set Database__Provider=PostgreSql` trước khi `dotnet run`).
3. `dotnet run`. Lần đầu app tự tạo 29 bảng, chỉ mục và nạp dữ liệu mẫu.

Đổi mật khẩu trong `docker-compose.yml` và `ConnectionStrings:PostgreSql` trước khi đưa vào dùng thật.

## Thiết kế
- Khóa ngoại đầy đủ giữa các bảng; xóa bàn/món không làm mất lịch sử đơn (`Restrict` / `SetNull`).
- Tiền và số lượng kho: `numeric(18,3)`.
- Chỉ mục cho truy vấn nóng: đơn theo chi nhánh/trạng thái/ngày đóng, màn hình bếp, đặt bàn theo ngày, nhật ký, chấm công.
- Giá bán và giá vốn được chốt vào dòng đơn tại lúc gọi nên báo cáo không đổi khi sửa thực đơn.
- `GET /health` trả 200 khi kết nối database được (dùng cho giám sát).

## Sao lưu
- SQLite: vào mục **Dữ liệu** trên web, bấm "Tải bản sao lưu".
- PostgreSQL: `docker exec vinha-db pg_dump -U vinha vinha > vinha-$(date +%F).sql`
  khôi phục: `docker exec -i vinha-db psql -U vinha vinha < vinha-2026-10-06.sql`

## Làm lại từ đầu
- SQLite: tắt web, xóa `vinha.db`, chạy lại.
- PostgreSQL: `docker compose down -v` rồi `docker compose up -d`.
