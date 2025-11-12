using System;
using System.Collections.Generic;

namespace BreakFastShop.Models
{
    public class OrderPageViewModel
    {
        public OrderTableInfo Table { get; set; }

        public List<OrderMealInfo> Meals { get; set; } = new List<OrderMealInfo>();

        public List<OrderMealCategoryInfo> Categories { get; set; } = new List<OrderMealCategoryInfo>();

        public string Error { get; set; }
    }

    public class OrderTableInfo
    {
        public Guid Id { get; set; }

        public int Number { get; set; }

        public Guid ShopId { get; set; }

        public string ShopName { get; set; }

        public string Zone { get; set; }
    }

    public class OrderMealInfo
    {
        public Guid Id { get; set; }

        public string Name { get; set; }

        public decimal Money { get; set; }

        public string Element { get; set; }

        public Guid? CategoryId { get; set; }

        public string CategoryName { get; set; }
    }

    public class OrderMealCategoryInfo
    {
        public Guid Id { get; set; }

        public string Name { get; set; }
    }
}
