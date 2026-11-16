using System.ComponentModel.DataAnnotations;

namespace QuanLyNhaHang.Models;

public class CustomerAccount
{
    public int Id { get; set; }
    [Required, StringLength(120)] public string FullName { get; set; } = "";
    [Required, EmailAddress, StringLength(180)] public string Email { get; set; } = "";
    [Required, StringLength(300)] public string PasswordHash { get; set; } = "";
    [StringLength(30)] public string Phone { get; set; } = "";
    [StringLength(500)] public string DefaultAddress { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool Active { get; set; } = true;
}
