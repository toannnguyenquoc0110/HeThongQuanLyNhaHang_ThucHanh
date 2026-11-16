using Microsoft.AspNetCore.Mvc;
using System.Data;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaHang.Data;
using QuanLyNhaHang.Models;
using QuanLyNhaHang.Services;

namespace QuanLyNhaHang.Controllers;

public class OrdersController : BaseController
{
    private readonly OrderService _svc;
    private readonly KitchenDispatcher _dispatcher;
    public OrdersController(AppDbContext db, OrderService svc, KitchenDispatcher dispatcher) : base(db) { _svc = svc; _dispatcher = dispatcher; }

    // ---------- Danh sách đơn ----------
    public async Task<IActionResult> Index(DateTime? date, string status)
    {
        var d = (date ?? DateTime.Today).Date;
        var q = Db.Orders.Include(o => o.Table).Include(o => o.Employee).Include(o => o.Lines)
            .Where(o => o.BranchId == BranchId && o.OpenedAt >= d && o.OpenedAt < d.AddDays(1));
        if (!string.IsNullOrEmpty(status)) q = q.Where(o => o.Status == status);
        ViewBag.Date = d; ViewBag.Status = status;
        return View(await q.OrderByDescending(o => o.OpenedAt).ToListAsync());
    }

    // ---------- Tiếp nhận đơn online ----------
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptOnline(int id)
    {
        var order = await Db.Orders.Include(x => x.Lines).Include(x => x.Coupon).FirstOrDefaultAsync(x => x.Id == id && x.BranchId == BranchId && x.Type == "Online");
        if (order == null) return NotFound();
        if (order.Status != St.New) { Err("Đơn không còn ở trạng thái chờ tiếp nhận."); return RedirectToAction(nameof(Index)); }
        await using var tx = await Db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        // Kiểm tra tồn kho lại ở thời điểm tiếp nhận để tránh dựa vào số liệu lúc khách thêm giỏ.
        var required = new Dictionary<int, decimal>();
        foreach (var line in order.Lines)
        {
            var parts = new List<(int menuId, int qty)>();
            if (line.MenuItemId.HasValue) parts.Add((line.MenuItemId.Value, line.Qty));
            else if (line.ComboId.HasValue) { var comboParts = await Db.ComboItems.Where(x => x.ComboId == line.ComboId.Value).ToListAsync(); parts.AddRange(comboParts.Select(x => (x.MenuItemId, x.Qty * line.Qty))); }
            foreach (var part in parts)
            {
                var recipe = await Db.RecipeLines.Where(x => x.MenuItemId == part.menuId).ToListAsync();
                if (recipe.Count == 0) { Err("Không thể xác nhận: món chưa có công thức nguyên liệu."); return RedirectToAction(nameof(Index)); }
                foreach (var r in recipe) required[r.IngredientId] = required.GetValueOrDefault(r.IngredientId) + r.Qty * part.qty;
            }
        }
        foreach (var pair in required)
        {
            var available = await Db.BranchStocks.Where(x => x.BranchId == order.BranchId && x.IngredientId == pair.Key).Select(x => (decimal?)x.Quantity).FirstOrDefaultAsync() ?? 0;
            if (available < pair.Value) { Err($"Không đủ tồn kho nguyên liệu ID {pair.Key}; cần {pair.Value}, hiện có {available}."); return RedirectToAction(nameof(Index)); }
        }
        try
        {
            // Xuất kho ngay khi nhà hàng xác nhận đơn online để không nhận vượt tồn.
            foreach (var line in order.Lines)
                await _svc.DeductAsync(line, order.BranchId);
            order.StockDeducted = true;
            order.StockDeductedAt = DateTime.Now;
            order.Status = St.Wait;
            foreach (var line in order.Lines) { line.Status = St.Wait; line.SentAt = DateTime.Now; }
            if (order.Coupon != null) order.Coupon.UsedCount++;
            await Db.SaveChangesAsync();
            await tx.CommitAsync();
            Ok($"Đã tiếp nhận đơn online #{order.Id}.");
            await _dispatcher.DispatchAsync(order.BranchId);
        }
        catch (InvalidOperationException ex)
        {
            Err("Không thể tiếp nhận đơn: " + ex.Message);
        }
        catch (DbUpdateException)
        {
            Err("Có xung đột khi cập nhật tồn kho. Đơn chưa được xác nhận; vui lòng tải lại và kiểm tra tồn kho.");
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectOnline(int id, string reason)
    {
        var order = await Db.Orders.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id && x.BranchId == BranchId && x.Type == "Online");
        if (order == null) return NotFound();
        if (order.Status != St.New) { Err("Chỉ từ chối đơn đang chờ tiếp nhận."); return RedirectToAction(nameof(Index)); }
        if (string.IsNullOrWhiteSpace(reason)) { Err("Cần nhập lý do từ chối đơn."); return RedirectToAction(nameof(Index)); }
        order.Status = St.Cancel; order.RejectReason = reason.Trim(); order.ClosedAt = DateTime.Now;
        foreach (var line in order.Lines) line.Status = St.Cancel;
        await Db.SaveChangesAsync(); Ok($"Đã từ chối đơn #{order.Id}.");
        return RedirectToAction(nameof(Index));
    }


    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteOnline(int id)
    {
        var order = await Db.Orders.FirstOrDefaultAsync(x => x.Id == id && x.BranchId == BranchId && x.Type == "Online");
        if (order == null) return NotFound();
        if (order.Status != St.Delivering && order.Status != St.OnlineReady)
        {
            Err("Chỉ hoàn thành đơn đang giao hoặc sẵn sàng nhận.");
            return RedirectToAction(nameof(Index));
        }
        order.Status = St.Completed;
        order.ClosedAt = DateTime.Now;
        await Db.SaveChangesAsync();
        Ok($"Đã hoàn thành đơn online #{order.Id}.");
        return RedirectToAction(nameof(Index));
    }


    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkCodPaid(int id)
    {
        var order = await Db.Orders.Include(x => x.Lines).Include(x => x.Branch).Include(x => x.Coupon)
            .FirstOrDefaultAsync(x => x.Id == id && x.BranchId == BranchId && x.Type == "Online");
        if (order == null) return NotFound();
        if (order.PaymentMethod != "COD" || order.Status != St.Completed || order.PaymentStatus == "Đã thanh toán")
        {
            Err("Chỉ xác nhận thu tiền COD cho đơn đã hoàn thành và chưa thanh toán.");
            return RedirectToAction(nameof(Index));
        }
        var bill = _svc.Calc(order);
        order.Total = bill.Total;
        order.PaymentStatus = "Đã thanh toán";
        Db.Payments.Add(new Payment { OrderId = order.Id, Method = "COD", Amount = bill.Total, PaidAt = DateTime.Now });
        await Db.SaveChangesAsync();
        await _svc.LogAsync(BranchId, Actor, "Thu tiền đơn online", $"Đơn #{order.Id}: {bill.Total:N0}đ COD");
        Ok($"Đã ghi nhận thu tiền COD cho đơn #{order.Id}.");
        return RedirectToAction(nameof(Index));
    }

    // ---------- Màn hình gọi món ----------
    public async Task<IActionResult> Screen(int id)
    {
        var o = await _svc.LoadAsync(id);
        if (o == null || o.BranchId != BranchId) return RedirectToAction("Index", "Tables");
        if (o.Status != St.Open) return RedirectToAction(nameof(Receipt), new { id });

        var vm = new OrderScreenVM
        {
            Cart = new CartVM { Order = o, Bill = _svc.Calc(o) },
            Categories = await Db.Categories.OrderBy(c => c.SortOrder).ToListAsync(),
            Items = await Db.MenuItems.Include(m => m.Recipes).OrderBy(m => m.Name).ToListAsync(),
            Combos = await Db.Combos.Where(c => c.Active).ToListAsync(),
            Stock = await _svc.StockAsync(BranchId),
        };
        foreach (var c in vm.Categories) vm.Promo[c.Id] = await _svc.BestPromoAsync(c.Id);

        var busy = await Db.Orders.Where(x => x.BranchId == BranchId && x.Status == St.Open && x.TableId != null).Select(x => x.TableId.Value).ToListAsync();
        ViewBag.FreeTables = await Db.Tables.Where(t => t.BranchId == BranchId && !busy.Contains(t.Id)).OrderBy(t => t.Id).ToListAsync();
        ViewBag.OtherOrders = await Db.Orders.Include(x => x.Table).Where(x => x.BranchId == BranchId && x.Status == St.Open && x.Id != id).ToListAsync();
        return View(vm);
    }

    async Task<IActionResult> Cart(int orderId, string error = null)
    {
        if (error != null) Response.Headers["X-Error"] = Uri.EscapeDataString(error);
        var o = await _svc.LoadAsync(orderId);
        return PartialView("_Cart", new CartVM { Order = o, Bill = _svc.Calc(o) });
    }

    async Task<Order> Mine(int id)
    {
        var o = await Db.Orders.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id);
        return o != null && o.BranchId == BranchId && o.Status == St.Open ? o : null;
    }

