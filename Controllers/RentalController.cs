using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VDK_BookRental.Data;
using VDK_BookRental.Models;

namespace VDK_BookRental.Controllers
{
    public class RentalController : Controller
    {
        private readonly AppDbContext _context;

        private readonly ILogger<RentalController>
            _logger;

        private static readonly string[]
            CurrentRentalStatuses =
            {
                "Pending",
                "Approved",
                "Borrowing"
            };

        public RentalController(
            AppDbContext context,
            ILogger<RentalController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // =====================================================
        // ROLLBACK TRANSACTION AN TOÀN
        // =====================================================

        private async Task RollbackSafelyAsync(
            IDbContextTransaction transaction)
        {
            try
            {
                await transaction.RollbackAsync();
            }
            catch (Exception rollbackException)
            {
                _logger.LogError(
                    rollbackException,
                    "Không thể rollback transaction khi hủy đơn thuê.");
            }
        }

        // =====================================================
        // URL: /Rental
        // =====================================================

        [HttpGet]
        public IActionResult Index()
        {
            return RedirectToAction(
                nameof(Current));
        }

        // =====================================================
        // SÁCH ĐANG THUÊ
        // URL: /Rental/Current
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Current()
        {
            var userId =
                GetCurrentUserId();

            if (userId == null)
            {
                TempData["ErrorMessage"] =
                    "Vui lòng đăng nhập để xem sách đang thuê.";

                return RedirectToAction(
                    "Login",
                    "Account");
            }

            try
            {
                var rentals =
                    await _context.Rentals
                        .AsNoTracking()
                        .Where(rental =>
                            rental.UserId ==
                                userId.Value
                            &&
                            CurrentRentalStatuses
                                .Contains(
                                    rental.Status))
                        .Include(rental =>
                            rental.Payment)
                        .Include(rental =>
                            rental.RentalDetails)
                            .ThenInclude(detail =>
                                detail.Book)
                        .OrderBy(rental =>
                            rental.Status ==
                                "Borrowing"
                                    ? 0
                                    : rental.Status ==
                                        "Approved"
                                            ? 1
                                            : 2)
                        .ThenBy(rental =>
                            rental.ReturnDate)
                        .ThenByDescending(rental =>
                            rental.Id)
                        .ToListAsync();

                return View(
                    "~/Views/Rental/Current.cshtml",
                    rentals);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Không thể tải đơn thuê hiện tại " +
                    "của UserId {UserId}.",
                    userId.Value);

                ViewBag.LoadError =
                    "Không thể tải danh sách sách đang thuê. " +
                    "Chi tiết lỗi đã được ghi trong Visual Studio.";

                return View(
                    "~/Views/Rental/Current.cshtml",
                    new List<Rental>());
            }
        }

        // =====================================================
        // LỊCH SỬ THUÊ
        // URL: /Rental/History
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> History()
        {
            var userId =
                GetCurrentUserId();

            if (userId == null)
            {
                TempData["ErrorMessage"] =
                    "Vui lòng đăng nhập để xem lịch sử thuê.";

                return RedirectToAction(
                    "Login",
                    "Account");
            }

            try
            {
                var rentals =
                    await _context.Rentals
                        .AsNoTracking()
                        .Where(rental =>
                            rental.UserId ==
                            userId.Value)
                        .Include(rental =>
                            rental.Payment)
                        .Include(rental =>
                            rental.RentalDetails)
                            .ThenInclude(detail =>
                                detail.Book)
                        .OrderByDescending(rental =>
                            rental.RentalDate)
                        .ThenByDescending(rental =>
                            rental.Id)
                        .ToListAsync();

                return View(
                    "~/Views/Rental/History.cshtml",
                    rentals);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Không thể tải lịch sử thuê " +
                    "của UserId {UserId}.",
                    userId.Value);

                TempData["ErrorMessage"] =
                    "Không thể tải lịch sử thuê sách.";

                return RedirectToAction(
                    "Index",
                    "Books");
            }
        }

