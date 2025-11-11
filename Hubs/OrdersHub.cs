using BreakFastShop.Models;
using Microsoft.AspNet.SignalR;
using System;
using System.Threading.Tasks;

namespace BreakFastShop.Hubs
{
    public class OrdersHub : Hub
    {
        private static string BuildShopGroupName(Guid shopId) => $"shop:{shopId:D}";

        public void CreateOrder(OrderCreateDto dto)
        {
            if (dto == null || dto.ShopId == Guid.Empty)
            {
                Clients.Caller.orderChanged(new { type = "error", error = "店家識別碼無效。" });
                return;
            }

            var payload = new { type = "created", dto, ts = DateTimeOffset.UtcNow };
            Clients.Group(BuildShopGroupName(dto.ShopId)).orderChanged(payload);
            Clients.Caller.orderChanged(payload);
        }

        public async Task<bool> JoinShop(Guid shopId)
        {
            if (shopId == Guid.Empty)
            {
                return false;
            }

            await Groups.Add(Context.ConnectionId, BuildShopGroupName(shopId));
            return true;
        }

        public Task LeaveShop(Guid shopId)
        {
            if (shopId == Guid.Empty)
            {
                return Task.FromResult<object>(null);
            }

            return Groups.Remove(Context.ConnectionId, BuildShopGroupName(shopId));
        }
    }
}
