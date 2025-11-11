using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace BreakFastShop.Controllers
{
    public class AdminController : Controller
    {
        private const string AdminSessionKey = "__ADMIN_USER";
        private string ConnStr => ConfigurationManager.ConnectionStrings["BreakfastShop"].ConnectionString;
        private string AdminAccount => ConfigurationManager.AppSettings["AdminUser"] ?? "admin";
        private string AdminPassword => ConfigurationManager.AppSettings["AdminPassword"] ?? "admin123";
        private bool IsAuthenticated => Session[AdminSessionKey] != null;

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var allowAnonymous = filterContext.ActionDescriptor.IsDefined(typeof(AllowAnonymousAttribute), inherit: true);
            if (!allowAnonymous && !IsAuthenticated)
            {
                if (filterContext.HttpContext.Request.IsAjaxRequest())
                {
                    filterContext.HttpContext.Response.StatusCode = 401;
                    filterContext.Result = Json(new { ok = false, error = "尚未登入，請重新登入後台。" }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    TempData["Alert"] = "請先登入後台。";
                    filterContext.Result = RedirectToAction("Login");
                }
                return;
            }

            ViewBag.AdminUser = Session[AdminSessionKey] as string;
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
        public ActionResult Login(string account, string password)
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

            if (ValidateAdmin(account, password))
            {
                Session[AdminSessionKey] = account.Trim();
                TempData["Welcome"] = $"歡迎回來，{account.Trim()}！";
                return RedirectToAction("Index");
            }

            ViewBag.Error = "帳號或密碼錯誤";
            ViewBag.Account = account;
            Response.StatusCode = 401;
            return View();
        }

        public ActionResult Logout()
        {
            Session.Remove(AdminSessionKey);
            TempData["Alert"] = "您已成功登出。";
            return RedirectToAction("Login");
        }

        public ActionResult Index()
        {
            ViewBag.InitialToast = TempData["Welcome"] as string;
            return View();
        }

        private bool ValidateAdmin(string account, string password)
        {
            return string.Equals(account?.Trim(), AdminAccount, StringComparison.OrdinalIgnoreCase)
                && string.Equals(password, AdminPassword, StringComparison.Ordinal);
        }

        [HttpGet]
        public async Task<ActionResult> Shops()
        {
            const string sql = "SELECT Id,Name,Phone,Addr,Account,Password,IsActive FROM Shop ORDER BY Name";

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                await connection.OpenAsync();
                var reader = await command.ExecuteReaderAsync();
                var list = new List<object>();

                while (await reader.ReadAsync())
                {
                    list.Add(new
                    {
                        Id = reader.GetGuid(0),
                        Name = reader.GetString(1),
                        Phone = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Addr = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Account = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Password = reader.IsDBNull(5) ? null : reader.GetString(5),
                        IsActive = reader.GetBoolean(6)
                    });
                }

                return Json(list, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public async Task<ActionResult> UpsertShop(Guid? id, string name, string phone, string addr, string account, string password, bool isActive = true)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Json(new { ok = false, error = "Name required" });
            }

            const string sql = @"IF(@Id IS NULL OR @Id='00000000-0000-0000-0000-000000000000')
                                BEGIN
                                    INSERT INTO Shop(Id,Name,Phone,Addr,Account,Password,IsActive,CreateDate,UpdateDate)
                                    VALUES(NEWID(),@Name,@Phone,@Addr,@Account,@Password,@IsActive,GETDATE(),GETDATE());
                                END
                                ELSE
                                BEGIN
                                    UPDATE Shop
                                    SET Name=@Name,
                                        Phone=@Phone,
                                        Addr=@Addr,
                                        Account=@Account,
                                        Password=@Password,
                                        IsActive=@IsActive,
                                        UpdateDate=GETDATE()
                                    WHERE Id=@Id;
                                END";

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                var trimmedName = name.Trim();
                var trimmedPhone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
                var trimmedAddr = string.IsNullOrWhiteSpace(addr) ? null : addr.Trim();
                var trimmedAccount = string.IsNullOrWhiteSpace(account) ? null : account.Trim();
                var trimmedPassword = string.IsNullOrWhiteSpace(password) ? null : password.Trim();

                command.Parameters.AddWithValue("@Id", (object)id ?? DBNull.Value);
                command.Parameters.AddWithValue("@Name", trimmedName);
                command.Parameters.AddWithValue("@Phone", (object)trimmedPhone ?? DBNull.Value);
                command.Parameters.AddWithValue("@Addr", (object)trimmedAddr ?? DBNull.Value);
                command.Parameters.AddWithValue("@Account", (object)trimmedAccount ?? DBNull.Value);
                command.Parameters.AddWithValue("@Password", (object)trimmedPassword ?? DBNull.Value);
                command.Parameters.AddWithValue("@IsActive", isActive);

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
                return Json(new { ok = true });
            }
        }

        [HttpPost]
        public async Task<ActionResult> DeleteShop(Guid id)
        {
            const string sql = "DELETE FROM Shop WHERE Id=@Id";

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);

                await connection.OpenAsync();
                var rows = await command.ExecuteNonQueryAsync();
                return Json(new { ok = rows > 0 });
            }
        }

        [HttpGet]
        public async Task<ActionResult> Categories(Guid shopId)
        {
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
        public async Task<ActionResult> UpsertCategory(Guid shopId, Guid? id, string name, int sortOrder = 0, bool isActive = true)
        {
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
                command.Parameters.AddWithValue("@Name", name ?? string.Empty);
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
            const string sql = "DELETE FROM MealCategory WHERE Id=@Id";

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);

                await connection.OpenAsync();
                var rows = await command.ExecuteNonQueryAsync();
                return Json(new { ok = rows > 0 });
            }
        }

        [HttpGet]
        public async Task<ActionResult> Meals(Guid shopId, Guid? categoryId)
        {
            var sql = "SELECT Id,Name,Money,IsActive,CategoryId,Element FROM Meals WHERE ShopId=@sid";
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
                        Element = reader.IsDBNull(5) ? null : reader.GetString(5)
                    });
                }

                return Json(list, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public async Task<ActionResult> UpsertMeal(Guid shopId, Guid? id, string name, decimal money, Guid? categoryId, string element, bool isActive = true)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Json(new { ok = false, error = "Name required" });
            }

            if (money < 0)
            {
                return Json(new { ok = false, error = "Money must be >= 0" });
            }

            const string sql = @"IF(@Id IS NULL OR @Id='00000000-0000-0000-0000-000000000000')
                                BEGIN
                                    INSERT INTO Meals(Id,ShopId,Name,Money,IsActive,CreateDate,UpdateDate,CategoryId,Element)
                                    VALUES(NEWID(),@ShopId,@Name,@Money,@IsActive,GETDATE(),GETDATE(),@CategoryId,@Element);
                                END
                                ELSE
                                BEGIN
                                    UPDATE Meals
                                    SET Name=@Name,
                                        Money=@Money,
                                        IsActive=@IsActive,
                                        CategoryId=@CategoryId,
                                        Element=@Element,
                                        UpdateDate=GETDATE()
                                    WHERE Id=@Id AND ShopId=@ShopId;
                                END";

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                var trimmedName = name.Trim();
                var trimmedElement = string.IsNullOrWhiteSpace(element) ? null : element.Trim();

                command.Parameters.AddWithValue("@Id", (object)id ?? DBNull.Value);
                command.Parameters.AddWithValue("@ShopId", shopId);
                command.Parameters.AddWithValue("@Name", trimmedName);
                command.Parameters.AddWithValue("@Money", money);
                command.Parameters.AddWithValue("@CategoryId", (object)categoryId ?? DBNull.Value);
                command.Parameters.AddWithValue("@Element", (object)trimmedElement ?? DBNull.Value);
                command.Parameters.AddWithValue("@IsActive", isActive);

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
                return Json(new { ok = true });
            }
        }

        [HttpPost]
        public async Task<ActionResult> DeleteMeal(Guid id)
        {
            const string sql = "DELETE FROM Meals WHERE Id=@Id";

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);

                await connection.OpenAsync();
                var rows = await command.ExecuteNonQueryAsync();
                return Json(new { ok = rows > 0 });
            }
        }

        [HttpGet]
        public async Task<ActionResult> Tables(Guid shopId)
        {
            const string sql = "SELECT Id,ShopId,Number,Zone,IsActive FROM [Table] WHERE ShopId=@sid ORDER BY Number";

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
                        Number = reader.GetInt32(2),
                        Zone = reader.IsDBNull(3) ? null : reader.GetString(3),
                        IsActive = reader.GetBoolean(4)
                    });
                }

                return Json(list, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public async Task<ActionResult> UpsertTable(Guid shopId, Guid? id, int number, string zone, bool isActive = true)
        {
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
                command.Parameters.AddWithValue("@Id", (object)id ?? DBNull.Value);
                command.Parameters.AddWithValue("@ShopId", shopId);
                command.Parameters.AddWithValue("@Number", number);
                command.Parameters.AddWithValue("@Zone", (object)zone ?? DBNull.Value);
                command.Parameters.AddWithValue("@IsActive", isActive);

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
                return Json(new { ok = true });
            }
        }

        [HttpPost]
        public async Task<ActionResult> DeleteTable(Guid id)
        {
            const string sql = "DELETE FROM [Table] WHERE Id=@Id";

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);

                await connection.OpenAsync();
                var rows = await command.ExecuteNonQueryAsync();
                return Json(new { ok = rows > 0 });
            }
        }

        [HttpGet]
        public async Task<ActionResult> Combos(Guid shopId)
        {
            const string sql = "SELECT Id,ShopId,Title,ComboMeal,Money,IsActive FROM Combo WHERE ShopId=@sid ORDER BY Title";

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
                        Title = reader.GetString(2),
                        ComboMeal = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Money = reader.GetDecimal(4),
                        IsActive = reader.GetBoolean(5)
                    });
                }

                return Json(list, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public async Task<ActionResult> UpsertCombo(Guid shopId, Guid? id, string title, string comboMeal, decimal money, bool isActive = true)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return Json(new { ok = false, error = "Title required" });
            }

            if (money < 0)
            {
                return Json(new { ok = false, error = "Money must be >= 0" });
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
                command.Parameters.AddWithValue("@Id", (object)id ?? DBNull.Value);
                command.Parameters.AddWithValue("@ShopId", shopId);
                command.Parameters.AddWithValue("@Title", title.Trim());
                var content = string.IsNullOrWhiteSpace(comboMeal) ? null : comboMeal.Trim();
                command.Parameters.AddWithValue("@ComboMeal", (object)content ?? DBNull.Value);
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
            const string sql = "DELETE FROM Combo WHERE Id=@Id";

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);

                await connection.OpenAsync();
                var rows = await command.ExecuteNonQueryAsync();
                return Json(new { ok = rows > 0 });
            }
        }

        [HttpGet]
        public async Task<ActionResult> Orders(Guid? shopId)
        {
            var sql = "SELECT TOP 100 Id,ShopId,OrderType,TableId,TakeoutCode,Notes,Status,CreatedAt,UpdatedAt FROM Orders";
            if (shopId.HasValue)
            {
                sql += " WHERE ShopId=@sid";
            }
            sql += " ORDER BY CreatedAt DESC";

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                if (shopId.HasValue)
                {
                    command.Parameters.AddWithValue("@sid", shopId.Value);
                }

                await connection.OpenAsync();
                var reader = await command.ExecuteReaderAsync();
                var list = new List<object>();

                while (await reader.ReadAsync())
                {
                    list.Add(new
                    {
                        Id = reader.GetGuid(0),
                        ShopId = reader.IsDBNull(1) ? (Guid?)null : reader.GetGuid(1),
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
            const string sql = "SELECT Id,MealId,MealName,Quantity,UnitPrice,Notes FROM OrderItems WHERE OrderId=@oid ORDER BY CreateDate";

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@oid", orderId);

                await connection.OpenAsync();
                var reader = await command.ExecuteReaderAsync();
                var list = new List<object>();

                while (await reader.ReadAsync())
                {
                    list.Add(new
                    {
                        Id = reader.GetGuid(0),
                        MealId = reader.GetGuid(1),
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
            var allowed = new[] { "Pending", "Preparing", "Completed", "Cancelled" };
            if (Array.IndexOf(allowed, status) < 0)
            {
                return Json(new { ok = false, error = "bad status" });
            }

            const string sql = "UPDATE Orders SET Status=@s, UpdatedAt=GETDATE() WHERE Id=@id";

            using (var connection = new SqlConnection(ConnStr))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@id", id);
                command.Parameters.AddWithValue("@s", status);

                await connection.OpenAsync();
                var rows = await command.ExecuteNonQueryAsync();
                return Json(new { ok = rows > 0 });
            }
        }
    }
}
