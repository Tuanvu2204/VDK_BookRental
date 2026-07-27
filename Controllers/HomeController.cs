using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using VDK_BookRental.Models;

namespace VDK_BookRental.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToAction("Index", "Books");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id
                    ?? HttpContext.TraceIdentifier
            });
        }
    }
}