    [HttpPost]
    public async Task<IActionResult> Add(int orderId, int itemId, int qty = 1)
    {
        var o = await Mine(orderId); if (o == null) return NotFound();
        return await Cart(orderId, await _svc.AddItemAsync(o, itemId, Math.Max(1, qty)));
    }

    [HttpPost]
    public async Task<IActionResult> AddCombo(int orderId, int comboId)
    {
        var o = await Mine(orderId); if (o == null) return NotFound();
        return await Cart(orderId, await _svc.AddComboAsync(o, comboId));
    }

    [HttpPost]
    public async Task<IActionResult> Qty(int lineId, int delta)
    {
        var l = await Db.OrderLines.FindAsync(lineId); if (l == null) return NotFound();
        string err = null;
        if (l.Status != St.New) err = "Món đã gửi bếp, không đổi số lượng được.";
        else
        {
            if (delta > 0 && l.MenuItemId != null)
            {
                var o = await Db.Orders.FindAsync(l.OrderId);
                var m = await Db.MenuItems.FindAsync(l.MenuItemId.Value);
                err = await _svc.MissingAsync(m, o.BranchId, l.Qty + delta);
                if (err != null) err = "Không đủ nguyên liệu: " + err;
            }
            if (err == null) { l.Qty += delta; if (l.Qty <= 0) Db.OrderLines.Remove(l); await Db.SaveChangesAsync(); }
        }
        return await Cart(l.OrderId, err);
    }

