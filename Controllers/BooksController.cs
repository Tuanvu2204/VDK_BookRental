using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VDK_BookRental.Data;
using VDK_BookRental.Models;

namespace VDK_BookRental.Controllers
{
    public class BooksController : Controller
    {
        private readonly AppDbContext _context;

        public BooksController(AppDbContext context)
        {
            _context = context;
        }

        // =====================================================
        // DANH SÁCH TẤT CẢ SÁCH
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var books = await _context.Books
                .AsNoTracking()
                .Include(book => book.Category)
                .OrderBy(book => book.Title)
                .ToListAsync();

            return View(books);
        }

        // =====================================================
        // CHI TIẾT SÁCH
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Details(int id)
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

        // =====================================================
        // SÁCH NỔI BẬT
        // LẤY SÁCH ĐƯỢC THUÊ NHIỀU NHẤT
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Featured()
        {
            var rentalStatistics = await _context.RentalDetails
                .AsNoTracking()
                .GroupBy(detail => detail.BookId)
                .Select(group => new
                {
                    BookId = group.Key,
                    RentalCount = group.Sum(detail => detail.Quantity)
                })
                .OrderByDescending(item => item.RentalCount)
                .Take(8)
                .ToListAsync();

            if (!rentalStatistics.Any())
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

            ViewBag.RentalCounts = rentalStatistics
                .ToDictionary(
                    item => item.BookId,
                    item => item.RentalCount
                );

            return View(books);
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
            // 1. KIỂM TRA NGÀY THUÊ
            // -------------------------------------------------

            if (rentalDays <= 0)
            {
                TempData["ErrorMessage"] =
                    "Số ngày thuê phải lớn hơn 0.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = bookId }
                );
            }

            if (rentalDays > 30)
            {
                TempData["ErrorMessage"] =
                    "Thời gian thuê tối đa là 30 ngày.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = bookId }
                );
            }

            // -------------------------------------------------
            // 2. KIỂM TRA ĐĂNG NHẬP
            // -------------------------------------------------

            var userIdText =
                HttpContext.Session.GetString("UserId");

            if (string.IsNullOrWhiteSpace(userIdText) ||
                !int.TryParse(userIdText, out int userId))
            {
                TempData["ErrorMessage"] =
                    "Vui lòng đăng nhập trước khi thuê sách.";

                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }

            // -------------------------------------------------
            // 3. KIỂM TRA TÀI KHOẢN
            // -------------------------------------------------

            var user = await _context.Users
                .FirstOrDefaultAsync(item => item.Id == userId);

            if (user == null)
            {
                HttpContext.Session.Clear();

                TempData["ErrorMessage"] =
                    "Tài khoản không tồn tại. Vui lòng đăng nhập lại.";

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
            // 4. BẮT ĐẦU TRANSACTION
            // -------------------------------------------------

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                // ---------------------------------------------
                // 5. LẤY SÁCH TRONG TRANSACTION
                // ---------------------------------------------

                var book = await _context.Books
                    .FirstOrDefaultAsync(item => item.Id == bookId);

                if (book == null)
                {
                    await transaction.RollbackAsync();

                    return NotFound();
                }

                if (book.Quantity <= 0)
                {
                    await transaction.RollbackAsync();

                    TempData["ErrorMessage"] =
                        "Sách này hiện đã hết.";

                    return RedirectToAction(
                        nameof(Details),
                        new { id = bookId }
                    );
                }

                if (book.Status == "Unavailable")
                {
                    await transaction.RollbackAsync();

                    TempData["ErrorMessage"] =
                        "Sách này hiện không thể thuê.";

                    return RedirectToAction(
                        nameof(Details),
                        new { id = bookId }
                    );
                }

                // ---------------------------------------------
                // 6. TÍNH TIỀN THUÊ
                // ---------------------------------------------

                var rentalDate = DateTime.Now;

                var returnDate =
                    rentalDate.AddDays(rentalDays);

                var totalAmount =
                    book.RentalPrice * rentalDays;

                // ---------------------------------------------
                // 7. TẠO ĐƠN THUÊ
                // ---------------------------------------------

                var rental = new Rental
                {
                    UserId = userId,
                    RentalDate = rentalDate,
                    ReturnDate = returnDate,
                    TotalAmount = totalAmount,
                    Status = "Pending"
                };

                _context.Rentals.Add(rental);

                // Lưu lần đầu để EF tạo Rental.Id
                await _context.SaveChangesAsync();

                // ---------------------------------------------
                // 8. TẠO CHI TIẾT ĐƠN THUÊ
                // ---------------------------------------------

                var rentalDetail = new RentalDetail
                {
                    RentalId = rental.Id,
                    BookId = book.Id,
                    Quantity = 1,
                    Price = book.RentalPrice,
                    RentalDays = rentalDays,
                    SubTotal = totalAmount
                };

                _context.RentalDetails.Add(rentalDetail);

                // ---------------------------------------------
                // 9. TẠO GIAO DỊCH THANH TOÁN
                // ---------------------------------------------

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

                // ---------------------------------------------
                // 10. TRỪ TỒN KHO
                // ---------------------------------------------

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

                // ---------------------------------------------
                // 11. LƯU TOÀN BỘ THAY ĐỔI
                // ---------------------------------------------

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    $"Đã tạo đơn thuê #{rental.Id}. Vui lòng hoàn tất thanh toán.";

                return RedirectToAction(
                    "Checkout",
                    "Payment",
                    new { rentalId = rental.Id }
                );
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();

                TempData["ErrorMessage"] =
                    "Không thể tạo đơn thuê do lỗi dữ liệu. Vui lòng thử lại.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = bookId }
                );
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();

                TempData["ErrorMessage"] =
                    "Đã xảy ra lỗi khi tạo đơn thuê. Vui lòng thử lại sau.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = bookId }
                );
            }
        }
    }
}