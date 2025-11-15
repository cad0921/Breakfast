using System;
using System.Collections.Generic;

namespace BreakFastShop.Models
{
    public class OrderCreateDto
    {
        public Guid ShopId { get; set; }

        public string ShopName { get; set; }

        public Guid TableId { get; set; }

        public int TableNumber { get; set; }

        public string TableZone { get; set; }

        public string Notes { get; set; }

        public List<OrderItemDto> Items { get; set; } = new List<OrderItemDto>();
    }

    public class OrderItemDto
    {
        public Guid? MealId { get; set; }

        public string Name { get; set; }

        public int Qty { get; set; }

        public decimal Price { get; set; }

        public string Notes { get; set; }
    }
}
