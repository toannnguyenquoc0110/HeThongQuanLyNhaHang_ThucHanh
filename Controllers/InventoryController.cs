using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaHang.Data;
using QuanLyNhaHang.Models;

namespace QuanLyNhaHang.Controllers;

public class InventoryController : BaseController
{
    public InventoryController(AppDbContext db) : base(db) { }

    public async Task<IActionResult> Index(string tab = "stock")
    {
        ViewBag.Tab = tab;
        ViewBag.Stocks = await Db.BranchStocks.Include(s => s.Ingredient).Where(s => s.BranchId == BranchId).OrderBy(s => s.Ingredient.Name).ToListAsync();
        ViewBag.Ingredients = await Db.Ingredients.OrderBy(i => i.Name).ToListAsync();
        ViewBag.Suppliers = await Db.Suppliers.OrderBy(s => s.Name).ToListAsync();
        ViewBag.Orders = await Db.PurchaseOrders.Include(p => p.Supplier).Include(p => p.Lines).ThenInclude(l => l.Ingredient)
            .Where(p => p.BranchId == BranchId).OrderByDescending(p => p.Date).ThenByDescending(p => p.Id).Take(30).ToListAsync();
        ViewBag.Moves = await Db.StockMovements.Include(m => m.Ingredient).Where(m => m.BranchId == BranchId).OrderByDescending(m => m.Date).Take(60).ToListAsync();
        var from = DateTime.Today.AddDays(-29);
        var recentMoves = await Db.StockMovements.Include(m => m.Ingredient).Where(m => m.BranchId == BranchId && m.Date >= from).ToListAsync();
        ViewBag.AutoConsumption = recentMoves.Where(m => m.Type == "Xuất bán").GroupBy(m => new { m.Ingredient.Name, m.Ingredient.Unit })
            .Select(g => (Name: g.Key.Name, Unit: g.Key.Unit, Qty: -g.Sum(m => m.Qty))).OrderByDescending(x => x.Qty).ToList();
        ViewBag.RecordedWaste = recentMoves.Where(m => m.Type == "Hao hụt").GroupBy(m => new { m.Ingredient.Name, m.Ingredient.Unit })
            .Select(g => (Name: g.Key.Name, Unit: g.Key.Unit, Qty: -g.Sum(m => m.Qty))).OrderByDescending(x => x.Qty).ToList();
        ViewBag.InventoryPeriodFrom = from;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> CreateIngredient(string name, string unit, decimal minLevel, decimal cost)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(unit) || minLevel < 0 || cost < 0) { Err("Nhập tên, đơn vị và giá/mức tồn không âm hợp lệ."); return RedirectToAction(nameof(Index)); }
        if (await Db.Ingredients.AnyAsync(x => x.Name.ToLower() == name.Trim().ToLower())) { Err("Tên nguyên liệu đã tồn tại."); return RedirectToAction(nameof(Index)); }
        var ing = new Ingredient { Name = name.Trim(), Unit = unit.Trim(), MinLevel = minLevel, Cost = cost };
        Db.Ingredients.Add(ing); await Db.SaveChangesAsync();
        foreach (var b in await Db.Branches.ToListAsync()) Db.BranchStocks.Add(new BranchStock { BranchId = b.Id, IngredientId = ing.Id, Quantity = 0 });
        await Db.SaveChangesAsync();
        Ok("Đã thêm nguyên liệu " + ing.Name);
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Kiểm kê (đặt lại số lượng thực tế) hoặc ghi hao hụt (trừ bớt).</summary>
    [HttpPost]
    public async Task<IActionResult> Adjust(int ingredientId, decimal qty, string type, string note)
    {
        var s = await Db.BranchStocks.FirstOrDefaultAsync(x => x.BranchId == BranchId && x.IngredientId == ingredientId);
        if (s == null) return RedirectToAction(nameof(Index));
        if (qty < 0) { Err("Số lượng kiểm kê/hao hụt không được âm."); return RedirectToAction(nameof(Index)); }
        decimal delta;
        if (type == "Hao hụt") { delta = -Math.Min(qty, s.Quantity); s.Quantity += delta; }
        else { delta = qty - s.Quantity; s.Quantity = qty; type = "Kiểm kê"; }
        Db.StockMovements.Add(new StockMovement { BranchId = BranchId, IngredientId = ingredientId, Qty = delta, Type = type, Note = note, Ref = Actor });
        await Db.SaveChangesAsync();
        Ok("Đã ghi " + type.ToLower());
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> CreateSupplier(Supplier s)
    {
        if (!string.IsNullOrWhiteSpace(s.Name)) { s.Id = 0; Db.Suppliers.Add(s); await Db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index), new { tab = "suppliers" });
    }

