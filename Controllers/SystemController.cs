using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaHang.Data;
using QuanLyNhaHang.Models;

namespace QuanLyNhaHang.Controllers;

/// <summary>Trang "Dữ liệu": tình trạng database, số dòng từng bảng, nhật ký thao tác, sao lưu.</summary>
public class SystemController : BaseController
{
    readonly DbInfo _info;
    public SystemController(AppDbContext db, DbInfo info) : base(db) { _info = info; }

    public async Task<IActionResult> Index()
    {
        var vm = new SystemVM { Provider = _info.Provider, IsSqlite = Db.Database.IsSqlite() };
        var sw = System.Diagnostics.Stopwatch.StartNew();
        vm.CanConnect = await Db.Database.CanConnectAsync();
        vm.PingMs = sw.ElapsedMilliseconds;

        vm.Counts = new List<(string Name, int Rows)>
        {
            ("Chi nhánh", await Db.Branches.CountAsync()), ("Bàn", await Db.Tables.CountAsync()),
            ("Nhân viên", await Db.Employees.CountAsync()), ("Món", await Db.MenuItems.CountAsync()),
            ("Combo", await Db.Combos.CountAsync()), ("Nguyên liệu", await Db.Ingredients.CountAsync()),
            ("Phiếu nhập", await Db.PurchaseOrders.CountAsync()), ("Biến động kho", await Db.StockMovements.CountAsync()),
            ("Đặt bàn", await Db.Reservations.CountAsync()), ("Đơn hàng", await Db.Orders.CountAsync()),
            ("Dòng món", await Db.OrderLines.CountAsync()), ("Thanh toán", await Db.Payments.CountAsync()),
            ("Chi phí", await Db.Expenses.CountAsync()), ("Nhật ký", await Db.AuditLogs.CountAsync()),
        };
        vm.TableCount = Db.Model.GetEntityTypes().Count();
        vm.IndexCount = Db.Model.GetEntityTypes().Sum(e => e.GetIndexes().Count() + e.GetForeignKeys().Count());

        if (vm.IsSqlite)
        {
            var file = new FileInfo(Db.Database.GetDbConnection().DataSource);
            if (file.Exists) { vm.SizeKb = file.Length / 1024; vm.Location = file.Name; }
        }
        else vm.Location = "Máy chủ PostgreSQL";

        vm.Logs = await Db.AuditLogs.OrderByDescending(a => a.At).ThenByDescending(a => a.Id).Take(40).ToListAsync();
        return View(vm);
    }

    /// <summary>Tải bản sao lưu (chỉ SQLite; PostgreSQL dùng pg_dump).</summary>
    public async Task<IActionResult> Backup()
    {
        if (!Db.Database.IsSqlite()) { Err("PostgreSQL sao lưu bằng pg_dump, xem hướng dẫn trong db/README.md."); return RedirectToAction(nameof(Index)); }
        var tmp = Path.Combine(Path.GetTempPath(), $"vinha-{Guid.NewGuid():N}.db");
        try
        {
            await Db.Database.ExecuteSqlRawAsync($"VACUUM INTO '{tmp.Replace("'", "''")}'");
            var bytes = await System.IO.File.ReadAllBytesAsync(tmp);
            Db.AuditLogs.Add(new AuditLog { BranchId = BranchId, Actor = Actor, Action = "Sao lưu", Detail = "Tải bản sao lưu database" });
            await Db.SaveChangesAsync();
            return File(bytes, "application/octet-stream", $"vinha-{DateTime.Now:yyyyMMdd-HHmm}.db");
        }
        finally { if (System.IO.File.Exists(tmp)) System.IO.File.Delete(tmp); }
    }
}
