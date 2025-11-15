using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace BreakFastShop.Controllers
{
    public class ShopController : Controller
    {
        private const string ShopSessionKey = "__SHOP_USER";

        private string ConnStr => ConfigurationManager.ConnectionStrings["BreakfastShop"].ConnectionString;

        private ShopSessionInfo CurrentShop => Session[ShopSessionKey] as ShopSessionInfo;

        private Guid? CurrentShopId => CurrentShop?.Id;

        private bool IsAuthenticated => CurrentShop != null;

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var allowAnonymous = filterContext.ActionDescriptor.IsDefined(typeof(AllowAnonymousAttribute), inherit: true);
            if (!allowAnonymous && !IsAuthenticated)
            {
                if (filterContext.HttpContext.Request.IsAjaxRequest())
                {
                    filterContext.HttpContext.Response.StatusCode = 401;
                    filterContext.Result = Json(new { ok = false, error = "尚未登入，請重新登入店家後台。" }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    TempData["Alert"] = "請先登入店家後台。";
                    filterContext.Result = RedirectToAction("Login");
                }
                return;
            }

            if (IsAuthenticated)
            {
                ViewBag.ShopName = CurrentShop.Name;
                ViewBag.ShopAccount = CurrentShop.Account;
            }

            base.OnActionExecuting(filterContext);
        }

        [AllowAnonymous]
        public ActionResult Login()
        {
            if (IsAuthenticated)
            {
                return RedirectToAction("Index");
            }

            ViewBag.Message = TempData["Alert"] as string;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(string account, string password)
        {
            if (IsAuthenticated)
            {
                return RedirectToAction("Index");
            }

            ViewBag.Message = TempData["Alert"] as string;

            if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "請輸入帳號與密碼";
                ViewBag.Account = account;
                Response.StatusCode = 400;
                return View();
            }

            var info = await AuthenticateShopAsync(account, password);
            if (info != null)
            {
                Session[ShopSessionKey] = info;
                TempData["Welcome"] = $"歡迎回來，{info.Name}!";
                return RedirectToAction("Index");
            }

            ViewBag.Error = "帳號或密碼錯誤，或店家已停用";
            ViewBag.Account = account;
            Response.StatusCode = 401;
            return View();
        }

        public ActionResult Logout()
        {
            Session.Remove(ShopSessionKey);
            TempData["Alert"] = "您已成功登出。";
            return RedirectToAction("Login");
        }

        public async Task<ActionResult> Index()
        {
            var shopId = CurrentShopId;
            if (!shopId.HasValue)
            {
                return RedirectToAction("Login");
            }

            var profile = await GetShopProfileAsync(shopId.Value);
            if (profile != null)
            {
                ViewBag.ShopPhone = profile.Phone;
                ViewBag.ShopAddr = profile.Address;
                ViewBag.ShopAccount = profile.Account;
            }

            ViewBag.InitialToast = TempData["Welcome"] as string ?? string.Empty;
            return View();
        }

        [HttpGet]
        public async Task<ActionResult> Categories()
        {
            var shopId = RequireShopId();
            const string sql = "SELECT Id,Name,SortOrder,IsActive FROM MealCategory WHERE ShopId=@sid ORDER BY SortOrder,Name";

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@sid", shopId);
                await connection.OpenAsync();
                var reader = await command.ExecuteReaderAsync();
                var list = new List<object>();

                while (await reader.ReadAsync())
                {
                    list.Add(new
                    {
                        Id = reader.GetGuid(0),
                        Name = reader.GetString(1),
                        SortOrder = reader.GetInt32(2),
                        IsActive = reader.GetBoolean(3)
                    });
                }

                return Json(list, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public async Task<ActionResult> UpsertCategory(Guid? id, string name, int sortOrder = 0, bool isActive = true)
        {
            var shopId = RequireShopId();

            if (string.IsNullOrWhiteSpace(name))
            {
                return Json(new { ok = false, error = "名稱為必填" });
            }

            const string sql = @"IF(@Id IS NULL OR @Id='00000000-0000-0000-0000-000000000000')
                                BEGIN
                                    INSERT INTO MealCategory(Id,ShopId,Name,SortOrder,IsActive,CreateDate,UpdateDate)
                                    VALUES(NEWID(),@ShopId,@Name,@SortOrder,@IsActive,GETDATE(),GETDATE());
                                END
                                ELSE
                                BEGIN
                                    UPDATE MealCategory
                                    SET Name=@Name,
                                        SortOrder=@SortOrder,
                                        IsActive=@IsActive,
                                        UpdateDate=GETDATE()
                                    WHERE Id=@Id AND ShopId=@ShopId;
                                END";

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", (object)id ?? DBNull.Value);
                command.Parameters.AddWithValue("@ShopId", shopId);
                command.Parameters.AddWithValue("@Name", name.Trim());
                command.Parameters.AddWithValue("@SortOrder", sortOrder);
                command.Parameters.AddWithValue("@IsActive", isActive);

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
                return Json(new { ok = true });
            }
        }

        [HttpPost]
        public async Task<ActionResult> DeleteCategory(Guid id)
        {
            var shopId = RequireShopId();
            const string sql = "DELETE FROM MealCategory WHERE Id=@Id AND ShopId=@ShopId";

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                command.Parameters.AddWithValue("@ShopId", shopId);

                await connection.OpenAsync();
                var rows = await command.ExecuteNonQueryAsync();
                return Json(new { ok = rows > 0 });
            }
        }

        [HttpGet]
        public async Task<ActionResult> Meals(Guid? categoryId)
        {
            var shopId = RequireShopId();
            var sql = "SELECT Id,Name,Money,IsActive,CategoryId,Element,OptionsJson FROM Meals WHERE ShopId=@sid";
            if (categoryId.HasValue)
            {
                sql += " AND CategoryId=@cid";
            }
            sql += " ORDER BY Name";

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@sid", shopId);
                if (categoryId.HasValue)
                {
                    command.Parameters.AddWithValue("@cid", categoryId.Value);
                }

                await connection.OpenAsync();
                var reader = await command.ExecuteReaderAsync();
                var list = new List<object>();

                while (await reader.ReadAsync())
                {
                    list.Add(new
                    {
                        Id = reader.GetGuid(0),
                        Name = reader.GetString(1),
                        Money = reader.GetDecimal(2),
                        IsActive = reader.GetBoolean(3),
                        CategoryId = reader.IsDBNull(4) ? (Guid?)null : reader.GetGuid(4),
                        Element = reader.IsDBNull(5) ? null : reader.GetString(5),
                        OptionsJson = reader.IsDBNull(6) ? null : reader.GetString(6)
                    });
                }

                return Json(list, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public async Task<ActionResult> UpsertMeal(Guid? id, string name, decimal money, Guid? categoryId, string element, string optionsJson, bool isActive = true)
        {
            var shopId = RequireShopId();

            if (string.IsNullOrWhiteSpace(name))
            {
                return Json(new { ok = false, error = "名稱為必填" });
            }

            if (money < 0)
            {
                return Json(new { ok = false, error = "價格須大於等於 0" });
            }

            if (categoryId.HasValue && !await CategoryBelongsToShopAsync(categoryId.Value, shopId))
            {
                return Json(new { ok = false, error = "分類不存在" });
            }

            const string sql = @"IF(@Id IS NULL OR @Id='00000000-0000-0000-0000-000000000000')
                                BEGIN
                                    INSERT INTO Meals(Id,ShopId,Name,Money,IsActive,CreateDate,UpdateDate,CategoryId,Element,OptionsJson)
                                    VALUES(NEWID(),@ShopId,@Name,@Money,@IsActive,GETDATE(),GETDATE(),@CategoryId,@Element,@OptionsJson);
                                END
                                ELSE
                                BEGIN
                                    UPDATE Meals
                                    SET Name=@Name,
                                        Money=@Money,
                                        IsActive=@IsActive,
                                        CategoryId=@CategoryId,
                                        Element=@Element,
                                        OptionsJson=@OptionsJson,
                                        UpdateDate=GETDATE()
                                    WHERE Id=@Id AND ShopId=@ShopId;
                                END";

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                var trimmedName = name.Trim();
                var trimmedElement = string.IsNullOrWhiteSpace(element) ? null : element.Trim();
                var trimmedOptionsJson = string.IsNullOrWhiteSpace(optionsJson) ? null : optionsJson.Trim();

                command.Parameters.AddWithValue("@Id", (object)id ?? DBNull.Value);
                command.Parameters.AddWithValue("@ShopId", shopId);
                command.Parameters.AddWithValue("@Name", trimmedName);
                command.Parameters.AddWithValue("@Money", money);
                command.Parameters.AddWithValue("@CategoryId", (object)categoryId ?? DBNull.Value);
                command.Parameters.AddWithValue("@Element", (object)trimmedElement ?? DBNull.Value);
                command.Parameters.AddWithValue("@OptionsJson", (object)trimmedOptionsJson ?? DBNull.Value);
                command.Parameters.AddWithValue("@IsActive", isActive);

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
                return Json(new { ok = true });
            }
        }

        [HttpPost]
        public async Task<ActionResult> DeleteMeal(Guid id)
        {
            var shopId = RequireShopId();
            const string sql = "DELETE FROM Meals WHERE Id=@Id AND ShopId=@ShopId";

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                command.Parameters.AddWithValue("@ShopId", shopId);

                await connection.OpenAsync();
                var rows = await command.ExecuteNonQueryAsync();
                return Json(new { ok = rows > 0 });
            }
        }

        [HttpGet]
        public async Task<ActionResult> Tables()
        {
            var shopId = RequireShopId();
            const string sql = "SELECT Id,Number,Zone,IsActive FROM [Table] WHERE ShopId=@sid ORDER BY Number";

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@sid", shopId);

                await connection.OpenAsync();
                var reader = await command.ExecuteReaderAsync();
                var list = new List<object>();

                while (await reader.ReadAsync())
                {
                    list.Add(new
                    {
                        Id = reader.GetGuid(0),
                        Number = reader.GetInt32(1),
                        Zone = reader.IsDBNull(2) ? null : reader.GetString(2),
                        IsActive = reader.GetBoolean(3)
                    });
                }

                return Json(list, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public async Task<ActionResult> UpsertTable(Guid? id, int number, string zone, bool isActive = true)
        {
            var shopId = RequireShopId();

            if (number <= 0)
            {
                return Json(new { ok = false, error = "桌號必須大於 0" });
            }

            const string sql = @"IF(@Id IS NULL OR @Id='00000000-0000-0000-0000-000000000000')
                                BEGIN
                                    INSERT INTO [Table](Id,ShopId,Number,Zone,IsActive,CreateDate,UpdateDate)
                                    VALUES(NEWID(),@ShopId,@Number,@Zone,@IsActive,GETDATE(),GETDATE());
                                END
                                ELSE
                                BEGIN
                                    UPDATE [Table]
                                    SET Number=@Number,
                                        Zone=@Zone,
                                        IsActive=@IsActive,
                                        UpdateDate=GETDATE()
                                    WHERE Id=@Id AND ShopId=@ShopId;
                                END";

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                var trimmedZone = string.IsNullOrWhiteSpace(zone) ? null : zone.Trim();

                command.Parameters.AddWithValue("@Id", (object)id ?? DBNull.Value);
                command.Parameters.AddWithValue("@ShopId", shopId);
                command.Parameters.AddWithValue("@Number", number);
                command.Parameters.AddWithValue("@Zone", (object)trimmedZone ?? DBNull.Value);
                command.Parameters.AddWithValue("@IsActive", isActive);

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
                return Json(new { ok = true });
            }
        }

        [HttpPost]
        public async Task<ActionResult> DeleteTable(Guid id)
        {
            var shopId = RequireShopId();
            const string sql = "DELETE FROM [Table] WHERE Id=@Id AND ShopId=@ShopId";

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                command.Parameters.AddWithValue("@ShopId", shopId);

                await connection.OpenAsync();
                var rows = await command.ExecuteNonQueryAsync();
                return Json(new { ok = rows > 0 });
            }
        }

        [HttpGet]
        public async Task<ActionResult> Combos()
        {
            var shopId = RequireShopId();
            const string sql = "SELECT Id,Title,ComboMeal,Money,IsActive FROM Combo WHERE ShopId=@sid ORDER BY Title";

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@sid", shopId);

                await connection.OpenAsync();
                var reader = await command.ExecuteReaderAsync();
                var list = new List<object>();

                while (await reader.ReadAsync())
                {
                    list.Add(new
                    {
                        Id = reader.GetGuid(0),
                        Title = reader.GetString(1),
                        ComboMeal = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Money = reader.GetDecimal(3),
                        IsActive = reader.GetBoolean(4)
                    });
                }

                return Json(list, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public async Task<ActionResult> UpsertCombo(Guid? id, string title, string comboMeal, decimal money, bool isActive = true)
        {
            var shopId = RequireShopId();

            if (string.IsNullOrWhiteSpace(title))
            {
                return Json(new { ok = false, error = "標題為必填" });
            }

            if (money < 0)
            {
                return Json(new { ok = false, error = "價格須大於等於 0" });
            }

            const string sql = @"IF(@Id IS NULL OR @Id='00000000-0000-0000-0000-000000000000')
                                BEGIN
                                    INSERT INTO Combo(Id,ShopId,Title,ComboMeal,Money,IsActive,CreateDate,UpdateDate)
                                    VALUES(NEWID(),@ShopId,@Title,@ComboMeal,@Money,@IsActive,GETDATE(),GETDATE());
                                END
                                ELSE
                                BEGIN
                                    UPDATE Combo
                                    SET Title=@Title,
                                        ComboMeal=@ComboMeal,
                                        Money=@Money,
                                        IsActive=@IsActive,
                                        UpdateDate=GETDATE()
                                    WHERE Id=@Id AND ShopId=@ShopId;
                                END";

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                var trimmedTitle = title.Trim();
                var trimmedCombo = string.IsNullOrWhiteSpace(comboMeal) ? null : comboMeal.Trim();

                command.Parameters.AddWithValue("@Id", (object)id ?? DBNull.Value);
                command.Parameters.AddWithValue("@ShopId", shopId);
                command.Parameters.AddWithValue("@Title", trimmedTitle);
                command.Parameters.AddWithValue("@ComboMeal", (object)trimmedCombo ?? DBNull.Value);
                command.Parameters.AddWithValue("@Money", money);
                command.Parameters.AddWithValue("@IsActive", isActive);

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
                return Json(new { ok = true });
            }
        }

        [HttpPost]
        public async Task<ActionResult> DeleteCombo(Guid id)
        {
            var shopId = RequireShopId();
            const string sql = "DELETE FROM Combo WHERE Id=@Id AND ShopId=@ShopId";

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                command.Parameters.AddWithValue("@ShopId", shopId);

                await connection.OpenAsync();
                var rows = await command.ExecuteNonQueryAsync();
                return Json(new { ok = rows > 0 });
            }
        }

        [HttpGet]
        public async Task<ActionResult> Orders()
        {
            var shopId = RequireShopId();
            const string sql = @"SELECT TOP 100 Id,ShopId,OrderType,TableId,TakeoutCode,Notes,Status,CreatedAt,UpdatedAt
                                  FROM Orders WHERE ShopId=@sid ORDER BY CreatedAt DESC";

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@sid", shopId);

                await connection.OpenAsync();
                var reader = await command.ExecuteReaderAsync();
                var list = new List<object>();

                while (await reader.ReadAsync())
                {
                    list.Add(new
                    {
                        Id = reader.GetGuid(0),
                        ShopId = reader.GetGuid(1),
                        OrderType = reader.GetString(2),
                        TableId = reader.IsDBNull(3) ? (Guid?)null : reader.GetGuid(3),
                        TakeoutCode = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Notes = reader.IsDBNull(5) ? null : reader.GetString(5),
                        Status = reader.GetString(6),
                        CreatedAt = reader.GetDateTime(7),
                        UpdatedAt = reader.GetDateTime(8)
                    });
                }

                return Json(list, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public async Task<ActionResult> OrderItems(Guid orderId)
        {
            var shopId = RequireShopId();
            const string sql = @"SELECT oi.Id,oi.MealId,oi.MealName,oi.Quantity,oi.UnitPrice,oi.Notes
                                  FROM OrderItems oi
                                  INNER JOIN Orders o ON o.Id = oi.OrderId
                                  WHERE oi.OrderId=@oid AND o.ShopId=@sid
                                  ORDER BY oi.CreateDate";

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@oid", orderId);
                command.Parameters.AddWithValue("@sid", shopId);

                await connection.OpenAsync();
                var reader = await command.ExecuteReaderAsync();
                var list = new List<object>();

                while (await reader.ReadAsync())
                {
                    list.Add(new
                    {
                        Id = reader.GetGuid(0),
                        MealId = reader.IsDBNull(1) ? (Guid?)null : reader.GetGuid(1),
                        MealName = reader.GetString(2),
                        Quantity = reader.GetInt32(3),
                        UnitPrice = reader.GetDecimal(4),
                        Notes = reader.IsDBNull(5) ? null : reader.GetString(5)
                    });
                }

                return Json(list, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public async Task<ActionResult> UpdateOrderStatus(Guid id, string status)
        {
            var shopId = RequireShopId();
            var allowed = new[] { "Pending", "Preparing", "Completed", "Cancelled" };
            if (string.IsNullOrWhiteSpace(status) || !allowed.Contains(status))
            {
                return Json(new { ok = false, error = "狀態不合法" });
            }

            const string sql = "UPDATE Orders SET Status=@Status, UpdatedAt=GETDATE() WHERE Id=@Id AND ShopId=@ShopId";

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Status", status);
                command.Parameters.AddWithValue("@Id", id);
                command.Parameters.AddWithValue("@ShopId", shopId);

                await connection.OpenAsync();
                var rows = await command.ExecuteNonQueryAsync();
                return Json(new { ok = rows > 0 });
            }
        }

        private Guid RequireShopId()
        {
            var shopId = CurrentShopId;
            if (!shopId.HasValue)
            {
                throw new InvalidOperationException("Shop session not found");
            }

            return shopId.Value;
        }

        private async Task<ShopSessionInfo> AuthenticateShopAsync(string account, string password)
        {
            const string sql = @"SELECT TOP 1 Id,Name,Account
                                  FROM Shop
                                  WHERE Account=@Account AND Password=@Password AND IsActive=1";

            var trimmedAccount = account.Trim();
            var trimmedPassword = password.Trim();

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Account", trimmedAccount);
                command.Parameters.AddWithValue("@Password", trimmedPassword);

                await connection.OpenAsync();
                var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new ShopSessionInfo
                    {
                        Id = reader.GetGuid(0),
                        Name = reader.GetString(1),
                        Account = reader.IsDBNull(2) ? trimmedAccount : reader.GetString(2)
                    };
                }
            }

            return null;
        }

        private async Task<ShopProfile> GetShopProfileAsync(Guid id)
        {
            const string sql = "SELECT Id,Name,Phone,Addr,Account FROM Shop WHERE Id=@Id";

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);

                await connection.OpenAsync();
                var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new ShopProfile
                    {
                        Id = reader.GetGuid(0),
                        Name = reader.GetString(1),
                        Phone = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Address = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Account = reader.IsDBNull(4) ? null : reader.GetString(4)
                    };
                }
            }

            return null;
        }

        private async Task<bool> CategoryBelongsToShopAsync(Guid categoryId, Guid shopId)
        {
            const string sql = "SELECT COUNT(1) FROM MealCategory WHERE Id=@Id AND ShopId=@ShopId";

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", categoryId);
                command.Parameters.AddWithValue("@ShopId", shopId);

                await connection.OpenAsync();
                var result = await command.ExecuteScalarAsync();
                var count = Convert.ToInt32(result);
                return count > 0;
            }
        }

        private class ShopSessionInfo
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public string Account { get; set; }
        }

        private class ShopProfile
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public string Phone { get; set; }
            public string Address { get; set; }
            public string Account { get; set; }
        }
    }
}
