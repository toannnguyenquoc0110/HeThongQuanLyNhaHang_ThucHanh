namespace QuanLyNhaHang.Models;

public class Customer
{
    public int Id { get; set; }
    public string FullName { get; set; }
    public string Phone { get; set; }         // Dùng số điện thoại làm tài khoản đăng nhập chính của khách
    public string Password { get; set; }      // Mật khẩu (hoặc đăng nhập nhanh bằng OTP/SĐT)
    public string Address { get; set; }       // Địa chỉ giao hàng mặc định
    public int? BranchId { get; set; }        // Chi nhánh quen thuộc
    public Branch Branch { get; set; }
}