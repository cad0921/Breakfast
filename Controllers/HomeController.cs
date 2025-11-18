using BreakFastShop.Models;
using BreakFastShop.Infrastructure;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace BreakFastShop.Controllers
{
    public class HomeController : Controller
    {
        private string ConnStr => ConfigurationManager.ConnectionStrings["BreakfastShop"]?.ConnectionString;

        public ActionResult Index()
        {
            return View();
        }

        //訂單
        public async Task<ActionResult> Order(Guid? id, Guid? shopId)
        {
            var model = new OrderPageViewModel();
            var hasTableId = id.HasValue && id.Value != Guid.Empty;
            var hasShopId = shopId.HasValue && shopId.Value != Guid.Empty;

            if (!hasTableId && !hasShopId)
            {
                return View(model);
            }

            var connStr = ConnStr;

            if (string.IsNullOrWhiteSpace(connStr))
            {
                model.Error = "尚未設定資料庫連線字串。";
                return View(model);
            }

            const string tableSql = @"SELECT TOP 1 t.Id, t.Number, t.ShopId, s.Name, t.Zone
                                 FROM [Table] t
                                 INNER JOIN Shop s ON s.Id = t.ShopId
                                 WHERE t.Id=@Id AND t.IsActive=1 AND s.IsActive=1";

            const string categoriesSql = @"SELECT Id,Name
                                 FROM MealCategory
                                 WHERE ShopId=@ShopId AND IsActive=1
                                 ORDER BY SortOrder,Name";

            const string mealsSql = @"SELECT m.Id,m.Name,m.Money,m.Element,m.CategoryId,c.Name,m.OptionsJson
                                FROM Meals m
                                LEFT JOIN MealCategory c ON c.Id = m.CategoryId AND c.IsActive=1
                                WHERE m.ShopId=@ShopId AND m.IsActive=1
                                ORDER BY c.SortOrder,c.Name,m.Name";

            const string shopSql = @"SELECT TOP 1 Id,Name
                                 FROM Shop
                                 WHERE Id=@Id AND IsActive=1";

            try
            {
                using (var connection = new SqlConnection(connStr))
                {
                    await connection.OpenAsync();

                    if (hasTableId)
                    {
                        using (var tableCommand = new SqlCommand(tableSql, connection))
                        {
                            tableCommand.Parameters.AddWithValue("@Id", id.Value);

                            using (var reader = await tableCommand.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    model.Table = new OrderTableInfo
                                    {
                                        Id = reader.GetGuid(0),
                                        Number = reader.GetInt32(1),
                                        ShopId = reader.GetGuid(2),
                                        ShopName = reader.IsDBNull(3) ? null : reader.GetString(3),
                                        Zone = reader.IsDBNull(4) ? null : reader.GetString(4)
                                    };
                                }
                            }
                        }

                        if (model.Table != null)
                        {
                            await LoadShopMealsAsync(connection, categoriesSql, mealsSql, model.Table.ShopId, model);
                        }
                        else
                        {
                            model.Error = "查無對應桌號。";
                        }
                    }
                    else if (hasShopId)
                    {
                        using (var shopCommand = new SqlCommand(shopSql, connection))
                        {
                            shopCommand.Parameters.AddWithValue("@Id", shopId.Value);

                            using (var reader = await shopCommand.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    model.Shop = new OrderShopInfo
                                    {
                                        Id = reader.GetGuid(0),
                                        Name = reader.IsDBNull(1) ? null : reader.GetString(1)
                                    };
                                }
                            }
                        }

                        if (model.Shop != null)
                        {
                            await LoadShopMealsAsync(connection, categoriesSql, mealsSql, model.Shop.Id, model);
                        }
                        else
                        {
                            model.Error = "沒有此店家。";
                        }
                    }
                }
            }
            catch
            {
                model.Error = hasTableId ? "查詢桌號時發生錯誤。" : "查詢店家時發生錯誤。";
            }

            return View(model);
        }

        private static async Task LoadShopMealsAsync(SqlConnection connection, string categoriesSql, string mealsSql, Guid shopId, OrderPageViewModel model)
        {
            using (var categoriesCommand = new SqlCommand(categoriesSql, connection))
            {
                categoriesCommand.Parameters.AddWithValue("@ShopId", shopId);

                using (var reader = await categoriesCommand.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        model.Categories.Add(new OrderMealCategoryInfo
                        {
                            Id = reader.GetGuid(0),
                            Name = reader.GetString(1)
                        });
                    }
                }
            }

            using (var mealsCommand = new SqlCommand(mealsSql, connection))
            {
                mealsCommand.Parameters.AddWithValue("@ShopId", shopId);

                using (var reader = await mealsCommand.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        model.Meals.Add(new OrderMealInfo
                        {
                            Id = reader.GetGuid(0),
                            Name = reader.GetString(1),
                            Money = reader.GetDecimal(2),
                            Element = reader.IsDBNull(3) ? null : reader.GetString(3),
                            CategoryId = reader.IsDBNull(4) ? (Guid?)null : reader.GetGuid(4),
                            CategoryName = reader.IsDBNull(5) ? null : reader.GetString(5),
                            OptionsJson = reader.IsDBNull(6) ? null : reader.GetString(6)
                        });
                    }
                }
            }
        }


        //接收頁面
        [ShopAuthorize]
        public ActionResult Receive(string id)
        {
            var model = new ReceivePageViewModel();

            if (!string.IsNullOrWhiteSpace(id))
            {
                model.InitialShopId = id.Trim();
            }

            return View(model);
        }

        [HttpGet]
        public async Task<ActionResult> TableInfo(int number)
        {
            if (number <= 0)
            {
                Response.StatusCode = 400;
                return Json(new { ok = false, error = "桌號必須為正整數。" }, JsonRequestBehavior.AllowGet);
            }

            var connStr = ConnStr;
            if (string.IsNullOrWhiteSpace(connStr))
            {
                Response.StatusCode = 500;
                return Json(new { ok = false, error = "尚未設定資料庫連線字串。" }, JsonRequestBehavior.AllowGet);
            }

            const string sql = @"SELECT TOP 1 t.Id, t.Number, t.ShopId, s.Name, t.Zone
                                 FROM [Table] t
                                 INNER JOIN Shop s ON s.Id = t.ShopId
                                 WHERE t.Number=@Number AND t.IsActive=1 AND s.IsActive=1
                                 ORDER BY s.Name";

            try
            {
                using (var connection = new SqlConnection(connStr))
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@Number", number);
                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var result = new
                            {
                                ok = true,
                                tableId = reader.GetGuid(0),
                                number = reader.GetInt32(1),
                                shopId = reader.GetGuid(2),
                                shopName = reader.IsDBNull(3) ? null : reader.GetString(3),
                                zone = reader.IsDBNull(4) ? null : reader.GetString(4)
                            };

                            return Json(result, JsonRequestBehavior.AllowGet);
                        }
                    }
                }
            }
            catch
            {
                Response.StatusCode = 500;
                return Json(new { ok = false, error = "查詢桌號時發生錯誤。" }, JsonRequestBehavior.AllowGet);
            }

            Response.StatusCode = 404;
            return Json(new { ok = false, error = "查無對應桌號。" }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public async Task<ActionResult> ShopInfo(Guid? id)
        {
            if (!id.HasValue || id.Value == Guid.Empty)
            {
                Response.StatusCode = 400;
                return Json(new { ok = false, error = "店家識別碼無效。" }, JsonRequestBehavior.AllowGet);
            }

            var connStr = ConnStr;
            if (string.IsNullOrWhiteSpace(connStr))
            {
                Response.StatusCode = 500;
                return Json(new { ok = false, error = "尚未設定資料庫連線字串。" }, JsonRequestBehavior.AllowGet);
            }

            const string sql = @"SELECT TOP 1 Id,Name
                                 FROM Shop
                                 WHERE Id=@Id AND IsActive=1";

            try
            {
                using (var connection = new SqlConnection(connStr))
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@Id", id.Value);

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var shop = new
                            {
                                id = reader.GetGuid(0),
                                name = reader.IsDBNull(1) ? null : reader.GetString(1)
                            };

                            return Json(new { ok = true, shop }, JsonRequestBehavior.AllowGet);
                        }
                    }
                }
            }
            catch
            {
                Response.StatusCode = 500;
                return Json(new { ok = false, error = "查詢店家時發生錯誤。" }, JsonRequestBehavior.AllowGet);
            }

            Response.StatusCode = 404;
            return Json(new { ok = false, error = "沒有此店家" }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public async Task<ActionResult> ShopMeals(Guid? shopId)
        {
            if (!shopId.HasValue || shopId.Value == Guid.Empty)
            {
                Response.StatusCode = 400;
                return Json(new { ok = false, error = "店家識別碼無效。" }, JsonRequestBehavior.AllowGet);
            }

            var connStr = ConnStr;
            if (string.IsNullOrWhiteSpace(connStr))
            {
                Response.StatusCode = 500;
                return Json(new { ok = false, error = "尚未設定資料庫連線字串。" }, JsonRequestBehavior.AllowGet);
            }

            const string categoriesSql = @"SELECT Id,Name
                                 FROM MealCategory
                                 WHERE ShopId=@ShopId AND IsActive=1
                                 ORDER BY SortOrder,Name";

            const string sql = @"SELECT m.Id,m.Name,m.Money,m.Element,m.CategoryId,c.Name,m.OptionsJson
                                 FROM Meals m
                                 LEFT JOIN MealCategory c ON c.Id = m.CategoryId AND c.IsActive=1
                                 WHERE m.ShopId=@ShopId AND m.IsActive=1
                                 ORDER BY c.SortOrder,c.Name,m.Name";

            try
            {
                using (var connection = new SqlConnection(connStr))
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@ShopId", shopId.Value);

                    await connection.OpenAsync();

                    var categories = new List<object>();
                    using (var catCommand = new SqlCommand(categoriesSql, connection))
                    {
                        catCommand.Parameters.AddWithValue("@ShopId", shopId.Value);

                        using (var catReader = await catCommand.ExecuteReaderAsync())
                        {
                            while (await catReader.ReadAsync())
                            {
                                categories.Add(new
                                {
                                    id = catReader.GetGuid(0),
                                    name = catReader.GetString(1)
                                });
                            }
                        }
                    }

                    var list = new List<object>();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new
                            {
                                id = reader.GetGuid(0),
                                name = reader.GetString(1),
                                money = reader.GetDecimal(2),
                                element = reader.IsDBNull(3) ? null : reader.GetString(3),
                                categoryId = reader.IsDBNull(4) ? (Guid?)null : reader.GetGuid(4),
                                categoryName = reader.IsDBNull(5) ? null : reader.GetString(5),
                                optionsJson = reader.IsDBNull(6) ? null : reader.GetString(6)
                            });
                        }
                    }

                    return Json(new { ok = true, items = list, categories }, JsonRequestBehavior.AllowGet);
                }
            }
            catch
            {
                Response.StatusCode = 500;
                return Json(new { ok = false, error = "查詢餐點時發生錯誤。" }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public async Task<ActionResult> ShopTables(Guid? shopId)
        {
            if (!shopId.HasValue || shopId.Value == Guid.Empty)
            {
                Response.StatusCode = 400;
                return Json(new { ok = false, error = "店家識別碼無效。" }, JsonRequestBehavior.AllowGet);
            }

            var connStr = ConnStr;
            if (string.IsNullOrWhiteSpace(connStr))
            {
                Response.StatusCode = 500;
                return Json(new { ok = false, error = "尚未設定資料庫連線字串。" }, JsonRequestBehavior.AllowGet);
            }

            const string sql = @"SELECT Id,Number,Zone
                                 FROM [Table]
                                 WHERE ShopId=@ShopId AND IsActive=1
                                 ORDER BY Number";

            try
            {
                using (var connection = new SqlConnection(connStr))
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@ShopId", shopId.Value);

                    await connection.OpenAsync();

                    var list = new List<object>();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new
                            {
                                id = reader.GetGuid(0),
                                number = reader.GetInt32(1),
                                zone = reader.IsDBNull(2) ? null : reader.GetString(2)
                            });
                        }
                    }

                    return Json(new { ok = true, items = list }, JsonRequestBehavior.AllowGet);
                }
            }
            catch
            {
                Response.StatusCode = 500;
                return Json(new { ok = false, error = "查詢桌號時發生錯誤。" }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public async Task<ActionResult> TodayOrders(Guid? shopId)
        {
            if (!shopId.HasValue || shopId.Value == Guid.Empty)
            {
                Response.StatusCode = 400;
                return Json(new { ok = false, error = "店家識別碼無效。" }, JsonRequestBehavior.AllowGet);
            }

            var connStr = ConnStr;
            if (string.IsNullOrWhiteSpace(connStr))
            {
                Response.StatusCode = 500;
                return Json(new { ok = false, error = "尚未設定資料庫連線字串。" }, JsonRequestBehavior.AllowGet);
            }

            var startOfDay = DateTime.UtcNow.Date;
            var endOfDay = startOfDay.AddDays(1);

            const string ordersSql = @"SELECT o.Id,o.OrderType,o.TableId,t.Number,t.Zone,o.TakeoutCode,o.Notes,o.Status,o.CreatedAt,o.UpdatedAt,s.Name
                                 FROM Orders o
                                 LEFT JOIN [Table] t ON t.Id = o.TableId
                                 LEFT JOIN Shop s ON s.Id = o.ShopId
                                 WHERE o.ShopId=@ShopId AND o.CreatedAt>=@StartOfDay AND o.CreatedAt<@EndOfDay
                                 ORDER BY o.CreatedAt";

            const string itemsSql = @"SELECT oi.OrderId,oi.MealId,oi.MealName,oi.Quantity,oi.UnitPrice,oi.Notes
                                 FROM OrderItems oi
                                 INNER JOIN Orders o ON o.Id = oi.OrderId
                                 WHERE o.ShopId=@ShopId AND o.CreatedAt>=@StartOfDay AND o.CreatedAt<@EndOfDay";

            try
            {
                using (var connection = new SqlConnection(connStr))
                {
                    await connection.OpenAsync();

                    var orders = new List<dynamic>();
                    var orderItemsMap = new Dictionary<Guid, List<object>>();

                    using (var ordersCommand = new SqlCommand(ordersSql, connection))
                    {
                        ordersCommand.Parameters.AddWithValue("@ShopId", shopId.Value);
                        ordersCommand.Parameters.AddWithValue("@StartOfDay", startOfDay);
                        ordersCommand.Parameters.AddWithValue("@EndOfDay", endOfDay);

                        using (var reader = await ordersCommand.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var orderId = reader.GetGuid(0);

                                orders.Add(new
                                {
                                    Id = orderId,
                                    OrderType = reader.IsDBNull(1) ? null : reader.GetString(1),
                                    TableId = reader.IsDBNull(2) ? (Guid?)null : reader.GetGuid(2),
                                    TableNumber = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3),
                                    TableZone = reader.IsDBNull(4) ? null : reader.GetString(4),
                                    TakeoutCode = reader.IsDBNull(5) ? null : reader.GetString(5),
                                    Notes = reader.IsDBNull(6) ? null : reader.GetString(6),
                                    Status = reader.IsDBNull(7) ? "Pending" : reader.GetString(7),
                                    CreatedAt = reader.IsDBNull(8) ? (DateTime?)null : reader.GetDateTime(8),
                                    UpdatedAt = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTime(9),
                                    ShopName = reader.IsDBNull(10) ? null : reader.GetString(10)
                                });

                                orderItemsMap[orderId] = new List<object>();
                            }
                        }
                    }

                    if (orders.Count > 0)
                    {
                        using (var itemsCommand = new SqlCommand(itemsSql, connection))
                        {
                            itemsCommand.Parameters.AddWithValue("@ShopId", shopId.Value);
                            itemsCommand.Parameters.AddWithValue("@StartOfDay", startOfDay);
                            itemsCommand.Parameters.AddWithValue("@EndOfDay", endOfDay);

                            using (var reader = await itemsCommand.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    var orderId = reader.GetGuid(0);
                                    if (!orderItemsMap.TryGetValue(orderId, out var items))
                                    {
                                        continue;
                                    }

                                    items.Add(new
                                    {
                                        mealId = reader.IsDBNull(1) ? (Guid?)null : reader.GetGuid(1),
                                        name = reader.IsDBNull(2) ? null : reader.GetString(2),
                                        qty = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                                        price = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                                        notes = reader.IsDBNull(5) ? null : reader.GetString(5)
                                    });
                                }
                            }
                        }
                    }

                    var payloads = new List<object>();

                    foreach (var order in orders)
                    {
                        var orderId = (Guid)order.Id;
                        var items = orderItemsMap.TryGetValue(orderId, out var list) ? list : new List<object>();
                        var orderType = string.IsNullOrWhiteSpace(order.OrderType) ? "DineIn" : order.OrderType;
                        var status = string.IsNullOrWhiteSpace(order.Status) ? "Pending" : order.Status;
                        var createdAt = (DateTime?)(order.CreatedAt ?? DateTime.UtcNow);
                        var updatedAt = (DateTime?)(order.UpdatedAt ?? createdAt);

                        var orderPayload = new
                        {
                            id = orderId,
                            shopId = shopId.Value,
                            tableId = (Guid?)order.TableId,
                            tableNumber = (int?)order.TableNumber,
                            tableZone = (string)order.TableZone,
                            takeoutCode = (string)order.TakeoutCode,
                            notes = (string)order.Notes,
                            status,
                            orderType,
                            createdAt,
                            updatedAt,
                            items
                        };

                        var dto = new
                        {
                            shopId = shopId.Value,
                            shopName = (string)order.ShopName,
                            tableId = (Guid?)order.TableId,
                            tableNumber = (int?)order.TableNumber,
                            tableZone = (string)order.TableZone,
                            takeoutCode = (string)order.TakeoutCode,
                            orderType,
                            notes = (string)order.Notes,
                            items
                        };

                        payloads.Add(new
                        {
                            type = "created",
                            order = orderPayload,
                            dto,
                            ts = createdAt
                        });
                    }

                    return Json(new { ok = true, orders = payloads }, JsonRequestBehavior.AllowGet);
                }
            }
            catch
            {
                Response.StatusCode = 500;
                return Json(new { ok = false, error = "讀取今日訂單時發生錯誤。" }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}
