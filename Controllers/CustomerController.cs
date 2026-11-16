using System.Security.Claims;
using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using QuanLyNhaHang.Data;
using QuanLyNhaHang.Models;
using QuanLyNhaHang.Services;

namespace QuanLyNhaHang.Controllers;

[Route("khach-hang")]
public class CustomerController(AppDbContext db, IPasswordHasher<CustomerAccount> hasher, OrderService orderService) : Controller
{
    private const string CartKey = "customer-cart-v1";
    public record CartItem(int MenuItemId, int Qty);
    public class RegisterInput
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Họ tên phải có từ 2 đến 120 ký tự.")]
        public string FullName { get; set; } = "";

        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
        [StringLength(180, ErrorMessage = "Email quá dài.")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Vui lòng tạo mật khẩu.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự.")]
        public string Password { get; set; } = "";

        [Phone(ErrorMessage = "Số điện thoại không đúng định dạng.")]
        [StringLength(30)]
        public string Phone { get; set; } = "";

        [StringLength(500, ErrorMessage = "Địa chỉ tối đa 500 ký tự.")]
        public string Address { get; set; } = "";
    }

    public class LoginInput
    {
        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
        public string Password { get; set; } = "";
    }
    public record CheckoutInput(int BranchId, string FulfillmentType, string Address, string Phone, string DriverNote, string PaymentMethod, string CouponCode);
    public record ProfileInput(string FullName, string Phone, string DefaultAddress);

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewBag.Combos = await db.Combos.Include(x => x.Items).ThenInclude(x => x.MenuItem).Where(x => x.Active).OrderBy(x => x.Name).ToListAsync();
        ViewBag.Promotions = await db.Promotions.Include(x => x.Category).Where(x => x.Active).OrderBy(x => x.Name).ToListAsync();
        ViewBag.CartCount = ReadCart().Sum(x => x.Qty);
        return View(await db.MenuItems.Include(x => x.Category).Where(x => !x.Stopped).OrderBy(x => x.Category.SortOrder).ThenBy(x => x.Name).ToListAsync());
    }

    [HttpGet("dang-ky")]
    public async Task<IActionResult> Register()
    {
        if (await CustomerSessionExistsAsync()) return RedirectToAction(nameof(Index));
        return View(new RegisterInput());
    }

    [HttpPost("dang-ky"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterInput input)
    {
        input.FullName = input.FullName?.Trim() ?? "";
        input.Email = input.Email?.Trim().ToLowerInvariant() ?? "";
        input.Phone = input.Phone?.Trim() ?? "";
        input.Address = input.Address?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(input.FullName)) ModelState.AddModelError(nameof(input.FullName), "Vui lòng nhập họ tên.");
        if (!ModelState.IsValid) return View(input);

        if (await db.CustomerAccounts.AnyAsync(x => x.Email == input.Email))
        {
            ModelState.AddModelError(nameof(input.Email), "Email này đã được đăng ký. Hãy đăng nhập hoặc sử dụng email khác.");
            return View(input);
        }

        var account = new CustomerAccount
        {
            FullName = input.FullName,
            Email = input.Email,
            Phone = input.Phone,
            DefaultAddress = input.Address,
            Active = true,
            CreatedAt = DateTime.Now
        };
        account.PasswordHash = hasher.HashPassword(account, input.Password);
        db.CustomerAccounts.Add(account);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Chỉ mục unique trong database xử lý trường hợp hai lượt đăng ký cùng email đồng thời.
            db.Entry(account).State = EntityState.Detached;
            if (await db.CustomerAccounts.AnyAsync(x => x.Email == input.Email))
            {
                ModelState.AddModelError(nameof(input.Email), "Email này vừa được đăng ký. Hãy đăng nhập hoặc sử dụng email khác.");
                return View(input);
            }
            throw;
        }

        await SignIn(account);
        TempData["CustomerWelcome"] = $"Chào mừng {account.FullName}! Tài khoản của bạn đã được tạo.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("dang-nhap")]
    public async Task<IActionResult> Login(string returnUrl = null)
    {
        if (await CustomerSessionExistsAsync()) return RedirectToAction(nameof(Index));
        ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : "";
        return View(new LoginInput());
    }

    [HttpPost("dang-nhap"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginInput input, string returnUrl = null)
    {
        input.Email = input.Email?.Trim().ToLowerInvariant() ?? "";
        ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : "";
        if (!ModelState.IsValid) return View(input);

        // Chuẩn hóa email trong truy vấn để tránh khác biệt chữ hoa/chữ thường giữa provider.
        var account = await db.CustomerAccounts.FirstOrDefaultAsync(x => x.Email.ToLower() == input.Email && x.Active);
        var validPassword = false;
        if (account != null && !string.IsNullOrWhiteSpace(account.PasswordHash) && !string.IsNullOrEmpty(input.Password))
        {
            try
            {
                validPassword = hasher.VerifyHashedPassword(account, account.PasswordHash, input.Password) != PasswordVerificationResult.Failed;
            }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentException || ex is InvalidOperationException)
            {
                validPassword = false;
            }
        }
        if (!validPassword)
        {
            ModelState.AddModelError("", "Email hoặc mật khẩu không chính xác. Vui lòng kiểm tra và thử lại.");
            return View(input);
        }

        await SignIn(account);
        TempData["CustomerWelcome"] = $"Xin chào {account.FullName}, bạn đã đăng nhập thành công.";
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Index));
    }

    async Task<bool> CustomerSessionExistsAsync()
    {
        var result = await HttpContext.AuthenticateAsync("CustomerCookie");
        return result.Succeeded && result.Principal?.IsInRole("Customer") == true;
    }

    [HttpPost("dang-xuat"), ValidateAntiForgeryToken] public async Task<IActionResult> Logout() { await HttpContext.SignOutAsync("CustomerCookie"); return RedirectToAction(nameof(Index)); }

    [HttpGet("tai-khoan"), Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = "CustomerCookie")]
    public async Task<IActionResult> Profile()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var customer = await db.CustomerAccounts.FirstOrDefaultAsync(x => x.Id == id && x.Active);
        return customer == null ? Challenge("CustomerCookie") : View(customer);
    }

    [HttpPost("tai-khoan"), ValidateAntiForgeryToken, Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = "CustomerCookie")]
    public async Task<IActionResult> Profile(ProfileInput input)
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var customer = await db.CustomerAccounts.FirstOrDefaultAsync(x => x.Id == id && x.Active);
        if (customer == null) return NotFound();
        if (string.IsNullOrWhiteSpace(input.FullName)) ModelState.AddModelError(nameof(input.FullName), "Vui lòng nhập họ tên.");
        if (input.FullName?.Trim().Length > 120) ModelState.AddModelError(nameof(input.FullName), "Họ tên tối đa 120 ký tự.");
        if (input.Phone?.Trim().Length > 30) ModelState.AddModelError(nameof(input.Phone), "Số điện thoại tối đa 30 ký tự.");
        if (input.DefaultAddress?.Trim().Length > 500) ModelState.AddModelError(nameof(input.DefaultAddress), "Địa chỉ tối đa 500 ký tự.");
        if (!ModelState.IsValid) return View(customer);
        customer.FullName = input.FullName.Trim();
        customer.Phone = input.Phone?.Trim();
        customer.DefaultAddress = input.DefaultAddress?.Trim();
        await db.SaveChangesAsync();
        TempData["ProfileMessage"] = "Đã cập nhật thông tin tài khoản.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost("gio-hang/them"), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart(int menuItemId, int qty = 1)
    {
        if (!await db.MenuItems.AnyAsync(x => x.Id == menuItemId && !x.Stopped)) return BadRequest("Món không khả dụng.");
        var cart = ReadCart(); var existing = cart.FirstOrDefault(x => x.MenuItemId == menuItemId);
        if (existing != null) cart[cart.IndexOf(existing)] = existing with { Qty = Math.Clamp(existing.Qty + qty, 1, 30) };
        else cart.Add(new(menuItemId, Math.Clamp(qty, 1, 30)));
        HttpContext.Session.SetString(CartKey, JsonSerializer.Serialize(cart));
        if (IsAjaxRequest()) return Json(new { success = true, count = cart.Sum(x => x.Qty), message = "Đã thêm món vào giỏ hàng." });
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("gio-hang/them-combo"), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComboToCart(int comboId, int qty = 1)
    {
        if (!await db.Combos.AnyAsync(x => x.Id == comboId && x.Active)) return BadRequest("Combo không khả dụng.");
        var cart = ReadCart(); var key = -comboId; var existing = cart.FirstOrDefault(x => x.MenuItemId == key);
        if (existing != null) cart[cart.IndexOf(existing)] = existing with { Qty = Math.Clamp(existing.Qty + qty, 1, 30) };
        else cart.Add(new(key, Math.Clamp(qty, 1, 30)));
        HttpContext.Session.SetString(CartKey, JsonSerializer.Serialize(cart));
        if (IsAjaxRequest()) return Json(new { success = true, count = cart.Sum(x => x.Qty), message = "Đã thêm combo vào giỏ hàng." });
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("gio-hang")] public async Task<IActionResult> Cart()
    {
        var cart = ReadCart();
        var ids = cart.Where(x => x.MenuItemId > 0).Select(x => x.MenuItemId).ToList();
        var comboIds = cart.Where(x => x.MenuItemId < 0).Select(x => -x.MenuItemId).ToList();
        ViewBag.Cart = cart;
        ViewBag.CartCombos = await db.Combos.Include(x => x.Items).ThenInclude(x => x.MenuItem).Where(x => comboIds.Contains(x.Id)).ToListAsync();
        return View(await db.MenuItems.Where(x => ids.Contains(x.Id)).ToListAsync());
    }

    [HttpPost("gio-hang/cap-nhat"), ValidateAntiForgeryToken]
    public IActionResult UpdateCart(int menuItemId, int qty)
    {
        var cart = ReadCart(); var item = cart.FirstOrDefault(x => x.MenuItemId == menuItemId);
        if (item != null) { if (qty <= 0) cart.Remove(item); else cart[cart.IndexOf(item)] = item with { Qty = Math.Clamp(qty, 1, 30) }; }
        HttpContext.Session.SetString(CartKey, JsonSerializer.Serialize(cart)); return RedirectToAction(nameof(Cart));
    }

    [HttpGet("thanh-toan"), Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = "CustomerCookie")]
    public async Task<IActionResult> Checkout()
    {
        ViewBag.Branches = await db.Branches.OrderBy(x => x.Name).ToListAsync();
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        ViewBag.Customer = await db.CustomerAccounts.FindAsync(id);
        return View();
    }

    [HttpPost("thanh-toan"), ValidateAntiForgeryToken, Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = "CustomerCookie")]
    public async Task<IActionResult> Checkout(CheckoutInput input)
    {
        var cart = ReadCart(); if (cart.Count == 0) return RedirectToAction(nameof(Cart));
        var branch = await db.Branches.FindAsync(input.BranchId); if (branch == null) { ModelState.AddModelError("", "Chi nhánh không hợp lệ."); return await Checkout(); }
        var customerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var customer = await db.CustomerAccounts.FirstOrDefaultAsync(x => x.Id == customerId && x.Active);
        if (customer == null) return Challenge("CustomerCookie");
        var ids = cart.Where(x => x.MenuItemId > 0).Select(x => x.MenuItemId).Distinct().ToList();
        var comboIds = cart.Where(x => x.MenuItemId < 0).Select(x => -x.MenuItemId).Distinct().ToList();
        // Giữ transaction cho bước kiểm tra tồn + ghi đơn để giảm nguy cơ hai checkout cùng tiêu thụ một lượng tồn.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var menu = await db.MenuItems.Include(x => x.Recipes).ThenInclude(x => x.Ingredient).Where(x => ids.Contains(x.Id)).ToListAsync();
        var combos = await db.Combos.Include(x => x.Items).ThenInclude(x => x.MenuItem).ThenInclude(x => x.Recipes).ThenInclude(x => x.Ingredient).Where(x => comboIds.Contains(x.Id) && x.Active).ToListAsync();
        if (menu.Count != ids.Count || menu.Any(x => x.Stopped) || combos.Count != comboIds.Count || combos.Any(x => x.Items.Count == 0)) { ModelState.AddModelError("", "Có món/combo vừa hết hàng, không còn bán hoặc thiếu thành phần. Vui lòng cập nhật giỏ hàng."); return await Checkout(); }
        var required = new Dictionary<int, decimal>();
        foreach (var item in cart)
        {
            var parts = new List<(MenuItem Item, int Qty)>();
            if (item.MenuItemId > 0) parts.Add((menu.Single(x => x.Id == item.MenuItemId), item.Qty));
            else
            {
                var combo = combos.Single(x => x.Id == -item.MenuItemId);
                parts.AddRange(combo.Items.Select(x => (x.MenuItem, x.Qty * item.Qty)));
            }
            foreach (var part in parts)
            {
                if (part.Item.Stopped) { ModelState.AddModelError("", $"{part.Item.Name} hiện tạm ngừng bán."); return await Checkout(); }
                if (part.Item.Recipes.Count == 0) { ModelState.AddModelError("", $"{part.Item.Name} chưa có công thức nguyên liệu."); return await Checkout(); }
                if (part.Item.Recipes.Any(r => r.Qty <= 0 || r.Ingredient == null || r.Ingredient.Cost <= 0)) { ModelState.AddModelError("", $"{part.Item.Name} có công thức hoặc giá nguyên liệu chưa hợp lệ."); return await Checkout(); }
                foreach (var recipe in part.Item.Recipes)
                    required[recipe.IngredientId] = required.GetValueOrDefault(recipe.IngredientId) + recipe.Qty * part.Qty;
            }
        }
        foreach (var pair in required)
        {
            var ingredient = await db.Ingredients.FindAsync(pair.Key);
            var stock = await db.BranchStocks.Where(x => x.BranchId == branch.Id && x.IngredientId == pair.Key).Select(x => (decimal?)x.Quantity).FirstOrDefaultAsync() ?? 0;
            if (stock < pair.Value) { ModelState.AddModelError("", $"Không đủ nguyên liệu {ingredient?.Name ?? ("ID " + pair.Key)}: cần {pair.Value}, hiện có {stock}."); return await Checkout(); }
        }
        var selectedPayment = input.PaymentMethod == "ONLINE" ? "ONLINE" : "COD";
        var order = new Order { BranchId = branch.Id, CustomerAccountId = customerId, CustomerName = customer.FullName, Type = "Online", FulfillmentType = input.FulfillmentType == "Tự đến lấy" ? "Tự đến lấy" : "Giao tận nhà", DeliveryAddress = input.FulfillmentType == "Tự đến lấy" ? "Tự đến lấy tại chi nhánh" : input.Address?.Trim(), DeliveryPhone = input.Phone?.Trim(), DriverNote = input.DriverNote?.Trim(), PaymentMethod = selectedPayment, PaymentStatus = "Chưa thanh toán", Note = "Đơn đặt online", Status = St.New, OpenedAt = DateTime.Now };
        if (order.FulfillmentType == "Giao tận nhà" && (string.IsNullOrWhiteSpace(order.DeliveryAddress) || string.IsNullOrWhiteSpace(order.DeliveryPhone))) { ModelState.AddModelError("", "Cần nhập địa chỉ giao hàng và số điện thoại."); return await Checkout(); }
        foreach (var c in cart)
        {
            if (c.MenuItemId > 0)
            {
                var m = menu.Single(x => x.Id == c.MenuItemId);
                var cost = m.Recipes.Sum(x => x.Qty * x.Ingredient.Cost);
                var promo = await orderService.BestPromoAsync(m.CategoryId);
                var salePrice = Math.Round(m.Price * (100 - promo) / 100m);
                order.Lines.Add(new OrderLine { MenuItemId = m.Id, Name = m.Name, Qty = c.Qty, Price = salePrice, Cost = cost, PrepMinutes = m.PrepMinutes, Status = St.New });
            }
            else
            {
                var combo = combos.Single(x => x.Id == -c.MenuItemId);
                var cost = combo.Items.Sum(i => i.MenuItem.Recipes.Sum(r => r.Qty * r.Ingredient.Cost) * i.Qty);
                order.Lines.Add(new OrderLine { ComboId = combo.Id, Name = combo.Name, Qty = c.Qty, Price = combo.Price, Cost = cost, PrepMinutes = combo.Items.Max(i => i.MenuItem.PrepMinutes), Status = St.New });
            }
        }
        var coupon = string.IsNullOrWhiteSpace(input.CouponCode) ? null : await db.Coupons.FirstOrDefaultAsync(x => x.Code == input.CouponCode.Trim().ToUpper() && x.Active && (x.ExpireDate == null || x.ExpireDate >= DateTime.Today));
        var subtotal = order.Lines.Sum(x => x.Price * x.Qty);
        if (!string.IsNullOrWhiteSpace(input.CouponCode) && (coupon == null || subtotal < coupon.MinTotal))
            ModelState.AddModelError("", "Mã giảm giá không hợp lệ, hết hạn hoặc chưa đạt giá trị đơn tối thiểu.");
        if (!ModelState.IsValid) return await Checkout();
        if (coupon != null) order.CouponId = coupon.Id;
        db.Orders.Add(order); await db.SaveChangesAsync();
        await tx.CommitAsync();
        HttpContext.Session.Remove(CartKey);
        // COD chỉ là phương thức dự kiến; thanh toán online phải có tích hợp cổng và xác minh webhook riêng.
        TempData["OrderMessage"] = selectedPayment == "COD" ? "Đơn đã gửi nhà hàng. Thanh toán khi nhận hàng; hiện chưa ghi nhận đã thu tiền." : "Đơn đã gửi nhà hàng. Chưa ghi nhận thanh toán online; không có giao dịch nào được đánh dấu thành công. Nhà hàng cần cấu hình cổng thanh toán/QR và xác minh giao dịch trước khi thu tiền.";
        return RedirectToAction(nameof(OrderDetail), new { id = order.Id });
    }

    [HttpGet("don-hang/{id:int}"), Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = "CustomerCookie")]
    public async Task<IActionResult> OrderDetail(int id)
    {
        var customerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var order = await db.Orders.Include(x => x.Lines).Include(x => x.Branch).Include(x => x.Coupon).FirstOrDefaultAsync(x => x.Id == id && x.CustomerAccountId == customerId);
        if (order == null) return NotFound();
        ViewBag.EstimatedTotal = orderService.Calc(order).Total;
        return View(order);
    }

    [HttpGet("lich-su"), Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = "CustomerCookie")]
    public async Task<IActionResult> History()
    {
        var customerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return View(await db.Orders.Include(x => x.Branch).Where(x => x.CustomerAccountId == customerId).OrderByDescending(x => x.OpenedAt).ToListAsync());
    }

    async Task SignIn(CustomerAccount a)
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, a.Id.ToString()), new Claim(ClaimTypes.Name, a.FullName), new Claim(ClaimTypes.Email, a.Email), new Claim(ClaimTypes.Role, "Customer") }, "CustomerCookie");
        await HttpContext.SignInAsync("CustomerCookie", new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14) });
    }
    private bool IsAjaxRequest() => string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

    List<CartItem> ReadCart() { try { return JsonSerializer.Deserialize<List<CartItem>>(HttpContext.Session.GetString(CartKey) ?? "[]") ?? new(); } catch { return new(); } }
}
