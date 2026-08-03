using Microsoft.AspNetCore.Mvc;

namespace VDK_BookRental.Core.AI
{
    public class IEnterpriseChatService : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
