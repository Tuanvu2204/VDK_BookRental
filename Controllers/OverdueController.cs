using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VDK_BookRental.Data;
using VDK_BookRental.Models;

namespace VDK_BookRental.Controllers
{
    public class OverdueController : Controller
    {
        private readonly AppDbContext _context;

        public OverdueController(AppDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // DANH SÁCH ĐƠN THUÊ QUÁ HẠN
        // URL: /Overdue
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var accessResult = CheckStaffAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            var today = DateTime.Today;

            var rentals = await _context.Rentals
                .AsNoTracking()
                .Where(rental =>
                    rental.Status == "Borrowing" &&
                    rental.ReturnDate.Date < today)
                .Include(rental => rental.User)
                .Include(rental => rental.Payment)
                .Include(rental => rental.RentalDetails)
                    .ThenInclude(detail => detail.Book)
                .OrderBy(rental => rental.ReturnDate)
                .ToListAsync();

            return View(
                "~/Views/Overdue/Index.cshtml",
                rentals
            );
        }

        // =========================================================
        // XÁC NHẬN KHÁCH ĐÃ TRẢ SÁCH
        // Borrowing -> Returned
        // Hoàn lại số lượng sách trong kho
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmReturn(int rentalId)
        {
            var accessResult = CheckStaffAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            var rental = await _context.Rentals
                .Include(item => item.RentalDetails)
                    .ThenInclude(detail => detail.Book)
                .FirstOrDefaultAsync(item =>
                    item.Id == rentalId);

            if (rental == null)
            {
                TempData["ErrorMessage"] =
                    "Không tìm thấy đơn thuê.";

                return RedirectToAction(nameof(Index));
            }

            if (rental.Status != "Borrowing")
            {
                TempData["ErrorMessage"] =
                    "Chỉ có thể xác nhận trả đối với đơn đang thuê.";

                return RedirectToAction(nameof(Index));
            }

            foreach (var detail in rental.RentalDetails)
            {
                if (detail.Book != null)
                {
                    detail.Book.Quantity += detail.Quantity;
                }
            }

            rental.Status = "Returned";

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Đã xác nhận trả sách cho đơn #{rental.Id}.";

            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // KIỂM TRA QUYỀN STAFF / ADMIN
        // =========================================================
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
                    "Account"
                );
            }

            if (userRole != "Staff" &&
                userRole != "Admin")
            {
                TempData["ErrorMessage"] =
                    "Bạn không có quyền truy cập chức năng này.";

                return RedirectToAction(
                    "AccessDenied",
                    "Home"
                );
            }

            return null;
        }
    }
}