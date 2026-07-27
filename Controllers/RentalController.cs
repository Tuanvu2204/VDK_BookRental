using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VDK_BookRental.Data;

namespace VDK_BookRental.Controllers
{
    public class RentalController : Controller
    {
        private readonly AppDbContext _context;

        public RentalController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Current()
        {
            var userIdText = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrWhiteSpace(userIdText) ||
                !int.TryParse(userIdText, out int userId))
            {
                TempData["ErrorMessage"] =
                    "Vui lòng đăng nhập để xem sách đang thuê.";

                return RedirectToAction("Login", "Account");
            }

            var rentals = _context.Rentals
                .AsNoTracking()
                .Include(r => r.RentalDetails)
                    .ThenInclude(rd => rd.Book)
                .Include(r => r.Payment)
                .Where(r =>
                    r.UserId == userId &&
                    r.Status != "Returned" &&
                    r.Status != "Cancelled")
                .OrderByDescending(r => r.RentalDate)
                .ToList();

            return View(rentals);
        }

        public IActionResult History()
        {
            var userIdText = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrWhiteSpace(userIdText) ||
                !int.TryParse(userIdText, out int userId))
            {
                TempData["ErrorMessage"] =
                    "Vui lòng đăng nhập để xem lịch sử thuê sách.";

                return RedirectToAction("Login", "Account");
            }

            var rentals = _context.Rentals
                .AsNoTracking()
                .Include(r => r.RentalDetails)
                    .ThenInclude(rd => rd.Book)
                .Include(r => r.Payment)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.RentalDate)
                .ToList();

            return View(rentals);
        }
    }
}