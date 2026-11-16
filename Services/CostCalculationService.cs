using Microsoft.EntityFrameworkCore;
using QuanLyNhaHang.Data;

namespace QuanLyNhaHang.Services
{
    public interface ICostCalculationService
    {
        Task UpdateMenuItemCostAsync(int menuItemId);
        Task UpdateAllMenuCostsAsync();
    }

    public class CostCalculationService : ICostCalculationService
    {
        private readonly AppDbContext _context;

        public CostCalculationService(AppDbContext context)
        {
            _context = context;
        }

        // Tự động tính và cập nhật giá vốn cho một món ăn cụ thể
        public async Task UpdateMenuItemCostAsync(int menuItemId)
        {
            var menuItem = await _context.MenuItems
                .Include(m => m.Recipes)
                .ThenInclude(r => r.Ingredient)
                .FirstOrDefaultAsync(m => m.Id == menuItemId);

            if (menuItem == null || menuItem.Recipes == null || !menuItem.Recipes.Any())
                return;

            decimal totalCost = 0;
            foreach (var recipe in menuItem.Recipes)
            {
                if (recipe.Ingredient != null)
                {
                    // Công thức: Định lượng × Đơn giá nguyên liệu chuẩn
                    totalCost += recipe.Qty * recipe.Ingredient.Cost;
                }
            }

            // Gán giá vốn tự động tính được vào MenuItem
            menuItem.Cost = Math.Round(totalCost, 0);
            _context.MenuItems.Update(menuItem);
            await _context.SaveChangesAsync();
        }

        // Tự động cập nhật giá vốn cho toàn bộ thực đơn trong nhà hàng
        public async Task UpdateAllMenuCostsAsync()
        {
            var menuItems = await _context.MenuItems.Select(m => m.Id).ToListAsync();
            foreach (var id in menuItems)
            {
                await UpdateMenuItemCostAsync(id);
            }
        }
    }
}