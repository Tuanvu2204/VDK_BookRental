using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VDK_BookRental.Data;
using VDK_BookRental.ViewModels;

namespace VDK_BookRental.Controllers
{
    public class NotificationController : Controller
    {
        private const int LowStockThreshold = 3;

        private readonly AppDbContext _context;

        public NotificationController(AppDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // TRUNG TÂM THÔNG BÁO DÀNH CHO STAFF / ADMIN
        // URL: /Notification
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

            var pendingRentalCount = await _context.Rentals
                .AsNoTracking()
                .CountAsync(rental =>
                    rental.Status == "Pending");

            var awaitingPaymentCount = await _context.Payments
                .AsNoTracking()
                .CountAsync(payment =>
                    payment.Status == "AwaitingConfirmation");

            var overdueRentalCount = await _context.Rentals
                .AsNoTracking()
                .CountAsync(rental =>
                    rental.Status == "Borrowing" &&
                    rental.ReturnDate.Date < today);

            var lowStockBookCount = await _context.Books
                .AsNoTracking()
                .CountAsync(book =>
                    book.Quantity <= LowStockThreshold);

            var pendingRentals = await _context.Rentals
                .AsNoTracking()
                .Where(rental =>
                    rental.Status == "Pending")
                .Include(rental =>
                    rental.User)
                .OrderByDescending(rental =>
                    rental.Id)
                .Take(10)
                .Select(rental =>
                    new PendingRentalNotificationItem
                    {
                        RentalId = rental.Id,

                        CustomerName =
                            rental.User != null &&
                            !string.IsNullOrWhiteSpace(
                                rental.User.FullName)
                                ? rental.User.FullName
                                : rental.User != null
                                    ? rental.User.UserName
                                    : "Không xác định",

                        RentalDate =
                            rental.RentalDate,

                        TotalAmount =
                            rental.TotalAmount
                    })
                .ToListAsync();

            var awaitingPayments = await _context.Payments
                .AsNoTracking()
                .Where(payment =>
                    payment.Status == "AwaitingConfirmation")
                .Include(payment =>
                    payment.Rental)
                    .ThenInclude(rental =>
                        rental!.User)
                .OrderByDescending(payment =>
                    payment.Id)
                .Take(10)
                .Select(payment =>
                    new AwaitingPaymentNotificationItem
                    {
                        PaymentId =
                            payment.Id,

                        RentalId =
                            payment.RentalId,

                        CustomerName =
                            payment.Rental != null &&
                            payment.Rental.User != null &&
                            !string.IsNullOrWhiteSpace(
                                payment.Rental.User.FullName)
                                ? payment.Rental.User.FullName
                                : payment.Rental != null &&
                                  payment.Rental.User != null
                                    ? payment.Rental.User.UserName
                                    : "Không xác định",

                        PaymentMethod =
                            string.IsNullOrWhiteSpace(
                                payment.PaymentMethod)
                                ? "Chưa xác định"
                                : payment.PaymentMethod,

                        Amount =
                            payment.Rental != null
                                ? payment.Rental.TotalAmount
                                : 0
                    })
                .ToListAsync();

            var overdueRentals = await _context.Rentals
                .AsNoTracking()
                .Where(rental =>
                    rental.Status == "Borrowing" &&
                    rental.ReturnDate.Date < today)
                .Include(rental =>
                    rental.User)
                .OrderBy(rental =>
                    rental.ReturnDate)
                .Take(10)
                .Select(rental =>
                    new OverdueNotificationItem
                    {
                        RentalId =
                            rental.Id,

                        CustomerName =
                            rental.User != null &&
                            !string.IsNullOrWhiteSpace(
                                rental.User.FullName)
                                ? rental.User.FullName
                                : rental.User != null
                                    ? rental.User.UserName
                                    : "Không xác định",

                        ReturnDate =
                            rental.ReturnDate,

                        OverdueDays =
                            EF.Functions.DateDiffDay(
                                rental.ReturnDate.Date,
                                today
                            )
                    })
                .ToListAsync();

            var lowStockBooks = await _context.Books
                .AsNoTracking()
                .Where(book =>
                    book.Quantity <= LowStockThreshold)
                .OrderBy(book =>
                    book.Quantity)
                .ThenBy(book =>
                    book.Title)
                .Take(10)
                .Select(book =>
                    new LowStockNotificationItem
                    {
                        BookId =
                            book.Id,

                        Title =
                            book.Title,

                        Quantity =
                            book.Quantity
                    })
                .ToListAsync();

            var model =
                new NotificationCenterViewModel
                {
                    PendingRentalCount =
                        pendingRentalCount,

                    AwaitingPaymentCount =
                        awaitingPaymentCount,

                    OverdueRentalCount =
                        overdueRentalCount,

                    LowStockBookCount =
                        lowStockBookCount,

                    PendingRentals =
                        pendingRentals,

                    AwaitingPayments =
                        awaitingPayments,

                    OverdueRentals =
                        overdueRentals,

                    LowStockBooks =
                        lowStockBooks
                };

            return View(
                "~/Views/Notification/Index.cshtml",
                model
            );
        }

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

            var hasAccess =
                string.Equals(
                    userRole,
                    "Staff",
                    StringComparison.OrdinalIgnoreCase
                )
                ||
                string.Equals(
                    userRole,
                    "Admin",
                    StringComparison.OrdinalIgnoreCase
                );

            if (!hasAccess)
            {
                TempData["ErrorMessage"] =
                    "Bạn không có quyền xem trung tâm thông báo.";

                return RedirectToAction(
                    "AccessDenied",
                    "Home"
                );
            }

            return null;
        }
    }
}