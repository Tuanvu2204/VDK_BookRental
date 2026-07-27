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

        public IActionResult Checkout(int rentalId)
        {
            var rental = _context.Rentals
                .Include(r => r.User)
                .Include(r => r.RentalDetails)
                    .ThenInclude(rd => rd.Book)
                .Include(r => r.Payment)
                .FirstOrDefault(r => r.Id == rentalId);

            if (rental == null)
            {
                return NotFound();
            }

            return View(rental);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Confirm(
            int rentalId,
            string paymentMethod)
        {
            var payment = _context.Payments
                .FirstOrDefault(p => p.RentalId == rentalId);

            if (payment == null)
            {
                return NotFound();
            }

            var allowedMethods = new[]
            {
                "MB Bank",
                "Ví MoMo"
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

            payment.PaymentMethod = paymentMethod;

            /*
             * Đây mới là người dùng tự xác nhận đã chuyển tiền.
             * Chuẩn thực tế nên để chờ quản trị viên kiểm tra.
             */
            payment.Status = "AwaitingConfirmation";

            _context.SaveChanges();

            return RedirectToAction(nameof(Success));
        }

        public IActionResult Success()
        {
            return View();
        }
    }
}