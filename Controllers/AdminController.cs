using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VDK_BookRental.Data;
using VDK_BookRental.Models;
using VDK_BookRental.ViewModels;

namespace VDK_BookRental.Controllers
{
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        private static readonly string[] AllowedImageExtensions =
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

        private const long MaximumImageSize = 5 * 1024 * 1024;

        public AdminController(
            AppDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // =====================================================
        // DASHBOARD ADMIN
        // =====================================================

        public IActionResult Index()
        {
            var accessResult = CheckAdminAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            ViewBag.TotalUsers = _context.Users.Count();

            ViewBag.TotalCustomers = _context.Users
                .Count(u => u.Role == "Customer");

            ViewBag.TotalStaff = _context.Users
                .Count(u => u.Role == "Staff");

            ViewBag.TotalBooks = _context.Books.Count();

            ViewBag.TotalRentals = _context.Rentals.Count();

            ViewBag.PendingPayments = _context.Payments
                .Count(p =>
                    p.Status == "Pending" ||
                    p.Status == "AwaitingConfirmation");

            return View();
        }

        // =====================================================
        // QUẢN LÝ NGƯỜI DÙNG
        // =====================================================

        public IActionResult Users()
        {
            var accessResult = CheckAdminAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            var users = _context.Users
                .AsNoTracking()
                .OrderByDescending(u => u.CreatedAt)
                .ToList();

            return View(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ChangeRole(
            int userId,
            string role)
        {
            var accessResult = CheckAdminAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            var allowedRoles = new[]
            {
                "Admin",
                "Staff",
                "Customer"
            };

            if (!allowedRoles.Contains(role))
            {
                TempData["ErrorMessage"] =
                    "Quyền được chọn không hợp lệ.";

                return RedirectToAction(nameof(Users));
            }

            var user = _context.Users
                .FirstOrDefault(u => u.Id == userId);

            if (user == null)
            {
                return NotFound();
            }

            var currentUserIdText =
                HttpContext.Session.GetString("UserId");

            int.TryParse(
                currentUserIdText,
                out var currentUserId);

            if (currentUserId == userId &&
                role != "Admin")
            {
                TempData["ErrorMessage"] =
                    "Bạn không thể tự hạ quyền Admin của mình.";

                return RedirectToAction(nameof(Users));
            }

            user.Role = role;

            _context.SaveChanges();

            TempData["SuccessMessage"] =
                $"Đã đổi quyền của {user.FullName} thành {role}.";

            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleLock(int userId)
        {
            var accessResult = CheckAdminAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            var currentUserIdText =
                HttpContext.Session.GetString("UserId");

            int.TryParse(
                currentUserIdText,
                out var currentUserId);

            if (currentUserId == userId)
            {
                TempData["ErrorMessage"] =
                    "Bạn không thể tự khóa tài khoản của mình.";

                return RedirectToAction(nameof(Users));
            }

            var user = _context.Users
                .FirstOrDefault(u => u.Id == userId);

            if (user == null)
            {
                return NotFound();
            }

            user.IsLocked = !user.IsLocked;

            _context.SaveChanges();

            TempData["SuccessMessage"] =
                user.IsLocked
                    ? $"Đã khóa tài khoản {user.FullName}."
                    : $"Đã mở khóa tài khoản {user.FullName}.";

            return RedirectToAction(nameof(Users));
        }

        // =====================================================
        // QUẢN LÝ SÁCH
        // =====================================================

        public IActionResult Books()
        {
            var accessResult = CheckAdminAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            var books = _context.Books
                .AsNoTracking()
                .Include(b => b.Category)
                .OrderByDescending(b => b.Id)
                .ToList();

            return View(books);
        }

        // =====================================================
        // THÊM SÁCH
        // =====================================================

        [HttpGet]
        public IActionResult CreateBook()
        {
            var accessResult = CheckAdminAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            LoadCategories();

            return View(new BookFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBook(
            BookFormViewModel model)
        {
            var accessResult = CheckAdminAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            ValidateImage(model.ImageFile, imageRequired: true);

            if (!ModelState.IsValid)
            {
                LoadCategories();

                return View(model);
            }

            string imageUrl;

            try
            {
                imageUrl = await SaveImageAsync(model.ImageFile!);
            }
            catch (IOException)
            {
                ModelState.AddModelError(
                    nameof(model.ImageFile),
                    "Không thể lưu ảnh. Vui lòng thử lại.");

                LoadCategories();

                return View(model);
            }

            var book = new Book
            {
                Title = model.Title.Trim(),
                Author = model.Author.Trim(),
                CategoryId = model.CategoryId,
                RentalPrice = model.RentalPrice,
                Quantity = model.Quantity,
                Description = model.Description?.Trim(),
                ImageUrl = imageUrl,
                Status = model.Quantity > 0
                    ? "Còn sách"
                    : "Hết sách"
            };

            _context.Books.Add(book);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Đã thêm sách \"{book.Title}\".";

            return RedirectToAction(nameof(Books));
        }

        // =====================================================
        // SỬA SÁCH
        // =====================================================

        [HttpGet]
        public IActionResult EditBook(int id)
        {
            var accessResult = CheckAdminAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            var book = _context.Books
                .AsNoTracking()
                .FirstOrDefault(b => b.Id == id);

            if (book == null)
            {
                return NotFound();
            }

            var model = new BookFormViewModel
            {
                Id = book.Id,
                Title = book.Title,
                Author = book.Author,
                CategoryId = book.CategoryId,
                RentalPrice = book.RentalPrice,
                Quantity = book.Quantity,
                Description = book.Description,
                ExistingImageUrl = book.ImageUrl
            };

            LoadCategories();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBook(
            BookFormViewModel model)
        {
            var accessResult = CheckAdminAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            ValidateImage(model.ImageFile, imageRequired: false);

            if (!ModelState.IsValid)
            {
                LoadCategories();

                return View(model);
            }

            var book = await _context.Books
                .FirstOrDefaultAsync(b => b.Id == model.Id);

            if (book == null)
            {
                return NotFound();
            }

            var oldImageUrl = book.ImageUrl;

            if (model.ImageFile != null)
            {
                try
                {
                    var newImageUrl =
                        await SaveImageAsync(model.ImageFile);

                    book.ImageUrl = newImageUrl;

                    DeleteImageIfOwned(oldImageUrl);
                }
                catch (IOException)
                {
                    ModelState.AddModelError(
                        nameof(model.ImageFile),
                        "Không thể lưu ảnh mới. Vui lòng thử lại.");

                    model.ExistingImageUrl = oldImageUrl;

                    LoadCategories();

                    return View(model);
                }
            }

            book.Title = model.Title.Trim();
            book.Author = model.Author.Trim();
            book.CategoryId = model.CategoryId;
            book.RentalPrice = model.RentalPrice;
            book.Quantity = model.Quantity;
            book.Description = model.Description?.Trim();

            book.Status = model.Quantity > 0
                ? "Còn sách"
                : "Hết sách";

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Đã cập nhật sách \"{book.Title}\".";

            return RedirectToAction(nameof(Books));
        }

        // =====================================================
        // XÓA SÁCH
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBook(int id)
        {
            var accessResult = CheckAdminAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            var book = await _context.Books
                .FirstOrDefaultAsync(b => b.Id == id);

            if (book == null)
            {
                return NotFound();
            }

            var hasRentalDetails = await _context.RentalDetails
                .AnyAsync(rd => rd.BookId == id);

            if (hasRentalDetails)
            {
                TempData["ErrorMessage"] =
                    "Không thể xóa sách đã từng phát sinh đơn thuê.";

                return RedirectToAction(nameof(Books));
            }

            var imageUrl = book.ImageUrl;

            _context.Books.Remove(book);

            await _context.SaveChangesAsync();

            DeleteImageIfOwned(imageUrl);

            TempData["SuccessMessage"] =
                $"Đã xóa sách \"{book.Title}\".";

            return RedirectToAction(nameof(Books));
        }

        // =====================================================
        // KIỂM TRA FILE ẢNH
        // =====================================================

        private void ValidateImage(
            IFormFile? imageFile,
            bool imageRequired)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                if (imageRequired)
                {
                    ModelState.AddModelError(
                        nameof(BookFormViewModel.ImageFile),
                        "Vui lòng chọn ảnh bìa sách.");
                }

                return;
            }

            var extension = Path
                .GetExtension(imageFile.FileName)
                .ToLowerInvariant();

            if (!AllowedImageExtensions.Contains(extension))
            {
                ModelState.AddModelError(
                    nameof(BookFormViewModel.ImageFile),
                    "Chỉ chấp nhận ảnh JPG, JPEG, PNG hoặc WEBP.");
            }

            if (imageFile.Length > MaximumImageSize)
            {
                ModelState.AddModelError(
                    nameof(BookFormViewModel.ImageFile),
                    "Dung lượng ảnh không được vượt quá 5 MB.");
            }

            var allowedContentTypes = new[]
            {
                "image/jpeg",
                "image/png",
                "image/webp"
            };

            if (!allowedContentTypes.Contains(
                    imageFile.ContentType.ToLowerInvariant()))
            {
                ModelState.AddModelError(
                    nameof(BookFormViewModel.ImageFile),
                    "Nội dung file không phải định dạng ảnh hợp lệ.");
            }
        }

        // =====================================================
        // LƯU ẢNH VÀO WWWROOT
        // =====================================================

        private async Task<string> SaveImageAsync(
            IFormFile imageFile)
        {
            var uploadFolder = Path.Combine(
                _environment.WebRootPath,
                "images",
                "books");

            Directory.CreateDirectory(uploadFolder);

            var extension = Path
                .GetExtension(imageFile.FileName)
                .ToLowerInvariant();

            var originalName = Path
                .GetFileNameWithoutExtension(imageFile.FileName);

            var safeName = CreateSafeFileName(originalName);

            var uniqueFileName =
                $"{safeName}_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}{extension}";

            var fullPath = Path.Combine(
                uploadFolder,
                uniqueFileName);

            await using var stream =
                new FileStream(
                    fullPath,
                    FileMode.CreateNew);

            await imageFile.CopyToAsync(stream);

            return $"/images/books/{uniqueFileName}";
        }

        // =====================================================
        // TẠO TÊN FILE AN TOÀN
        // =====================================================

        private static string CreateSafeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return "book";
            }

            var invalidChars =
                Path.GetInvalidFileNameChars();

            var cleaned = new string(
                fileName
                    .Trim()
                    .Select(character =>
                        invalidChars.Contains(character)
                            ? '-'
                            : character)
                    .ToArray());

            cleaned = cleaned
                .Replace(" ", "-")
                .ToLowerInvariant();

            return string.IsNullOrWhiteSpace(cleaned)
                ? "book"
                : cleaned;
        }

        // =====================================================
        // XÓA ẢNH DO HỆ THỐNG UPLOAD
        // =====================================================

        private void DeleteImageIfOwned(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return;
            }

            const string uploadPrefix =
                "/images/books/";

            if (!imageUrl.StartsWith(
                    uploadPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var fileName = Path.GetFileName(imageUrl);

            if (string.IsNullOrWhiteSpace(fileName) ||
                fileName.Equals(
                    "default-book.jpg",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var fullPath = Path.Combine(
                _environment.WebRootPath,
                "images",
                "books",
                fileName);

            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }

        // =====================================================
        // NẠP DANH MỤC
        // =====================================================

        private void LoadCategories()
        {
            ViewBag.Categories = _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToList();
        }

        // =====================================================
        // KIỂM TRA QUYỀN ADMIN
        // =====================================================

        private IActionResult? CheckAdminAccess()
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

            if (userRole != "Admin")
            {
                return RedirectToAction(
                    "AccessDenied",
                    "Home");
            }

            return null;
        }
    }
}