using Microsoft.EntityFrameworkCore;
using QuanLyNhaHang.Models;

namespace QuanLyNhaHang.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Tổ chức
    public DbSet<Branch> Branches { get; set; }
    public DbSet<Zone> Zones { get; set; }
    public DbSet<DiningTable> Tables { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    // Nhân sự
    public DbSet<Employee> Employees { get; set; }
    public DbSet<StaffAccount> StaffAccounts { get; set; }
    public DbSet<Shift> Shifts { get; set; }
    public DbSet<ShiftAssignment> ShiftAssignments { get; set; }
    public DbSet<Attendance> Attendances { get; set; }
    public DbSet<LeaveRequest> LeaveRequests { get; set; }
    public DbSet<Payroll> Payrolls { get; set; }
    // Thực đơn
    public DbSet<MenuCategory> Categories { get; set; }
    public DbSet<MenuItem> MenuItems { get; set; }
    public DbSet<Combo> Combos { get; set; }
    public DbSet<ComboItem> ComboItems { get; set; }
    public DbSet<Promotion> Promotions { get; set; }
    public DbSet<Coupon> Coupons { get; set; }
    // Kho
    public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<Ingredient> Ingredients { get; set; }
    public DbSet<BranchStock> BranchStocks { get; set; }
    public DbSet<RecipeLine> RecipeLines { get; set; }
    public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
    public DbSet<PurchaseLine> PurchaseLines { get; set; }
    public DbSet<StockMovement> StockMovements { get; set; }
    // Khách hàng online
    public DbSet<CustomerAccount> CustomerAccounts { get; set; }
    // Bán hàng
    public DbSet<Reservation> Reservations { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderLine> OrderLines { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Expense> Expenses { get; set; }
    public DbSet<CashClose> CashCloses { get; set; }

    protected override void ConfigureConventions(ModelConfigurationBuilder cb)
    {
        // Tiền và số lượng kho: 18 chữ số, 3 số lẻ (PostgreSQL tạo cột numeric(18,3), SQLite bỏ qua)
        cb.Properties<decimal>().HavePrecision(18, 3);
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // Chỉ mục cho các truy vấn nóng: sơ đồ bàn, bếp, báo cáo theo ngày, nhật ký
        mb.Entity<Order>().HasIndex(o => new { o.BranchId, o.Status, o.ClosedAt }).HasDatabaseName("IX_Orders_Branch_Status_ClosedAt");
        mb.Entity<Order>().HasIndex(o => new { o.BranchId, o.TableId, o.Status }).HasDatabaseName("IX_Orders_Branch_Table_Status");
        mb.Entity<OrderLine>().HasIndex(l => new { l.Status, l.SentAt }).HasDatabaseName("IX_OrderLines_Status_SentAt");
        mb.Entity<Reservation>().HasIndex(r => new { r.BranchId, r.Date, r.Status }).HasDatabaseName("IX_Reservations_Branch_Date_Status");
        mb.Entity<StockMovement>().HasIndex(m => new { m.BranchId, m.Date }).HasDatabaseName("IX_StockMovements_Branch_Date");
        mb.Entity<Expense>().HasIndex(e => new { e.BranchId, e.Date }).HasDatabaseName("IX_Expenses_Branch_Date");
        mb.Entity<AuditLog>().HasIndex(a => new { a.BranchId, a.At }).HasDatabaseName("IX_AuditLogs_Branch_At");
        mb.Entity<Attendance>().HasIndex(a => new { a.EmployeeId, a.Date }).HasDatabaseName("IX_Attendances_Employee_Date");
        mb.Entity<ShiftAssignment>().HasIndex(a => a.Date).HasDatabaseName("IX_ShiftAssignments_Date");
        mb.Entity<BranchStock>().HasIndex(x => new { x.BranchId, x.IngredientId }).IsUnique();
        mb.Entity<CustomerAccount>().HasIndex(x => x.Email).IsUnique();
        mb.Entity<StaffAccount>().HasIndex(x => x.Email).IsUnique();
        mb.Entity<StockMovement>().HasIndex(x => x.OperationKey).IsUnique();
        mb.Entity<Order>().HasIndex(x => new { x.CustomerAccountId, x.OpenedAt });
        mb.Entity<Order>().HasOne(x => x.CustomerAccount).WithMany().HasForeignKey(x => x.CustomerAccountId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Coupon>().HasIndex(x => x.Code).IsUnique();
        mb.Entity<Order>().HasOne(o => o.Table).WithMany().HasForeignKey(o => o.TableId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<Reservation>().HasOne(r => r.Table).WithMany().HasForeignKey(r => r.TableId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<OrderLine>().HasOne(l => l.MenuItem).WithMany().HasForeignKey(l => l.MenuItemId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<OrderLine>().HasOne(l => l.Combo).WithMany().HasForeignKey(l => l.ComboId).OnDelete(DeleteBehavior.SetNull);
    }
}
