using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VDK_BookRental.Data;
using VDK_BookRental.Models;
using VDK_BookRental.ViewModels;

namespace VDK_BookRental.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;

        public AccountController(AppDbContext context)
        {
            _context = context;
        }

        // =========================
        // ĐĂNG KÝ
        // =========================

        [HttpGet]
        public IActionResult Register()
        {
            if (HttpContext.Session.GetString("UserId") != null)
            {
                return RedirectUserByRole();
            }

            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var normalizedEmail = model.Email
                .Trim()
                .ToLower();

            var emailExists = _context.Users
                .Any(u => u.Email.ToLower() == normalizedEmail);

            if (emailExists)
            {
                ModelState.AddModelError(
                    nameof(model.Email),
                    "Email này đã được sử dụng.");

                return View(model);
            }

            var user = new User
            {
                FullName = model.FullName.Trim(),
                Email = normalizedEmail,
                Phone = model.Phone.Trim(),

                PasswordHash = BCrypt.Net.BCrypt
                    .HashPassword(model.Password),

                // Người tự đăng ký chỉ được cấp quyền Customer
                Role = "Customer",

                IsLocked = false,
                CreatedAt = DateTime.Now
            };

            _context.Users.Add(user);
            _context.SaveChanges();

            TempData["SuccessMessage"] =
                "Đăng ký thành công. Vui lòng đăng nhập.";

            return RedirectToAction(nameof(Login));
        }

        // =========================
        // ĐĂNG NHẬP
        // =========================

        [HttpGet]
      
        public IActionResult Login()
        {
            if (HttpContext.Session.GetString("UserId") != null)
            {
                return RedirectUserByRole();
            }

            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userName = model.UserName.Trim().ToLower();

            var user = _context.Users
                .FirstOrDefault(u => u.UserName.ToLower() == userName);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty,
                    "Tên tài khoản hoặc mật khẩu không đúng.");
                return View(model);
            }

            if (!BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty,
                    "Tên tài khoản hoặc mật khẩu không đúng.");
                return View(model);
            }

            if (user.IsLocked)
            {
                ModelState.AddModelError(string.Empty,
                    "Tài khoản đã bị khóa.");
                return View(model);
            }

            HttpContext.Session.SetString("UserId", user.Id.ToString());
            HttpContext.Session.SetString("FullName", user.FullName);
            HttpContext.Session.SetString("UserName", user.UserName);
            HttpContext.Session.SetString("UserRole", user.Role);

            TempData["SuccessMessage"] =
                $"Đăng nhập thành công. Xin chào {user.FullName}!";

            return RedirectUserByRole();
        }

        // =========================
        // ĐĂNG XUẤT
        // =========================

        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();

            TempData["SuccessMessage"] =
                "Bạn đã đăng xuất khỏi hệ thống.";

            return RedirectToAction(nameof(Login));
        }

        // =========================
        // QUÊN MẬT KHẨU
        // =========================

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ForgotPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                ViewBag.ErrorMessage =
                    "Vui lòng nhập địa chỉ email.";

                return View();
            }

            var normalizedEmail = email
                .Trim()
                .ToLower();

            var userExists = _context.Users
                .AsNoTracking()
                .Any(u => u.Email.ToLower() == normalizedEmail);

            ViewBag.SuccessMessage =
                "Nếu email tồn tại trong hệ thống, hướng dẫn đặt lại mật khẩu sẽ được gửi đến bạn.";

            return View();
        }

        // =========================
        // ĐIỀU HƯỚNG THEO QUYỀN
        // =========================

        private IActionResult RedirectUserByRole()
        {
            var role = HttpContext.Session
                .GetString("UserRole");

            if (role == "Admin")
            {
                return RedirectToAction(
                    "Index",
                    "Admin");
            }

            if (role == "Staff")
            {
                return RedirectToAction(
                    "Index",
                    "Staff");
            }

            return RedirectToAction(
                "Index",
                "Books");
        }
    }
}