using Microsoft.EntityFrameworkCore;
using QuanLyNhaHang.Data;
using QuanLyNhaHang.Models;

namespace QuanLyNhaHang.Services;

public sealed class CostingService(AppDbContext db)
{
    public sealed record CostResult(bool Complete, decimal Cost, IReadOnlyList<string> Warnings);

    public async Task<CostResult> MenuItemCostAsync(int menuItemId, CancellationToken ct = default)
    {
        var lines = await db.RecipeLines.Include(x => x.Ingredient)
            .Where(x => x.MenuItemId == menuItemId).ToListAsync(ct);
        var warnings = new List<string>();
        if (lines.Count == 0) warnings.Add("Chưa khai báo công thức nguyên liệu.");
        foreach (var line in lines)
        {
            if (line.Qty <= 0) warnings.Add($"Định lượng không hợp lệ: {line.Ingredient?.Name ?? "nguyên liệu"}.");
            if (line.Ingredient == null || string.IsNullOrWhiteSpace(line.Ingredient.Unit)) warnings.Add("Thiếu nguyên liệu hoặc đơn vị tính.");
            if (line.Ingredient != null && line.Ingredient.Cost <= 0) warnings.Add($"Thiếu giá vốn hợp lệ: {line.Ingredient.Name}.");
        }
        var complete = warnings.Count == 0;
        return new CostResult(complete, complete ? lines.Sum(x => x.Qty * x.Ingredient.Cost) : 0m, warnings);
    }

    public async Task<CostResult> ComboCostAsync(int comboId, CancellationToken ct = default)
    {
        var combo = await db.Combos.Include(x => x.Items).ThenInclude(x => x.MenuItem).FirstOrDefaultAsync(x => x.Id == comboId, ct);
        if (combo == null) return new(false, 0, new[] { "Không tìm thấy combo." });
        decimal total = 0; var warnings = new List<string>();
        foreach (var item in combo.Items)
        {
            if (item.Qty <= 0) { warnings.Add($"Số lượng thành phần {item.MenuItem?.Name ?? "món"} không hợp lệ."); continue; }
            var cost = await MenuItemCostAsync(item.MenuItemId, ct);
            if (!cost.Complete) warnings.AddRange(cost.Warnings.Select(w => $"{item.MenuItem?.Name}: {w}"));
            else total += cost.Cost * item.Qty;
        }
        return new(warnings.Count == 0 && combo.Items.Count > 0, warnings.Count == 0 ? total : 0m,
            warnings.Count == 0 && combo.Items.Count == 0 ? new[] { "Combo chưa có món thành phần." } : warnings);
    }
}
