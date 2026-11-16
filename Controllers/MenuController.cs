using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaHang.Data;
using QuanLyNhaHang.Models;
using QuanLyNhaHang.Services;

namespace QuanLyNhaHang.Controllers;

public class MenuController : BaseController
{
    private readonly CostingService _costing;
    public MenuController(AppDbContext db, CostingService costing) : base(db) { _costing = costing; }

    public async Task<IActionResult> Index(string tab = "items")
    {
        ViewBag.Tab = tab;
        ViewBag.Categories = await Db.Categories.OrderBy(c => c.SortOrder).ToListAsync();
        ViewBag.Items = await Db.MenuItems.Include(m => m.Category).Include(m => m.Recipes).OrderBy(m => m.Category.SortOrder).ThenBy(m => m.Name).ToListAsync();
        ViewBag.Combos = await Db.Combos.Include(c => c.Items).ThenInclude(i => i.MenuItem).OrderBy(c => c.Id).ToListAsync();
        ViewBag.Promos = await Db.Promotions.Include(p => p.Category).OrderBy(p => p.Id).ToListAsync();
        ViewBag.Coupons = await Db.Coupons.OrderBy(c => c.Id).ToListAsync();
        var costInfo = new Dictionary<int, CostingService.CostResult>();
        foreach (var item in (List<MenuItem>)ViewBag.Items) costInfo[item.Id] = await _costing.MenuItemCostAsync(item.Id);
        ViewBag.CostInfo = costInfo;
        var comboCostInfo = new Dictionary<int, CostingService.CostResult>();
        foreach (var combo in (List<Combo>)ViewBag.Combos) comboCostInfo[combo.Id] = await _costing.ComboCostAsync(combo.Id);
        ViewBag.ComboCostInfo = comboCostInfo;
        return View();
    }

