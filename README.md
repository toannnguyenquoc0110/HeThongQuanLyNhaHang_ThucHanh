# Vị Nhà — Hệ thống quản lý nhà hàng + đặt món online

Ứng dụng ASP.NET Core MVC, C# và Entity Framework Core (.NET 8), mặc định dùng SQLite với Code First. Dữ liệu mẫu được nạp từ `Data/SeedData.cs` khi database mới chưa có chi nhánh.

## Chạy lần đầu bằng Visual Studio 2022

1. Cài workload **ASP.NET and web development** và .NET 8 SDK.
2. Mở `QuanLyNhaHang.sln`.
3. Để `Database:Provider` là `Sqlite` trong `QuanLyNhaHang/appsettings.json`.
4. Nhấn F5 hoặc chạy trong thư mục dự án:

   ```bash
   dotnet restore
   dotnet run
   ```

5. Ứng dụng dùng EF Core Code First `EnsureCreated` để tạo schema ban đầu từ các model, sau đó gọi `SeedData` nếu chưa có chi nhánh. File SQLite `vinha.db` được tạo trong working directory của ứng dụng; không cần database bên ngoài để demo lần đầu.
6. Địa chỉ phát triển được cấu hình trong `Properties/launchSettings.json`, hiện là `http://localhost:5080`.

**Lưu ý về schema:** `EnsureCreated` dành cho database mới; nó không nâng cấp các bảng đã tồn tại khi model đổi. Khi phát triển tiếp sau bản demo, hãy sinh và áp dụng EF Core migrations (`dotnet ef migrations add ...`, `dotnet ef database update`) trên bản sao dữ liệu trước. Không xóa `vinha.db` để sửa lỗi nếu cần giữ dữ liệu.

## Tài khoản demo khách hàng

- Trang đặt món: `/khach-hang`
- Đăng ký: `/khach-hang/dang-ky`
- Đăng nhập: `/khach-hang/dang-nhap`
- Tài khoản seed: `khachhang@vinhanh.vn`
- Mật khẩu seed: `Demo@12345`

Mật khẩu chỉ lưu dạng hash thông qua `PasswordHasher<CustomerAccount>`. Có thể đăng ký tài khoản riêng để trình bày luồng đặt hàng. Tài khoản demo chỉ được seed khi database khởi tạo mới và không được seed đè lên database có chi nhánh.

## Đăng ký / đăng nhập khách hàng

- Giao diện đăng ký: `/khach-hang/dang-ky`; yêu cầu họ tên, email hợp lệ và mật khẩu tối thiểu 8 ký tự. Số điện thoại và địa chỉ mặc định là tùy chọn.
- Giao diện đăng nhập: `/khach-hang/dang-nhap`; email được chuẩn hóa chữ thường trước khi tra cứu.
- Mật khẩu lưu bằng `PasswordHasher<CustomerAccount>`; không lưu mật khẩu gốc. Email có unique index trong EF Core, và xử lý trường hợp hai lượt đăng ký cùng email.
- Form POST có antiforgery token, validation phía server, thông báo lỗi theo trường và nút hiện/ẩn mật khẩu.
- Cookie khách hàng dùng scheme riêng `CustomerCookie`, độc lập với cookie nội bộ. Đăng xuất dùng POST và antiforgery token.
- Sau khi tạo tài khoản/đăng nhập, người dùng được chuyển về trang thực đơn; có thể mở **Tài khoản** để cập nhật hồ sơ và **Đơn hàng** để xem lịch sử.

## Luồng demo đề xuất

1. Mở trang khách hàng, xem món và combo, thêm món/combo vào giỏ.
2. Đăng nhập/đăng ký, cập nhật địa chỉ tại **Tài khoản**, đặt giao tận nhà hoặc tự đến lấy.
3. Chọn chi nhánh có đủ tồn kho, dùng mã `CHAOBAN` hoặc `GIAM50K` với điều kiện đơn tối thiểu. Đơn mới chưa được coi là đã thanh toán.
4. Vào hệ thống nội bộ, chọn đúng chi nhánh, vào **Đơn hàng**, tiếp nhận hoặc từ chối đơn online có lý do.
5. Khi tiếp nhận đơn online, hệ thống kiểm tra công thức/tồn kho trong transaction và xuất kho theo công thức; các dòng kho có `OperationKey` duy nhất chống xử lý lặp.
6. Vào màn hình **Bếp**, chuyển món từ chờ chế biến sang đang chế biến rồi hoàn thành. Khi món cuối cùng hoàn thành, đơn giao hàng chuyển sang **Đang giao**, đơn tự lấy chuyển sang **Sẵn sàng nhận**. Nhân viên xác nhận hoàn tất tại màn hình Đơn hàng.
7. Vào **Thực đơn** để xem giá vốn, giá bán, biên lợi nhuận dự kiến và cảnh báo thiếu công thức/giá nguyên liệu.