    [HttpPost]
    public async Task<IActionResult> Remove(int lineId)
    {
        var l = await Db.OrderLines.FindAsync(lineId); if (l == null) return NotFound();
        string err = null;
        if (l.Status == St.New) { Db.OrderLines.Remove(l); await Db.SaveChangesAsync(); }
        else if (l.Status == St.Wait)
        {
            // Món đã gửi bếp đã bị trừ kho; hủy khi bếp chưa làm thì kho tự được hoàn lại.
            await OrderService.StockGate.WaitAsync();
            try
            {
                l.Status = St.Cancel;
                await _svc.RestockAsync(l, BranchId, "Hủy món chưa chế biến");
                await Db.SaveChangesAsync();
            }
            finally { OrderService.StockGate.Release(); }
            await _svc.LogAsync(BranchId, Actor, "Hủy món", l.Name + " x" + l.Qty);
        }
        else err = "Bếp đã làm món này, không hủy được.";
        return await Cart(l.OrderId, err);
    }

    [HttpPost]
    public async Task<IActionResult> Note(int lineId, string note)
    {
        var l = await Db.OrderLines.FindAsync(lineId); if (l == null) return NotFound();
        l.Note = note; await Db.SaveChangesAsync();
        return await Cart(l.OrderId);
    }

    /// <summary>Gửi bếp = khách đã gọi món: kho tự trừ nguyên liệu theo công thức ngay lúc này, không ai phải nhập tay.</summary>
    [HttpPost]
    public async Task<IActionResult> Send(int orderId)
    {
        var o = await Mine(orderId); if (o == null) return NotFound();
        var sent = 0; var failed = new List<string>();
        await OrderService.StockGate.WaitAsync();
        try
        {
            foreach (var l in o.Lines.Where(l => l.Status == St.New).ToList())
            {
                try { await _svc.DeductAsync(l, o.BranchId); }
                catch (InvalidOperationException ex) { failed.Add(l.Name + " (" + ex.Message + ")"); continue; }
                l.Status = St.Wait; l.SentAt = DateTime.Now; sent++;
            }
            await Db.SaveChangesAsync();
        }
        finally { OrderService.StockGate.Release(); }
        if (sent > 0) await _dispatcher.DispatchAsync(o.BranchId);

        string err = null;
        if (failed.Count > 0) err = "Chưa gửi bếp vì thiếu nguyên liệu: " + string.Join("; ", failed) + (sent > 0 ? ". Các món còn lại đã gửi bếp." : ".");
        else if (sent == 0) err = "Chưa có món mới để gửi bếp.";
        return await Cart(orderId, err);
    }

