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
        public ActionResult Order()
        {
            return View();
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
    }
}
