using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VDK_BookRental.Data;
using VDK_BookRental.Models;

namespace VDK_BookRental.Controllers
{
    public class CategoryController : Controller
    {
        private readonly AppDbContext _context;

        public CategoryController(AppDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // DANH SÁCH THỂ LOẠI
        // URL: /Category
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var accessResult = CheckStaffAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            var categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(category => category.Name)
                .ToListAsync();

            var bookCounts = await _context.Books
                .AsNoTracking()
                .Where(book => book.Category != null)
                .GroupBy(book => book.Category!.Id)
                .Select(group => new
                {
                    CategoryId = group.Key,
                    Count = group.Count()
                })
                .ToDictionaryAsync(
                    item => item.CategoryId,
                    item => item.Count
                );

            ViewBag.BookCounts = bookCounts;

            return View(
                "~/Views/Category/Index.cshtml",
                categories
            );
        }

        // =========================================================
        // THÊM THỂ LOẠI
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string? name)
        {
            var accessResult = CheckStaffAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            var normalizedName = name?.Trim();

            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                TempData["ErrorMessage"] =
                    "Vui lòng nhập tên thể loại.";

                return RedirectToAction(nameof(Index));
            }

            if (normalizedName.Length > 100)
            {
                TempData["ErrorMessage"] =
                    "Tên thể loại không được vượt quá 100 ký tự.";

                return RedirectToAction(nameof(Index));
            }

            var lowerName = normalizedName.ToLower();

            var duplicated = await _context.Categories
                .AnyAsync(category =>
                    category.Name.ToLower() == lowerName);

            if (duplicated)
            {
                TempData["ErrorMessage"] =
                    "Tên thể loại đã tồn tại.";

                return RedirectToAction(nameof(Index));
            }

            var category = new Category
            {
                Name = normalizedName
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Thêm thể loại thành công.";

            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // CẬP NHẬT THỂ LOẠI
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            string? name)
        {
            var accessResult = CheckStaffAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            var category = await _context.Categories
                .FirstOrDefaultAsync(item => item.Id == id);

            if (category == null)
            {
                TempData["ErrorMessage"] =
                    "Không tìm thấy thể loại.";

                return RedirectToAction(nameof(Index));
            }

            var normalizedName = name?.Trim();

            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                TempData["ErrorMessage"] =
                    "Vui lòng nhập tên thể loại.";

                return RedirectToAction(nameof(Index));
            }

            if (normalizedName.Length > 100)
            {
                TempData["ErrorMessage"] =
                    "Tên thể loại không được vượt quá 100 ký tự.";

                return RedirectToAction(nameof(Index));
            }

            var lowerName = normalizedName.ToLower();

            var duplicated = await _context.Categories
                .AnyAsync(item =>
                    item.Id != id &&
                    item.Name.ToLower() == lowerName);

            if (duplicated)
            {
                TempData["ErrorMessage"] =
                    "Tên thể loại đã tồn tại.";

                return RedirectToAction(nameof(Index));
            }

            category.Name = normalizedName;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Cập nhật thể loại thành công.";

            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // XÓA THỂ LOẠI
        // Không cho xóa khi còn sách thuộc thể loại
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var accessResult = CheckStaffAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            var category = await _context.Categories
                .FirstOrDefaultAsync(item => item.Id == id);

            if (category == null)
            {
                TempData["ErrorMessage"] =
                    "Không tìm thấy thể loại.";

                return RedirectToAction(nameof(Index));
            }

            var hasBooks = await _context.Books
                .AnyAsync(book =>
                    book.Category != null &&
                    book.Category.Id == id);

            if (hasBooks)
            {
                TempData["ErrorMessage"] =
                    "Không thể xóa thể loại đang có sách. Hãy chuyển sách sang thể loại khác trước.";

                return RedirectToAction(nameof(Index));
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Xóa thể loại thành công.";

            return RedirectToAction(nameof(Index));
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