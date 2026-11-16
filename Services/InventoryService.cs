using Microsoft.EntityFrameworkCore;
using QuanLyNhaHang.Data;
using QuanLyNhaHang.Models;

namespace QuanLyNhaHang.Services;

public interface IInventoryService
{
    Task DeductStockForOrderAsync(int orderId);
    Task RestoreStockForOrderAsync(int orderId);
}

/// <summary>Xuất/hoàn kho có dấu vết StockMovement để thao tác lặp không làm lệch tồn.</summary>
public sealed class InventoryService : IInventoryService
{
    private readonly AppDbContext _context;
    public InventoryService(AppDbContext context) => _context = context;

    public async Task DeductStockForOrderAsync(int orderId)
    {
        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var order = await _context.Orders.Include(o => o.Lines).ThenInclude(l => l.MenuItem)
                .ThenInclude(m => m.Recipes).ThenInclude(r => r.Ingredient)
                .FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) throw new InvalidOperationException($"Không tìm thấy đơn #{orderId}.");

            // Nếu đã ghi xuất cho đơn này, không trừ lần nữa.
            if (await _context.StockMovements.AnyAsync(m => m.Ref == $"ORDER:{orderId}:OUT"))
            { await tx.CommitAsync(); return; }

            var requirements = new Dictionary<int, decimal>();
            foreach (var line in order.Lines.Where(l => l.Status != St.Cancel && l.Qty > 0))
            {
                if (line.MenuItemId == null) continue;
                foreach (var recipe in line.MenuItem?.Recipes ?? new List<RecipeLine>())
                    requirements[recipe.IngredientId] = requirements.GetValueOrDefault(recipe.IngredientId) + recipe.Qty * line.Qty;
            }

            foreach (var req in requirements)
            {
                var stock = await _context.BranchStocks.SingleOrDefaultAsync(s => s.BranchId == order.BranchId && s.IngredientId == req.Key);
                var ingredient = await _context.Ingredients.FindAsync(req.Key);
                if (stock == null) throw new InvalidOperationException($"Chi nhánh chưa khai báo tồn kho nguyên liệu {ingredient?.Name ?? req.Key.ToString()}.");
                if (stock.Quantity < req.Value) throw new InvalidOperationException($"Không đủ {ingredient?.Name ?? req.Key.ToString()}. Tồn: {stock.Quantity}, cần: {req.Value}.");
                stock.Quantity -= req.Value;
                _context.StockMovements.Add(new StockMovement { BranchId = order.BranchId, IngredientId = req.Key, Qty = -req.Value, Type = "Xuất bán", Ref = $"ORDER:{orderId}:OUT", Note = $"Xuất nguyên liệu cho đơn #{orderId}", Date = DateTime.Now });
            }
            _context.AuditLogs.Add(new AuditLog { BranchId = order.BranchId, Actor = "Hệ thống", Action = "Xuất kho", Detail = $"Xuất nguyên liệu cho đơn #{orderId}", At = DateTime.Now });
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    public async Task RestoreStockForOrderAsync(int orderId)
    {
        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) throw new InvalidOperationException($"Không tìm thấy đơn #{orderId}.");
            if (await _context.StockMovements.AnyAsync(m => m.Ref == $"ORDER:{orderId}:RESTORE"))
            { await tx.CommitAsync(); return; }

            var outs = await _context.StockMovements.Where(m => m.Ref == $"ORDER:{orderId}:OUT" && m.Qty < 0).ToListAsync();
            if (outs.Count == 0) { await tx.CommitAsync(); return; } // chưa xuất kho thì không hoàn kho
            foreach (var movement in outs)
            {
                var stock = await _context.BranchStocks.SingleOrDefaultAsync(s => s.BranchId == movement.BranchId && s.IngredientId == movement.IngredientId);
                if (stock == null) throw new InvalidOperationException($"Không tìm thấy tồn kho nguyên liệu ID {movement.IngredientId}.");
                var qty = -movement.Qty;
                stock.Quantity += qty;
                _context.StockMovements.Add(new StockMovement { BranchId = movement.BranchId, IngredientId = movement.IngredientId, Qty = qty, Type = "Hoàn kho", Ref = $"ORDER:{orderId}:RESTORE", Note = $"Hoàn nguyên liệu đơn #{orderId}", Date = DateTime.Now });
            }
            _context.AuditLogs.Add(new AuditLog { BranchId = order.BranchId, Actor = "Hệ thống", Action = "Hoàn kho", Detail = $"Hoàn nguyên liệu cho đơn #{orderId}", At = DateTime.Now });
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
    }
}