        // =====================================================
        // ĐI ĐẾN THANH TOÁN
        // URL: /Rental/Payment/5
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Payment(int id)
        {
            var userId =
                GetCurrentUserId();

            if (userId == null)
            {
                TempData["ErrorMessage"] =
                    "Vui lòng đăng nhập để thanh toán.";

                return RedirectToAction(
                    "Login",
                    "Account");
            }

            if (id <= 0)
            {
                TempData["ErrorMessage"] =
                    "Mã đơn thuê không hợp lệ.";

                return RedirectToAction(
                    nameof(Current));
            }

            try
            {
                var rental =
                    await _context.Rentals
                        .AsNoTracking()
                        .Include(item =>
                            item.Payment)
                        .FirstOrDefaultAsync(item =>
                            item.Id == id
                            &&
                            item.UserId ==
                                userId.Value);

                if (rental == null)
                {
                    TempData["ErrorMessage"] =
                        "Không tìm thấy đơn thuê hoặc " +
                        "bạn không có quyền truy cập.";

                    return RedirectToAction(
                        nameof(Current));
                }

                if (string.Equals(
                        rental.Status,
                        "Cancelled",
                        StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] =
                        "Đơn thuê đã bị hủy.";

                    return RedirectToAction(
                        nameof(History));
                }

                if (string.Equals(
                        rental.Status,
                        "Returned",
                        StringComparison.OrdinalIgnoreCase))
                {
                    TempData["InfoMessage"] =
                        "Đơn thuê đã hoàn tất.";

                    return RedirectToAction(
                        nameof(History));
                }

                var paymentStatus =
                    rental.Payment?.Status;

                if (string.Equals(
                        paymentStatus,
                        "Paid",
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    string.Equals(
                        paymentStatus,
                        "Completed",
                        StringComparison.OrdinalIgnoreCase))
                {
                    TempData["SuccessMessage"] =
                        "Đơn thuê đã được thanh toán.";

                    return RedirectToAction(
                        nameof(Current));
                }

                if (string.Equals(
                        paymentStatus,
                        "AwaitingConfirmation",
                        StringComparison.OrdinalIgnoreCase))
                {
                    TempData["InfoMessage"] =
                        "Thanh toán đang chờ xác nhận.";

                    return RedirectToAction(
                        nameof(Current));
                }

                return RedirectToAction(
                    "Checkout",
                    "Payment",
                    new
                    {
                        rentalId = rental.Id
                    });
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Lỗi mở thanh toán RentalId {RentalId}.",
                    id);

                TempData["ErrorMessage"] =
                    "Không thể mở trang thanh toán.";

                return RedirectToAction(
                    nameof(Current));
            }
        }

