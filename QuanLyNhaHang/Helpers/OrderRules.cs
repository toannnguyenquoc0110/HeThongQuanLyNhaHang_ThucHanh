using System.Linq.Expressions;
using QuanLyNhaHang.Models;

namespace QuanLyNhaHang.Helpers;

/// <summary>
/// Đơn nào được tính vào doanh thu: đơn tại bàn đã thanh toán, và đơn online đã thu tiền (COD đã xác nhận).
/// Doanh thu luôn tính theo Order.BranchId, tức chi nhánh mà khách đã chọn khi đặt.
/// </summary>
public static class OrderRules
{
    public static readonly Expression<Func<Order, bool>> IsRevenue =
        o => o.Status == St.Paid || (o.Type == "Online" && o.PaymentStatus == "Đã thanh toán");

    public static bool IsRevenueOrder(Order o) =>
        o.Status == St.Paid || (o.Type == "Online" && o.PaymentStatus == "Đã thanh toán");
}
