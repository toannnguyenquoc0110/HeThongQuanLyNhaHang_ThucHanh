using Microsoft.AspNetCore.Mvc;
using System.Data;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaHang.Data;
using QuanLyNhaHang.Models;
using QuanLyNhaHang.Services;

namespace QuanLyNhaHang.Controllers;

public class KitchenController : BaseController
{
    private readonly OrderService _svc;
    public KitchenController(AppDbContext db, OrderService svc) : base(db) { _svc = svc; }

    async Task<List<OrderLine>> Lines() => await Db.OrderLines
        .Include(l => l.Order).ThenInclude(o => o.Table)
        .Where(l => l.Order.BranchId == BranchId && (l.Order.Status == St.Open || (l.Order.Type == "Online" && l.Order.Status == St.Wait)) && (l.Status == St.Wait || l.Status == St.Cook || l.Status == St.Done))
        .OrderByDescending(l => l.Priority).ThenBy(l => l.SentAt).ToListAsync();

    public async Task<IActionResult> Index()
    {
        ViewBag.Items = await Db.MenuItems.OrderBy(m => m.Name).ToListAsync();
        return View(await Lines());
    }

    public async Task<IActionResult> Board() => PartialView("_Board", await Lines());

    [HttpPost]
    public async Task<IActionResult> Start(int id)
    {
        var l = await Db.OrderLines.Include(x => x.Order).FirstOrDefaultAsync(x => x.Id == id);
        if (l != null && l.Status == St.Wait)
        {
            try
            {
                await using var tx = await Db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                if (!l.Order.StockDeducted) await _svc.DeductAsync(l, l.Order.BranchId);
                l.Status = St.Cook; l.StartedAt = DateTime.Now;
                await Db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (InvalidOperationException ex) { TempData["err"] = ex.Message; }
            catch (DbUpdateException) { TempData["err"] = "Có xung đột cập nhật kho; vui lòng tải lại và kiểm tra giao dịch kho."; }
        }
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

    /// <summary>Bếp báo hết / có lại món, phục vụ sẽ thấy ngay trên màn hình gọi món.</summary>
    [HttpPost]
    public async Task<IActionResult> Stop(int itemId)
    {
        var m = await Db.MenuItems.FindAsync(itemId);
        if (m != null)
        {
            m.Stopped = !m.Stopped; await Db.SaveChangesAsync();
            await _svc.LogAsync(BranchId, Actor, m.Stopped ? "Báo hết món" : "Có lại món", m.Name);
            Ok(m.Stopped ? m.Name + " đã báo hết." : m.Name + " bán lại.");
        }
        return RedirectToAction(nameof(Index));
    }
}
