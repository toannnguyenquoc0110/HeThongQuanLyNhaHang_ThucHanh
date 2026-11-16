# Kiểm tra bản demo — Vị Nhà

## Đã cập nhật trong source
- Hoàn thiện giao diện đăng ký/đăng nhập khách hàng theo thiết kế responsive; trường nhập có nhãn, hướng dẫn, báo lỗi và nút hiện/ẩn mật khẩu.
- Kiểm tra dữ liệu đăng ký bằng DataAnnotations phía server, chuẩn hóa email, xử lý email trùng và xác thực mật khẩu hash; form dùng antiforgery token.
- Bố cục khách hàng có điều hướng tới thực đơn, giỏ hàng, lịch sử đơn, tài khoản và đăng xuất.
- Code First: lần chạy đầu tạo schema từ `AppDbContext` bằng `EnsureCreatedAsync`, sau đó seed dữ liệu nếu chưa có chi nhánh.
- Seed dữ liệu 4 chi nhánh, nhân sự, thực đơn/công thức, tồn kho đầu kỳ và giao dịch tồn đầu kỳ.
- Tài khoản khách hàng demo dùng `PasswordHasher`, không lưu mật khẩu thuần.
- Khách hàng có trang thực đơn, combo, giỏ hàng, checkout, hồ sơ và lịch sử/chi tiết đơn theo quyền sở hữu.
- Giá vốn món/combo tính từ công thức và giá nguyên liệu; không còn trường nhập giá vốn tại form sửa món.
- Đơn online được kiểm tra tồn kho tổng hợp, khi tiếp nhận sẽ xuất kho theo công thức trong transaction; có khóa `OperationKey` duy nhất.
- Luồng bếp cập nhật trạng thái giao hàng/tự lấy; có thao tác ghi nhận thu tiền COD sau khi đơn hoàn tất.
- Bổ sung bảng tổng hợp tiêu thụ tự động và hao hụt đã ghi nhận trong trang kho.
- Cookie khách hàng được tách khỏi hệ thống nội bộ; phiên khách hàng hợp lệ bị chuyển khỏi controller quản trị.

## Chưa xác nhận được
- Không có .NET SDK trong môi trường tạo ZIP, nên chưa chạy được `dotnet restore`, `dotnet build`, `dotnet test` hoặc chạy website thực tế.
- Chưa tích hợp cổng thanh toán/QR thật, webhook ngân hàng, SMS/email, dịch vụ vận chuyển.
- Vai trò nhân viên hiện vẫn mô phỏng bằng cookie `role`/`branch` từ source gốc; cần tài khoản nhân viên và policy authorization thực trước khi triển khai production.
- `EnsureCreated` phù hợp database demo mới, không nâng cấp schema database cũ. Nếu sau này đổi model khi đã có DB, cần sinh EF Core migrations và thử trên bản sao trước.
- Cần kiểm thử trên Visual Studio/Windows thực tế, đặc biệt các luồng đặt đồng thời, nhận/hủy đơn và giao dịch kho.

## Tài khoản khách hàng demo
- Email: `khach@demo.vn`
- Mật khẩu: `Demo@12345`

Ứng dụng hiện seed tài khoản demo độc lập với dữ liệu chi nhánh, nên sẽ bổ sung tài khoản nếu chưa có mà không xóa dữ liệu hiện tại. Nếu đang dùng source cũ, hãy sao lưu database trước khi cập nhật.


## Tài khoản đăng nhập demo

- Quản lý: `admin@demo.vn`
- Phục vụ: `phucvu@demo.vn`
- Bếp: `bep@demo.vn`
- Mật khẩu chung: `Demo@12345`
- Đăng nhập nhân viên: `/nhan-vien/dang-nhap`
- Đăng ký/đăng nhập khách hàng: `/khach-hang/dang-ky` và `/khach-hang/dang-nhap`

Lần khởi động, ứng dụng sẽ tạo các bảng còn thiếu theo cách bổ sung an toàn cho SQLite hiện có và thêm tài khoản demo nếu chưa có. Không xóa `vinha.db`. Hãy sao lưu database trước khi cập nhật source. Nếu database được mở từ đường dẫn tương đối, kiểm tra thư mục làm việc của ứng dụng để chắc chắn đang dùng đúng file.
