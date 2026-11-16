namespace QuanLyNhaHang.Models;

public class TileVM { public DiningTable Table; public string State; public Order Order; public Reservation Reservation; public decimal Amount; }

public class OrderScreenVM
{
    public Order Order;
    public CartVM Cart;
    public List<MenuCategory> Categories = new();
    public List<MenuItem> Items = new();
    public List<Combo> Combos = new();
    public Dictionary<int, decimal> Stock = new();
    public Dictionary<int, int> Promo = new();   // categoryId -> % giảm đang áp dụng
}
public class CartVM { public Order Order; public QuanLyNhaHang.Services.OrderService.Bill Bill; }

public class DashboardVM
{
    public decimal RevenueToday, ProfitToday; public int OrdersToday, OpenOrders, PendingRes;
    public List<(Branch Branch, decimal Revenue, int Orders)> ByBranch = new();
    public List<BranchStock> Low = new(), Expiring = new();
    public List<(Employee Emp, Shift Shift)> OnShift = new();
    public List<Reservation> NextRes = new();
    public List<(string Name, int Qty)> TopItems = new();
    public List<(DateTime Day, decimal Revenue)> Week = new();   // 7 ngày gần nhất của chi nhánh
}

public class ReportVM
{
    public DateTime From, To; public bool AllBranches;
    public decimal Revenue, Cogs, Expenses; public int Orders, Guests;
    public List<(DateTime Day, decimal Revenue)> Daily = new();
    public List<(string Name, int Qty, decimal Revenue, decimal Profit)> Items = new();
    public List<(int Hour, int Orders)> Hours = new();
    public List<(string Method, decimal Amount)> Methods = new();
    public List<(string Branch, decimal Revenue, int Orders, decimal Expenses)> Branches = new();
}

public class SystemVM
{
    public string Provider, Location; public bool IsSqlite, CanConnect; public long PingMs, SizeKb; public int TableCount, IndexCount;
    public List<(string Name, int Rows)> Counts = new();
    public List<AuditLog> Logs = new();
}
