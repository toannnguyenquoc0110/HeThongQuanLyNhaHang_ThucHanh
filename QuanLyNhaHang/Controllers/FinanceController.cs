using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaHang.Data;
using QuanLyNhaHang.Models;

namespace QuanLyNhaHang.Controllers;

public class FinanceController : BaseController
{
    public FinanceController(AppDbContext db) : base(db) { }

    public async Task<IActionResult> Index(string tab = "expenses")
    {
        var m1 = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var pays = await Db.Payments.Include(p => p.Order).Where(p => p.Order.BranchId == BranchId && p.PaidAt >= m1).ToListAsync();
        var exp = await Db.Expenses.Where(e => e.BranchId == BranchId && e.Date >= m1).OrderByDescending(e => e.Date).ThenByDescending(e => e.Id).ToListAsync();
        var lastClose = await Db.CashCloses.Where(c => c.BranchId == BranchId).OrderByDescending(c => c.At).FirstOrDefaultAsync();
        var since = lastClose?.At ?? DateTime.Today;

        ViewBag.Tab = tab;
        ViewBag.Expenses = exp;
        ViewBag.Revenue = pays.Sum(p => p.Amount);
        ViewBag.ExpenseTotal = exp.Sum(e => e.Amount);
        ViewBag.Methods = pays.GroupBy(p => p.Method).Select(g => (g.Key, g.Sum(p => p.Amount))).OrderByDescending(x => x.Item2).ToList();
        ViewBag.ByCategory = exp.GroupBy(e => e.Category).Select(g => (g.Key, g.Sum(e => e.Amount))).OrderByDescending(x => x.Item2).ToList();
        ViewBag.CashExpected = pays.Where(p => p.Method == "Tiền mặt" && p.PaidAt >= since).Sum(p => p.Amount);
        ViewBag.Since = since;
        ViewBag.Closes = await Db.CashCloses.Where(c => c.BranchId == BranchId).OrderByDescending(c => c.At).Take(15).ToListAsync();
        ViewBag.Logs = await Db.AuditLogs.Where(a => a.BranchId == BranchId).OrderByDescending(a => a.At).Take(80).ToListAsync();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> CreateExpense(Expense e)
    {
        if (e.Amount <= 0 || string.IsNullOrWhiteSpace(e.Category)) { Err("Nhập loại chi phí và số tiền."); return RedirectToAction(nameof(Index)); }
        e.Id = 0; e.BranchId = BranchId; if (e.Date == default) e.Date = DateTime.Today;
        Db.Expenses.Add(e); await Db.SaveChangesAsync();
        Ok("Đã ghi chi phí");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> CloseCash(decimal counted, string note)
    {
        var m1 = DateTime.Today;
        var last = await Db.CashCloses.Where(c => c.BranchId == BranchId).OrderByDescending(c => c.At).FirstOrDefaultAsync();
        var since = last?.At ?? m1;
        // SQLite không cộng decimal ở phía database nên lấy danh sách rồi cộng trong bộ nhớ
        var cash = await Db.Payments.Include(p => p.Order)
            .Where(p => p.Order.BranchId == BranchId && p.Method == "Tiền mặt" && p.PaidAt >= since).ToListAsync();
        var expected = cash.Sum(p => p.Amount);
        Db.CashCloses.Add(new CashClose { BranchId = BranchId, Expected = expected, Counted = counted, Note = note });
        await Db.SaveChangesAsync();
        Ok(counted == expected ? "Đóng ca khớp tiền." : $"Đóng ca, chênh lệch {(counted - expected):N0}đ.");
        return RedirectToAction(nameof(Index), new { tab = "cash" });
    }
}
