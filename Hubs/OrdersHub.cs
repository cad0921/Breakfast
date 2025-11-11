using Microsoft.AspNet.SignalR;
using System;

namespace BreakFastShop.Hubs
{
    public class OrdersHub : Hub
    {
        public void CreateOrder(object dto)
        {
            Clients.All.orderChanged(new { type = "created", dto, ts = DateTimeOffset.UtcNow });
        }
    }
}
