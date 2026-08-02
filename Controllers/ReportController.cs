using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VDK_BookRental.Data;
using VDK_BookRental.ViewModels;

namespace VDK_BookRental.Controllers
{
    public class ReportController : Controller
    {
        private readonly AppDbContext _context;

        public ReportController(AppDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // BÁO CÁO DOANH THU VÀ HOẠT ĐỘNG THUÊ SÁCH
        // URL: /Report
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Index(
            DateTime? startDate,
            DateTime? endDate)
        {
            var accessResult = CheckStaffAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            var today = DateTime.Today;

            var fromDate =
                startDate?.Date
                ?? new DateTime(
                    today.Year,
                    today.Month,
                    1
                );

            var toDate =
                endDate?.Date
                ?? today;

            if (fromDate > toDate)
            {
                TempData["ErrorMessage"] =
                    "Ngày bắt đầu không được lớn hơn ngày kết thúc.";

                return RedirectToAction(nameof(Index));
            }

            var toDateExclusive =
                toDate.AddDays(1);

            var rentals = await _context.Rentals
                .AsNoTracking()
                .Where(rental =>
                    rental.RentalDate >= fromDate &&
                    rental.RentalDate < toDateExclusive)
                .Include(rental => rental.User)
                .Include(rental => rental.Payment)
                .Include(rental => rental.RentalDetails)
                    .ThenInclude(detail => detail.Book)
                .OrderByDescending(rental => rental.Id)
                .ToListAsync();

            var paidRentals = rentals
                .Where(rental =>
                    rental.Payment != null &&
                    (rental.Payment.Status == "Paid" ||
                     rental.Payment.Status == "Completed"))
                .ToList();

            var firstChartMonth =
                new DateTime(
                    today.Year,
                    today.Month,
                    1
                )
                .AddMonths(-5);

            var chartRentals = await _context.Rentals
                .AsNoTracking()
                .Where(rental =>
                    rental.RentalDate >= firstChartMonth)
                .Include(rental => rental.Payment)
                .ToListAsync();

            var monthlyRevenue =
                new List<MonthlyRevenueItem>();

            for (var monthOffset = 0;
                 monthOffset < 6;
                 monthOffset++)
            {
                var monthStart =
                    firstChartMonth.AddMonths(monthOffset);

                var nextMonth =
                    monthStart.AddMonths(1);

                var monthPaidRentals = chartRentals
                    .Where(rental =>
                        rental.RentalDate >= monthStart &&
                        rental.RentalDate < nextMonth &&
                        rental.Payment != null &&
                        (rental.Payment.Status == "Paid" ||
                         rental.Payment.Status == "Completed"))
                    .ToList();

                monthlyRevenue.Add(
                    new MonthlyRevenueItem
                    {
                        Label =
                            $"Tháng {monthStart.Month}/{monthStart.Year}",

                        Revenue =
                            monthPaidRentals.Sum(
                                rental => rental.TotalAmount
                            ),

                        RentalCount =
                            monthPaidRentals.Count
                    }
                );
            }

            var rentalStatuses =
                new List<RentalStatusItem>
                {
                    new RentalStatusItem
                    {
                        Status = "Pending",
                        DisplayName = "Chờ duyệt",
                        Count = rentals.Count(
                            rental =>
                                rental.Status == "Pending"
                        )
                    },

                    new RentalStatusItem
                    {
                        Status = "Approved",
                        DisplayName = "Đã duyệt",
                        Count = rentals.Count(
                            rental =>
                                rental.Status == "Approved"
                        )
                    },

                    new RentalStatusItem
                    {
                        Status = "Borrowing",
                        DisplayName = "Đang thuê",
                        Count = rentals.Count(
                            rental =>
                                rental.Status == "Borrowing"
                        )
                    },

                    new RentalStatusItem
                    {
                        Status = "Returned",
                        DisplayName = "Đã trả",
                        Count = rentals.Count(
                            rental =>
                                rental.Status == "Returned"
                        )
                    },

                    new RentalStatusItem
                    {
                        Status = "Cancelled",
                        DisplayName = "Đã hủy",
                        Count = rentals.Count(
                            rental =>
                                rental.Status == "Cancelled"
                        )
                    }
                };

            var topBooks = paidRentals
                .SelectMany(
                    rental => rental.RentalDetails
                )
                .Where(detail =>
                    detail.Book != null)
                .GroupBy(
                    detail => new
                    {
                        detail.BookId,
                        detail.Book!.Title
                    }
                )
                .Select(group =>
                    new TopBookItem
                    {
                        BookId = group.Key.BookId,

                        Title = group.Key.Title,

                        Quantity = group.Sum(
                            detail => detail.Quantity
                        ),

                        Revenue = group.Sum(
                            detail => detail.SubTotal
                        )
                    }
                )
                .OrderByDescending(item =>
                    item.Quantity)
                .ThenByDescending(item =>
                    item.Revenue)
                .Take(5)
                .ToList();

            var recentTransactions = paidRentals
                .OrderByDescending(rental =>
                    rental.Id)
                .Take(10)
                .Select(rental =>
                    new RecentRevenueItem
                    {
                        RentalId = rental.Id,

                        PaymentId =
                            rental.Payment?.Id ?? 0,

                        CustomerName =
                            !string.IsNullOrWhiteSpace(
                                rental.User?.FullName)
                                ? rental.User.FullName
                                : rental.User?.UserName
                                  ?? "Không xác định",

                        PaymentMethod =
                            !string.IsNullOrWhiteSpace(
                                rental.Payment?.PaymentMethod)
                                ? rental.Payment.PaymentMethod
                                : "Chưa xác định",

                        RentalDate =
                            rental.RentalDate,

                        Amount =
                            rental.TotalAmount,

                        PaymentStatus =
                            rental.Payment?.Status
                            ?? "Không xác định"
                    }
                )
                .ToList();

            var model =
                new ReportViewModel
                {
                    StartDate = fromDate,

                    EndDate = toDate,

                    TotalRevenue =
                        paidRentals.Sum(
                            rental => rental.TotalAmount
                        ),

                    TotalRentals =
                        rentals.Count,

                    PaidPayments =
                        paidRentals.Count,

                    PendingPayments =
                        rentals.Count(
                            rental =>
                                rental.Payment == null ||
                                rental.Payment.Status == "Pending" ||
                                rental.Payment.Status ==
                                    "AwaitingConfirmation"
                        ),

                    BorrowingRentals =
                        rentals.Count(
                            rental =>
                                rental.Status == "Borrowing"
                        ),

                    ReturnedRentals =
                        rentals.Count(
                            rental =>
                                rental.Status == "Returned"
                        ),

                    CancelledRentals =
                        rentals.Count(
                            rental =>
                                rental.Status == "Cancelled"
                        ),

                    MonthlyRevenue =
                        monthlyRevenue,

                    RentalStatuses =
                        rentalStatuses,

                    TopBooks =
                        topBooks,

                    RecentTransactions =
                        recentTransactions
                };

            return View(
                "~/Views/Report/Index.cshtml",
                model
            );
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