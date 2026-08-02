using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VDK_BookRental.Data;

namespace VDK_BookRental.Controllers
{
    public class PaymentController : Controller
    {
        private readonly AppDbContext _context;

        public PaymentController(AppDbContext context)
        {
            _context = context;
        }

        // =====================================================
        // TRANG THANH TOÁN
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Checkout(int rentalId)
        {
            var userIdText =
                HttpContext.Session.GetString("UserId");

            if (string.IsNullOrWhiteSpace(userIdText) ||
                !int.TryParse(userIdText, out int userId))
            {
                TempData["ErrorMessage"] =
                    "Vui lòng đăng nhập để tiếp tục thanh toán.";

                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }

            var rental = await _context.Rentals
                .AsNoTracking()
                .Include(r => r.User)
                .Include(r => r.RentalDetails)
                    .ThenInclude(rd => rd.Book)
                .Include(r => r.Payment)
                .FirstOrDefaultAsync(r => r.Id == rentalId);

            if (rental == null)
            {
                return NotFound();
            }

            if (rental.UserId != userId)
            {
                TempData["ErrorMessage"] =
                    "Bạn không có quyền xem đơn thuê này.";

                return RedirectToAction(
                    "Index",
                    "Books"
                );
            }

            if (rental.Status == "Cancelled")
            {
                TempData["ErrorMessage"] =
                    "Đơn thuê này đã bị hủy và không thể thanh toán.";

                return RedirectToAction(
                    "History",
                    "Rental"
                );
            }

            if (rental.Status == "Returned" ||
                rental.Status == "Completed")
            {
                TempData["ErrorMessage"] =
                    "Đơn thuê này đã hoàn tất.";

                return RedirectToAction(
                    "History",
                    "Rental"
                );
            }

            if (rental.Payment == null)
            {
                TempData["ErrorMessage"] =
                    "Không tìm thấy thông tin thanh toán của đơn thuê.";

                return RedirectToAction(
                    "History",
                    "Rental"
                );
            }

            if (rental.Payment.Status == "Paid" ||
                rental.Payment.Status == "Completed")
            {
                TempData["SuccessMessage"] =
                    "Đơn thuê này đã được thanh toán.";

                return RedirectToAction(nameof(Success));
            }

            return View(rental);
        }

        // =====================================================
        // KHÁCH HÀNG XÁC NHẬN ĐÃ THANH TOÁN
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(
            int rentalId,
            string paymentMethod)
        {
            var userIdText =
                HttpContext.Session.GetString("UserId");

            if (string.IsNullOrWhiteSpace(userIdText) ||
                !int.TryParse(userIdText, out int userId))
            {
                TempData["ErrorMessage"] =
                    "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";

                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }

            if (string.IsNullOrWhiteSpace(paymentMethod))
            {
                TempData["ErrorMessage"] =
                    "Vui lòng chọn phương thức thanh toán.";

                return RedirectToAction(
                    nameof(Checkout),
                    new { rentalId }
                );
            }

            var allowedMethods = new[]
            {
                "MB Bank",
                "Ví MoMo",
                "Tiền mặt"
            };

            if (!allowedMethods.Contains(paymentMethod))
            {
                TempData["ErrorMessage"] =
                    "Phương thức thanh toán không hợp lệ.";

                return RedirectToAction(
                    nameof(Checkout),
                    new { rentalId }
                );
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var rental = await _context.Rentals
                    .Include(r => r.Payment)
                    .FirstOrDefaultAsync(r => r.Id == rentalId);

                if (rental == null)
                {
                    await transaction.RollbackAsync();

                    return NotFound();
                }

                if (rental.UserId != userId)
                {
                    await transaction.RollbackAsync();

                    TempData["ErrorMessage"] =
                        "Bạn không có quyền thanh toán đơn thuê này.";

                    return RedirectToAction(
                        "Index",
                        "Books"
                    );
                }

                if (rental.Status == "Cancelled")
                {
                    await transaction.RollbackAsync();

                    TempData["ErrorMessage"] =
                        "Đơn thuê đã bị hủy và không thể thanh toán.";

                    return RedirectToAction(
                        nameof(Checkout),
                        new { rentalId }
                    );
                }

                if (rental.Status == "Returned" ||
                    rental.Status == "Completed")
                {
                    await transaction.RollbackAsync();

                    TempData["ErrorMessage"] =
                        "Đơn thuê đã hoàn tất.";

                    return RedirectToAction(
                        "History",
                        "Rental"
                    );
                }

                var payment = rental.Payment;

                if (payment == null)
                {
                    await transaction.RollbackAsync();

                    TempData["ErrorMessage"] =
                        "Không tìm thấy thông tin thanh toán.";

                    return RedirectToAction(
                        nameof(Checkout),
                        new { rentalId }
                    );
                }

                if (payment.Status == "Paid" ||
                    payment.Status == "Completed")
                {
                    await transaction.RollbackAsync();

                    TempData["SuccessMessage"] =
                        "Giao dịch này đã được thanh toán.";

                    return RedirectToAction(nameof(Success));
                }

                if (payment.Status == "AwaitingConfirmation")
                {
                    await transaction.RollbackAsync();

                    TempData["SuccessMessage"] =
                        "Thanh toán của bạn đang chờ nhân viên xác nhận.";

                    return RedirectToAction(nameof(Success));
                }

                payment.PaymentMethod = paymentMethod;
                payment.Status = "AwaitingConfirmation";

                rental.Status = "Pending";

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    "Đã gửi yêu cầu xác nhận thanh toán thành công.";

                return RedirectToAction(
                    nameof(Success),
                    new { rentalId }
                );
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();

                TempData["ErrorMessage"] =
                    "Không thể cập nhật thanh toán do lỗi dữ liệu.";

                return RedirectToAction(
                    nameof(Checkout),
                    new { rentalId }
                );
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();

                TempData["ErrorMessage"] =
                    "Đã xảy ra lỗi trong quá trình thanh toán.";

                return RedirectToAction(
                    nameof(Checkout),
                    new { rentalId }
                );
            }
        }

        // =====================================================
        // TRANG THANH TOÁN THÀNH CÔNG
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Success(int? rentalId)
        {
            var userIdText =
                HttpContext.Session.GetString("UserId");

            if (string.IsNullOrWhiteSpace(userIdText) ||
                !int.TryParse(userIdText, out int userId))
            {
                TempData["ErrorMessage"] =
                    "Vui lòng đăng nhập để xem thông tin thanh toán.";

                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }

            if (rentalId == null)
            {
                return View();
            }

            var rental = await _context.Rentals
                .AsNoTracking()
                .Include(r => r.Payment)
                .Include(r => r.RentalDetails)
                    .ThenInclude(rd => rd.Book)
                .FirstOrDefaultAsync(r => r.Id == rentalId.Value);

            if (rental == null)
            {
                return NotFound();
            }

            if (rental.UserId != userId)
            {
                TempData["ErrorMessage"] =
                    "Bạn không có quyền xem thông tin đơn thuê này.";

                return RedirectToAction(
                    "Index",
                    "Books"
                );
            }

            return View(rental);
        }
    }
}