        // =====================================================
        // HỦY ĐƠN
        // Chỉ cho hủy đơn Pending và chưa thanh toán.
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(
            int rentalId)
        {
            var userId =
                GetCurrentUserId();

            if (userId == null)
            {
                TempData["ErrorMessage"] =
                    "Phiên đăng nhập đã hết hạn.";

                return RedirectToAction(
                    "Login",
                    "Account");
            }

            if (rentalId <= 0)
            {
                TempData["ErrorMessage"] =
                    "Mã đơn thuê không hợp lệ.";

                return RedirectToAction(
                    nameof(Current));
            }

            try
            {
                var executionStrategy =
                    _context.Database.CreateExecutionStrategy();

                return await executionStrategy.ExecuteAsync(async () =>
                {
                    _context.ChangeTracker.Clear();

                    await using var transaction =
                        await _context.Database.BeginTransactionAsync();

                    try
                    {
                        var rental =
                            await _context.Rentals
                                .Include(item => item.Payment)
                                .Include(item => item.RentalDetails)
                                    .ThenInclude(detail => detail.Book)
                                .FirstOrDefaultAsync(item =>
                                    item.Id == rentalId &&
                                    item.UserId == userId.Value);

                        if (rental == null)
                        {
                            await RollbackSafelyAsync(transaction);

                            TempData["ErrorMessage"] =
                                "Không tìm thấy đơn thuê.";

                            return RedirectToAction(nameof(Current));
                        }

                        if (!string.Equals(rental.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                        {
                            await RollbackSafelyAsync(transaction);

                            TempData["ErrorMessage"] =
                                "Chỉ có thể hủy đơn đang chờ duyệt.";

                            return RedirectToAction(nameof(Current));
                        }

                        var paymentStatus = rental.Payment?.Status;

                        if (string.Equals(paymentStatus, "Paid", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(paymentStatus, "Completed", StringComparison.OrdinalIgnoreCase))
                        {
                            await RollbackSafelyAsync(transaction);

                            TempData["ErrorMessage"] =
                                "Không thể hủy đơn đã thanh toán.";

                            return RedirectToAction(nameof(Current));
                        }

                        if (string.Equals(paymentStatus, "AwaitingConfirmation", StringComparison.OrdinalIgnoreCase))
                        {
                            await RollbackSafelyAsync(transaction);

                            TempData["ErrorMessage"] =
                                "Thanh toán đang chờ xác nhận nên chưa thể hủy đơn.";

                            return RedirectToAction(nameof(Current));
                        }

                        foreach (var detail in rental.RentalDetails)
                        {
                            if (detail.Book == null)
                            {
                                continue;
                            }

                            if (detail.Quantity > 0)
                            {
                                detail.Book.Quantity += detail.Quantity;
                            }

                            if (detail.Book.Quantity > 0)
                            {
                                detail.Book.Status = "Available";
                            }
                        }

                        rental.Status = "Cancelled";

                        if (rental.Payment != null)
                        {
                            rental.Payment.Status = "Cancelled";
                        }

                        await _context.SaveChangesAsync();

                        await transaction.CommitAsync();

                        TempData["SuccessMessage"] =
                            $"Đã hủy đơn #RENT-{rental.Id:D4} và hoàn lại sách vào kho.";

                        return RedirectToAction(nameof(Current));
                    }
                    catch
                    {
                        await RollbackSafelyAsync(transaction);

                        throw;
                    }
                });
            }
            catch (DbUpdateConcurrencyException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Xung đột dữ liệu khi hủy RentalId {RentalId}.",
                    rentalId);

                TempData["ErrorMessage"] =
                    "Dữ liệu vừa được thay đổi. " +
                    "Vui lòng tải lại trang.";

                return RedirectToAction(
                    nameof(Current));
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(
                    exception,
                    "Lỗi database khi hủy RentalId {RentalId}.",
                    rentalId);

                TempData["ErrorMessage"] =
                    "Database không thể cập nhật đơn thuê.";

                return RedirectToAction(
                    nameof(Current));
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Lỗi khi hủy RentalId {RentalId}.",
                    rentalId);

                TempData["ErrorMessage"] =
                    "Đã xảy ra lỗi khi hủy đơn thuê.";

                return RedirectToAction(
                    nameof(Current));
            }
        }

        // =====================================================
        // KIỂM TRA QUYỀN SỞ HỮU ĐƠN
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Check(int id)
        {
            var userId =
                GetCurrentUserId();

            if (userId == null)
            {
                return Unauthorized();
            }

            try
            {
                var exists =
                    await _context.Rentals
                        .AsNoTracking()
                        .AnyAsync(rental =>
                            rental.Id == id
                            &&
                            rental.UserId ==
                                userId.Value);

                return exists
                    ? Ok()
                    : NotFound();
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Lỗi kiểm tra RentalId {RentalId}.",
                    id);

                return StatusCode(
                    StatusCodes
                        .Status500InternalServerError);
            }
        }

        // =====================================================
        // SESSION
        // =====================================================

        private int? GetCurrentUserId()
        {
            var value =
                HttpContext.Session.GetString(
                    "UserId");

            return int.TryParse(
                value,
                out var userId)
                    ? userId
                    : null;
        }
    }
}