    [HttpPost]
    public async Task<IActionResult> Serve(int lineId)
    {
        var l = await Db.OrderLines.FindAsync(lineId); if (l == null) return NotFound();
        if (l.Status == St.Done) { l.Status = St.Served; l.ServedAt = DateTime.Now; await Db.SaveChangesAsync(); }
        return await Cart(l.OrderId);
    }

    [HttpPost]
    public async Task<IActionResult> Coupon(int orderId, string code)
    {
        var o = await Mine(orderId); if (o == null) return NotFound();
        if (string.IsNullOrWhiteSpace(code)) { o.CouponId = null; await Db.SaveChangesAsync(); return await Cart(orderId); }
        var c = await Db.Coupons.FirstOrDefaultAsync(x => x.Code == code.Trim().ToUpper() && x.Active);
        if (c == null || (c.ExpireDate != null && c.ExpireDate < DateTime.Today)) return await Cart(orderId, "Mã giảm giá không hợp lệ hoặc đã hết hạn.");
        o.CouponId = c.Id; await Db.SaveChangesAsync();
        return await Cart(orderId);
    }

    [HttpPost]
    public async Task<IActionResult> Discount(int orderId, int pct)
    {
        var o = await Mine(orderId); if (o == null) return NotFound();
        if (CurrentRole != Role.QuanLy) return await Cart(orderId, "Chỉ quản lý được giảm giá thủ công.");
        o.DiscountPct = Math.Clamp(pct, 0, 100); await Db.SaveChangesAsync();
        await _svc.LogAsync(BranchId, Actor, "Giảm giá", $"Đơn #{o.Id}: {o.DiscountPct}%");
        return await Cart(orderId);
    }

    // ---------- Thanh toán ----------
    [HttpPost]
    public async Task<IActionResult> Pay(int id, string method)
    {
        var o = await _svc.LoadAsync(id);
        if (o == null || o.BranchId != BranchId || o.Status != St.Open) return RedirectToAction("Index", "Tables");
        if (o.Lines.Any(l => l.Status == St.New)) { Err("Còn món chưa gửi bếp. Hãy gửi bếp hoặc xóa món trước khi thanh toán."); return RedirectToAction(nameof(Screen), new { id }); }
        if (!o.Lines.Any(l => l.Status != St.Cancel)) { Err("Đơn chưa có món nào."); return RedirectToAction(nameof(Screen), new { id }); }

        var bill = _svc.Calc(o);
        o.Total = bill.Total; o.Status = St.Paid; o.ClosedAt = DateTime.Now;
        Db.Payments.Add(new Payment { OrderId = o.Id, Method = string.IsNullOrEmpty(method) ? "Tiền mặt" : method, Amount = bill.Total });
        if (o.Coupon != null) o.Coupon.UsedCount++;
        await Db.SaveChangesAsync();
        await _svc.LogAsync(BranchId, Actor, "Thanh toán", $"Đơn #{o.Id}: {bill.Total:N0}đ ({method})");
        return RedirectToAction(nameof(Receipt), new { id });
    }

