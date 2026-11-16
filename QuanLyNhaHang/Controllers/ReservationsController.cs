using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaHang.Data;
using QuanLyNhaHang.Models;
using QuanLyNhaHang.Services;

namespace QuanLyNhaHang.Controllers;

public class ReservationsController : BaseController
{
    private readonly OrderService _svc;
    public ReservationsController(AppDbContext db, OrderService svc) : base(db) { _svc = svc; }

    public async Task<IActionResult> Index(DateTime? date)
    {
        var d = (date ?? DateTime.Today).Date;
        ViewBag.Date = d;
        return View(await Db.Reservations.Include(r => r.Table)
            .Where(r => r.BranchId == BranchId && r.Date == d).OrderBy(r => r.Time).ToListAsync());
    }

    public async Task<IActionResult> Free(DateTime date, string time, int pax)
    {
        var list = await _svc.FreeTablesAsync(BranchId, date, string.IsNullOrEmpty(time) ? "18:00" : time, Math.Max(1, pax));
        return Json(list.Select(t => new { t.Id, t.Name, t.Seats }));
    }

    [HttpPost]
    public async Task<IActionResult> Create(Reservation r)
    {
        if (string.IsNullOrWhiteSpace(r.CustomerName) || string.IsNullOrWhiteSpace(r.Time) || r.TableId == 0)
        { Err("Nhập tên khách, giờ và chọn bàn."); return RedirectToAction(nameof(Index), new { date = r.Date }); }
        var free = await _svc.FreeTablesAsync(BranchId, r.Date, r.Time, 1);
        if (!free.Any(t => t.Id == r.TableId)) { Err("Bàn này đã có lịch hoặc đang có khách trong khung giờ đó."); return RedirectToAction(nameof(Index), new { date = r.Date }); }
        r.Id = 0; r.BranchId = BranchId; r.Status = St.Confirmed; r.CreatedAt = DateTime.Now; r.Date = r.Date.Date;
        Db.Reservations.Add(r);
        await Db.SaveChangesAsync();
        await _svc.LogAsync(BranchId, Actor, "Đặt bàn", $"{r.CustomerName} lúc {r.Time} ngày {r.Date:dd/MM}");
        Ok("Đã giữ bàn cho " + r.CustomerName);
        return RedirectToAction(nameof(Index), new { date = r.Date });
    }

    [HttpPost]
    public async Task<IActionResult> Status(int id, string status)
    {
        var r = await Db.Reservations.FindAsync(id);
        if (r != null && r.BranchId == BranchId) { r.Status = status; await Db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index), new { date = r?.Date });
    }
}
