using System;

namespace BreakFastShop.Models
{
    public class ReceivePageViewModel
    {
        public string InitialShopId { get; set; }

        public bool HasInitialShopId => !string.IsNullOrWhiteSpace(InitialShopId);
    }
}
