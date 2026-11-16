using Microsoft.EntityFrameworkCore;
using QuanLyNhaHang.Data;
using QuanLyNhaHang.Models;

namespace QuanLyNhaHang.Services;

/// <summary>
/// Bộ điều phối bếp tự động: món đã gửi vào "Chờ chế biến" được nhận vào "Đang chế biến" ngay
/// (ưu tiên trước, rồi theo giờ gửi). Số món nấu cùng lúc giới hạn bởi Kitchen:MaxCooking.
/// Nguyên liệu đã được trừ kho từ lúc khách gọi món (phục vụ bấm Gửi bếp) hoặc lúc xác nhận đơn online,
/// nên ở đây chỉ trừ bù cho món cũ chưa từng bị trừ (DeductAsync bỏ qua nếu đã trừ rồi).
/// </summary>
public class KitchenDispatcher
{
    // Chỉ một lượt điều phối chạy tại một thời điểm trong tiến trình, tránh hai luồng cùng nhận một món.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly AppDbContext _db;
    private readonly OrderService _svc;
    private readonly IConfiguration _cfg;

    public KitchenDispatcher(AppDbContext db, OrderService svc, IConfiguration cfg)
    {
        _db = db; _svc = svc; _cfg = cfg;
    }

    /// <summary>Đưa các món đang chờ vào bếp nếu còn chỗ và đủ nguyên liệu. Trả về số món vừa bắt đầu.</summary>
    public async Task<int> DispatchAsync(int? branchId = null)
    {
        await Gate.WaitAsync();
        try
        {
            var max = _cfg.GetValue<int>("Kitchen:MaxCooking", 10);
            var cooking = (await _db.OrderLines.Where(l => l.Status == St.Cook).Select(l => l.Order.BranchId).ToListAsync())
                .GroupBy(b => b).ToDictionary(g => g.Key, g => g.Count());

            var queue = await _db.OrderLines.Include(l => l.Order)
                .Where(l => l.Status == St.Wait
                    && (l.Order.Status == St.Open || (l.Order.Type == "Online" && l.Order.Status == St.Wait))
                    && (branchId == null || l.Order.BranchId == branchId))
                .OrderByDescending(l => l.Priority).ThenBy(l => l.SentAt)
                .ToListAsync();

            var started = 0;
            foreach (var line in queue)
            {
                var b = line.Order.BranchId;
                var busy = cooking.GetValueOrDefault(b);
                if (busy >= max) continue;

                await OrderService.StockGate.WaitAsync();
                try
                {
                    await _svc.DeductAsync(line, b);
                    line.Status = St.Cook;
                    line.StartedAt = DateTime.Now;
                    await _db.SaveChangesAsync();
                    cooking[b] = busy + 1;
                    started++;
                }
                catch (InvalidOperationException ex)
                {
                    await NoteShortageAsync(line, ex.Message);
                }
                finally
                {
                    OrderService.StockGate.Release();
                }
            }

            return started;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Ghi nhật ký thiếu nguyên liệu, mỗi món chỉ ghi lại tối đa một lần trong 10 phút để tránh trùng lặp.</summary>
    private async Task NoteShortageAsync(OrderLine line, string reason)
    {
        var detail = $"#{line.OrderId} {line.Name}: {reason}";
        var since = DateTime.Now.AddMinutes(-10);
        if (await _db.AuditLogs.AnyAsync(a => a.Action == "Thiếu nguyên liệu" && a.Detail == detail && a.At > since)) return;
        _db.AuditLogs.Add(new AuditLog
        {
            BranchId = line.Order.BranchId, Actor = "Hệ thống", Action = "Thiếu nguyên liệu",
            Detail = detail, At = DateTime.Now,
        });
        await _db.SaveChangesAsync();
    }
}