    public async Task<IActionResult> Receipt(int id)
    {
        var o = await _svc.LoadAsync(id);
        if (o == null || o.BranchId != BranchId) return RedirectToAction("Index", "Tables");
        ViewBag.Payment = await Db.Payments.Where(p => p.OrderId == id).FirstOrDefaultAsync();
        return View(new CartVM { Order = o, Bill = _svc.Calc(o) });
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id)
    {
        var o = await Mine(id);
        if (o == null) return RedirectToAction("Index", "Tables");
        if (o.Lines.Any(l => l.Status != St.New && l.Status != St.Cancel && l.Status != St.Wait)) { Err("Bếp đã chế biến món, không hủy được cả đơn."); return RedirectToAction(nameof(Screen), new { id }); }
        o.Status = St.Void; o.ClosedAt = DateTime.Now;
        await OrderService.StockGate.WaitAsync();
        try
        {
            foreach (var l in o.Lines.Where(l => l.Status != St.Cancel))
            {
                // Món đã gửi bếp (đã trừ kho) nhưng chưa làm thì hoàn lại kho.
                if (l.Status == St.Wait) await _svc.RestockAsync(l, BranchId, "Hủy đơn #" + o.Id);
                l.Status = St.Cancel;
            }
            await Db.SaveChangesAsync();
        }
        finally { OrderService.StockGate.Release(); }
        await _svc.LogAsync(BranchId, Actor, "Hủy đơn", "Đơn #" + o.Id);
        return RedirectToAction("Index", "Tables");
    }

    // ---------- Chuyển / gộp / tách bàn ----------
    [HttpPost]
    public async Task<IActionResult> Move(int id, int toTableId)
    {
        var o = await Mine(id); if (o == null) return RedirectToAction("Index", "Tables");
        if (await Db.Orders.AnyAsync(x => x.TableId == toTableId && x.Status == St.Open)) { Err("Bàn đích đang có khách."); return RedirectToAction(nameof(Screen), new { id }); }
        var t = await Db.Tables.FindAsync(toTableId);
        if (t == null || t.BranchId != BranchId) return RedirectToAction(nameof(Screen), new { id });
        o.TableId = toTableId; o.Type = "Tại bàn";
        await Db.SaveChangesAsync();
        await _svc.LogAsync(BranchId, Actor, "Chuyển bàn", $"Đơn #{id} sang {t.Name}");
        Ok("Đã chuyển sang " + t.Name);
        return RedirectToAction(nameof(Screen), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Merge(int id, int targetOrderId)
    {
        var src = await Mine(id); var dst = await Mine(targetOrderId);
        if (src == null || dst == null) return RedirectToAction("Index", "Tables");
        foreach (var l in src.Lines.ToList()) l.OrderId = dst.Id;
        dst.Pax += src.Pax;
        src.Status = St.Void; src.ClosedAt = DateTime.Now; src.Note = "Gộp vào đơn #" + dst.Id;
        await Db.SaveChangesAsync();
        await _svc.LogAsync(BranchId, Actor, "Gộp bàn", $"Đơn #{id} vào #{dst.Id}");
        Ok("Đã gộp đơn");
        return RedirectToAction(nameof(Screen), new { id = dst.Id });
    }

    [HttpPost]
    public async Task<IActionResult> Split(int id, int[] lineIds, int toTableId)
    {
        var src = await Mine(id); if (src == null) return RedirectToAction("Index", "Tables");
        if (lineIds == null || lineIds.Length == 0) { Err("Hãy chọn các món cần tách."); return RedirectToAction(nameof(Screen), new { id }); }
        var dst = await Db.Orders.FirstOrDefaultAsync(x => x.TableId == toTableId && x.Status == St.Open);
        if (dst == null)
        {
            dst = new Order { BranchId = BranchId, TableId = toTableId, Pax = 1, EmployeeId = src.EmployeeId, CustomerName = src.CustomerName };
            Db.Orders.Add(dst); await Db.SaveChangesAsync();
        }
        foreach (var l in src.Lines.Where(l => lineIds.Contains(l.Id))) l.OrderId = dst.Id;
        await Db.SaveChangesAsync();
        await _svc.LogAsync(BranchId, Actor, "Tách bàn", $"Đơn #{id} sang đơn #{dst.Id}");
        Ok("Đã tách món sang đơn #" + dst.Id);
        return RedirectToAction(nameof(Screen), new { id });
    }
}
