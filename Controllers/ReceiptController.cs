using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VDK_BookRental.Data;

namespace VDK_BookRental.Controllers
{
    public class ReceiptController : Controller
    {
        private readonly AppDbContext _context;

        public ReceiptController(AppDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // HÓA ĐƠN / PHIẾU THUÊ
        //
        // Hỗ trợ cả hai đường dẫn:
        // /Receipt/Details/1   (đúng chính tả)
        // /Recepit/Details/1   (đường dẫn gõ nhầm trước đó)
        //
        // Đồng thời hỗ trợ:
        // /Receipt/Details?id=1
        // /Recepit/Details?id=1
        // =========================================================

        [HttpGet("/Receipt/Details/{id:int}")]
        [HttpGet("/Receipt/Details")]
        [HttpGet("/Recepit/Details/{id:int}")]
        [HttpGet("/Recepit/Details")]
        public async Task<IActionResult> Details(int id)
        {
            var userId = GetCurrentUserId();

            var userRole =
                HttpContext.Session.GetString("UserRole");

            if (userId == null)
            {
                TempData["ErrorMessage"] =
                    "Vui lòng đăng nhập để xem hóa đơn.";

                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }

            if (id <= 0)
            {
                TempData["ErrorMessage"] =
                    "Mã đơn thuê không hợp lệ.";

                return RedirectToAction(
                    "History",
                    "Rental"
                );
            }

            var rental = await _context.Rentals
                .AsNoTracking()
                .Include(item => item.User)
                .Include(item => item.Payment)
                .Include(item => item.RentalDetails)
                    .ThenInclude(detail => detail.Book)
                .FirstOrDefaultAsync(item =>
                    item.Id == id);

            if (rental == null)
            {
                TempData["ErrorMessage"] =
                    $"Không tìm thấy đơn thuê #{id}.";

                return RedirectToAction(
                    "History",
                    "Rental"
                );
            }

            var isStaff =
                userRole == "Staff" ||
                userRole == "Admin";

            var isOwner =
                rental.UserId == userId.Value;

            if (!isStaff && !isOwner)
            {
                TempData["ErrorMessage"] =
                    "Bạn không có quyền xem hóa đơn này.";

                return RedirectToAction(
                    "AccessDenied",
                    "Home"
                );
            }

            return View(
                "~/Views/Receipt/Details.cshtml",
                rental
            );
        }

        // =========================================================
        // LẤY USER ID TỪ SESSION
        // =========================================================
        private int? GetCurrentUserId()
        {
            var value =
                HttpContext.Session.GetString("UserId");

            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return int.TryParse(
                value,
                out var userId
            )
                ? userId
                : null;
        }
    }
}