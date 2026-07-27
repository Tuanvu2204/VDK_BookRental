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

        // Danh sách tất cả sách
        public IActionResult Index()
        {
            var books = _context.Books
                .Include(b => b.Category)
                .ToList();

            return View(books);
        }

        // Chi tiết sách
        public IActionResult Details(int id)
        {
            var book = _context.Books
                .Include(b => b.Category)
                .FirstOrDefault(b => b.Id == id);

            if (book == null)
            {
                return NotFound();
            }

            return View(book);
        }

        // Sách nổi bật - được thuê nhiều nhất
        public IActionResult Featured()
        {
            // Đếm số lượt thuê của từng quyển sách
            var rentalStatistics = _context.RentalDetails
                .AsNoTracking()
                .GroupBy(rd => rd.BookId)
                .Select(group => new
                {
                    BookId = group.Key,
                    RentalCount = group.Sum(rd => rd.Quantity)
                })
                .OrderByDescending(item => item.RentalCount)
                .Take(8)
                .ToList();

            // Nếu chưa có dữ liệu thuê thì lấy 8 sách đầu tiên
            if (!rentalStatistics.Any())
            {
                var defaultBooks = _context.Books
                    .AsNoTracking()
                    .Include(b => b.Category)
                    .Take(8)
                    .ToList();

                ViewBag.RentalCounts = new Dictionary<int, int>();

                return View(defaultBooks);
            }

            var bookIds = rentalStatistics
                .Select(item => item.BookId)
                .ToList();

            var books = _context.Books
                .AsNoTracking()
                .Include(b => b.Category)
                .Where(b => bookIds.Contains(b.Id))
                .ToList();

            // Sắp xếp sách theo số lượt thuê giảm dần
            books = books
                .OrderBy(b => bookIds.IndexOf(b.Id))
                .ToList();

            // Gửi số lượt thuê sang View
            ViewBag.RentalCounts = rentalStatistics
                .ToDictionary(
                    item => item.BookId,
                    item => item.RentalCount
                );

            return View(books);
        }

        // Tạo đơn thuê sách
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Rent(int bookId, int rentalDays)
        {
            if (rentalDays <= 0)
            {
                TempData["ErrorMessage"] =
                    "Số ngày thuê phải lớn hơn 0.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = bookId }
                );
            }

            var book = _context.Books
                .FirstOrDefault(b => b.Id == bookId);

            if (book == null)
            {
                return NotFound();
            }

            if (book.Quantity <= 0)
            {
                TempData["ErrorMessage"] =
                    "Sách này hiện đã hết.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = bookId }
                );
            }

            var userIdText =
                HttpContext.Session.GetString("UserId");

            if (string.IsNullOrWhiteSpace(userIdText) ||
                !int.TryParse(userIdText, out int userId))
            {
                TempData["ErrorMessage"] =
                    "Vui lòng đăng nhập trước khi thuê sách.";

                return RedirectToAction("Login", "Account");
            }

            var totalAmount =
                book.RentalPrice * rentalDays;

            var rental = new Rental
            {
                UserId = userId,
                RentalDate = DateTime.Now,
                ReturnDate = DateTime.Now.AddDays(rentalDays),
                TotalAmount = totalAmount,
                Status = "Pending"
            };

            _context.Rentals.Add(rental);
            _context.SaveChanges();

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

            var payment = new Payment
            {
                RentalId = rental.Id,
                Amount = totalAmount,
                PaymentMethod = "QR",
                QrCodeUrl = "/images/books/payment-qr.jpg",
                TransferContent = $"RENTAL_{rental.Id}",
                Status = "Pending",
                CreatedAt = DateTime.Now
            };

            _context.Payments.Add(payment);

            book.Quantity -= 1;

            _context.SaveChanges();

            return RedirectToAction(
                "Checkout",
                "Payment",
                new { rentalId = rental.Id }
            );
        }
    }
}