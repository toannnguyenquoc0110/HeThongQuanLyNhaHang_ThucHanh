using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace QuanLyNhaHang.Hubs
{
    public class RestaurantHub : Hub
    {
        public async Task SendNewOrderToKitchen()
        {
            await Clients.Group("Kitchen").SendAsync("ReceiveNewOrder");
        }

        public async Task NotifyDishReady(string tenMon, string tenBan)
        {
            await Clients.Group("Staff").SendAsync("ReceiveDishReady", tenMon, tenBan);
        }

        public override async Task OnConnectedAsync()
        {
            // Tạm thời cho tất cả vào 2 group để test
            await Groups.AddToGroupAsync(Context.ConnectionId, "Kitchen");
            await Groups.AddToGroupAsync(Context.ConnectionId, "Staff");
            await base.OnConnectedAsync();
        }
    }
}