## Các phần nghiệp vụ chính

- 4 chi nhánh demo, sơ đồ bàn, nhân sự/ca, thực đơn, combo, khuyến mãi, mã giảm giá, nhập hàng, kho, tài chính, báo cáo và lịch sử đơn.
- Khách hàng online có tài khoản riêng, giỏ hàng session, hồ sơ, địa chỉ, lịch sử đơn và kiểm tra quyền sở hữu khi xem chi tiết đơn.
- Giá vốn hiện tại được tính từ `RecipeLine.Qty × Ingredient.Cost`; giá vốn dòng đơn được lưu tại thời điểm đặt hàng.
- Combo mở rộng về món thành phần để tính giá vốn và lượng nguyên liệu.
- Xuất kho theo công thức trong transaction; `StockMovement.OperationKey` duy nhất ngăn xuất lặp khi xử lý lại cùng dòng đơn.
- Seed tạo tồn kho đầu kỳ và StockMovement tương ứng để dễ trình bày lịch sử kho.

## Phân quyền và giới hạn demo cần nói rõ

- Cookie khách hàng được tách riêng; trang lịch sử/chi tiết đơn lọc theo ID khách đang đăng nhập.
- **Vai trò nhân viên nội bộ trong source gốc vẫn là chế độ mô phỏng qua cookie `role` và `branch`, chưa phải xác thực nhân viên production.** Không triển khai công khai trước khi thay bằng tài khoản nhân viên, mật khẩu hash, policy authorization và kiểm tra quyền ở mọi action.
- Thanh toán COD được ghi là phương thức dự kiến. Thanh toán QR/online chưa tích hợp cổng thanh toán/webhook nên không được tự đánh dấu đã trả tiền.
- Chưa tích hợp hãng vận chuyển, SMS/email thông báo, hoặc theo dõi vị trí shipper.
- Hao hụt ngoài thực tế chỉ xác định được qua kiểm kê/nhập số lượng thực tế hoặc thiết bị đo; hệ thống không thể tự biết hao hụt vật lý nếu không có dữ liệu.
- Nghiệp vụ kho đã có khóa chống lặp theo dòng đơn; trước khi gọi là production-ready vẫn cần chạy kiểm thử tích hợp và tải đồng thời trên provider đích.

## Kiến trúc thư mục

- `Models/`: entity và ViewModel.
- `Data/AppDbContext.cs`: ánh xạ EF Core và index.
- `Data/SeedData.cs`: dữ liệu demo chỉ chạy khi chưa có chi nhánh.
- `Services/CostingService.cs`: tính giá vốn từ công thức.
- `Services/OrderService.cs`: hóa đơn, khuyến mãi, công thức và xuất kho.
- `Controllers/CustomerController.cs`: tài khoản và đặt hàng online.
- `Controllers/OrdersController.cs`, `KitchenController.cs`: tiếp nhận đơn và xử lý bếp.
- `Views/Customer/`: giao diện đặt món, đăng ký, đăng nhập và tài khoản responsive.
- `Views/Shared/_CustomerLayout.cshtml`: thanh điều hướng và bố cục riêng của khu vực khách hàng.
- `wwwroot/css/site.css`: giao diện đăng ký/đăng nhập responsive và trạng thái validation.

## Trước khi nộp demo

- Build `Release` trong Visual Studio và xử lý mọi warning/error.
- Chạy thử trên database demo mới; đặt một đơn, từ chối một đơn, tiếp nhận một đơn đủ kho và thử một đơn thiếu kho.
- Kiểm tra hai tài khoản khách hàng không thể mở đơn của nhau.
- Kiểm tra `StockMovements` không có `OperationKey` trùng và tồn kho giảm đúng định lượng.
- Không dùng database demo chứa dữ liệu thử nghiệm nếu cần giữ dữ liệu; backup trước khi đổi model/schema.


### Ghi chú xử lý đăng nhập/đăng xuất
- Đăng xuất khách hàng và nhân viên phải gửi bằng biểu mẫu `POST` có `@Html.AntiForgeryToken()`; không đổi nút đăng xuất thành liên kết `href`. Nếu trình duyệt vẫn báo lỗi tại URL đăng xuất, dừng ứng dụng rồi chạy lại để tải bản mới.
- Chỉ giữ **một** controller xử lý route `/nhan-vien/dang-nhap`. Nếu dự án có cả `SessionController` và `StaffAuthController` cùng khai báo route này, hãy gộp chức năng và xóa controller trùng hoặc đổi route của một controller; nếu không sẽ phát sinh `AmbiguousMatchException`.
