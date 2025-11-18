using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace BreakFastShop.Infrastructure
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class ShopAuthorizeAttribute : AuthorizeAttribute
    {
        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            if (httpContext == null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            return ShopAuthentication.IsAuthenticated(httpContext.Session);
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            if (filterContext == null)
            {
                throw new ArgumentNullException(nameof(filterContext));
            }

            if (filterContext.HttpContext.Request.IsAjaxRequest())
            {
                filterContext.HttpContext.Response.StatusCode = 401;
                filterContext.Result = new JsonResult
                {
                    Data = new { ok = false, error = "請先登入店家後台。" },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
                return;
            }

            filterContext.Controller.TempData["Alert"] = "請先登入店家後台。";
            filterContext.Result = new RedirectToRouteResult(new RouteValueDictionary(new
            {
                controller = "Shop",
                action = "Login"
            }));
        }
    }
}
