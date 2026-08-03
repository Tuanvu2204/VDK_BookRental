using Microsoft.AspNetCore.Mvc;

namespace VDK_BookRental.Infrastructure.AI
{
    public class GeminiEnterpriseChatService : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
