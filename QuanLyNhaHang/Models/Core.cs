namespace QuanLyNhaHang.Models;

public enum Role { QuanLy = 0, PhucVu = 1, Bep = 2 }

/// <summary>Các trạng thái dùng chung trong hệ thống.</summary>
public static class St
{
    public const string New = "Mới", Wait = "Chờ chế biến", Cook = "Đang chế biến", Done = "Hoàn thành", Served = "Đã phục vụ", Cancel = "Đã hủy";
    public const string OnlineReady = "Sẵn sàng nhận", Delivering = "Đang giao", Completed = "Hoàn thành đơn";
    public const string Open = "Đang phục vụ", Paid = "Đã thanh toán", Void = "Đã hủy";
    public const string Confirmed = "Đã xác nhận", Arrived = "Đã đến", NoShow = "Không đến";
}

/// <summary>Danh sách quyền điều hướng theo vai trò; vai trò thực tế lấy từ tài khoản đã xác thực.</summary>
public static class RoleMap
{
    static readonly Dictionary<Role, string[]> Allowed = new()
    {
        [Role.QuanLy] = new[] { "Dashboard", "Tables", "Orders", "Kitchen", "Reservations", "Menu", "Inventory", "Staff", "Reports", "Finance", "System" },
        [Role.PhucVu] = new[] { "Tables", "Orders", "Reservations" },
        [Role.Bep] = new[] { "Kitchen" },
    };
    static readonly (string Ctrl, string Label)[] All =
    {
        ("Dashboard", "Tổng quan"), ("Tables", "Sơ đồ bàn"), ("Orders", "Đơn hôm nay"), ("Reservations", "Đặt bàn"), ("Kitchen", "Bếp"),
        ("Menu", "Thực đơn"), ("Inventory", "Kho & mua hàng"), ("Staff", "Nhân sự"), ("Reports", "Báo cáo"), ("Finance", "Tài chính"), ("System", "Dữ liệu"),
    };
    public static bool Can(Role r, string ctrl) => Allowed[r].Contains(ctrl);
    public static string Home(Role r) => r switch { Role.QuanLy => "Dashboard", Role.PhucVu => "Tables", _ => "Kitchen" };
    public static string Name(Role r) => r switch { Role.QuanLy => "Quản lý", Role.PhucVu => "Phục vụ", _ => "Bếp" };
    public static IEnumerable<(string Ctrl, string Label)> Nav(Role r) => All.Where(x => Can(r, x.Ctrl));
}
