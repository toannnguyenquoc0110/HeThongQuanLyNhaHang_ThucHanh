namespace QuanLyNhaHang.Models;

public class Reservation
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public Branch Branch { get; set; }
    public int TableId { get; set; }
    public DiningTable Table { get; set; }
    public string CustomerName { get; set; }
    public string Phone { get; set; }
    public DateTime Date { get; set; }
    public string Time { get; set; }       // "HH:mm"
    public int Pax { get; set; }
    public string Note { get; set; }
    public decimal Deposit { get; set; }
    public string Status { get; set; } = St.Confirmed;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class Order
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public Branch Branch { get; set; }
    public int? TableId { get; set; }          // null = đơn mang về
    public DiningTable Table { get; set; }
    public int? ReservationId { get; set; }
    public Reservation Reservation { get; set; }
    public int? EmployeeId { get; set; }       // nhân viên phụ trách
    public Employee Employee { get; set; }
    public string Type { get; set; } = "Tại bàn";
    // Thông tin phục vụ đơn online/giao hàng; nullable để bảo toàn các đơn cũ.
    public int? CustomerAccountId { get; set; }
    public CustomerAccount CustomerAccount { get; set; }
    public string FulfillmentType { get; set; } = "Tại quán"; // Giao tận nhà / Tự đến lấy / Tại quán
    public string DeliveryAddress { get; set; }
    public string DeliveryPhone { get; set; }
    public string DriverNote { get; set; }
    public string PaymentMethod { get; set; } = "COD"; // COD / ONLINE; chỉ là lựa chọn, không chứng minh đã thanh toán
    public string PaymentStatus { get; set; } = "Chưa thanh toán";
    public string RejectReason { get; set; }
    public bool StockDeducted { get; set; }
    public DateTime? StockDeductedAt { get; set; }
    public string CustomerName { get; set; }
    public int Pax { get; set; }
    public DateTime OpenedAt { get; set; } = DateTime.Now;
    public DateTime? ClosedAt { get; set; }
    public string Status { get; set; } = St.Open;
    public int? CouponId { get; set; }
    public Coupon Coupon { get; set; }
    public int DiscountPct { get; set; }
    public string Note { get; set; }
    public decimal Total { get; set; }         // chốt khi thanh toán
    public List<OrderLine> Lines { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
}

public class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; }
    public int? MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; }
    public int? ComboId { get; set; }
    public Combo Combo { get; set; }
    public string Name { get; set; }
    public int Qty { get; set; } = 1;
    public decimal Price { get; set; }         // giá bán tại thời điểm gọi (đã tính khuyến mãi)
    public decimal Cost { get; set; }          // giá vốn tại thời điểm gọi
    public string Note { get; set; }
    public string Status { get; set; } = St.New;
    public bool Priority { get; set; }
    public int PrepMinutes { get; set; } = 10;
    public DateTime? SentAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? DoneAt { get; set; }
    public DateTime? ServedAt { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; }
    public string Method { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.Now;
}

public class Expense
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public Branch Branch { get; set; }
    public DateTime Date { get; set; }
    public string Category { get; set; }
    public decimal Amount { get; set; }
    public string Note { get; set; }
}

/// <summary>Đóng ca: đối soát tiền mặt thực tế với tiền mặt trên hệ thống.</summary>
public class CashClose
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public DateTime At { get; set; } = DateTime.Now;
    public decimal Expected { get; set; }
    public decimal Counted { get; set; }
    public string Note { get; set; }
}
