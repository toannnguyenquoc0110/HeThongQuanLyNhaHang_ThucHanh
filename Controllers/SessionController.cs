using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaHang.Data;
using QuanLyNhaHang.Models;

namespace QuanLyNhaHang.Controllers;

[Route("nhan-vien")]
public class SessionController(AppDbContext db, IPasswordHasher<StaffAccount> hasher) : Controller
{
    public class LoginInput
    {
        [Required, EmailAddress] public string Email { get; set; } = "";
        [Required] public string Password { get; set; } = "";
    }

    [HttpGet("dang-nhap")]
    public async Task<IActionResult> Login(string returnUrl = null)
    {
        var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (auth.Succeeded) return RedirectToAction(nameof(Home));
        ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : "";
        return View(new LoginInput());
    }

    [HttpPost("dang-nhap"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginInput input, string returnUrl = null)
    {
        input.Email = input.Email?.Trim().ToLowerInvariant() ?? "";
        ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : "";
        if (!ModelState.IsValid) return View(input);

        var account = await db.StaffAccounts.FirstOrDefaultAsync(x => x.Email.ToLower() == input.Email && x.Active);
        var valid = false;
        if (account != null && !string.IsNullOrWhiteSpace(account.PasswordHash))
        {
            try { valid = hasher.VerifyHashedPassword(account, account.PasswordHash, input.Password) != PasswordVerificationResult.Failed; }
            catch (FormatException) { valid = false; }
        }
        if (!valid)
        {
            ModelState.AddModelError("", "Email hoặc mật khẩu không đúng, hoặc tài khoản đã bị khóa.");
            return View(input);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Name, account.FullName),
            new(ClaimTypes.Email, account.Email),
            new(ClaimTypes.Role, ((int)account.Role).ToString()),
            new("BranchId", account.BranchId.ToString())
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8), AllowRefresh = true });
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Home));
    }

    [HttpGet("trang-chu")]
    public async Task<IActionResult> Home()
    {
        var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!auth.Succeeded) return RedirectToAction(nameof(Login));
        var roleText = auth.Principal?.FindFirst(ClaimTypes.Role)?.Value;
        if (!int.TryParse(roleText, out var roleValue) || !Enum.IsDefined(typeof(Role), roleValue))
            return RedirectToAction(nameof(Login));
        return Redirect("/" + RoleMap.Home((Role)roleValue));
    }

    [HttpGet("tu-choi")]
    public IActionResult AccessDenied() => View();

    [HttpPost("dang-xuat"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    // Endpoint cũ chỉ được giữ để không làm hỏng URL bookmark; không cho phép tự đổi role bằng URL.
    [HttpGet("/Session/Role/{id:int}")]
    public IActionResult Role(int id, string back = null) => RedirectToAction(nameof(Login));

    [HttpGet("/Session/Branch/{id:int}")]
    public async Task<IActionResult> Branch(int id, string back = null)
    {
        var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!auth.Succeeded) return RedirectToAction(nameof(Login));
        var branch = await db.Branches.AnyAsync(x => x.Id == id);
        if (branch) Response.Cookies.Append("branch", id.ToString(), new CookieOptions { Expires = DateTimeOffset.Now.AddYears(1), HttpOnly = true, SameSite = SameSiteMode.Lax });
        return LocalRedirect(Url.IsLocalUrl(back) ? back : "/");
    }
}
