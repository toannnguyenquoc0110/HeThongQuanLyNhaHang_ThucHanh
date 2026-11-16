namespace QuanLyNhaHang.Models;

public class Employee
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public Branch Branch { get; set; }
    public string Code { get; set; }
    public string FullName { get; set; }
    public string Phone { get; set; }
    public Role Role { get; set; }
    public decimal BaseSalary { get; set; }   // lương cứng / tháng
    public decimal HourlyRate { get; set; }   // lương theo giờ
    public DateTime HireDate { get; set; }
    public bool Active { get; set; } = true;
}

public class Shift
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string StartTime { get; set; }   // "HH:mm"
    public string EndTime { get; set; }
}

public class ShiftAssignment
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; }
    public int ShiftId { get; set; }
    public Shift Shift { get; set; }
    public DateTime Date { get; set; }
}

public class Attendance
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; }
    public DateTime Date { get; set; }
    public DateTime? CheckIn { get; set; }
    public DateTime? CheckOut { get; set; }
}

public class LeaveRequest
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string Reason { get; set; }
    public string Status { get; set; } = "Chờ duyệt";
}

public class Payroll
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public double Hours { get; set; }
    public decimal Gross { get; set; }
    public decimal Deduction { get; set; }
    public decimal Net { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
