using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaHang.Data;
using QuanLyNhaHang.Models;

namespace QuanLyNhaHang.Controllers;

public class StaffController : BaseController
{
    public StaffController(AppDbContext db) : base(db) { }

    static DateTime Monday(DateTime d) => d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    public async Task<IActionResult> Index(string tab = "list", DateTime? week = null, int? month = null, int? year = null)
    {
        var emps = await Db.Employees.Where(e => e.BranchId == BranchId).OrderBy(e => e.Role).ThenBy(e => e.FullName).ToListAsync();
        var ids = emps.Select(e => e.Id).ToList();
        var mon = Monday(week ?? DateTime.Today);
        var m = month ?? DateTime.Today.Month; var y = year ?? DateTime.Today.Year;
        ViewBag.Tab = tab; ViewBag.Emps = emps; ViewBag.Week = mon; ViewBag.Month = m; ViewBag.Year = y;
        ViewBag.Shifts = await Db.Shifts.OrderBy(s => s.Id).ToListAsync();
        ViewBag.Assign = await Db.ShiftAssignments.Where(a => ids.Contains(a.EmployeeId) && a.Date >= mon && a.Date < mon.AddDays(7)).ToListAsync();
        ViewBag.Today = await Db.Attendances.Where(a => ids.Contains(a.EmployeeId) && a.Date == DateTime.Today).ToListAsync();
        ViewBag.TodayShifts = await Db.ShiftAssignments.Include(a => a.Shift).Where(a => ids.Contains(a.EmployeeId) && a.Date == DateTime.Today).ToListAsync();
        ViewBag.Leaves = await Db.LeaveRequests.Include(l => l.Employee).Where(l => ids.Contains(l.EmployeeId)).OrderByDescending(l => l.FromDate).Take(40).ToListAsync();
        ViewBag.Payrolls = await Db.Payrolls.Include(p => p.Employee).Where(p => ids.Contains(p.EmployeeId) && p.Month == m && p.Year == y).OrderBy(p => p.Employee.FullName).ToListAsync();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create(Employee e)
    {
        if (string.IsNullOrWhiteSpace(e.FullName)) { Err("Nhập họ tên."); return RedirectToAction(nameof(Index)); }
        e.Id = 0; e.BranchId = BranchId; e.HireDate = DateTime.Today;
        e.Code = "NV" + (await Db.Employees.CountAsync() + 1).ToString("000");
        Db.Employees.Add(e); await Db.SaveChangesAsync();
        Ok("Đã thêm nhân viên " + e.FullName);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Toggle(int id)
    {
        var e = await Db.Employees.FindAsync(id);
        if (e != null && e.BranchId == BranchId) { e.Active = !e.Active; await Db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Assign(int employeeId, DateTime date, int shiftId, DateTime week)
    {
        var a = await Db.ShiftAssignments.FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.Date == date.Date);
        if (shiftId == 0) { if (a != null) Db.ShiftAssignments.Remove(a); }
        else if (a == null) Db.ShiftAssignments.Add(new ShiftAssignment { EmployeeId = employeeId, ShiftId = shiftId, Date = date.Date });
        else a.ShiftId = shiftId;
        await Db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { tab = "schedule", week });
    }

    [HttpPost]
    public async Task<IActionResult> CheckIn(int employeeId)
    {
        if (!await Db.Attendances.AnyAsync(a => a.EmployeeId == employeeId && a.Date == DateTime.Today))
        { Db.Attendances.Add(new Attendance { EmployeeId = employeeId, Date = DateTime.Today, CheckIn = DateTime.Now }); await Db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index), new { tab = "attendance" });
    }

    [HttpPost]
    public async Task<IActionResult> CheckOut(int employeeId)
    {
        var a = await Db.Attendances.FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.Date == DateTime.Today);
        if (a != null && a.CheckOut == null) { a.CheckOut = DateTime.Now; await Db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index), new { tab = "attendance" });
    }

    [HttpPost]
    public async Task<IActionResult> CreateLeave(LeaveRequest l)
    {
        if (l.EmployeeId != 0) { l.Id = 0; l.Status = "Chờ duyệt"; Db.LeaveRequests.Add(l); await Db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index), new { tab = "leave" });
    }

    [HttpPost]
    public async Task<IActionResult> LeaveStatus(int id, string status)
    {
        var l = await Db.LeaveRequests.FindAsync(id);
        if (l != null) { l.Status = status; await Db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index), new { tab = "leave" });
    }

    /// <summary>Tính lương tháng: lương cứng + giờ công × đơn giá giờ − 10,5% bảo hiểm trên lương cứng.</summary>
    [HttpPost]
    public async Task<IActionResult> Payroll(int month, int year)
    {
        var emps = await Db.Employees.Where(e => e.BranchId == BranchId && e.Active).ToListAsync();
        var ids = emps.Select(e => e.Id).ToList();
        var from = new DateTime(year, month, 1); var to = from.AddMonths(1);
        var old = await Db.Payrolls.Where(p => ids.Contains(p.EmployeeId) && p.Month == month && p.Year == year).ToListAsync();
        Db.Payrolls.RemoveRange(old);
        var att = await Db.Attendances.Where(a => ids.Contains(a.EmployeeId) && a.Date >= from && a.Date < to && a.CheckIn != null && a.CheckOut != null).ToListAsync();
        foreach (var e in emps)
        {
            var hours = att.Where(a => a.EmployeeId == e.Id).Sum(a => (a.CheckOut.Value - a.CheckIn.Value).TotalHours);
            var gross = e.BaseSalary + e.HourlyRate * (decimal)hours;
            var ded = Math.Round(e.BaseSalary * 0.105m);
            Db.Payrolls.Add(new Payroll { EmployeeId = e.Id, Month = month, Year = year, Hours = Math.Round(hours, 1), Gross = Math.Round(gross), Deduction = ded, Net = Math.Round(gross) - ded });
        }
        await Db.SaveChangesAsync();
        Ok($"Đã tính lương tháng {month}/{year}");
        return RedirectToAction(nameof(Index), new { tab = "payroll", month, year });
    }
}
