using System.ComponentModel.DataAnnotations;

namespace QuanLyNhaHang.Models;

/// <summary>Tài khoản đăng nhập nội bộ, tách biệt với hồ sơ nhân viên và khách hàng.</summary>
public class StaffAccount
{
    public int Id { get; set; }
    [Required, StringLength(180)] public string Email { get; set; } = "";
    [Required, StringLength(120)] public string FullName { get; set; } = "";
    [Required, StringLength(300)] public string PasswordHash { get; set; } = "";
    public Role Role { get; set; }
    public int BranchId { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
