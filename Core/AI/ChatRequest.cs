using Microsoft.AspNetCore.Mvc;

namespace VDK_BookRental.Core.AI
{
    public class ChatRequest : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
