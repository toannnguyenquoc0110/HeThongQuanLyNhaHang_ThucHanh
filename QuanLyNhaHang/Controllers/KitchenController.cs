using Microsoft.AspNetCore.Mvc;
using System.Data;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaHang.Data;
using QuanLyNhaHang.Models;
using QuanLyNhaHang.Services;
using Microsoft.Extensions.Configuration;

namespace QuanLyNhaHang.Controllers;

public class KitchenController : BaseController
{
    private readonly OrderService _svc;
    private readonly KitchenDispatcher _dispatcher;
    private readonly IConfiguration _cfg;
    public KitchenController(AppDbContext db, OrderService svc, KitchenDispatcher dispatcher, IConfiguration cfg) : base(db) { _svc = svc; _dispatcher = dispatcher; _cfg = cfg; }

    /// <summary>Ngưỡng báo động: món chờ quá WaitAlertMinutes sẽ được nhắc lại mỗi RemindMinutes cho đến khi vào bếp.</summary>
    void SetThresholds()
    {
        ViewBag.WaitAlertMinutes = _cfg.GetValue<int>("Kitchen:WaitAlertMinutes", 5);
        ViewBag.RemindMinutes = _cfg.GetValue<int>("Kitchen:RemindMinutes", 3);
    }

    async Task<List<OrderLine>> Lines() => await Db.OrderLines
        .Include(l => l.Order).ThenInclude(o => o.Table)
        .Where(l => l.Order.BranchId == BranchId && (l.Order.Status == St.Open || (l.Order.Type == "Online" && l.Order.Status == St.Wait)) && (l.Status == St.Wait || l.Status == St.Cook || l.Status == St.Done))
        .OrderByDescending(l => l.Priority).ThenBy(l => l.SentAt).ToListAsync();

    public async Task<IActionResult> Index()
    {
        ViewBag.Ingredients = await Db.Ingredients.OrderBy(i => i.Name).ToListAsync();
        ViewBag.Stock = await _svc.StockAsync(BranchId);
        SetThresholds();
        return View(await Lines());
    }

    public async Task<IActionResult> Board() { SetThresholds(); return PartialView("_Board", await Lines()); }

    /// <summary>Không còn bấm tay: món tự vào bếp khi gửi. Endpoint này chỉ chạy lại điều phối để tương thích trang cũ.</summary>
    [HttpPost]
    public async Task<IActionResult> Start(int id)
    {
        await _dispatcher.DispatchAsync(BranchId);
        return PartialView("_Board", await Lines());
    }

    [HttpPost]
    public async Task<IActionResult> Done(int id)
    {
        var l = await Db.OrderLines.Include(x => x.Order).ThenInclude(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id);
        if (l != null && l.Status == St.Cook)
        {
            l.Status = St.Done; l.DoneAt = DateTime.Now;
            if (l.Order.Type == "Online" && l.Order.Lines.Where(x => x.Status != St.Cancel).All(x => x.Id == l.Id || x.Status == St.Done))
            {
                l.Order.Status = l.Order.FulfillmentType == "Giao tận nhà" ? St.Delivering : St.OnlineReady;
            }
            await Db.SaveChangesAsync();
            // Xong một món là giải phóng chỗ: đưa món đang chờ vào bếp ngay.
            await _dispatcher.DispatchAsync(BranchId);
        }
        return PartialView("_Board", await Lines());
    }

    [HttpPost]
    public async Task<IActionResult> Priority(int id)
    {
        var l = await Db.OrderLines.FindAsync(id);
        if (l != null) { l.Priority = !l.Priority; await Db.SaveChangesAsync(); }
        return PartialView("_Board", await Lines());
    }

    /// <summary>Bếp báo hết nguyên liệu: kho của chi nhánh tự về 0, các món dùng nguyên liệu này tự khóa. Có hàng lại khi nhập kho đơn hàng mới.</summary>
    [HttpPost]
    public async Task<IActionResult> OutOfStock(int ingredientId)
    {
        var (ok, msg) = await _svc.ReportOutAsync(BranchId, ingredientId, Actor);
        if (ok) Ok(msg); else Err(msg);
        return RedirectToAction(nameof(Index));
    }
}
