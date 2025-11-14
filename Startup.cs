using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(BreakFastShop.Startup))]

namespace BreakFastShop
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.MapSignalR();
        }
    }
}
