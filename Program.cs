using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using QuanLyNhaHang.Models;
using QuanLyNhaHang.Data;
using QuanLyNhaHang.Services;

// Npgsql 6+ chỉ nhận DateTime kiểu UTC cho cột timestamptz; hệ thống này lưu giờ địa phương nên bật chế độ cũ.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options => { options.Cookie.HttpOnly = true; options.Cookie.IsEssential = true; options.IdleTimeout = TimeSpan.FromHours(2); });
builder.Services.AddAuthorization();
builder.Services.AddAuthentication(options => { options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme; })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options => { options.LoginPath = "/nhan-vien/dang-nhap"; options.AccessDeniedPath = "/nhan-vien/tu-choi"; options.Cookie.Name = "RestaurantStaffSession"; options.ExpireTimeSpan = TimeSpan.FromHours(8); options.SlidingExpiration = true; options.Cookie.HttpOnly = true; options.Cookie.SameSite = SameSiteMode.Lax; })
    .AddCookie("CustomerCookie", options => { options.LoginPath = "/khach-hang/dang-nhap"; options.Cookie.Name = "RestaurantCustomerSession"; options.Cookie.HttpOnly = true; options.Cookie.SameSite = SameSiteMode.Lax; options.ExpireTimeSpan = TimeSpan.FromDays(14); });
builder.Services.AddScoped<IPasswordHasher<CustomerAccount>, PasswordHasher<CustomerAccount>>();
builder.Services.AddScoped<IPasswordHasher<StaffAccount>, PasswordHasher<StaffAccount>>();

// Chọn database ở appsettings.json (Database:Provider) hoặc biến môi trường Database__Provider
var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var cs = builder.Configuration.GetConnectionString(provider) ?? builder.Configuration.GetConnectionString("Default") ?? "Data Source=vinha.db";
builder.Services.AddSingleton(new DbInfo(provider, cs));
builder.Services.AddDbContext<AppDbContext>(o =>
{
    if (provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
        o.UseNpgsql(cs, n => n.EnableRetryOnFailure(3));
    else
        o.UseSqlite(cs);
});
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<CostingService>();

var app = builder.Build();

// Dự án hiện ở chế độ Code First demo: lần chạy đầu tiên tạo schema từ các model.
// EnsureCreated KHÔNG nâng cấp schema đã tồn tại; khi model thay đổi sau này hãy dùng EF migrations.
// Không dùng EnsureDeleted, không xóa database và chỉ seed khi chưa có chi nhánh.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await db.Database.EnsureCreatedAsync();
        await SqliteSchemaRepair.ApplyAsync(db);
        SeedData.Run(db);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Không thể khởi tạo schema/seed. Kiểm tra quyền ghi thư mục và cấu hình database.");
        throw;
    }
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", async (AppDbContext db) =>
    await db.Database.CanConnectAsync() ? Results.Ok(new { status = "ok" }) : Results.StatusCode(503));
app.MapControllerRoute("default", "{controller=Dashboard}/{action=Index}/{id?}");
app.Run();

/// <summary>Thông tin kết nối để trang "Dữ liệu" hiển thị (không chứa mật khẩu).</summary>
public record DbInfo(string Provider, string ConnectionString);
