
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

// Dùng route riêng để không trùng SessionController.
[Route("staff-auth")]
public class StaffAuthController(
    AppDbContext db,
    IPasswordHasher<StaffAccount> hasher) : Controller
{
    public class LoginInput
    {
        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required, StringLength(100, MinimumLength = 8)]
        public string Password { get; set; } = "";
    }

    [HttpGet("dang-nhap")]
    public IActionResult Login(string returnUrl = null)
    {
        ViewBag.ReturnUrl = SafeLocal(returnUrl);
        return View(new LoginInput());
    }

    [HttpPost("dang-nhap")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        LoginInput input,
        string returnUrl = null)
    {
        input.Email = input.Email?.Trim().ToLowerInvariant() ?? "";
        ViewBag.ReturnUrl = SafeLocal(returnUrl);

        if (!ModelState.IsValid)
            return View(input);

        var account = await db.StaffAccounts
            .SingleOrDefaultAsync(x =>
                x.Email.ToLower() == input.Email && x.Active);

        if (account == null ||
            !Enum.IsDefined(typeof(Role), account.Role))
        {
            ModelState.AddModelError(
                "",
                "Email hoặc mật khẩu không chính xác, hoặc tài khoản đã bị khóa.");
            return View(input);
        }

        bool validPassword;

        try
        {
            validPassword =
                !string.IsNullOrWhiteSpace(account.PasswordHash) &&
                hasher.VerifyHashedPassword(
                    account,
                    account.PasswordHash,
                    input.Password) != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            validPassword = false;
        }

        if (!validPassword)
        {
            ModelState.AddModelError(
                "",
                "Email hoặc mật khẩu không chính xác, hoặc tài khoản đã bị khóa.");
            return View(input);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Name, account.FullName),
            new(ClaimTypes.Email, account.Email),
            new(ClaimTypes.Role, account.Role.ToString()),
            new("BranchId", account.BranchId.ToString())
        };

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
                AllowRefresh = true
            });

        var safeReturnUrl = SafeLocal(returnUrl);

        return LocalRedirect(
            safeReturnUrl ?? "/" + RoleMap.Home(account.Role));
    }

    [HttpPost("dang-xuat")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        return RedirectToAction(nameof(Login));
    }

    [HttpGet("tu-choi")]
    public IActionResult Denied()
    {
        return View();
    }

    private string SafeLocal(string url)
    {
        return !string.IsNullOrWhiteSpace(url) &&
               Url.IsLocalUrl(url)
            ? url
            : null;
    }
}
