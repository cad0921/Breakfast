using BreakFastShop.Models;
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
        public async Task<ActionResult> Order(Guid? id)
        {
            if (!id.HasValue || id.Value == Guid.Empty)
            {
                return View(new OrderPageViewModel());
            }

            var model = new OrderPageViewModel();
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

            const string mealsSql = @"SELECT Id,Name,Money,Element
                                FROM Meals
                                WHERE ShopId=@ShopId AND IsActive=1
                                ORDER BY Name";

            try
            {
                using (var connection = new SqlConnection(connStr))
                {
                    await connection.OpenAsync();

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
                        using (var mealsCommand = new SqlCommand(mealsSql, connection))
                        {
                            mealsCommand.Parameters.AddWithValue("@ShopId", model.Table.ShopId);

                            using (var reader = await mealsCommand.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    model.Meals.Add(new OrderMealInfo
                                    {
                                        Id = reader.GetGuid(0),
                                        Name = reader.GetString(1),
                                        Money = reader.GetDecimal(2),
                                        Element = reader.IsDBNull(3) ? null : reader.GetString(3)
                                    });
                                }
                            }
                        }
                    }
                    else
                    {
                        model.Error = "查無對應桌號。";
                    }
                }
            }
            catch
            {
                model.Error = "查詢桌號時發生錯誤。";
            }

            return View(model);
        }


        //接收頁面
        public ActionResult Receive() {
            return View();
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

            const string sql = @"SELECT Id,Name,Money,Element
                                 FROM Meals
                                 WHERE ShopId=@ShopId AND IsActive=1
                                 ORDER BY Name";

            try
            {
                using (var connection = new SqlConnection(connStr))
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@ShopId", shopId.Value);

                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        var list = new List<object>();

                        while (await reader.ReadAsync())
                        {
                            list.Add(new
                            {
                                id = reader.GetGuid(0),
                                name = reader.GetString(1),
                                money = reader.GetDecimal(2),
                                element = reader.IsDBNull(3) ? null : reader.GetString(3)
                            });
                        }

                        return Json(new { ok = true, items = list }, JsonRequestBehavior.AllowGet);
                    }
                }
            }
            catch
            {
                Response.StatusCode = 500;
                return Json(new { ok = false, error = "查詢餐點時發生錯誤。" }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}
