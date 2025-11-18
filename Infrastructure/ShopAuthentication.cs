using System.Web;

namespace BreakFastShop.Infrastructure
{
    public static class ShopAuthentication
    {
        public const string SessionKey = "__SHOP_USER";

        public static bool IsAuthenticated(HttpSessionStateBase session)
        {
            return session != null && session[SessionKey] != null;
        }
    }
}