    // ---------- Món ----------
    [HttpPost]
    public async Task<IActionResult> CreateItem(MenuItem m)
    {
        if (string.IsNullOrWhiteSpace(m.Name) || m.CategoryId == 0) { Err("Nhập tên món và chọn nhóm."); return RedirectToAction(nameof(Index)); }
        m.Id = 0; m.Cost = 0m; m.Icon = string.IsNullOrWhiteSpace(m.Icon) ? "🍽️" : m.Icon;
        Db.MenuItems.Add(m); await Db.SaveChangesAsync();
        Ok("Đã thêm món " + m.Name);
        return RedirectToAction(nameof(Edit), new { id = m.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var m = await Db.MenuItems.Include(x => x.Recipes).ThenInclude(r => r.Ingredient).FirstOrDefaultAsync(x => x.Id == id);
        if (m == null) return RedirectToAction(nameof(Index));
        ViewBag.Categories = await Db.Categories.OrderBy(c => c.SortOrder).ToListAsync();
        ViewBag.Ingredients = await Db.Ingredients.OrderBy(i => i.Name).ToListAsync();
        ViewBag.CostInfo = await _costing.MenuItemCostAsync(m.Id);
        return View(m);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(MenuItem f)
    {
        var m = await Db.MenuItems.FindAsync(f.Id); if (m == null) return RedirectToAction(nameof(Index));
        m.Name = f.Name; m.CategoryId = f.CategoryId; m.Icon = f.Icon; m.ImageUrl = f.ImageUrl; m.Description = f.Description;
        m.Price = f.Price; m.PrepMinutes = f.PrepMinutes; m.Allergens = f.Allergens;
        await Db.SaveChangesAsync();
        var costResult = await _costing.MenuItemCostAsync(m.Id);
        m.Cost = costResult.Complete ? costResult.Cost : 0m;
        await Db.SaveChangesAsync(); Ok(costResult.Complete ? "Đã lưu món và đồng bộ giá vốn từ công thức." : "Đã lưu món; giá vốn chưa đủ dữ liệu công thức/nguyên liệu.");
        return RedirectToAction(nameof(Edit), new { id = m.Id });
    }

    [HttpPost]
    public async Task<IActionResult> Toggle(int id)
    {
        var m = await Db.MenuItems.FindAsync(id);
        if (m != null) { m.Stopped = !m.Stopped; await Db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> AddRecipe(int menuItemId, int ingredientId, decimal qty)
    {
        if (qty > 0)
        {
            var r = await Db.RecipeLines.FirstOrDefaultAsync(x => x.MenuItemId == menuItemId && x.IngredientId == ingredientId);
            if (r == null) Db.RecipeLines.Add(new RecipeLine { MenuItemId = menuItemId, IngredientId = ingredientId, Qty = qty });
            else r.Qty = qty;
            // Giá vốn món = tổng giá nguyên liệu theo công thức
            await Db.SaveChangesAsync();
            var lines = await Db.RecipeLines.Include(x => x.Ingredient).Where(x => x.MenuItemId == menuItemId).ToListAsync();
            var m = await Db.MenuItems.FindAsync(menuItemId);
            var costResult = await _costing.MenuItemCostAsync(menuItemId);
            m.Cost = costResult.Complete ? costResult.Cost : 0m;
            await Db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Edit), new { id = menuItemId });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveRecipe(int id, int menuItemId)
    {
        var r = await Db.RecipeLines.FindAsync(id);
        if (r != null) { Db.RecipeLines.Remove(r); await Db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Edit), new { id = menuItemId });
    }

    [HttpPost]
    public async Task<IActionResult> CreateCategory(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            var max = await Db.Categories.AnyAsync() ? await Db.Categories.MaxAsync(c => c.SortOrder) : 0;
            Db.Categories.Add(new MenuCategory { Name = name.Trim(), SortOrder = max + 1 }); await Db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    // ---------- Combo ----------
    [HttpPost]
    public async Task<IActionResult> CreateCombo(string name, decimal price, int[] itemIds)
    {
        if (string.IsNullOrWhiteSpace(name) || itemIds == null || itemIds.Length == 0) { Err("Nhập tên combo và chọn ít nhất một món."); return RedirectToAction(nameof(Index), new { tab = "combos" }); }
        var c = new Combo { Name = name.Trim(), Price = price };
        foreach (var id in itemIds.Distinct()) c.Items.Add(new ComboItem { MenuItemId = id, Qty = 1 });
        Db.Combos.Add(c); await Db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { tab = "combos" });
    }

    [HttpPost]
    public async Task<IActionResult> ToggleCombo(int id)
    {
        var c = await Db.Combos.FindAsync(id);
        if (c != null) { c.Active = !c.Active; await Db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index), new { tab = "combos" });
    }

    // ---------- Khuyến mãi theo giờ & mã giảm giá ----------
    [HttpPost]
    public async Task<IActionResult> CreatePromo(Promotion p)
    {
        if (string.IsNullOrWhiteSpace(p.Name) || p.Percent <= 0) { Err("Nhập tên và % giảm."); return RedirectToAction(nameof(Index), new { tab = "promos" }); }
        p.Id = 0; if (p.CategoryId == 0) p.CategoryId = null;
        Db.Promotions.Add(p); await Db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { tab = "promos" });
    }

    [HttpPost]
    public async Task<IActionResult> TogglePromo(int id)
    {
        var p = await Db.Promotions.FindAsync(id);
        if (p != null) { p.Active = !p.Active; await Db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index), new { tab = "promos" });
    }

    [HttpPost]
    public async Task<IActionResult> CreateCoupon(Coupon c)
    {
        c.Code = (c.Code ?? "").Trim().ToUpper();
        if (c.Code == "" || (c.Percent <= 0 && c.Amount <= 0)) { Err("Nhập mã và mức giảm."); return RedirectToAction(nameof(Index), new { tab = "coupons" }); }
        if (await Db.Coupons.AnyAsync(x => x.Code == c.Code)) { Err("Mã này đã tồn tại."); return RedirectToAction(nameof(Index), new { tab = "coupons" }); }
        c.Id = 0; Db.Coupons.Add(c); await Db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { tab = "coupons" });
    }

    [HttpPost]
    public async Task<IActionResult> ToggleCoupon(int id)
    {
        var c = await Db.Coupons.FindAsync(id);
        if (c != null) { c.Active = !c.Active; await Db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index), new { tab = "coupons" });
    }
}
