using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VDK_BookRental.Data;

namespace VDK_BookRental.Controllers
{
    public class StaffController : Controller
    {
        private readonly AppDbContext _context;

        public StaffController(AppDbContext context)
        {
            _context = context;
        }

        // ========================================
        // DASHBOARD STAFF
        // Hiển thị Views/Staff/Admin.cshtml
        // ========================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var accessResult = CheckStaffAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            ViewBag.TotalBooks =
                await _context.Books.CountAsync();

            ViewBag.AvailableBooks =
                await _context.Books.CountAsync(
                    book => book.Quantity > 0);

            ViewBag.TotalRentals =
                await _context.Rentals.CountAsync();

            ViewBag.PendingPayments =
                await _context.Payments.CountAsync(
                    payment =>
                        payment.Status == "Pending" ||
                        payment.Status == "AwaitingConfirmation");

            return View("Admin");
        }

        // ========================================
        // TRUY CẬP TRỰC TIẾP /Staff/Admin
        // ========================================

        [HttpGet]
        public async Task<IActionResult> Admin()
        {
            var accessResult = CheckStaffAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            ViewBag.TotalBooks =
                await _context.Books.CountAsync();

            ViewBag.AvailableBooks =
                await _context.Books.CountAsync(
                    book => book.Quantity > 0);

            ViewBag.TotalRentals =
                await _context.Rentals.CountAsync();

            ViewBag.PendingPayments =
                await _context.Payments.CountAsync(
                    payment =>
                        payment.Status == "Pending" ||
                        payment.Status == "AwaitingConfirmation");

            return View();
        }

        // ========================================
        // DANH SÁCH SÁCH
        // ========================================

        [HttpGet]
        public async Task<IActionResult> Books()
        {
            var accessResult = CheckStaffAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            var books = await _context.Books
                .AsNoTracking()
                .Include(book => book.Category)
                .OrderBy(book => book.Title)
                .ToListAsync();

            return View(books);
        }

        // ========================================
        // DANH SÁCH ĐƠN THUÊ
        // ========================================

        [HttpGet]
        public async Task<IActionResult> Rentals()
        {
            var accessResult = CheckStaffAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            var rentals = await _context.Rentals
                .AsNoTracking()
                .Include(rental => rental.User)
                .OrderByDescending(rental => rental.Id)
                .ToListAsync();

            return View(rentals);
        }

        // ========================================
        // KIỂM TRA QUYỀN STAFF / ADMIN
        // ========================================

        private IActionResult? CheckStaffAccess()
        {
            var userId =
                HttpContext.Session.GetString("UserId");

            var userRole =
                HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["ErrorMessage"] =
                    "Vui lòng đăng nhập để tiếp tục.";

                return RedirectToAction(
                    "Login",
                    "Account");
            }

            if (userRole != "Staff" &&
                userRole != "Admin")
            {
                return RedirectToAction(
                    "AccessDenied",
                    "Home");
            }

            return null;
        }
    }
}