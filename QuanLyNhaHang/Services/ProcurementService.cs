using Microsoft.EntityFrameworkCore;
using QuanLyNhaHang.Data;
using QuanLyNhaHang.Models;

namespace QuanLyNhaHang.Services;

/// <summary>Một dòng gợi ý tồn kho cho một nguyên liệu tại một chi nhánh.</summary>
public record ForecastRow(int IngredientId, string Name, string Unit, decimal Stock, decimal Incoming,
    decimal DailyUse, decimal ReorderLevel, decimal ReorderQty, string State);

/// <summary>
/// Chỉ ĐỌC và gợi ý, không tự đặt hàng, không tự nhập kho.
/// Việc lập đơn đặt hàng và bấm nhận hàng vào kho do nhân viên làm tay ở trang Kho &amp; mua hàng.
/// Mức dùng/ngày tính từ 14 ngày bán gần nhất; "hàng đang về" là các đơn nhập chưa nhận.
/// </summary>
public class ProcurementService
{
    public const string StatusReceived = "Đã nhận";
    public static readonly string[] OpenStatuses = { "Đang đặt", "Đang giao" };
    private const int UsageWindowDays = 14;

    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;
    public ProcurementService(AppDbContext db, IConfiguration cfg) { _db = db; _cfg = cfg; }

    private decimal CoverDays => _cfg.GetValue<decimal>("Procurement:CoverDays", 3m);

    public async Task<List<ForecastRow>> ForecastAsync(int branchId)
    {
        var ings = await _db.Ingredients.OrderBy(i => i.Name).ToListAsync();
        var stocks = await _db.BranchStocks.Where(s => s.BranchId == branchId)
            .ToDictionaryAsync(s => s.IngredientId, s => s.Quantity);

        var from = DateTime.Today.AddDays(-UsageWindowDays);
        var moves = await _db.StockMovements
            .Where(m => m.BranchId == branchId && m.Type == "Xuất bán" && m.Date >= from)
            .Select(m => new { m.IngredientId, m.Qty }).ToListAsync();
        var usage = moves.GroupBy(m => m.IngredientId).ToDictionary(g => g.Key, g => -g.Sum(x => x.Qty));

        var incomingRows = await _db.PurchaseLines.Include(l => l.PurchaseOrder)
            .Where(l => l.PurchaseOrder.BranchId == branchId && OpenStatuses.Contains(l.PurchaseOrder.Status))
            .Select(l => new { l.IngredientId, l.Qty }).ToListAsync();
        var incoming = incomingRows.GroupBy(x => x.IngredientId).ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));

        var rows = new List<ForecastRow>();
        foreach (var i in ings)
        {
            var stock = stocks.GetValueOrDefault(i.Id);
            var inc = incoming.GetValueOrDefault(i.Id);
            var daily = Math.Round(usage.GetValueOrDefault(i.Id) / UsageWindowDays, 2);
            var level = Math.Max(i.MinLevel, Math.Round(daily * CoverDays, 2));
            var qty = Math.Max(Math.Round(daily * 7, 2), Math.Max(i.MinLevel * 2, 1));

            string state;
            if (inc > 0 && stock <= level) state = "Đã đặt";
            else if (stock <= 0) state = "Hết";
            else if (stock + inc <= level) state = "Nên đặt";
            else if (stock <= i.MinLevel) state = "Sắp hết";
            else state = "Đủ";

            rows.Add(new ForecastRow(i.Id, i.Name, i.Unit, stock, inc, daily, level, qty, state));
        }
        return rows;
    }
}
