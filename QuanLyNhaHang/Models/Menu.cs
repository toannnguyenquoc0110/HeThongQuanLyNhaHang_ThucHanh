namespace QuanLyNhaHang.Models;

public class MenuCategory
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int SortOrder { get; set; }
    public List<MenuItem> Items { get; set; } = new();
}

public class MenuItem
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public MenuCategory Category { get; set; }
    public string Name { get; set; }
    public string Icon { get; set; }          // emoji hiển thị khi chưa có ảnh
    public string ImageUrl { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }        // giá bán
    public decimal Cost { get; set; }         // giá vốn
    public int PrepMinutes { get; set; } = 10; // thời gian chế biến dự kiến
    public string Allergens { get; set; }     // ghi chú dị ứng
    public bool Stopped { get; set; }         // tạm ngừng / báo hết
    public List<RecipeLine> Recipes { get; set; } = new();
}

public class Combo
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public bool Active { get; set; } = true;
    public List<ComboItem> Items { get; set; } = new();
}

public class ComboItem
{
    public int Id { get; set; }
    public int ComboId { get; set; }
    public Combo Combo { get; set; }
    public int MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; }
    public int Qty { get; set; } = 1;
}

/// <summary>Khuyến mãi theo khung giờ (happy hour), có thể giới hạn theo nhóm món.</summary>
public class Promotion
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int Percent { get; set; }
    public string StartTime { get; set; }
    public string EndTime { get; set; }
    public int? CategoryId { get; set; }
    public MenuCategory Category { get; set; }
    public bool Active { get; set; } = true;
}

public class Coupon
{
    public int Id { get; set; }
    public string Code { get; set; }
    public int Percent { get; set; }       // giảm theo %
    public decimal Amount { get; set; }    // hoặc giảm số tiền cố định
    public decimal MinTotal { get; set; }
    public DateTime? ExpireDate { get; set; }
    public bool Active { get; set; } = true;
    public int UsedCount { get; set; }
}
