using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VDK_BookRental.Data;
using VDK_BookRental.Models;

namespace VDK_BookRental.Controllers
{
    public class PaymentController : Controller
    {
        private static readonly HashSet<string> AllowedPaymentMethods =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "MB Bank",
                "Ví MoMo"
            };

        private readonly AppDbContext _context;
        private readonly ILogger<PaymentController> _logger;
        private readonly IWebHostEnvironment _environment;

        public PaymentController(
            AppDbContext context,
            ILogger<PaymentController> logger,
            IWebHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _environment = environment;
        }

        // =========================================================
        // TRANG THANH TOÁN
        // URL: /Payment/Checkout?rentalId=5
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Checkout(int rentalId)
        {
            var userId = GetCurrentUserId();

            if (userId == null)
            {
                TempData["ErrorMessage"] =
                    "Vui lòng đăng nhập để thanh toán.";

                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }

            if (rentalId <= 0)
            {
                TempData["ErrorMessage"] =
                    "Mã đơn thuê không hợp lệ.";

                return RedirectToAction(
                    "Current",
                    "Rental"
                );
            }

            try
            {
                var rental = await LoadRentalAsync(
                    rentalId,
                    userId.Value,
                    asNoTracking: true
                );

                if (rental == null)
                {
                    TempData["ErrorMessage"] =
                        "Không tìm thấy đơn thuê hoặc " +
                        "bạn không có quyền truy cập.";

                    return RedirectToAction(
                        "Current",
                        "Rental"
                    );
                }

                if (IsRentalCancelled(rental.Status))
                {
                    TempData["ErrorMessage"] =
                        "Đơn thuê đã bị hủy nên không thể thanh toán.";

                    return RedirectToAction(
                        "History",
                        "Rental"
                    );
                }

                if (IsRentalReturned(rental.Status))
                {
                    TempData["InfoMessage"] =
                        "Đơn thuê này đã hoàn tất.";

                    return RedirectToAction(
                        "History",
                        "Rental"
                    );
                }

                return View(
                    "~/Views/Payment/Checkout.cshtml",
                    rental
                );
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Không thể mở trang thanh toán cho " +
                    "RentalId {RentalId}, UserId {UserId}.",
                    rentalId,
                    userId.Value);

                TempData["ErrorMessage"] =
                    GetGeneralErrorMessage(
                        exception,
                        "Không thể tải trang thanh toán."
                    );

                return RedirectToAction(
                    "Current",
                    "Rental"
                );
            }
        }

        // =========================================================
        // KHÁCH XÁC NHẬN ĐÃ CHUYỂN KHOẢN
        // Pending / Rejected -> AwaitingConfirmation
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(
            int rentalId,
            string? paymentMethod)
        {
            var userId = GetCurrentUserId();

            if (userId == null)
            {
                TempData["ErrorMessage"] =
                    "Phiên đăng nhập đã hết hạn. " +
                    "Vui lòng đăng nhập lại.";

                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }

            if (rentalId <= 0)
            {
                TempData["ErrorMessage"] =
                    "Mã đơn thuê không hợp lệ.";

                return RedirectToAction(
                    "Current",
                    "Rental"
                );
            }

            var normalizedPaymentMethod =
                NormalizePaymentMethod(paymentMethod);

            if (normalizedPaymentMethod == null)
            {
                TempData["ErrorMessage"] =
                    "Phương thức thanh toán không hợp lệ. " +
                    "Vui lòng chọn MB Bank hoặc Ví MoMo.";

                return RedirectToAction(
                    nameof(Checkout),
                    new
                    {
                        rentalId
                    }
                );
            }

            try
            {
                /*
                 * Không mở transaction thủ công ở đây.
                 * Một SaveChangesAsync là transaction nguyên tử,
                 * đồng thời tương thích với EnableRetryOnFailure.
                 */
                var rental = await _context.Rentals
                    .Include(item => item.Payment)
                    .FirstOrDefaultAsync(item =>
                        item.Id == rentalId &&
                        item.UserId == userId.Value);

                if (rental == null)
                {
                    TempData["ErrorMessage"] =
                        "Không tìm thấy đơn thuê hoặc " +
                        "bạn không có quyền thanh toán đơn này.";

                    return RedirectToAction(
                        "Current",
                        "Rental"
                    );
                }

                if (IsRentalCancelled(rental.Status))
                {
                    TempData["ErrorMessage"] =
                        "Đơn thuê đã bị hủy nên không thể thanh toán.";

                    return RedirectToAction(
                        "History",
                        "Rental"
                    );
                }

                if (IsRentalReturned(rental.Status))
                {
                    TempData["ErrorMessage"] =
                        "Đơn thuê đã hoàn tất nên không thể thanh toán lại.";

                    return RedirectToAction(
                        "History",
                        "Rental"
                    );
                }

                var payment = rental.Payment;

                if (payment != null &&
                    IsPaymentCompleted(payment.Status))
                {
                    TempData["InfoMessage"] =
                        "Đơn thuê này đã được thanh toán.";

                    return RedirectToAction(
                        "Details",
                        "Contract",
                        new
                        {
                            rentalId
                        }
                    );
                }

                if (payment != null &&
                    string.Equals(
                        payment.Status,
                        "AwaitingConfirmation",
                        StringComparison.OrdinalIgnoreCase))
                {
                    TempData["InfoMessage"] =
                        "Yêu cầu thanh toán đã được gửi trước đó. " +
                        "Vui lòng chờ nhân viên xác nhận.";

                    return RedirectToAction(
                        "Details",
                        "Contract",
                        new
                        {
                            rentalId
                        }
                    );
                }

                if (payment != null &&
                    string.Equals(
                        payment.Status,
                        "Cancelled",
                        StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] =
                        "Giao dịch thanh toán đã bị hủy. " +
                        "Vui lòng liên hệ nhân viên để được mở lại.";

                    return RedirectToAction(
                        nameof(Checkout),
                        new
                        {
                            rentalId
                        }
                    );
                }

                if (payment == null)
                {
                    payment = new Payment
                    {
                        RentalId = rental.Id,
                        Amount = rental.TotalAmount,
                        PaymentMethod = normalizedPaymentMethod,
                        QrCodeUrl =
                            GetQrCodeUrl(normalizedPaymentMethod),
                        TransferContent =
                            $"RENTAL_{rental.Id}",
                        Status = "AwaitingConfirmation",
                        CreatedAt = DateTime.Now
                    };

                    _context.Payments.Add(payment);
                }
                else
                {
                    payment.Amount =
                        rental.TotalAmount;

                    payment.PaymentMethod =
                        normalizedPaymentMethod;

                    payment.QrCodeUrl =
                        GetQrCodeUrl(normalizedPaymentMethod);

                    if (string.IsNullOrWhiteSpace(
                        payment.TransferContent))
                    {
                        payment.TransferContent =
                            $"RENTAL_{rental.Id}";
                    }

                    payment.Status =
                        "AwaitingConfirmation";
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "UserId {UserId} đã gửi xác nhận " +
                    "thanh toán PaymentId {PaymentId}, " +
                    "RentalId {RentalId}, Method {Method}, " +
                    "Amount {Amount}.",
                    userId.Value,
                    payment.Id,
                    rental.Id,
                    payment.PaymentMethod,
                    payment.Amount);

                TempData["SuccessMessage"] =
                    "Đã xác nhận chuyển khoản. " +
                    "Hợp đồng thuê sách đã được tạo.";

                return RedirectToAction(
                    "Details",
                    "Contract",
                    new
                    {
                        rentalId = rental.Id
                    }
                );
            }
            catch (DbUpdateConcurrencyException exception)
            {
                _context.ChangeTracker.Clear();

                _logger.LogWarning(
                    exception,
                    "Xung đột dữ liệu khi xác nhận thanh toán " +
                    "RentalId {RentalId}, UserId {UserId}.",
                    rentalId,
                    userId.Value);

                TempData["ErrorMessage"] =
                    "Dữ liệu thanh toán vừa được thay đổi. " +
                    "Vui lòng tải lại trang và thử lại.";

                return RedirectToAction(
                    nameof(Checkout),
                    new
                    {
                        rentalId
                    }
                );
            }
            catch (DbUpdateException exception)
            {
                _context.ChangeTracker.Clear();

                _logger.LogError(
                    exception,
                    "Lỗi database khi xác nhận thanh toán " +
                    "RentalId {RentalId}, UserId {UserId}. " +
                    "Lỗi gốc: {BaseMessage}",
                    rentalId,
                    userId.Value,
                    exception.GetBaseException().Message);

                TempData["ErrorMessage"] =
                    GetDatabaseErrorMessage(exception);

                return RedirectToAction(
                    nameof(Checkout),
                    new
                    {
                        rentalId
                    }
                );
            }
            catch (Exception exception)
            {
                _context.ChangeTracker.Clear();

                _logger.LogError(
                    exception,
                    "Lỗi khi xác nhận thanh toán " +
                    "RentalId {RentalId}, UserId {UserId}. " +
                    "Lỗi gốc: {BaseMessage}",
                    rentalId,
                    userId.Value,
                    exception.GetBaseException().Message);

                TempData["ErrorMessage"] =
                    GetGeneralErrorMessage(
                        exception,
                        "Đã xảy ra lỗi trong quá trình thanh toán."
                    );

                return RedirectToAction(
                    nameof(Checkout),
                    new
                    {
                        rentalId
                    }
                );
            }
        }

        // =========================================================
        // TRANG GỬI THANH TOÁN THÀNH CÔNG
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Success(int rentalId)
        {
            var userId = GetCurrentUserId();

            if (userId == null)
            {
                TempData["ErrorMessage"] =
                    "Vui lòng đăng nhập để xem thanh toán.";

                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }

            if (rentalId <= 0)
            {
                TempData["ErrorMessage"] =
                    "Mã đơn thuê không hợp lệ.";

                return RedirectToAction(
                    "History",
                    "Rental"
                );
            }

            try
            {
                var rental = await LoadRentalAsync(
                    rentalId,
                    userId.Value,
                    asNoTracking: true
                );

                if (rental == null)
                {
                    TempData["ErrorMessage"] =
                        "Không tìm thấy đơn thuê hoặc " +
                        "bạn không có quyền truy cập.";

                    return RedirectToAction(
                        "History",
                        "Rental"
                    );
                }

                return View(
                    "~/Views/Payment/Success.cshtml",
                    rental
                );
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Không thể tải trang thanh toán thành công " +
                    "RentalId {RentalId}, UserId {UserId}.",
                    rentalId,
                    userId.Value);

                TempData["ErrorMessage"] =
                    GetGeneralErrorMessage(
                        exception,
                        "Không thể tải thông tin thanh toán."
                    );

                return RedirectToAction(
                    "History",
                    "Rental"
                );
            }
        }

        // =========================================================
        // TẢI ĐƠN THUÊ ĐẦY ĐỦ CHO CHECKOUT / SUCCESS
        // =========================================================

        private async Task<Rental?> LoadRentalAsync(
            int rentalId,
            int userId,
            bool asNoTracking)
        {
            IQueryable<Rental> query =
                _context.Rentals;

            if (asNoTracking)
            {
                query = query.AsNoTracking();
            }

            return await query
                .Include(item => item.User)
                .Include(item => item.Payment)
                .Include(item => item.RentalDetails)
                    .ThenInclude(detail => detail.Book)
                .FirstOrDefaultAsync(item =>
                    item.Id == rentalId &&
                    item.UserId == userId);
        }

        // =========================================================
        // SESSION
        // =========================================================

        private int? GetCurrentUserId()
        {
            var value =
                HttpContext.Session.GetString("UserId");

            return int.TryParse(
                value,
                out var userId)
                    ? userId
                    : null;
        }

        // =========================================================
        // CHUẨN HÓA PHƯƠNG THỨC THANH TOÁN
        // =========================================================

        private static string? NormalizePaymentMethod(
            string? paymentMethod)
        {
            if (string.IsNullOrWhiteSpace(paymentMethod))
            {
                return null;
            }

            var trimmedMethod =
                paymentMethod.Trim();

            if (!AllowedPaymentMethods.Contains(
                trimmedMethod))
            {
                return null;
            }

            if (string.Equals(
                trimmedMethod,
                "MB Bank",
                StringComparison.OrdinalIgnoreCase))
            {
                return "MB Bank";
            }

            return "Ví MoMo";
        }

        private static string GetQrCodeUrl(
            string paymentMethod)
        {
            return string.Equals(
                paymentMethod,
                "Ví MoMo",
                StringComparison.OrdinalIgnoreCase)
                    ? "/images/books/momopayment-qr.jpg"
                    : "/images/books/payment-qr.jpg";
        }

        private static bool IsPaymentCompleted(
            string? status)
        {
            return string.Equals(
                       status,
                       "Paid",
                       StringComparison.OrdinalIgnoreCase)
                   ||
                   string.Equals(
                       status,
                       "Completed",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRentalCancelled(
            string? status)
        {
            return string.Equals(
                status,
                "Cancelled",
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRentalReturned(
            string? status)
        {
            return string.Equals(
                status,
                "Returned",
                StringComparison.OrdinalIgnoreCase);
        }

        // =========================================================
        // THÔNG BÁO LỖI
        // =========================================================

        private string GetDatabaseErrorMessage(
            DbUpdateException exception)
        {
            if (_environment.IsDevelopment())
            {
                return
                    "Lỗi database khi thanh toán: " +
                    exception.GetBaseException().Message;
            }

            return
                "Không thể cập nhật giao dịch thanh toán. " +
                "Vui lòng thử lại sau.";
        }

        private string GetGeneralErrorMessage(
            Exception exception,
            string publicMessage)
        {
            if (_environment.IsDevelopment())
            {
                return
                    publicMessage + " Chi tiết: " +
                    exception.GetBaseException().Message;
            }

            return publicMessage;
        }
    }
}