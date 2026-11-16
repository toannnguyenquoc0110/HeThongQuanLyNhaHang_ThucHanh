using Microsoft.EntityFrameworkCore;
using QuanLyNhaHang.Data;
using QuanLyNhaHang.Helpers;
using QuanLyNhaHang.Models;

namespace QuanLyNhaHang.Services;

/// <summary>Nghiệp vụ gọi món, tính hóa đơn, trừ kho và xếp bàn.</summary>
public class OrderService
{
    private readonly AppDbContext _db;
    public OrderService(AppDbContext db) { _db = db; }

    /// <summary>Mỗi lúc chỉ một thao tác làm đổi tồn kho (trừ kho, hoàn kho, bếp báo hết, nhập hàng) trong tiến trình, tránh hai phục vụ cùng trừ một lượng tồn.</summary>
    public static readonly SemaphoreSlim StockGate = new(1, 1);

    public record Bill(decimal Sub, decimal Disc, decimal Fee, decimal Vat, decimal Deposit, decimal Total);

    public Task<Order> LoadAsync(int id) => _db.Orders
        .Include(o => o.Lines).Include(o => o.Coupon).Include(o => o.Table)
        .Include(o => o.Reservation).Include(o => o.Branch)
        .FirstOrDefaultAsync(o => o.Id == id);

    public Bill Calc(Order o)
    {
        var sub = o.Lines.Where(l => l.Status != St.Cancel).Sum(l => l.Qty * l.Price);
        decimal disc = 0;
        if (o.Coupon != null && sub >= o.Coupon.MinTotal)
            disc += o.Coupon.Percent > 0 ? sub * o.Coupon.Percent / 100 : o.Coupon.Amount;
        disc += sub * o.DiscountPct / 100;
        disc = Math.Round(Math.Min(disc, sub));
        var net = sub - disc;
        var fee = Math.Round(net * (o.Branch?.ServiceRate ?? 0));
        var vat = Math.Round(net * (o.Branch?.VatRate ?? 0));
        var dep = o.Reservation?.Deposit ?? 0;
        return new Bill(sub, disc, fee, vat, dep, Math.Max(0, net + fee + vat - dep));
    }

    // ---------- Giá & khuyến mãi ----------
    static bool InWindow(Promotion p, TimeSpan now)
    {
        var s = TimeSpan.Parse(p.StartTime); var e = TimeSpan.Parse(p.EndTime);
        return s <= e ? now >= s && now <= e : now >= s || now <= e;
    }

    public async Task<int> BestPromoAsync(int categoryId)
    {
        var now = DateTime.Now.TimeOfDay;
        var list = await _db.Promotions.Where(p => p.Active && (p.CategoryId == null || p.CategoryId == categoryId)).ToListAsync();
        return list.Where(p => InWindow(p, now)).Select(p => p.Percent).DefaultIfEmpty(0).Max();
    }

    // ---------- Tồn kho ----------
    public Task<Dictionary<int, decimal>> StockAsync(int branchId) =>
        _db.BranchStocks.Where(s => s.BranchId == branchId).ToDictionaryAsync(s => s.IngredientId, s => s.Quantity);

    public static bool CanMake(MenuItem m, Dictionary<int, decimal> stock, int qty = 1) =>
        m.Recipes == null || m.Recipes.All(r => stock.GetValueOrDefault(r.IngredientId) >= r.Qty * qty);

    public async Task<string> MissingAsync(MenuItem m, int branchId, int qty)
    {
        var stock = await StockAsync(branchId);
        var recipes = await _db.RecipeLines.Include(r => r.Ingredient).Where(r => r.MenuItemId == m.Id).ToListAsync();
        var miss = recipes.Where(r => stock.GetValueOrDefault(r.IngredientId) < r.Qty * qty).Select(r => r.Ingredient.Name).ToList();
        return miss.Count == 0 ? null : string.Join(", ", miss);
    }

