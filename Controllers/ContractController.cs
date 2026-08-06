using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VDK_BookRental.Data;
using VDK_BookRental.ViewModels;

namespace VDK_BookRental.Controllers
{
    public class ContractController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ContractController> _logger;

        public ContractController(
            AppDbContext context,
            ILogger<ContractController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // =========================================================
        // XEM HỢP ĐỒNG THUÊ SÁCH
        //
        // URL:
        // /Contract/Details?rentalId=1028
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Details(
            int rentalId)
        {
            // -----------------------------------------------------
            // 1. KIỂM TRA ĐĂNG NHẬP
            // -----------------------------------------------------

            var userIdText =
                HttpContext.Session.GetString("UserId");

            if (string.IsNullOrWhiteSpace(userIdText) ||
                !int.TryParse(userIdText, out var userId))
            {
                TempData["ErrorMessage"] =
                    "Vui lòng đăng nhập để xem hợp đồng thuê sách.";

                return RedirectToAction(
                    "Login",
                    "Account",
                    new
                    {
                        returnUrl = Url.Action(
                            nameof(Details),
                            "Contract",
                            new
                            {
                                rentalId
                            }
                        )
                    }
                );
            }

            // -----------------------------------------------------
            // 2. KIỂM TRA MÃ ĐƠN THUÊ
            // -----------------------------------------------------

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
                // -------------------------------------------------
                // 3. LẤY ĐẦY ĐỦ DỮ LIỆU HỢP ĐỒNG
                // -------------------------------------------------

                var rental = await _context.Rentals
                    .AsNoTracking()
                    .Include(item => item.User)
                    .Include(item => item.Payment)
                    .Include(item => item.RentalDetails)
                        .ThenInclude(detail => detail.Book)
                    .FirstOrDefaultAsync(item =>
                        item.Id == rentalId &&
                        item.UserId == userId
                    );

                if (rental == null)
                {
                    TempData["ErrorMessage"] =
                        "Không tìm thấy hợp đồng hoặc " +
                        "bạn không có quyền truy cập hợp đồng này.";

                    return RedirectToAction(
                        "Current",
                        "Rental"
                    );
                }

                // -------------------------------------------------
                // 4. XÁC ĐỊNH TÊN KHÁCH HÀNG
                //
                // Model User của dự án dùng UserName,
                // không dùng Username.
                // -------------------------------------------------

                var customerName =
                    !string.IsNullOrWhiteSpace(
                        rental.User?.FullName)
                        ? rental.User.FullName
                        : rental.User?.UserName
                          ?? "Khách hàng";

                // -------------------------------------------------
                // 5. TẠO VIEWMODEL
                // -------------------------------------------------

                var viewModel =
                    new RentalContractViewModel
                    {
                        Rental = rental,

                        ContractNumber =
                            $"HĐTS-VDK-{rental.Id:D6}",

                        CreatedAt =
                            DateTime.Now,

                        CustomerName =
                            customerName,

                        CustomerPhone =
                            rental.User?.Phone
                            ?? string.Empty,

                        CustomerEmail =
                            rental.User?.Email
                            ?? string.Empty,

                        /*
                         * Model User hiện tại chưa xác nhận có
                         * thuộc tính Address nên để trống nhằm
                         * tránh lỗi biên dịch.
                         */
                        CustomerAddress =
                            string.Empty,

                        PaymentMethod =
                            !string.IsNullOrWhiteSpace(
                                rental.Payment?.PaymentMethod)
                                ? rental.Payment.PaymentMethod
                                : "Chưa xác định"
                    };

                // Dùng đường dẫn View rõ ràng để tránh đặt nhầm thư mục.
                return View(
                    "~/Views/Contract/Details.cshtml",
                    viewModel
                );
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Không thể tải hợp đồng RentalId {RentalId} " +
                    "cho UserId {UserId}.",
                    rentalId,
                    userId);

                TempData["ErrorMessage"] =
                    "Không thể tạo hợp đồng thuê sách. " +
                    "Vui lòng thử lại.";

                return RedirectToAction(
                    "Current",
                    "Rental"
                );
            }
        }
    }
}