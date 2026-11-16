using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaHang.Data;
using System.Linq;
using System.Threading.Tasks;

namespace QuanLyNhaHang.ViewComponents
{
    public class LowStockAlertViewComponent : ViewComponent
    {
        private readonly AppDbContext _context;
        public LowStockAlertViewComponent(AppDbContext context) => _context = context;

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // Lấy ID chi nhánh hiện tại (mặc định là 1 nếu chưa đăng nhập)
            int branchId = 1;
            if (HttpContext.User.HasClaim(c => c.Type == "BranchId"))
            {
                int.TryParse(HttpContext.User.FindFirst("BranchId").Value, out branchId);
            }

            // Tìm nguyên liệu dưới mức tối thiểu tại chi nhánh này
            var lowStockItems = await _context.BranchStocks
                .Include(b => b.Ingredient)
                .Where(b => b.BranchId == branchId && b.Quantity <= b.Ingredient.MinLevel)
                .ToListAsync();

            return View(lowStockItems);
        }
    }
}