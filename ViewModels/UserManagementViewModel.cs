using Microsoft.AspNetCore.Mvc;

namespace VDK_BookRental.ViewModels
{
    public class UserManagementViewModel : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