    [HttpPost]
    public async Task<IActionResult> CreatePurchase(int supplierId, string note, int[] ingredientId, decimal[] qty, decimal[] unitCost, DateTime?[] expiry)
    {
        var po = new PurchaseOrder { BranchId = BranchId, SupplierId = supplierId, Date = DateTime.Today, Note = note };
        if (ingredientId == null || qty == null || unitCost == null) { Err("Dữ liệu đơn nhập không hợp lệ."); return RedirectToAction(nameof(Index), new { tab = "purchase" }); }
        for (var i = 0; i < ingredientId.Length; i++)
        {
            if (ingredientId[i] == 0 || i >= qty.Length || qty[i] <= 0) continue;
            var price = i < unitCost.Length ? unitCost[i] : 0;
            if (price < 0 || !await Db.Ingredients.AnyAsync(x => x.Id == ingredientId[i])) { Err("Nguyên liệu hoặc đơn giá không hợp lệ."); return RedirectToAction(nameof(Index), new { tab = "purchase" }); }
            po.Lines.Add(new PurchaseLine { IngredientId = ingredientId[i], Qty = qty[i], UnitCost = price, ExpiryDate = i < expiry.Length ? expiry[i] : null });
        }
        if (po.Lines.Count == 0 || supplierId == 0) { Err("Chọn nhà cung cấp và ít nhất một nguyên liệu."); return RedirectToAction(nameof(Index), new { tab = "purchase" }); }
        po.Code = "PO" + DateTime.Now.ToString("yyMMddHHmm");
        Db.PurchaseOrders.Add(po); await Db.SaveChangesAsync();
        Ok("Đã tạo đơn nhập " + po.Code);
        return RedirectToAction(nameof(Index), new { tab = "purchase" });
    }

    [HttpPost]
    public async Task<IActionResult> Receive(int id)
    {
        var po = await Db.PurchaseOrders.Include(p => p.Lines).FirstOrDefaultAsync(p => p.Id == id && p.BranchId == BranchId);
        if (po == null || po.Status == "Đã nhận") return RedirectToAction(nameof(Index), new { tab = "purchase" });
        foreach (var l in po.Lines)
        {
            var s = await Db.BranchStocks.FirstOrDefaultAsync(x => x.BranchId == BranchId && x.IngredientId == l.IngredientId);
            if (s == null) { s = new BranchStock { BranchId = BranchId, IngredientId = l.IngredientId }; Db.BranchStocks.Add(s); }
            s.Quantity += l.Qty;
            if (l.ExpiryDate != null) s.ExpiryDate = l.ExpiryDate;
            var ing = await Db.Ingredients.FindAsync(l.IngredientId);
            if (ing != null && l.UnitCost > 0) ing.Cost = l.UnitCost;
            Db.StockMovements.Add(new StockMovement { BranchId = BranchId, IngredientId = l.IngredientId, Qty = l.Qty, Type = "Nhập", Ref = po.Code });
        }
        po.Status = "Đã nhận";
        // Ghi nhận chi phí mua hàng
        Db.Expenses.Add(new Expense { BranchId = BranchId, Date = DateTime.Today, Category = "Nguyên liệu", Amount = po.Lines.Sum(l => l.Qty * l.UnitCost), Note = "Đơn nhập " + po.Code });
        await Db.SaveChangesAsync();
        Ok("Đã nhập kho đơn " + po.Code);
        return RedirectToAction(nameof(Index), new { tab = "purchase" });
    }
}
