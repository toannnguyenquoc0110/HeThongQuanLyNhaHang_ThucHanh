using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaHang.Data;
using QuanLyNhaHang.Models;
using QuanLyNhaHang.Services;

namespace QuanLyNhaHang.Controllers;

public class TablesController : BaseController
{
    private readonly OrderService _svc;
    public TablesController(AppDbContext db, OrderService svc) : base(db) { _svc = svc; }

    public async Task<IActionResult> Index()
    {
        var zones = await Db.Zones.Where(z => z.BranchId == BranchId).OrderBy(z => z.Id).ToListAsync();
        var tables = await Db.Tables.Where(t => t.BranchId == BranchId).OrderBy(t => t.Id).ToListAsync();
        var open = await Db.Orders.Include(o => o.Lines).Include(o => o.Employee)
            .Where(o => o.BranchId == BranchId && o.Status == St.Open).ToListAsync();
        var res = await Db.Reservations.Where(r => r.BranchId == BranchId && r.Date == DateTime.Today && r.Status == St.Confirmed).ToListAsync();

        var now = DateTime.Now.TimeOfDay;
        var tiles = new List<TileVM>();
        foreach (var t in tables)
        {
            var o = open.FirstOrDefault(x => x.TableId == t.Id);
            var r = res.Where(x => x.TableId == t.Id)
                .FirstOrDefault(x => { var d = (TimeSpan.Parse(x.Time) - now).TotalMinutes; return d <= 90 && d >= -30; });
            tiles.Add(new TileVM
            {
                Table = t, Order = o, Reservation = r,
                State = o != null ? "Đang dùng" : r != null ? "Đã đặt" : "Trống",
                Amount = o == null ? 0 : o.Lines.Where(l => l.Status != St.Cancel).Sum(l => l.Qty * l.Price),
            });
        }
        ViewBag.Zones = zones;
        ViewBag.Takeaway = open.Where(o => o.TableId == null).ToList();
        ViewBag.Waiters = await Db.Employees.Where(e => e.BranchId == BranchId && e.Active && e.Role == Role.PhucVu).OrderBy(e => e.FullName).ToListAsync();
        return View(tiles);
    }

    [HttpPost]
    public async Task<IActionResult> Open(int tableId, int pax, string customerName, int? employeeId, int? reservationId)
    {
        var t = await Db.Tables.FindAsync(tableId);
        if (t == null || t.BranchId != BranchId) return RedirectToAction(nameof(Index));
        var existing = await Db.Orders.FirstOrDefaultAsync(o => o.TableId == tableId && o.Status == St.Open);
        if (existing != null) return RedirectToAction("Screen", "Orders", new { id = existing.Id });

        var o = new Order { BranchId = BranchId, TableId = tableId, Pax = pax <= 0 ? 1 : pax, CustomerName = customerName, EmployeeId = employeeId, ReservationId = reservationId };
        Db.Orders.Add(o);
        if (reservationId != null)
        {
            var r = await Db.Reservations.FindAsync(reservationId.Value);
            if (r != null) { r.Status = St.Arrived; if (string.IsNullOrEmpty(o.CustomerName)) o.CustomerName = r.CustomerName; }
        }
        await Db.SaveChangesAsync();
        await _svc.LogAsync(BranchId, Actor, "Mở bàn", $"{t.Name} - {o.Pax} khách");
        return RedirectToAction("Screen", "Orders", new { id = o.Id });
    }

    [HttpPost]
    public async Task<IActionResult> Takeaway(string customerName, string phone)
    {
        var o = new Order { BranchId = BranchId, Type = "Mang về", CustomerName = customerName, Note = phone, Pax = 1 };
        Db.Orders.Add(o);
        await Db.SaveChangesAsync();
        return RedirectToAction("Screen", "Orders", new { id = o.Id });
    }
}
