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

        // =========================================================
        // DASHBOARD NHÂN VIÊN
        // URL: /Staff hoặc /Staff/Index
        // =========================================================
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

            ViewBag.PendingRentals =
                await _context.Rentals.CountAsync(
                    rental => rental.Status == "Pending");

            ViewBag.ApprovedRentals =
                await _context.Rentals.CountAsync(
                    rental => rental.Status == "Approved");

            ViewBag.BorrowingRentals =
                await _context.Rentals.CountAsync(
                    rental => rental.Status == "Borrowing");

            ViewBag.ReturnedRentals =
                await _context.Rentals.CountAsync(
                    rental => rental.Status == "Returned");

            ViewBag.CancelledRentals =
                await _context.Rentals.CountAsync(
                    rental => rental.Status == "Cancelled");

            ViewBag.PendingPayments =
                await _context.Payments.CountAsync(
                    payment =>
                        payment.Status == "Pending" ||
                        payment.Status == "AwaitingConfirmation");

            ViewBag.PaidPayments =
                await _context.Payments.CountAsync(
                    payment =>
                        payment.Status == "Paid" ||
                        payment.Status == "Completed");

            ViewBag.RejectedPayments =
                await _context.Payments.CountAsync(
                    payment => payment.Status == "Rejected");

            ViewBag.TotalRevenue =
                await _context.Rentals
                    .Where(rental =>
                        rental.Payment != null &&
                        (rental.Payment.Status == "Paid" ||
                         rental.Payment.Status == "Completed"))
                    .SumAsync(rental => (decimal?)rental.TotalAmount)
                ?? 0;

            return View("~/Views/Staff/Admin.cshtml");
        }

        // =========================================================
        // URL: /Staff/Admin
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Admin()
        {
            return await Index();
        }

        // =========================================================
        // DANH SÁCH SÁCH
        // URL: /Staff/Books
        // =========================================================
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

        // =========================================================
        // DANH SÁCH ĐƠN THUÊ
        // URL: /Staff/Rentals
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Rentals(string? status)
        {
            var accessResult = CheckStaffAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            var query = _context.Rentals
                .AsNoTracking()
                .Include(rental => rental.User)
                .Include(rental => rental.RentalDetails)
                    .ThenInclude(detail => detail.Book)
                .Include(rental => rental.Payment)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(
                    rental => rental.Status == status);
            }

            var rentals = await query
                .OrderByDescending(rental => rental.Id)
                .ToListAsync();

            ViewBag.SelectedStatus = status;

            return View(rentals);
        }

        // =========================================================
        // CHI TIẾT ĐƠN THUÊ
        // URL: /Staff/RentalDetails/5
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> RentalDetails(int id)
        {
            var accessResult = CheckStaffAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            var rental = await _context.Rentals
                .AsNoTracking()
                .Include(item => item.User)
                .Include(item => item.Payment)
                .Include(item => item.RentalDetails)
                    .ThenInclude(detail => detail.Book)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (rental == null)
            {
                TempData["ErrorMessage"] =
                    "Không tìm thấy đơn thuê.";

                return RedirectToAction(nameof(Rentals));
            }

            return View(rental);
        }

        // =========================================================
        // DUYỆT ĐƠN THUÊ
        // Pending -> Approved
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRental(int rentalId)
        {
            var accessResult = CheckStaffAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            var rental = await _context.Rentals
                .Include(item => item.Payment)
                .FirstOrDefaultAsync(
                    item => item.Id == rentalId);

            if (rental == null)
            {
                TempData["ErrorMessage"] =
                    "Không tìm thấy đơn thuê.";

                return RedirectToAction(nameof(Rentals));
            }

            if (rental.Status != "Pending")
            {
                TempData["ErrorMessage"] =
                    "Chỉ có thể duyệt đơn đang chờ xử lý.";

                return RedirectToAction(nameof(Rentals));
            }

            if (rental.Payment == null ||
                (rental.Payment.Status != "Paid" &&
                 rental.Payment.Status != "Completed"))
            {
                TempData["ErrorMessage"] =
                    "Chỉ có thể duyệt đơn sau khi thanh toán đã được xác nhận.";

                return RedirectToAction(nameof(Rentals));
            }

            rental.Status = "Approved";

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Đã duyệt đơn thuê #{rental.Id}.";

            return RedirectToAction(nameof(Rentals));
        }

        // =========================================================
        // XÁC NHẬN GIAO SÁCH
        // Approved -> Borrowing
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmDelivery(int rentalId)
        {
            var accessResult = CheckStaffAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            var rental = await _context.Rentals
                .Include(item => item.Payment)
                .FirstOrDefaultAsync(
                    item => item.Id == rentalId);

            if (rental == null)
            {
                TempData["ErrorMessage"] =
                    "Không tìm thấy đơn thuê.";

                return RedirectToAction(nameof(Rentals));
            }

            if (rental.Status != "Approved")
            {
                TempData["ErrorMessage"] =
                    "Chỉ có thể giao sách cho đơn đã được duyệt.";

                return RedirectToAction(nameof(Rentals));
            }

            if (rental.Payment == null ||
                (rental.Payment.Status != "Paid" &&
                 rental.Payment.Status != "Completed"))
            {
                TempData["ErrorMessage"] =
                    "Đơn thuê chưa được xác nhận thanh toán.";

                return RedirectToAction(nameof(Rentals));
            }

            rental.Status = "Borrowing";

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Đã xác nhận giao sách cho đơn #{rental.Id}.";

            return RedirectToAction(nameof(Rentals));
        }

        // =========================================================
        // XÁC NHẬN TRẢ SÁCH
        // Borrowing -> Returned
        // Hoàn lại số lượng sách vào kho
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

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var rental = await _context.Rentals
                    .Include(item => item.RentalDetails)
                        .ThenInclude(detail => detail.Book)
                    .FirstOrDefaultAsync(
                        item => item.Id == rentalId);

                if (rental == null)
                {
                    TempData["ErrorMessage"] =
                        "Không tìm thấy đơn thuê.";

                    return RedirectToAction(nameof(Rentals));
                }

                if (rental.Status != "Borrowing")
                {
                    TempData["ErrorMessage"] =
                        "Chỉ có thể xác nhận trả đối với đơn đang thuê.";

                    return RedirectToAction(nameof(Rentals));
                }

                foreach (var detail in rental.RentalDetails)
                {
                    if (detail.Book == null)
                    {
                        continue;
                    }

                    detail.Book.Quantity += detail.Quantity;

                    if (detail.Book.Quantity > 0)
                    {
                        detail.Book.Status = "Available";
                    }
                }

                rental.Status = "Returned";

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    $"Đã xác nhận trả sách cho đơn #{rental.Id}.";

                return RedirectToAction(nameof(Rentals));
            }
            catch
            {
                await transaction.RollbackAsync();

                TempData["ErrorMessage"] =
                    "Có lỗi xảy ra khi xác nhận trả sách.";

                return RedirectToAction(nameof(Rentals));
            }
        }

        // =========================================================
        // HỦY ĐƠN THUÊ
        // Pending / Approved -> Cancelled
        // Hoàn lại số lượng sách
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelRental(int rentalId)
        {
            var accessResult = CheckStaffAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var rental = await _context.Rentals
                    .Include(item => item.RentalDetails)
                        .ThenInclude(detail => detail.Book)
                    .Include(item => item.Payment)
                    .FirstOrDefaultAsync(
                        item => item.Id == rentalId);

                if (rental == null)
                {
                    TempData["ErrorMessage"] =
                        "Không tìm thấy đơn thuê.";

                    return RedirectToAction(nameof(Rentals));
                }

                if (rental.Status != "Pending" &&
                    rental.Status != "Approved")
                {
                    TempData["ErrorMessage"] =
                        "Chỉ có thể hủy đơn đang chờ duyệt hoặc đã duyệt.";

                    return RedirectToAction(nameof(Rentals));
                }

                foreach (var detail in rental.RentalDetails)
                {
                    if (detail.Book == null)
                    {
                        continue;
                    }

                    detail.Book.Quantity += detail.Quantity;

                    if (detail.Book.Quantity > 0)
                    {
                        detail.Book.Status = "Available";
                    }
                }

                rental.Status = "Cancelled";

                if (rental.Payment != null &&
                    rental.Payment.Status != "Paid" &&
                    rental.Payment.Status != "Completed")
                {
                    rental.Payment.Status = "Cancelled";
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    $"Đã hủy đơn thuê #{rental.Id} và hoàn lại số lượng sách.";

                return RedirectToAction(nameof(Rentals));
            }
            catch
            {
                await transaction.RollbackAsync();

                TempData["ErrorMessage"] =
                    "Có lỗi xảy ra khi hủy đơn thuê.";

                return RedirectToAction(nameof(Rentals));
            }
        }

        // =========================================================
        // DANH SÁCH THANH TOÁN
        // URL: /Staff/Payments
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Payments(string? status)
        {
            var accessResult = CheckStaffAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            var query = _context.Payments
                .AsNoTracking()
                .Include(payment => payment.Rental!)
                    .ThenInclude(rental => rental.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(
                    payment => payment.Status == status);
            }

            var payments = await query
                .OrderByDescending(payment => payment.Id)
                .ToListAsync();

            ViewBag.SelectedStatus = status;

            return View(payments);
        }

        // =========================================================
        // XÁC NHẬN THANH TOÁN
        // AwaitingConfirmation -> Paid
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApprovePayment(int paymentId)
        {
            var accessResult = CheckStaffAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            var payment = await _context.Payments
                .Include(item => item.Rental)
                .FirstOrDefaultAsync(
                    item => item.Id == paymentId);

            if (payment == null)
            {
                TempData["ErrorMessage"] =
                    "Không tìm thấy giao dịch thanh toán.";

                return RedirectToAction(nameof(Payments));
            }

            if (payment.Status == "Paid" ||
                payment.Status == "Completed")
            {
                TempData["ErrorMessage"] =
                    "Giao dịch này đã được xác nhận trước đó.";

                return RedirectToAction(nameof(Payments));
            }

            if (payment.Status != "AwaitingConfirmation")
            {
                TempData["ErrorMessage"] =
                    "Chỉ có thể xác nhận giao dịch đang chờ kiểm tra.";

                return RedirectToAction(nameof(Payments));
            }

            payment.Status = "Paid";

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Đã xác nhận thanh toán #{payment.Id} cho đơn thuê #{payment.RentalId}.";

            return RedirectToAction(nameof(Payments));
        }

        // =========================================================
        // TỪ CHỐI THANH TOÁN
        // AwaitingConfirmation -> Rejected
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectPayment(int paymentId)
        {
            var accessResult = CheckStaffAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            var payment = await _context.Payments
                .FirstOrDefaultAsync(
                    item => item.Id == paymentId);

            if (payment == null)
            {
                TempData["ErrorMessage"] =
                    "Không tìm thấy giao dịch thanh toán.";

                return RedirectToAction(nameof(Payments));
            }

            if (payment.Status != "AwaitingConfirmation")
            {
                TempData["ErrorMessage"] =
                    "Chỉ có thể từ chối giao dịch đang chờ kiểm tra.";

                return RedirectToAction(nameof(Payments));
            }

            payment.Status = "Rejected";

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Đã từ chối thanh toán #{payment.Id}.";

            return RedirectToAction(nameof(Payments));
        }

        // =========================================================
        // ĐƯA GIAO DỊCH VỀ TRẠNG THÁI CHỜ THANH TOÁN
        // Rejected / Cancelled -> Pending
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RetryPayment(int paymentId)
        {
            var accessResult = CheckStaffAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            var payment = await _context.Payments
                .Include(item => item.Rental)
                .FirstOrDefaultAsync(
                    item => item.Id == paymentId);

            if (payment == null)
            {
                TempData["ErrorMessage"] =
                    "Không tìm thấy giao dịch thanh toán.";

                return RedirectToAction(nameof(Payments));
            }

            if (payment.Status != "Rejected" &&
                payment.Status != "Cancelled")
            {
                TempData["ErrorMessage"] =
                    "Chỉ có thể xử lý lại giao dịch đã bị từ chối hoặc đã hủy.";

                return RedirectToAction(nameof(Payments));
            }

            if (payment.Rental != null &&
                payment.Rental.Status == "Cancelled")
            {
                TempData["ErrorMessage"] =
                    "Không thể xử lý lại thanh toán vì đơn thuê đã bị hủy.";

                return RedirectToAction(nameof(Payments));
            }

            payment.Status = "Pending";
            payment.PaymentMethod = "";

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Đã mở lại thanh toán #{payment.Id}. Khách hàng có thể thanh toán lại.";

            return RedirectToAction(nameof(Payments));
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
                    "Account");
            }

            if (userRole != "Staff" &&
                userRole != "Admin")
            {
                TempData["ErrorMessage"] =
                    "Bạn không có quyền truy cập chức năng này.";

                return RedirectToAction(
                    "AccessDenied",
                    "Home");
            }

            return null;
        }
    }
}