    /// <summary>Xuất kho theo công thức, chống xử lý trùng theo từng dòng đơn.</summary>
    public async Task DeductAsync(OrderLine l, int branchId)
    {
        var operationKey = $"sale-line:{l.Id}";
        if (await _db.StockMovements.AnyAsync(x => x.OperationKey != null && x.OperationKey.StartsWith(operationKey + ":ingredient:"))) return;
        var parts = new List<(int Menu, int Qty)>();
        if (l.MenuItemId != null) parts.Add((l.MenuItemId.Value, l.Qty));
        else if (l.ComboId != null)
            foreach (var c in await _db.ComboItems.Where(c => c.ComboId == l.ComboId).ToListAsync())
                parts.Add((c.MenuItemId, c.Qty * l.Qty));
        if (parts.Count == 0) throw new InvalidOperationException("Dòng đơn không có món hoặc combo hợp lệ.");

        var recipeIds = parts.Select(x => x.Menu).Distinct().ToList();
        var recipes = await _db.RecipeLines.Where(r => recipeIds.Contains(r.MenuItemId)).ToListAsync();
        var menusWithoutRecipe = recipeIds.Where(menuId => !recipes.Any(r => r.MenuItemId == menuId)).ToList();
        if (menusWithoutRecipe.Count > 0) throw new InvalidOperationException("Thiếu công thức cho món ID " + string.Join(", ", menusWithoutRecipe) + "; không thể xuất kho.");
        if (recipes.Any(r => r.Qty <= 0)) throw new InvalidOperationException("Có định lượng công thức không hợp lệ; không thể xuất kho.");
        var required = parts.SelectMany(p => recipes.Where(r => r.MenuItemId == p.Menu).Select(r => (r.IngredientId, Qty: r.Qty * p.Qty)))
            .GroupBy(x => x.IngredientId).ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));
        var stocks = await _db.BranchStocks.Where(x => x.BranchId == branchId && required.Keys.Contains(x.IngredientId)).ToListAsync();
        foreach (var pair in required)
        {
            var stock = stocks.FirstOrDefault(x => x.IngredientId == pair.Key);
            if (stock == null || stock.Quantity < pair.Value)
            {
                var ing = await _db.Ingredients.FindAsync(pair.Key);
                throw new InvalidOperationException($"không đủ {ing?.Name ?? "nguyên liệu ID " + pair.Key}: cần {Fmt.Qty(pair.Value, ing?.Unit)}, kho còn {Fmt.Qty(stock?.Quantity ?? 0, ing?.Unit)}");
            }
        }
        foreach (var pair in required)
        {
            var stock = stocks.Single(x => x.IngredientId == pair.Key);
            stock.Quantity -= pair.Value;
            _db.StockMovements.Add(new StockMovement { BranchId = branchId, IngredientId = pair.Key, Qty = -pair.Value, Type = "Xuất bán", Ref = "Đơn #" + l.OrderId, Note = "Tự động xuất theo công thức", OperationKey = operationKey + ":ingredient:" + pair.Key });
        }
    }

    /// <summary>
    /// Hoàn kho khi hủy một dòng đã bị trừ kho nhưng bếp chưa làm. Chống hoàn lặp bằng OperationKey.
    /// Nếu bếp đã báo hết nguyên liệu đó sau lúc trừ thì không hoàn, vì hàng thực tế đã không còn.
    /// Người gọi phải SaveChanges.
    /// </summary>
    public async Task RestockAsync(OrderLine l, int branchId, string reason)
    {
        var prefix = $"sale-line:{l.Id}:ingredient:";
        var sold = await _db.StockMovements.Where(x => x.OperationKey != null && x.OperationKey.StartsWith(prefix)).ToListAsync();
        foreach (var m in sold)
        {
            var key = $"restock-line:{l.Id}:ingredient:{m.IngredientId}";
            if (await _db.StockMovements.AnyAsync(x => x.OperationKey == key)) continue;
            var soldAt = m.Date;
            if (await _db.StockMovements.AnyAsync(x => x.BranchId == branchId && x.IngredientId == m.IngredientId && x.Type == "Báo hết" && x.Date >= soldAt)) continue;
            var stock = await _db.BranchStocks.FirstOrDefaultAsync(x => x.BranchId == branchId && x.IngredientId == m.IngredientId);
            if (stock == null) { stock = new BranchStock { BranchId = branchId, IngredientId = m.IngredientId, Quantity = 0 }; _db.BranchStocks.Add(stock); }
            stock.Quantity += -m.Qty;
            _db.StockMovements.Add(new StockMovement { BranchId = branchId, IngredientId = m.IngredientId, Qty = -m.Qty, Type = "Hoàn kho", Ref = "Đơn #" + l.OrderId, Note = reason, OperationKey = key });
        }
    }

    /// <summary>Bếp báo hết một nguyên liệu: tồn kho của chi nhánh tự về 0. Các món dùng nguyên liệu này tự khóa trên màn hình gọi món cho đến khi nhập hàng.</summary>
    public async Task<(bool Ok, string Message)> ReportOutAsync(int branchId, int ingredientId, string actor)
    {
        await StockGate.WaitAsync();
        try
        {
            var ing = await _db.Ingredients.FindAsync(ingredientId);
            if (ing == null) return (false, "Nguyên liệu không tồn tại.");
            var stock = await _db.BranchStocks.FirstOrDefaultAsync(x => x.BranchId == branchId && x.IngredientId == ingredientId);
            var before = stock?.Quantity ?? 0;
            if (stock == null || before <= 0) return (false, ing.Name + " đã hết trong kho rồi.");

            stock.Quantity = 0;
            _db.StockMovements.Add(new StockMovement { BranchId = branchId, IngredientId = ingredientId, Qty = -before, Type = "Báo hết", Ref = actor, Note = "Bếp báo hết, kho tự về 0" });
            _db.AuditLogs.Add(new AuditLog { BranchId = branchId, Actor = actor, Action = "Bếp báo hết nguyên liệu", Detail = $"{ing.Name}: kho {Fmt.Qty(before, ing.Unit)} → 0", At = DateTime.Now });
            await _db.SaveChangesAsync();

            var dishes = await _db.RecipeLines.Where(r => r.IngredientId == ingredientId).Select(r => r.MenuItem.Name).Distinct().OrderBy(n => n).ToListAsync();
            var msg = $"Đã báo hết {ing.Name}, kho về 0.";
            if (dishes.Count > 0) msg += " Món tạm ngừng cho đến khi nhập hàng: " + string.Join(", ", dishes) + ".";
            return (true, msg);
        }
        finally { StockGate.Release(); }
    }

    // ---------- Gọi món ----------
    public async Task<string> AddItemAsync(Order o, int menuItemId, int qty)
    {
        var m = await _db.MenuItems.FindAsync(menuItemId);
        if (m == null) return "Món không tồn tại.";
        if (m.Stopped) return m.Name + " đang tạm ngừng.";
        var miss = await MissingAsync(m, o.BranchId, qty);
        if (miss != null) return "Không đủ nguyên liệu: " + miss;

        var recipes = await _db.RecipeLines.Include(r => r.Ingredient).Where(r => r.MenuItemId == m.Id).ToListAsync();
        if (recipes.Count == 0 || recipes.Any(r => r.Qty <= 0 || r.Ingredient == null || r.Ingredient.Cost <= 0)) return "Chưa đủ dữ liệu công thức/giá vốn cho món này.";
        var cost = recipes.Sum(r => r.Qty * r.Ingredient.Cost);
        var pct = await BestPromoAsync(m.CategoryId);
        var price = Math.Round(m.Price * (100 - pct) / 100);
        var line = o.Lines.FirstOrDefault(l => l.MenuItemId == m.Id && l.Status == St.New && string.IsNullOrEmpty(l.Note));
        if (line != null) { line.Qty += qty; line.Cost = cost; }
        else _db.OrderLines.Add(new OrderLine { OrderId = o.Id, MenuItemId = m.Id, Name = m.Name, Qty = qty, Price = price, Cost = cost, PrepMinutes = m.PrepMinutes });
        await _db.SaveChangesAsync();
        return null;
    }

    public async Task<string> AddComboAsync(Order o, int comboId)
    {
        var c = await _db.Combos.Include(x => x.Items).ThenInclude(i => i.MenuItem).FirstOrDefaultAsync(x => x.Id == comboId);
        if (c == null || !c.Active) return "Combo không còn bán.";
        if (c.Items.Count == 0) return "Combo chưa có món thành phần.";
        decimal cost = 0;
        foreach (var item in c.Items)
        {
            var recipes = await _db.RecipeLines.Include(r => r.Ingredient).Where(r => r.MenuItemId == item.MenuItemId).ToListAsync();
            if (recipes.Count == 0 || recipes.Any(r => r.Qty <= 0 || r.Ingredient == null || r.Ingredient.Cost <= 0)) return $"Món {item.MenuItem.Name} chưa đủ công thức/giá vốn.";
            cost += recipes.Sum(r => r.Qty * r.Ingredient.Cost) * item.Qty;
        }
        var prep = c.Items.Max(i => i.MenuItem.PrepMinutes);
        _db.OrderLines.Add(new OrderLine { OrderId = o.Id, ComboId = c.Id, Name = c.Name, Qty = 1, Price = c.Price, Cost = cost, PrepMinutes = prep });
        await _db.SaveChangesAsync();
        return null;
    }

    // ---------- Đặt bàn ----------
    /// <summary>Danh sách bàn còn trống trong khung giờ (cách các lịch khác tối thiểu 2 giờ).</summary>
    public async Task<List<DiningTable>> FreeTablesAsync(int branchId, DateTime date, string time, int pax)
    {
        var t = TimeSpan.Parse(time);
        var res = await _db.Reservations.Where(r => r.BranchId == branchId && r.Date == date.Date && r.Status == St.Confirmed).ToListAsync();
        var busy = res.Where(r => Math.Abs((TimeSpan.Parse(r.Time) - t).TotalMinutes) < 120).Select(r => r.TableId).ToHashSet();
        if (date.Date == DateTime.Today && Math.Abs((DateTime.Now.TimeOfDay - t).TotalMinutes) < 90)
        {
            var open = await _db.Orders.Where(o => o.BranchId == branchId && o.Status == St.Open && o.TableId != null).Select(o => o.TableId.Value).ToListAsync();
            foreach (var id in open) busy.Add(id);
        }
        var tables = await _db.Tables.Where(x => x.BranchId == branchId && x.Seats >= pax).OrderBy(x => x.Seats).ThenBy(x => x.Name).ToListAsync();
        return tables.Where(x => !busy.Contains(x.Id)).ToList();
    }

    public async Task LogAsync(int branchId, string actor, string action, string detail)
    {
        _db.AuditLogs.Add(new AuditLog { BranchId = branchId, Actor = actor, Action = action, Detail = detail });
        await _db.SaveChangesAsync();
    }
}
