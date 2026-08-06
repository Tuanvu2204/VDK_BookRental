using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VDK_BookRental.Data;
using VDK_BookRental.Models;

namespace VDK_BookRental.Controllers
{
    public class BooksController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<BooksController> _logger;
        private readonly IWebHostEnvironment _environment;

        public BooksController(
            AppDbContext context,
            ILogger<BooksController> logger,
            IWebHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _environment = environment;
        }

        // =====================================================
        // DANH SÁCH TẤT CẢ SÁCH
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var books = await _context.Books
                    .AsNoTracking()
                    .Include(book => book.Category)
                    .OrderBy(book => book.Title)
                    .ToListAsync();

                return View(books);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Không thể tải danh sách sách.");

                TempData["ErrorMessage"] =
                    "Không thể tải danh sách sách.";

                return View(new List<Book>());
            }
        }

        // =====================================================
        // CHI TIẾT SÁCH
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (id <= 0)
            {
                return NotFound();
            }

            try
            {
                var book = await _context.Books
                    .AsNoTracking()
                    .Include(item => item.Category)
                    .FirstOrDefaultAsync(item => item.Id == id);

                if (book == null)
                {
                    return NotFound();
                }

                return View(book);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Không thể tải chi tiết BookId {BookId}.",
                    id);

                TempData["ErrorMessage"] =
                    "Không thể tải thông tin sách.";

                return RedirectToAction(nameof(Index));
            }
        }

        // =====================================================
        // SÁCH NỔI BẬT
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Featured()
        {
            try
            {
                var rentalStatistics =
                    await _context.RentalDetails
                        .AsNoTracking()
                        .GroupBy(detail => detail.BookId)
                        .Select(group => new
                        {
                            BookId = group.Key,
                            RentalCount =
                                group.Sum(detail => detail.Quantity)
                        })
                        .OrderByDescending(item => item.RentalCount)
                        .Take(8)
                        .ToListAsync();

                if (rentalStatistics.Count == 0)
                {
                    var defaultBooks = await _context.Books
                        .AsNoTracking()
                        .Include(book => book.Category)
                        .OrderBy(book => book.Title)
                        .Take(8)
                        .ToListAsync();

                    ViewBag.RentalCounts =
                        new Dictionary<int, int>();

                    return View(defaultBooks);
                }

                var bookIds = rentalStatistics
                    .Select(item => item.BookId)
                    .ToList();

                var books = await _context.Books
                    .AsNoTracking()
                    .Include(book => book.Category)
                    .Where(book => bookIds.Contains(book.Id))
                    .ToListAsync();

                books = books
                    .OrderBy(book => bookIds.IndexOf(book.Id))
                    .ToList();

                ViewBag.RentalCounts =
                    rentalStatistics.ToDictionary(
                        item => item.BookId,
                        item => item.RentalCount
                    );

                return View(books);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Không thể tải danh sách sách nổi bật.");

                TempData["ErrorMessage"] =
                    "Không thể tải sách nổi bật.";

                return View(new List<Book>());
            }
        }

        // =====================================================
        // TẠO ĐƠN THUÊ SÁCH
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rent(
            int bookId,
            int rentalDays)
        {
            // -------------------------------------------------
            // 1. KIỂM TRA DỮ LIỆU
            // -------------------------------------------------

            if (bookId <= 0)
            {
                TempData["ErrorMessage"] =
                    "Mã sách không hợp lệ.";

                return RedirectToAction(nameof(Index));
            }

            if (rentalDays < 1 || rentalDays > 30)
            {
                TempData["ErrorMessage"] =
                    "Số ngày thuê phải từ 1 đến 30.";

                return RedirectToAction(
                    nameof(Details),
                    new
                    {
                        id = bookId
                    }
                );
            }

            // -------------------------------------------------
            // 2. KIỂM TRA ĐĂNG NHẬP
            // -------------------------------------------------

            var userIdText =
                HttpContext.Session.GetString("UserId");

            if (string.IsNullOrWhiteSpace(userIdText) ||
                !int.TryParse(userIdText, out var userId))
            {
                TempData["ErrorMessage"] =
                    "Vui lòng đăng nhập trước khi thuê sách.";

                return RedirectToAction(
                    "Login",
                    "Account",
                    new
                    {
                        returnUrl =
                            Url.Action(
                                nameof(Details),
                                "Books",
                                new
                                {
                                    id = bookId
                                }
                            )
                    }
                );
            }

            // -------------------------------------------------
            // 3. KIỂM TRA TÀI KHOẢN
            // -------------------------------------------------

            User? user;

            try
            {
                user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == userId);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Không thể kiểm tra UserId {UserId}.",
                    userId);

                TempData["ErrorMessage"] =
                    "Không thể kiểm tra thông tin tài khoản.";

                return RedirectToAction(
                    nameof(Details),
                    new
                    {
                        id = bookId
                    }
                );
            }

            if (user == null)
            {
                HttpContext.Session.Clear();

                TempData["ErrorMessage"] =
                    "Tài khoản không tồn tại. " +
                    "Vui lòng đăng nhập lại.";

                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }

            if (user.IsLocked)
            {
                HttpContext.Session.Clear();

                TempData["ErrorMessage"] =
                    "Tài khoản của bạn đang bị khóa.";

                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }

            // -------------------------------------------------
            // 4. EXECUTION STRATEGY CHO SQL SERVER RETRY
            // -------------------------------------------------

            var executionStrategy =
                _context.Database.CreateExecutionStrategy();

            try
            {
                return await executionStrategy
                    .ExecuteAsync<IActionResult>(
                        async () =>
                        {
                            /*
                             * Xóa entity còn được track từ lần chạy trước
                             * khi SQL Server tự retry operation.
                             */
                            _context.ChangeTracker.Clear();

                            await using var transaction =
                                await _context.Database
                                    .BeginTransactionAsync();

                            try
                            {
                                // ---------------------------------
                                // 5. LẤY VÀ KIỂM TRA SÁCH
                                // ---------------------------------

                                var book = await _context.Books
                                    .FirstOrDefaultAsync(item =>
                                        item.Id == bookId);

                                if (book == null)
                                {
                                    await RollbackSafelyAsync(
                                        transaction
                                    );

                                    TempData["ErrorMessage"] =
                                        "Không tìm thấy sách cần thuê.";

                                    return RedirectToAction(
                                        nameof(Index)
                                    );
                                }

                                if (book.Quantity <= 0)
                                {
                                    await RollbackSafelyAsync(
                                        transaction
                                    );

                                    TempData["ErrorMessage"] =
                                        "Sách này hiện đã hết.";

                                    return RedirectToAction(
                                        nameof(Details),
                                        new
                                        {
                                            id = bookId
                                        }
                                    );
                                }

                                if (!string.Equals(
                                        book.Status,
                                        "Available",
                                        StringComparison.OrdinalIgnoreCase))
                                {
                                    await RollbackSafelyAsync(
                                        transaction
                                    );

                                    TempData["ErrorMessage"] =
                                        "Sách này hiện không thể thuê.";

                                    return RedirectToAction(
                                        nameof(Details),
                                        new
                                        {
                                            id = bookId
                                        }
                                    );
                                }

                                if (book.RentalPrice < 0)
                                {
                                    await RollbackSafelyAsync(
                                        transaction
                                    );

                                    TempData["ErrorMessage"] =
                                        "Giá thuê của sách không hợp lệ.";

                                    return RedirectToAction(
                                        nameof(Details),
                                        new
                                        {
                                            id = bookId
                                        }
                                    );
                                }

                                // ---------------------------------
                                // 6. TÍNH NGÀY VÀ TỔNG TIỀN
                                // ---------------------------------

                                var rentalDate = DateTime.Now;

                                var returnDate =
                                    rentalDate.AddDays(rentalDays);

                                var totalAmount =
                                    book.RentalPrice * rentalDays;

                                // ---------------------------------
                                // 7. TẠO ĐƠN THUÊ
                                // ---------------------------------

                                var rental = new Rental
                                {
                                    UserId = userId,
                                    RentalDate = rentalDate,
                                    ReturnDate = returnDate,
                                    TotalAmount = totalAmount,
                                    Status = "Pending"
                                };

                                _context.Rentals.Add(rental);

                                /*
                                 * Lưu lần đầu để SQL Server
                                 * tạo giá trị Rental.Id.
                                 */
                                await _context.SaveChangesAsync();

                                // ---------------------------------
                                // 8. TẠO CHI TIẾT ĐƠN THUÊ
                                // ---------------------------------

                                var rentalDetail =
                                    new RentalDetail
                                    {
                                        RentalId = rental.Id,
                                        BookId = book.Id,
                                        Quantity = 1,
                                        Price = book.RentalPrice,
                                        RentalDays = rentalDays,
                                        SubTotal = totalAmount
                                    };

                                _context.RentalDetails.Add(
                                    rentalDetail
                                );

                                // ---------------------------------
                                // 9. TẠO GIAO DỊCH THANH TOÁN
                                // ---------------------------------

                                var payment = new Payment
                                {
                                    RentalId = rental.Id,
                                    Amount = totalAmount,
                                    PaymentMethod = "QR",
                                    QrCodeUrl =
                                        "/images/books/payment-qr.jpg",
                                    TransferContent =
                                        $"RENTAL_{rental.Id}",
                                    Status = "Pending",
                                    CreatedAt = DateTime.Now
                                };

                                _context.Payments.Add(payment);

                                // ---------------------------------
                                // 10. TRỪ SỐ LƯỢNG SÁCH
                                // ---------------------------------

                                book.Quantity -= 1;

                                if (book.Quantity <= 0)
                                {
                                    book.Quantity = 0;
                                    book.Status = "Unavailable";
                                }
                                else
                                {
                                    book.Status = "Available";
                                }

                                // ---------------------------------
                                // 11. LƯU VÀ COMMIT
                                // ---------------------------------

                                await _context.SaveChangesAsync();

                                await transaction.CommitAsync();

                                _logger.LogInformation(
                                    "Đã tạo RentalId {RentalId} " +
                                    "cho UserId {UserId}, " +
                                    "BookId {BookId}, " +
                                    "RentalDays {RentalDays}, " +
                                    "TotalAmount {TotalAmount}.",
                                    rental.Id,
                                    userId,
                                    bookId,
                                    rentalDays,
                                    totalAmount);

                                TempData["SuccessMessage"] =
                                    $"Đã tạo đơn thuê " +
                                    $"#RENT-{rental.Id:D4}. " +
                                    "Vui lòng hoàn tất thanh toán.";

                                return RedirectToAction(
                                    "Checkout",
                                    "Payment",
                                    new
                                    {
                                        rentalId = rental.Id
                                    }
                                );
                            }
                            catch
                            {
                                await RollbackSafelyAsync(
                                    transaction
                                );

                                throw;
                            }
                        }
                    );
            }
            catch (DbUpdateConcurrencyException exception)
            {
                _context.ChangeTracker.Clear();

                _logger.LogWarning(
                    exception,
                    "Xung đột dữ liệu khi UserId {UserId} " +
                    "thuê BookId {BookId}.",
                    userId,
                    bookId);

                TempData["ErrorMessage"] =
                    "Dữ liệu sách vừa được thay đổi. " +
                    "Vui lòng tải lại trang và thử lại.";

                return RedirectToAction(
                    nameof(Details),
                    new
                    {
                        id = bookId
                    }
                );
            }
            catch (DbUpdateException exception)
            {
                _context.ChangeTracker.Clear();

                _logger.LogError(
                    exception,
                    "Lỗi database khi UserId {UserId} " +
                    "thuê BookId {BookId}.",
                    userId,
                    bookId);

                TempData["ErrorMessage"] =
                    GetDatabaseErrorMessage(exception);

                return RedirectToAction(
                    nameof(Details),
                    new
                    {
                        id = bookId
                    }
                );
            }
            catch (Exception exception)
            {
                _context.ChangeTracker.Clear();

                _logger.LogError(
                    exception,
                    "Lỗi khi UserId {UserId} " +
                    "thuê BookId {BookId}.",
                    userId,
                    bookId);

                TempData["ErrorMessage"] =
                    GetGeneralErrorMessage(exception);

                return RedirectToAction(
                    nameof(Details),
                    new
                    {
                        id = bookId
                    }
                );
            }
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
                    "Không thể rollback transaction tạo đơn thuê.");
            }
        }

        // =====================================================
        // THÔNG BÁO LỖI DATABASE
        // =====================================================

        private string GetDatabaseErrorMessage(
            DbUpdateException exception)
        {
            if (_environment.IsDevelopment())
            {
                return
                    "Lỗi database khi tạo đơn thuê: " +
                    exception.GetBaseException().Message;
            }

            return
                "Không thể tạo đơn thuê do lỗi dữ liệu. " +
                "Vui lòng thử lại sau.";
        }

        // =====================================================
        // THÔNG BÁO LỖI CHUNG
        // =====================================================

        private string GetGeneralErrorMessage(
            Exception exception)
        {
            if (_environment.IsDevelopment())
            {
                return
                    "Lỗi tạo đơn thuê: " +
                    exception.GetBaseException().Message;
            }

            return
                "Đã xảy ra lỗi khi tạo đơn thuê. " +
                "Vui lòng thử lại sau.";
        }
    }
}