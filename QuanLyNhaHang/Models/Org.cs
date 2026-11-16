namespace QuanLyNhaHang.Models;

public class Branch
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Address { get; set; }
    public string Phone { get; set; }
    public string OpenHours { get; set; }
    public decimal ServiceRate { get; set; } = 0.05m;   // phí phục vụ
    public decimal VatRate { get; set; } = 0.08m;       // VAT
    public List<Zone> Zones { get; set; } = new();
    public List<DiningTable> Tables { get; set; } = new();
    public List<Employee> Employees { get; set; } = new();
}

public class Zone
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public Branch Branch { get; set; }
    public string Name { get; set; }
    public List<DiningTable> Tables { get; set; } = new();
}

public class DiningTable
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public Branch Branch { get; set; }
    public int ZoneId { get; set; }
    public Zone Zone { get; set; }
    public string Name { get; set; }
    public int Seats { get; set; }
}

public class AuditLog
{
    public int Id { get; set; }
    public DateTime At { get; set; } = DateTime.Now;
    public int BranchId { get; set; }
    public string Actor { get; set; }
    public string Action { get; set; }
    public string Detail { get; set; }
}
