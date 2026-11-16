using System.Globalization;

namespace QuanLyNhaHang.Helpers;

/// <summary>Định dạng hiển thị dùng chung trong các View.</summary>
public static class Fmt
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public static string Vnd(decimal v) => v.ToString("N0", Inv).Replace(",", ".") + "đ";
    public static string Dmy(DateTime d) => d.ToString("dd/MM/yyyy", Inv);
    public static string Hm(DateTime? d) => d.HasValue ? d.Value.ToString("HH:mm", Inv) : "—";
    public static string Num(decimal q) => q.ToString("0.##", Inv);
    public static string Day(DateTime d) => d.ToString("ddd dd/MM", new CultureInfo("vi-VN"));

    /// <summary>Tên lớp CSS (màu) cho nhãn trạng thái.</summary>
    public static string Chip(string s) => s switch
    {
        "Hoàn thành" or "Đã phục vụ" or "Đã thanh toán" or "Đã xác nhận" or "Đã nhận" or "Đã duyệt" or "Đủ" or "Trống" => "g",
        "Chờ chế biến" or "Chờ duyệt" or "Đang đặt" or "Sắp hết" or "Đã đặt" or "Mới" => "a",
        "Đang chế biến" or "Đang phục vụ" or "Đang dùng" or "Đã đến" => "b",
        "Đã hủy" or "Từ chối" or "Hết" or "Không đến" or "Tạm hết" => "r",
        _ => "n",
    };
}
