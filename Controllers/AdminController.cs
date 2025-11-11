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

        [HttpGet] public async Task<ActionResult> Shops() { using (var c = new SqlConnection(ConnStr)) using (var cmd = new SqlCommand("SELECT Id,Name,IsActive FROM Shop ORDER BY Name", c)) { await c.OpenAsync(); var r = await cmd.ExecuteReaderAsync(); var list = new List<object>(); while (await r.ReadAsync()) { list.Add(new { Id = r.GetGuid(0), Name = r.GetString(1), IsActive = r.GetBoolean(2) }); } return Json(list, JsonRequestBehavior.AllowGet); } }
        [HttpPost] public async Task<ActionResult> CreateShop(Guid? id, string name, string phone, string addr, bool isActive = true) { if (string.IsNullOrWhiteSpace(name)) return Json(new { ok = false, error = "Name required" }); using (var c = new SqlConnection(ConnStr)) using (var cmd = new SqlCommand(@"IF(@Id IS NULL OR @Id='00000000-0000-0000-0000-000000000000') BEGIN INSERT INTO Shop(Id,Name,Phone,Addr,IsActive,CreateDate,UpdateDate) VALUES(NEWID(),@Name,@Phone,@Addr,@IsActive,GETDATE(),GETDATE()); END ELSE BEGIN UPDATE Shop SET Name=@Name,Phone=@Phone,Addr=@Addr,IsActive=@IsActive,UpdateDate=GETDATE() WHERE Id=@Id; END", c)) { cmd.Parameters.AddWithValue("@Id", (object)id ?? DBNull.Value); cmd.Parameters.AddWithValue("@Name", name ?? ""); cmd.Parameters.AddWithValue("@Phone", (object)phone ?? DBNull.Value); cmd.Parameters.AddWithValue("@Addr", (object)addr ?? DBNull.Value); cmd.Parameters.AddWithValue("@IsActive", isActive); await c.OpenAsync(); await cmd.ExecuteNonQueryAsync(); return Json(new { ok = true }); } }
        [HttpPost] public async Task<ActionResult> DeleteShop(Guid id) { using (var c = new SqlConnection(ConnStr)) using (var cmd = new SqlCommand("DELETE FROM Shop WHERE Id=@Id", c)) { cmd.Parameters.AddWithValue("@Id", id); await c.OpenAsync(); var rows = await cmd.ExecuteNonQueryAsync(); return Json(new { ok = rows > 0 }); } }
        [HttpGet] public async Task<ActionResult> Categories(Guid shopId) { using (var c = new SqlConnection(ConnStr)) using (var cmd = new SqlCommand("SELECT Id,Name,SortOrder,IsActive FROM MealCategory WHERE ShopId=@sid ORDER BY SortOrder,Name", c)) { cmd.Parameters.AddWithValue("@sid", shopId); await c.OpenAsync(); var r = await cmd.ExecuteReaderAsync(); var list = new List<object>(); while (await r.ReadAsync()) { list.Add(new { Id = r.GetGuid(0), Name = r.GetString(1), SortOrder = r.GetInt32(2), IsActive = r.GetBoolean(3) }); } return Json(list, JsonRequestBehavior.AllowGet); } }
        [HttpPost] public async Task<ActionResult> UpsertCategory(Guid shopId, Guid? id, string name, int sortOrder = 0, bool isActive = true) { using (var c = new SqlConnection(ConnStr)) using (var cmd = new SqlCommand(@"IF(@Id IS NULL OR @Id='00000000-0000-0000-0000-000000000000') BEGIN INSERT INTO MealCategory(Id,ShopId,Name,SortOrder,IsActive,CreateDate,UpdateDate) VALUES(NEWID(),@ShopId,@Name,@SortOrder,@IsActive,GETDATE(),GETDATE()); END ELSE BEGIN UPDATE MealCategory SET Name=@Name,SortOrder=@SortOrder,IsActive=@IsActive,UpdateDate=GETDATE() WHERE Id=@Id AND ShopId=@ShopId; END", c)) { cmd.Parameters.AddWithValue("@Id", (object)id ?? DBNull.Value); cmd.Parameters.AddWithValue("@ShopId", shopId); cmd.Parameters.AddWithValue("@Name", name ?? ""); cmd.Parameters.AddWithValue("@SortOrder", sortOrder); cmd.Parameters.AddWithValue("@IsActive", isActive); await c.OpenAsync(); await cmd.ExecuteNonQueryAsync(); return Json(new { ok = true }); } }
        [HttpPost] public async Task<ActionResult> DeleteCategory(Guid id) { using (var c = new SqlConnection(ConnStr)) using (var cmd = new SqlCommand("DELETE FROM MealCategory WHERE Id=@Id", c)) { cmd.Parameters.AddWithValue("@Id", id); await c.OpenAsync(); var rows = await cmd.ExecuteNonQueryAsync(); return Json(new { ok = rows > 0 }); } }
        [HttpGet] public async Task<ActionResult> Meals(Guid shopId, Guid? categoryId) { var sql = "SELECT Id,Name,Money,IsActive,CategoryId FROM Meals WHERE ShopId=@sid" + (categoryId.HasValue ? " AND CategoryId=@cid" : "") + " ORDER BY Name"; using (var c = new SqlConnection(ConnStr)) using (var cmd = new SqlCommand(sql, c)) { cmd.Parameters.AddWithValue("@sid", shopId); if (categoryId.HasValue) cmd.Parameters.AddWithValue("@cid", categoryId.Value); await c.OpenAsync(); var r = await cmd.ExecuteReaderAsync(); var list = new List<object>(); while (await r.ReadAsync()) { list.Add(new { Id = r.GetGuid(0), Name = r.GetString(1), Money = r.GetDecimal(2), IsActive = r.GetBoolean(3), CategoryId = r.IsDBNull(4) ? (Guid?)null : r.GetGuid(4) }); } return Json(list, JsonRequestBehavior.AllowGet); } }
        [HttpPost] public async Task<ActionResult> UpsertMeal(Guid shopId, Guid? id, string name, decimal money, Guid? categoryId, bool isActive = true) { using (var c = new SqlConnection(ConnStr)) using (var cmd = new SqlCommand(@"IF(@Id IS NULL OR @Id='00000000-0000-0000-0000-000000000000') BEGIN INSERT INTO Meals(Id,ShopId,Name,Money,IsActive,CreateDate,UpdateDate,CategoryId) VALUES(NEWID(),@ShopId,@Name,@Money,@IsActive,GETDATE(),GETDATE(),@CategoryId); END ELSE BEGIN UPDATE Meals SET Name=@Name,Money=@Money,IsActive=@IsActive,CategoryId=@CategoryId,UpdateDate=GETDATE() WHERE Id=@Id AND ShopId=@ShopId; END", c)) { cmd.Parameters.AddWithValue("@Id", (object)id ?? DBNull.Value); cmd.Parameters.AddWithValue("@ShopId", shopId); cmd.Parameters.AddWithValue("@Name", name ?? ""); cmd.Parameters.AddWithValue("@Money", money); cmd.Parameters.AddWithValue("@CategoryId", (object)categoryId ?? DBNull.Value); cmd.Parameters.AddWithValue("@IsActive", isActive); await c.OpenAsync(); await cmd.ExecuteNonQueryAsync(); return Json(new { ok = true }); } }
        [HttpPost] public async Task<ActionResult> DeleteMeal(Guid id) { using (var c = new SqlConnection(ConnStr)) using (var cmd = new SqlCommand("DELETE FROM Meals WHERE Id=@Id", c)) { cmd.Parameters.AddWithValue("@Id", id); await c.OpenAsync(); var rows = await cmd.ExecuteNonQueryAsync(); return Json(new { ok = rows > 0 }); } }
        [HttpGet] public async Task<ActionResult> Tables(Guid shopId) { using (var c = new SqlConnection(ConnStr)) using (var cmd = new SqlCommand("SELECT Id,Number,Zone,IsActive FROM [Table] WHERE ShopId=@sid ORDER BY Number", c)) { cmd.Parameters.AddWithValue("@sid", shopId); await c.OpenAsync(); var r = await cmd.ExecuteReaderAsync(); var list = new List<object>(); while (await r.ReadAsync()) { list.Add(new { Id = r.GetGuid(0), Number = r.GetInt32(1), Zone = r.IsDBNull(2) ? null : r.GetString(2), IsActive = r.GetBoolean(3) }); } return Json(list, JsonRequestBehavior.AllowGet); } }
        [HttpPost] public async Task<ActionResult> UpsertTable(Guid shopId, Guid? id, int number, string zone, bool isActive = true) { using (var c = new SqlConnection(ConnStr)) using (var cmd = new SqlCommand(@"IF(@Id IS NULL OR @Id='00000000-0000-0000-0000-000000000000') BEGIN INSERT INTO [Table](Id,ShopId,Number,Zone,IsActive,CreateDate,UpdateDate) VALUES(NEWID(),@ShopId,@Number,@Zone,@IsActive,GETDATE(),GETDATE()); END ELSE BEGIN UPDATE [Table] SET Number=@Number,Zone=@Zone,IsActive=@IsActive,UpdateDate=GETDATE() WHERE Id=@Id AND ShopId=@ShopId; END", c)) { cmd.Parameters.AddWithValue("@Id", (object)id ?? DBNull.Value); cmd.Parameters.AddWithValue("@ShopId", shopId); cmd.Parameters.AddWithValue("@Number", number); cmd.Parameters.AddWithValue("@Zone", (object)zone ?? DBNull.Value); cmd.Parameters.AddWithValue("@IsActive", isActive); await c.OpenAsync(); await cmd.ExecuteNonQueryAsync(); return Json(new { ok = true }); } }
        [HttpGet] public async Task<ActionResult> Orders(Guid? shopId) { var sql = "SELECT TOP 100 Id,ShopId,OrderType,TableId,Status,CreatedAt,UpdatedAt FROM Orders" + (shopId.HasValue ? " WHERE ShopId=@sid" : "") + " ORDER BY CreatedAt DESC"; using (var c = new SqlConnection(ConnStr)) using (var cmd = new SqlCommand(sql, c)) { if (shopId.HasValue) cmd.Parameters.AddWithValue("@sid", shopId.Value); await c.OpenAsync(); var r = await cmd.ExecuteReaderAsync(); var list = new List<object>(); while (await r.ReadAsync()) { list.Add(new { Id = r.GetGuid(0), ShopId = r.IsDBNull(1) ? (Guid?)null : r.GetGuid(1), OrderType = r.GetString(2), TableId = r.IsDBNull(3) ? (Guid?)null : r.GetGuid(3), Status = r.GetString(4), CreatedAt = r.GetDateTime(5), UpdatedAt = r.GetDateTime(6) }); } return Json(list, JsonRequestBehavior.AllowGet); } }
        [HttpPost] public async Task<ActionResult> UpdateOrderStatus(Guid id, string status) { var allowed = new[] { "Pending", "Preparing", "Completed", "Cancelled" }; if (Array.IndexOf(allowed, status) < 0) return Json(new { ok = false, error = "bad status" }); using (var c = new SqlConnection(ConnStr)) using (var cmd = new SqlCommand("UPDATE Orders SET Status=@s, UpdatedAt=GETDATE() WHERE Id=@id", c)) { cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@s", status); await c.OpenAsync(); var rows = await cmd.ExecuteNonQueryAsync(); return Json(new { ok = rows > 0 }); } }
    }
}
