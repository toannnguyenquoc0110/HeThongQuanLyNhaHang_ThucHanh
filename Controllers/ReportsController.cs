using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaHang.Data;
using QuanLyNhaHang.Models;

namespace QuanLyNhaHang.Controllers;

public class ReportsController : BaseController
{
    public ReportsController(AppDbContext db) : base(db) { }

    async Task<ReportVM> Build(DateTime? from, DateTime? to, bool all)
    {
        var f = (from ?? DateTime.Today.AddDays(-13)).Date; var t = (to ?? DateTime.Today).Date;
        var orders = await Db.Orders.Include(o => o.Lines).Include(o => o.Payments).Include(o => o.Branch)
            .Where(o => o.Status == St.Paid && o.ClosedAt >= f && o.ClosedAt < t.AddDays(1) && (all || o.BranchId == BranchId)).ToListAsync();
        var exp = await Db.Expenses.Include(e => e.Branch).Where(e => e.Date >= f && e.Date <= t && (all || e.BranchId == BranchId)).ToListAsync();
        var lines = orders.SelectMany(o => o.Lines).Where(l => l.Status != St.Cancel).ToList();

        var vm = new ReportVM
        {
            From = f, To = t, AllBranches = all,
            Revenue = orders.Sum(o => o.Total), Orders = orders.Count, Guests = orders.Sum(o => o.Pax),
            Cogs = lines.Sum(l => l.Qty * l.Cost), Expenses = exp.Sum(e => e.Amount),
        };
        for (var d = f; d <= t; d = d.AddDays(1)) vm.Daily.Add((d, orders.Where(o => o.ClosedAt.Value.Date == d).Sum(o => o.Total)));
        vm.Items = lines.GroupBy(l => l.Name).Select(g => (g.Key, g.Sum(l => l.Qty), g.Sum(l => l.Qty * l.Price), g.Sum(l => l.Qty * (l.Price - l.Cost))))
            .OrderByDescending(x => x.Item2).Take(10).ToList();
        vm.Hours = orders.GroupBy(o => o.OpenedAt.Hour).Select(g => (g.Key, g.Count())).OrderBy(x => x.Key).ToList();
        vm.Methods = orders.SelectMany(o => o.Payments).GroupBy(p => p.Method).Select(g => (g.Key, g.Sum(p => p.Amount))).OrderByDescending(x => x.Item2).ToList();
        vm.Branches = (await Db.Branches.OrderBy(b => b.Id).ToListAsync()).Where(b => all || b.Id == BranchId)
            .Select(b => (b.Name, orders.Where(o => o.BranchId == b.Id).Sum(o => o.Total), orders.Count(o => o.BranchId == b.Id), exp.Where(e => e.BranchId == b.Id).Sum(e => e.Amount))).ToList();
        return vm;
    }

    public async Task<IActionResult> Index(DateTime? from, DateTime? to, bool all = false) => View(await Build(from, to, all));

    /// <summary>Xuất CSV (mở được bằng Excel).</summary>
    public async Task<IActionResult> Export(DateTime? from, DateTime? to, bool all = false)
    {
        var vm = await Build(from, to, all);
        var sb = new StringBuilder("\uFEFFNgày,Doanh thu\n");
        foreach (var d in vm.Daily) sb.AppendLine($"{d.Day:dd/MM/yyyy},{d.Revenue:0}");
        sb.AppendLine().AppendLine("Món,Số lượng,Doanh thu,Lợi nhuận gộp");
        foreach (var i in vm.Items) sb.AppendLine($"\"{i.Name}\",{i.Qty},{i.Revenue:0},{i.Profit:0}");
        sb.AppendLine().AppendLine("Chi nhánh,Doanh thu,Số đơn,Chi phí");
        foreach (var b in vm.Branches) sb.AppendLine($"\"{b.Branch}\",{b.Revenue:0},{b.Orders},{b.Expenses:0}");
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"bao-cao-{vm.From:yyyyMMdd}-{vm.To:yyyyMMdd}.csv");
    }
}
