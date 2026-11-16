using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaHang.Data;
using QuanLyNhaHang.Models;

namespace QuanLyNhaHang.Controllers;

public class DashboardController : BaseController
{
    public DashboardController(AppDbContext db) : base(db) { }

    public async Task<IActionResult> Index()
    {
        var today = DateTime.Today; var tomorrow = today.AddDays(1);
        var paid = await Db.Orders.Include(o => o.Lines)
            .Where(o => o.Status == St.Paid && o.ClosedAt >= today && o.ClosedAt < tomorrow).ToListAsync();

        var vm = new DashboardVM
        {
            RevenueToday = paid.Where(o => o.BranchId == BranchId).Sum(o => o.Total),
            ProfitToday = paid.Where(o => o.BranchId == BranchId).Sum(o => o.Lines.Where(l => l.Status != St.Cancel).Sum(l => l.Qty * (l.Price - l.Cost))),
            OrdersToday = paid.Count(o => o.BranchId == BranchId),
            OpenOrders = await Db.Orders.CountAsync(o => o.BranchId == BranchId && o.Status == St.Open),
            PendingRes = await Db.Reservations.CountAsync(r => r.BranchId == BranchId && r.Date == today && r.Status == St.Confirmed),
        };
        foreach (var b in await Db.Branches.OrderBy(b => b.Id).ToListAsync())
        {
            var list = paid.Where(o => o.BranchId == b.Id).ToList();
            vm.ByBranch.Add((b, list.Sum(o => o.Total), list.Count));
        }
        var soon = today.AddDays(7);
        var stocks = await Db.BranchStocks.Include(s => s.Ingredient).Where(s => s.BranchId == BranchId).ToListAsync();
        vm.Low = stocks.Where(s => s.Quantity <= s.Ingredient.MinLevel).OrderBy(s => s.Quantity / (s.Ingredient.MinLevel == 0 ? 1 : s.Ingredient.MinLevel)).Take(8).ToList();
        vm.Expiring = stocks.Where(s => s.ExpiryDate != null && s.ExpiryDate <= soon && s.Quantity > 0).OrderBy(s => s.ExpiryDate).Take(6).ToList();

        var assigns = await Db.ShiftAssignments.Include(a => a.Employee).Include(a => a.Shift)
            .Where(a => a.Date == today && a.Employee.BranchId == BranchId).ToListAsync();
        vm.OnShift = assigns.OrderBy(a => a.Shift.StartTime).Select(a => (a.Employee, a.Shift)).ToList();
        vm.NextRes = await Db.Reservations.Include(r => r.Table).Where(r => r.BranchId == BranchId && r.Date == today && r.Status == St.Confirmed).OrderBy(r => r.Time).Take(6).ToListAsync();
        vm.TopItems = paid.Where(o => o.BranchId == BranchId).SelectMany(o => o.Lines).Where(l => l.Status != St.Cancel)
            .GroupBy(l => l.Name).Select(g => (g.Key, g.Sum(l => l.Qty))).OrderByDescending(x => x.Item2).Take(5).ToList();
        var from = today.AddDays(-6);
        var week = await Db.Orders.Where(o => o.BranchId == BranchId && o.Status == St.Paid && o.ClosedAt >= from && o.ClosedAt < tomorrow)
            .Select(o => new { o.ClosedAt, o.Total }).ToListAsync();
        for (var d = from; d <= today; d = d.AddDays(1)) vm.Week.Add((d, week.Where(o => o.ClosedAt.Value.Date == d).Sum(o => o.Total)));
        return View(vm);
    }
}
