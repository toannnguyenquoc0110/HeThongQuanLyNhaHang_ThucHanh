namespace QuanLyNhaHang.Models;

public class Supplier
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Phone { get; set; }
    public string Address { get; set; }
    public string Note { get; set; }
}

public class Ingredient
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Unit { get; set; }
    public decimal MinLevel { get; set; }   // dưới mức này = sắp hết
    public decimal Cost { get; set; }       // giá vốn / đơn vị
}

/// <summary>Tồn kho của từng nguyên liệu tại từng chi nhánh.</summary>
public class BranchStock
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public Branch Branch { get; set; }
    public int IngredientId { get; set; }
    public Ingredient Ingredient { get; set; }
    public decimal Quantity { get; set; }
    public DateTime? ExpiryDate { get; set; }
}

public class RecipeLine
{
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; }
    public int IngredientId { get; set; }
    public Ingredient Ingredient { get; set; }
    public decimal Qty { get; set; }
}

public class PurchaseOrder
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public Branch Branch { get; set; }
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; }
    public string Code { get; set; }
    public DateTime Date { get; set; }
    public string Status { get; set; } = "Đang đặt";   // Đang đặt / Đã nhận
    public DateTime? ExpectedAt { get; set; }            // giờ dự kiến nhận hàng
    public bool Auto { get; set; }                       // đơn do hệ thống tự đặt
    public string Note { get; set; }
    public List<PurchaseLine> Lines { get; set; } = new();
}

public class PurchaseLine
{
    public int Id { get; set; }
    public int PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; }
    public int IngredientId { get; set; }
    public Ingredient Ingredient { get; set; }
    public decimal Qty { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime? ExpiryDate { get; set; }
}

public class StockMovement
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public int IngredientId { get; set; }
    public Ingredient Ingredient { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
    public decimal Qty { get; set; }       // dương = nhập, âm = xuất
    public string Type { get; set; }       // Nhập / Xuất bán / Hao hụt / Kiểm kê
    public string Ref { get; set; }
    public string Note { get; set; }
    public string OperationKey { get; set; } // duy nhất theo nghiệp vụ kho, nullable cho dữ liệu lịch sử
}
