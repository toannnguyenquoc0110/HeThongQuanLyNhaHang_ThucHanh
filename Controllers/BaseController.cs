using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaHang.Data;
using QuanLyNhaHang.Models;

namespace QuanLyNhaHang.Controllers;

/// <summary>
/// Phần nền của hệ thống nội bộ. Cookie khách hàng được tách khỏi hệ thống nhân viên;
/// nếu phiên khách hàng đang hoạt động thì không cho đi vào các controller quản trị.
/// Nhân viên phải đăng nhập bằng tài khoản thật; vai trò lấy từ claim được ký trong cookie xác thực.
/// </summary>
public abstract class BaseController : Controller
{
    protected readonly AppDbContext Db;
    protected int BranchId;
    protected Role CurrentRole;
    protected BaseController(AppDbContext db) { Db = db; }

    public override async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
    {
        var customerAuth = await ctx.HttpContext.AuthenticateAsync("CustomerCookie");
        if (customerAuth.Succeeded)
        {
            ctx.Result = new RedirectResult("/khach-hang");
            return;
        }

        var staffAuth = await ctx.HttpContext.AuthenticateAsync();
        if (!staffAuth.Succeeded)
        {
            ctx.Result = new RedirectResult("/nhan-vien/dang-nhap?returnUrl=" + Uri.EscapeDataString(ctx.HttpContext.Request.Path + ctx.HttpContext.Request.QueryString));
            return;
        }

        var principal = staffAuth.Principal;
        var roleText = principal?.FindFirst(ClaimTypes.Role)?.Value;
        if (!int.TryParse(roleText, out var roleValue) || !Enum.IsDefined(typeof(Role), roleValue))
        {
            ctx.Result = new RedirectResult("/nhan-vien/dang-nhap");
            return;
        }
        CurrentRole = (Role)roleValue;
        var claimBranch = principal?.FindFirst("BranchId")?.Value;
        var ck = Request.Cookies;
        BranchId = int.TryParse(ck["branch"], out var b) ? b : (int.TryParse(claimBranch, out var cb) ? cb : 0);

        var branches = await Db.Branches.OrderBy(x => x.Id).ToListAsync();
        var cur = branches.FirstOrDefault(x => x.Id == BranchId) ?? branches.FirstOrDefault();
        BranchId = cur?.Id ?? 0;
        if (CurrentRole != Role.QuanLy && int.TryParse(claimBranch, out var assignedBranch) && assignedBranch > 0)
        {
            BranchId = assignedBranch;
            cur = branches.FirstOrDefault(x => x.Id == BranchId) ?? cur;
        }
        ViewBag.Branches = branches; ViewBag.Branch = cur; ViewBag.Role = CurrentRole;

        var ctrl = (string)ctx.RouteData.Values["controller"];
        ViewBag.Ctrl = ctrl;
        if (!RoleMap.Can(CurrentRole, ctrl))
        {
            ctx.Result = RedirectToAction("Index", RoleMap.Home(CurrentRole));
            return;
        }
        await next();
    }

    protected string Actor => RoleMap.Name(CurrentRole);
    protected void Ok(string msg) => TempData["ok"] = msg;
    protected void Err(string msg) => TempData["err"] = msg;
}
