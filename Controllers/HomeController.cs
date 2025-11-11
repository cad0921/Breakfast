using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace BreakFastShop.Controllers
{
    public class HomeController : Controller
    {
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
    }
}