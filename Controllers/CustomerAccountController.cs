using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaHang.Data;
using QuanLyNhaHang.Models;
using System.Security.Claims;

namespace QuanLyNhaHang.Controllers;

public class CustomerAccountController : Controller
{
    private readonly AppDbContext _context;
    private readonly PasswordHasher<Customer> _hasher = new();

    public CustomerAccountController(AppDbContext context) => _context = context;

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string phone, string password)
    {
        phone = (phone ?? string.Empty).Trim();
        if (phone.Length == 0 || string.IsNullOrWhiteSpace(password))
        {
            ViewBag.Error = "Vui lòng nhập số điện thoại và mật khẩu.";
            return View();
        }

        var customer = await _context.Customers.SingleOrDefaultAsync(c => c.Phone == phone);
        if (customer == null || !VerifyPassword(customer, password))
        {
            ViewBag.Error = "Số điện thoại hoặc mật khẩu không chính xác.";
            return View();
        }

        // Nếu tài khoản cũ dùng mật khẩu thuần, nâng cấp sang hash sau lần đăng nhập hợp lệ.
        if (!LooksHashed(customer.Password))
        {
            customer.Password = _hasher.HashPassword(customer, password);
            await _context.SaveChangesAsync();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, customer.FullName ?? "Khách hàng"),
            new(ClaimTypes.NameIdentifier, customer.Id.ToString()),
            new(ClaimTypes.Role, "Customer"),
            new("UserId", customer.Id.ToString()),
            new("Phone", customer.Phone ?? string.Empty),
            new("BranchId", customer.BranchId?.ToString() ?? string.Empty)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return RedirectToAction("Menu", "Customer");
    }

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string fullName, string phone, string password, string address)
    {
        fullName = (fullName ?? string.Empty).Trim();
        phone = (phone ?? string.Empty).Trim();
        if (fullName.Length < 2 || phone.Length < 9 || phone.Length > 15 || string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            ViewBag.Error = "Vui lòng nhập họ tên, số điện thoại hợp lệ và mật khẩu tối thiểu 8 ký tự.";
            return View();
        }
        if (await _context.Customers.AnyAsync(c => c.Phone == phone))
        {
            ViewBag.Error = "Số điện thoại này đã được đăng ký.";
            return View();
        }
        var branchId = await _context.Branches.OrderBy(b => b.Id).Select(b => (int?)b.Id).FirstOrDefaultAsync();
        if (branchId == null)
        {
            ViewBag.Error = "Nhà hàng chưa có chi nhánh để nhận đơn. Vui lòng thử lại sau.";
            return View();
        }
        var customer = new Customer { FullName = fullName, Phone = phone, Address = (address ?? string.Empty).Trim(), BranchId = branchId };
        customer.Password = _hasher.HashPassword(customer, password);
        _context.Customers.Add(customer);
        try { await _context.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            ViewBag.Error = "Không thể tạo tài khoản. Số điện thoại có thể đã được đăng ký.";
            return View();
        }
        TempData["SuccessMessage"] = "Đăng ký thành công. Vui lòng đăng nhập.";
        return RedirectToAction(nameof(Login));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private bool VerifyPassword(Customer customer, string supplied)
    {
        if (string.IsNullOrEmpty(customer.Password)) return false;
        if (!LooksHashed(customer.Password)) return customer.Password == supplied; // tương thích tài khoản cũ
        return _hasher.VerifyHashedPassword(customer, customer.Password, supplied) != PasswordVerificationResult.Failed;
    }

    private static bool LooksHashed(string value) => !string.IsNullOrEmpty(value) && value.StartsWith("AQAAAA", StringComparison.Ordinal);
}
