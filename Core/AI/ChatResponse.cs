using Microsoft.AspNetCore.Mvc;

namespace VDK_BookRental.Core.AI
{
    public class ChatResponse : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
