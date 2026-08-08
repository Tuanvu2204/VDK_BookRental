using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using VDK_BookRental.Data;
using VDK_BookRental.Models;
using VDK_BookRental.ViewModels;
using VDK_BookRental.Filters;
using System.IO;

namespace VDK_BookRental.Controllers
{
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;

        private readonly ILogger<AdminController>
            _logger;
        private readonly IWebHostEnvironment _environment;

        private static readonly string[] AllowedRoles =
        {
            "Admin",
            "Staff",
            "Customer"
        };

        public AdminController(
            AppDbContext context,
            ILogger<AdminController> logger,
            IWebHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _environment = environment;
        }

        // =====================================================
        // DASHBOARD ADMIN
        // URL: /Admin
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!IsAdmin())
            {
                return RedirectToLogin();
            }

            try
            {
                ViewBag.TotalUsers =
                    await _context.Users
                        .AsNoTracking()
                        .CountAsync();

                ViewBag.TotalStaff =
                    await _context.Users
                        .AsNoTracking()
                        .CountAsync(user =>
                            user.Role == "Staff");

                ViewBag.TotalCustomers =
                    await _context.Users
                        .AsNoTracking()
                        .CountAsync(user =>
                            user.Role == "Customer");

                ViewBag.TotalBooks =
                    await _context.Books
                        .AsNoTracking()
                        .CountAsync();

                ViewBag.TotalRentals =
                    await _context.Rentals
                        .AsNoTracking()
                        .CountAsync();

                ViewBag.PendingPayments =
                    await _context.Payments
                        .AsNoTracking()
                        .CountAsync(payment =>
                            payment.Status == "Pending" ||
                            payment.Status ==
                                "AwaitingConfirmation");

                return View();
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Không thể tải Dashboard Admin.");

                TempData["ErrorMessage"] =
                    "Không thể tải đầy đủ số liệu quản trị.";

                return View();
            }

        }

        // =====================================================
        // QUẢN LÝ SÁCH (ADMIN)
        // URL: /Admin/Books
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Books(
            string? search,
            int? categoryId,
            string? stockStatus,
            int page = 1)
        {
            if (!IsAdmin())
            {
                return RedirectToLogin();
            }

            var normalizedSearch = search?.Trim() ?? string.Empty;
            var normalizedStock = (stockStatus ?? string.Empty).Trim().ToLowerInvariant();
            var pageNumber = Math.Max(1, page);
            var pageSize = 8;

            try
            {
                var query = _context.Books
                    .AsNoTracking()
                    .Include(b => b.Category)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(normalizedSearch))
                {
                    var like = $"%{normalizedSearch}%";
                    query = query.Where(b => EF.Functions.Like(b.Title, like) || EF.Functions.Like(b.Author, like));
                }

                if (categoryId.HasValue && categoryId.Value > 0)
                {
                    query = query.Where(b => b.CategoryId == categoryId.Value);
                }

                if (normalizedStock == "available")
                {
                    query = query.Where(b => b.Quantity > 3);
                }
                else if (normalizedStock == "low")
                {
                    query = query.Where(b => b.Quantity > 0 && b.Quantity <= 3);
                }
                else if (normalizedStock == "out")
                {
                    query = query.Where(b => b.Quantity <= 0);
                }

                var totalItems = await query.CountAsync();
                var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));

                var books = await query
                    .OrderBy(b => b.Title)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var categories = await _context.Categories
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                var model = new AdminBookListViewModel
                {
                    Search = normalizedSearch,
                    CategoryId = categoryId,
                    StockStatus = normalizedStock,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalItems = totalItems,
                    TotalPages = totalPages,
                    TotalBooks = await _context.Books.CountAsync(),
                    AvailableBooks = await _context.Books.CountAsync(b => b.Quantity > 3),
                    LowStockBooks = await _context.Books.CountAsync(b => b.Quantity > 0 && b.Quantity <= 3),
                    OutOfStockBooks = await _context.Books.CountAsync(b => b.Quantity <= 0),
                    Books = books,
                    Categories = categories
                };

                return View("~/Views/Admin/Books.cshtml", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cannot load admin book list.");
                TempData["ErrorMessage"] = "Không thể tải danh sách sách.";
                return View("~/Views/Admin/Books.cshtml", new AdminBookListViewModel());
            }
        }

        [HttpGet]
        public async Task<IActionResult> CreateBook()
        {
            if (!IsAdmin())
            {
                return RedirectToLogin();
            }

            var categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync();

            var vm = new AdminBookFormViewModel
            {
                Categories = categories.Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToList()
            };

            return View("~/Views/Admin/CreateBook.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBook(AdminBookFormViewModel model)
        {
            if (!IsAdmin())
            {
                return RedirectToLogin();
            }

            // Server-side validation for uploaded image
            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                var allowed = new[] { "image/jpeg", "image/png", "image/webp" };
                const long maxSize = 5 * 1024 * 1024; // 5 MB

                if (!allowed.Contains(model.ImageFile.ContentType))
                {
                    ModelState.AddModelError("ImageFile", "Định dạng ảnh không được hỗ trợ. Chỉ JPG, PNG, WEBP.");
                }

                if (model.ImageFile.Length > maxSize)
                {
                    ModelState.AddModelError("ImageFile", "Ảnh không được vượt quá 5 MB.");
                }
            }

            if (!ModelState.IsValid)
            {
                model.Categories = (await _context.Categories
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .ToListAsync())
                    .Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToList();

                return View("~/Views/Admin/CreateBook.cshtml", model);
            }

            try
            {
                var title = model.Title?.Trim() ?? string.Empty;
                var author = model.Author?.Trim() ?? string.Empty;

                // Prevent duplicate book (simple guard)
                var exists = await _context.Books
                    .AsNoTracking()
                    .AnyAsync(b =>
                        b.Title.ToLower() == title.ToLower() &&
                        b.Author.ToLower() == author.ToLower());

                if (exists)
                {
                    ModelState.AddModelError(string.Empty, "Sách với tên và tác giả này đã tồn tại.");

                    model.Categories = (await _context.Categories
                        .AsNoTracking()
                        .OrderBy(c => c.Name)
                        .ToListAsync())
                        .Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToList();

                    return View("~/Views/Admin/CreateBook.cshtml", model);
                }

                var book = new Book
                {
                    Title = title,
                    Author = author,
                    CategoryId = model.CategoryId,
                    RentalPrice = model.RentalPrice,
                    Quantity = model.Quantity,
                    Status = model.Quantity > 0 ? "Available" : "Unavailable",
                    ImageUrl = model.ExistingImageUrl
                };

                // Handle uploaded image (safe extension + content type already validated)
                if (model.ImageFile != null && model.ImageFile.Length > 0)
                {
                    var uploads = Path.Combine(_environment.WebRootPath, "images", "books");
                    if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);

                    var ext = Path.GetExtension(model.ImageFile.FileName)?.ToLowerInvariant() ?? string.Empty;
                    var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".webp" };

                    if (!allowedExt.Contains(ext))
                    {
                        ModelState.AddModelError("ImageFile", "Định dạng tệp không hợp lệ.");

                        model.Categories = (await _context.Categories
                            .AsNoTracking()
                            .OrderBy(c => c.Name)
                            .ToListAsync())
                            .Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToList();

                        return View("~/Views/Admin/CreateBook.cshtml", model);
                    }

                    var fileName = $"book_{Guid.NewGuid():N}{ext}";
                    var filePath = Path.Combine(uploads, fileName);

                    await using var stream = System.IO.File.Create(filePath);
                    await model.ImageFile.CopyToAsync(stream);

                    book.ImageUrl = $"/images/books/{fileName}";
                }

                _context.Books.Add(book);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Đã thêm sách mới.";

                return RedirectToAction(nameof(Books));
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Error creating book.");
                TempData["ErrorMessage"] = "Không thể thêm sách do lỗi database.";
                return RedirectToAction(nameof(Books));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating book.");
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi thêm sách.";
                return RedirectToAction(nameof(Books));
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditBook(int id)
        {
            if (!IsAdmin())
            {
                return RedirectToLogin();
            }

            if (id <= 0) return NotFound();

            var book = await _context.Books.FindAsync(id);
            if (book == null) return NotFound();

            var categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync();

            var vm = new AdminBookFormViewModel
            {
                Id = book.Id,
                Title = book.Title,
                Author = book.Author,
                CategoryId = book.CategoryId,
                RentalPrice = book.RentalPrice,
                Quantity = book.Quantity,
                ExistingImageUrl = string.IsNullOrWhiteSpace(book.ImageUrl) ? "/images/books/default-book.jpg" : book.ImageUrl,
                Categories = categories.Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToList()
            };

            return View("~/Views/Admin/EditBook.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBook(AdminBookFormViewModel model)
        {
            if (!IsAdmin())
            {
                return RedirectToLogin();
            }

            // Server-side validation for uploaded image
            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                var allowed = new[] { "image/jpeg", "image/png", "image/webp" };
                const long maxSize = 5 * 1024 * 1024; // 5 MB

                if (!allowed.Contains(model.ImageFile.ContentType))
                {
                    ModelState.AddModelError("ImageFile", "Định dạng ảnh không được hỗ trợ. Chỉ JPG, PNG, WEBP.");
                }

                if (model.ImageFile.Length > maxSize)
                {
                    ModelState.AddModelError("ImageFile", "Ảnh không được vượt quá 5 MB.");
                }
            }

            if (!ModelState.IsValid)
            {
                model.Categories = (await _context.Categories
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .ToListAsync())
                    .Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToList();

                return View("~/Views/Admin/EditBook.cshtml", model);
            }

            try
            {
                var book = await _context.Books.FindAsync(model.Id);
                if (book == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy sách.";
                    return RedirectToAction(nameof(Books));
                }

                book.Title = model.Title.Trim();
                book.Author = model.Author.Trim();
                book.CategoryId = model.CategoryId;
                book.RentalPrice = model.RentalPrice;
                book.Quantity = model.Quantity;
                book.Status = model.Quantity > 0 ? "Available" : "Unavailable";

                if (model.ImageFile != null && model.ImageFile.Length > 0)
                {
                    var uploads = Path.Combine(_environment.WebRootPath, "images", "books");
                    if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);

                    var ext = Path.GetExtension(model.ImageFile.FileName);
                    var fileName = $"book_{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}";
                    var filePath = Path.Combine(uploads, fileName);

                    await using var stream = System.IO.File.Create(filePath);
                    await model.ImageFile.CopyToAsync(stream);

                    book.ImageUrl = $"/images/books/{fileName}";
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Đã cập nhật sách.";
                return RedirectToAction(nameof(Books));
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Error updating book.");
                TempData["ErrorMessage"] = "Không thể cập nhật sách do lỗi database.";
                return RedirectToAction(nameof(Books));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating book.");
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi cập nhật sách.";
                return RedirectToAction(nameof(Books));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBook(int id)
        {
            if (!IsAdmin())
            {
                return RedirectToLogin();
            }

            try
            {
                var book = await _context.Books.FindAsync(id);
                if (book == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy sách.";
                    return RedirectToAction(nameof(Books));
                }

                // Check for related rental data that would prevent deletion
                var hasRentalDetails = await _context.RentalDetails
                    .AsNoTracking()
                    .AnyAsync(rd => rd.BookId == id);

                if (hasRentalDetails)
                {
                    TempData["ErrorMessage"] = "Không thể xóa sách này vì đã có giao dịch thuê liên quan.";
                    return RedirectToAction(nameof(Books));
                }

                _context.Books.Remove(book);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Đã xóa sách.";
                return RedirectToAction(nameof(Books));
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Error deleting book.");
                TempData["ErrorMessage"] = "Không thể xóa sách do ràng buộc dữ liệu.";
                return RedirectToAction(nameof(Books));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting book.");
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi xóa sách.";
                return RedirectToAction(nameof(Books));
            }
        }

        // =====================================================
        // QUẢN LÝ NGƯỜI DÙNG
        // URL: /Admin/Users
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Users(
            string? search,
            string? role,
            string? status)
        {
            if (!IsAdmin())
            {
                return RedirectToLogin();
            }

            var currentUserId =
                GetCurrentUserId() ?? 0;

            var normalizedSearch =
                search?.Trim() ?? string.Empty;

            var normalizedRole =
                NormalizeFilterRole(role);

            var normalizedStatus =
                NormalizeStatus(status);

            try
            {
                var usersQuery =
                    _context.Users
                        .AsNoTracking()
                        .AsQueryable();

                // =============================================
                // TÌM KIẾM
                // =============================================

                if (!string.IsNullOrWhiteSpace(
                        normalizedSearch))
                {
                    var searchPattern =
                        $"%{normalizedSearch}%";

                    usersQuery =
                        usersQuery.Where(user =>
                            EF.Functions.Like(
                                user.UserName,
                                searchPattern)
                            ||
                            EF.Functions.Like(
                                user.FullName,
                                searchPattern)
                            ||
                            EF.Functions.Like(
                                user.Email,
                                searchPattern)
                            ||
                            (
                                user.Phone != null
                                &&
                                EF.Functions.Like(
                                    user.Phone,
                                    searchPattern)
                            ));
                }

                // =============================================
                // LỌC QUYỀN
                // =============================================

                if (!string.IsNullOrWhiteSpace(
                        normalizedRole))
                {
                    usersQuery =
                        usersQuery.Where(user =>
                            user.Role ==
                            normalizedRole);
                }

                // =============================================
                // LỌC TRẠNG THÁI
                // =============================================

                if (normalizedStatus == "Active")
                {
                    usersQuery =
                        usersQuery.Where(user =>
                            !user.IsLocked);
                }
                else if (normalizedStatus == "Locked")
                {
                    usersQuery =
                        usersQuery.Where(user =>
                            user.IsLocked);
                }

                var users =
                    await usersQuery
                        .OrderBy(user =>
                            user.IsLocked)
                        .ThenBy(user =>
                            user.Role == "Admin"
                                ? 0
                                : user.Role == "Staff"
                                    ? 1
                                    : 2)
                        .ThenBy(user =>
                            user.FullName)
                        .ThenBy(user =>
                            user.Id)
                        .ToListAsync();

                // =============================================
                // ĐẾM ĐƠN THUÊ
                // =============================================

                var userIds =
                    users
                        .Select(user => user.Id)
                        .ToList();

                var rentalCounts =
                    userIds.Count == 0
                        ? new Dictionary<int, int>()
                        : await _context.Rentals
                            .AsNoTracking()
                            .Where(rental =>
                                userIds.Contains(
                                    rental.UserId))
                            .GroupBy(rental =>
                                rental.UserId)
                            .Select(group =>
                                new
                                {
                                    UserId = group.Key,
                                    Count = group.Count()
                                })
                            .ToDictionaryAsync(
                                item => item.UserId,
                                item => item.Count);

                // =============================================
                // THỐNG KÊ TOÀN HỆ THỐNG
                // =============================================

                var allUsers =
                    await _context.Users
                        .AsNoTracking()
                        .Select(user =>
                            new
                            {
                                user.Role,
                                user.IsLocked
                            })
                        .ToListAsync();

                var model =
                    new AdminUsersViewModel
                    {
                        Search =
                            normalizedSearch,

                        RoleFilter =
                            normalizedRole,

                        StatusFilter =
                            normalizedStatus,

                        CurrentUserId =
                            currentUserId,

                        TotalUsers =
                            allUsers.Count,

                        ActiveUsers =
                            allUsers.Count(user =>
                                !user.IsLocked),

                        LockedUsers =
                            allUsers.Count(user =>
                                user.IsLocked),

                        AdminUsers =
                            allUsers.Count(user =>
                                user.Role == "Admin"),

                        StaffUsers =
                            allUsers.Count(user =>
                                user.Role == "Staff"),

                        CustomerUsers =
                            allUsers.Count(user =>
                                user.Role == "Customer"),

                        Users =
                            users.Select(user =>
                                new AdminUserItemViewModel
                                {
                                    Id =
                                        user.Id,

                                    UserName =
                                        user.UserName ??
                                        string.Empty,

                                    FullName =
                                        user.FullName ??
                                        string.Empty,

                                    Email =
                                        user.Email ??
                                        string.Empty,

                                    Phone =
                                        user.Phone ??
                                        string.Empty,

                                    Role =
                                        NormalizeRole(
                                            user.Role),

                                    AvatarUrl =
                                        user.AvatarUrl ??
                                        string.Empty,

                                    IsLocked =
                                        user.IsLocked,

                                    CreatedAt =
                                        user.CreatedAt,

                                    RentalCount =
                                        rentalCounts.TryGetValue(
                                            user.Id,
                                            out var count)
                                                ? count
                                                : 0,

                                    IsCurrentUser =
                                        user.Id ==
                                        currentUserId
                                })
                            .ToList()
                    };

                return View(
                    "~/Views/Admin/Users.cshtml",
                    model);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Không thể tải danh sách người dùng.");

                TempData["ErrorMessage"] =
                    "Không thể tải danh sách người dùng.";

                return View(
                    "~/Views/Admin/Users.cshtml",
                    new AdminUsersViewModel
                    {
                        Search =
                            normalizedSearch,

                        RoleFilter =
                            normalizedRole,

                        StatusFilter =
                            normalizedStatus,

                        CurrentUserId =
                            currentUserId
                    });
            }
        }

        // =====================================================
        // CẬP NHẬT QUYỀN
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRole(
            int id,
            string? role,
            string? search,
            string? filterRole,
            string? status)
        {
            if (!IsAdmin())
            {
                return RedirectToLogin();
            }

            var normalizedRole =
                NormalizeRole(role);

            if (!AllowedRoles.Contains(
                    normalizedRole,
                    StringComparer.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] =
                    "Quyền tài khoản không hợp lệ.";

                return RedirectToFilteredUsers(
                    search,
                    filterRole,
                    status);
            }

            try
            {
                var user =
                    await _context.Users
                        .FirstOrDefaultAsync(item =>
                            item.Id == id);

                if (user == null)
                {
                    TempData["ErrorMessage"] =
                        "Không tìm thấy tài khoản.";

                    return RedirectToFilteredUsers(
                        search,
                        filterRole,
                        status);
                }

                var currentUserId =
                    GetCurrentUserId();

                // Không tự hạ quyền Admin đang đăng nhập.
                if (currentUserId == user.Id &&
                    normalizedRole != "Admin")
                {
                    TempData["ErrorMessage"] =
                        "Bạn không thể tự hạ quyền tài khoản Admin đang đăng nhập.";

                    return RedirectToFilteredUsers(
                        search,
                        filterRole,
                        status);
                }

                // Không được hạ quyền Admin hoạt động cuối cùng.
                if (user.Role == "Admin" &&
                    normalizedRole != "Admin" &&
                    !user.IsLocked)
                {
                    var activeAdminCount =
                        await _context.Users
                            .CountAsync(item =>
                                item.Role == "Admin" &&
                                !item.IsLocked);

                    if (activeAdminCount <= 1)
                    {
                        TempData["ErrorMessage"] =
                            "Không thể hạ quyền quản trị viên hoạt động cuối cùng.";

                        return RedirectToFilteredUsers(
                            search,
                            filterRole,
                            status);
                    }
                }

                if (string.Equals(
                        user.Role,
                        normalizedRole,
                        StringComparison.OrdinalIgnoreCase))
                {
                    TempData["InfoMessage"] =
                        $"Tài khoản {GetDisplayName(user)} đã có quyền {GetRoleText(normalizedRole)}.";

                    return RedirectToFilteredUsers(
                        search,
                        filterRole,
                        status);
                }

                var oldRole =
                    GetRoleText(user.Role);

                user.Role =
                    normalizedRole;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    $"Đã đổi quyền của {GetDisplayName(user)} " +
                    $"từ {oldRole} thành {GetRoleText(normalizedRole)}.";

                return RedirectToFilteredUsers(
                    search,
                    filterRole,
                    status);
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(
                    exception,
                    "Lỗi cập nhật quyền UserId {UserId}.",
                    id);

                TempData["ErrorMessage"] =
                    "Database không thể cập nhật quyền tài khoản.";

                return RedirectToFilteredUsers(
                    search,
                    filterRole,
                    status);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Lỗi cập nhật quyền UserId {UserId}.",
                    id);

                TempData["ErrorMessage"] =
                    "Đã xảy ra lỗi khi cập nhật quyền.";

                return RedirectToFilteredUsers(
                    search,
                    filterRole,
                    status);
            }
        }

        // =====================================================
        // KHÓA / MỞ KHÓA
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLock(
            int id,
            string? search,
            string? filterRole,
            string? status)
        {
            if (!IsAdmin())
            {
                return RedirectToLogin();
            }

            try
            {
                var user =
                    await _context.Users
                        .FirstOrDefaultAsync(item =>
                            item.Id == id);

                if (user == null)
                {
                    TempData["ErrorMessage"] =
                        "Không tìm thấy tài khoản.";

                    return RedirectToFilteredUsers(
                        search,
                        filterRole,
                        status);
                }

                var currentUserId =
                    GetCurrentUserId();

                if (currentUserId == user.Id)
                {
                    TempData["ErrorMessage"] =
                        "Bạn không thể tự khóa tài khoản đang đăng nhập.";

                    return RedirectToFilteredUsers(
                        search,
                        filterRole,
                        status);
                }

                // Không khóa Admin hoạt động cuối cùng.
                if (!user.IsLocked &&
                    user.Role == "Admin")
                {
                    var activeAdminCount =
                        await _context.Users
                            .CountAsync(item =>
                                item.Role == "Admin" &&
                                !item.IsLocked);

                    if (activeAdminCount <= 1)
                    {
                        TempData["ErrorMessage"] =
                            "Không thể khóa quản trị viên hoạt động cuối cùng.";

                        return RedirectToFilteredUsers(
                            search,
                            filterRole,
                            status);
                    }
                }

                user.IsLocked =
                    !user.IsLocked;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    user.IsLocked
                        ? $"Đã khóa tài khoản {GetDisplayName(user)}."
                        : $"Đã mở khóa tài khoản {GetDisplayName(user)}.";

                return RedirectToFilteredUsers(
                    search,
                    filterRole,
                    status);
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(
                    exception,
                    "Lỗi khóa hoặc mở khóa UserId {UserId}.",
                    id);

                TempData["ErrorMessage"] =
                    "Database không thể cập nhật trạng thái tài khoản.";

                return RedirectToFilteredUsers(
                    search,
                    filterRole,
                    status);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Lỗi khóa hoặc mở khóa UserId {UserId}.",
                    id);

                TempData["ErrorMessage"] =
                    "Đã xảy ra lỗi khi cập nhật tài khoản.";

                return RedirectToFilteredUsers(
                    search,
                    filterRole,
                    status);
            }
        }

        // =====================================================
        // TRỢ GIÚP
        // =====================================================

        private bool IsAdmin()
        {
            var userId =
                HttpContext.Session.GetString(
                    "UserId");

            var userRole =
                HttpContext.Session.GetString(
                    "UserRole");

            return !string.IsNullOrWhiteSpace(userId)
                   &&
                   string.Equals(
                       userRole?.Trim(),
                       "Admin",
                       StringComparison.OrdinalIgnoreCase);
        }

        private int? GetCurrentUserId()
        {
            var value =
                HttpContext.Session.GetString(
                    "UserId");

            return int.TryParse(
                value,
                out var userId)
                    ? userId
                    : null;
        }

        private IActionResult RedirectToLogin()
        {
            TempData["ErrorMessage"] =
                "Vui lòng đăng nhập bằng tài khoản Admin.";

            return RedirectToAction(
                "Login",
                "Account");
        }

        private IActionResult RedirectToFilteredUsers(
            string? search,
            string? filterRole,
            string? status)
        {
            return RedirectToAction(
                nameof(Users),
                new
                {
                    search,
                    role = filterRole,
                    status
                });
        }

        private static string NormalizeFilterRole(
            string? role)
        {
            var normalized =
                NormalizeRole(role);

            return AllowedRoles.Contains(
                normalized,
                StringComparer.OrdinalIgnoreCase)
                    ? normalized
                    : string.Empty;
        }

        private static string NormalizeStatus(
            string? status)
        {
            return status?.Trim() switch
            {
                "Active" => "Active",
                "Locked" => "Locked",
                _ => string.Empty
            };
        }

        private static string NormalizeRole(
            string? role)
        {
            return role?.Trim().ToLowerInvariant() switch
            {
                "admin" => "Admin",
                "administrator" => "Admin",

                "staff" => "Staff",
                "employee" => "Staff",

                "customer" => "Customer",
                "user" => "Customer",

                _ => string.Empty
            };
        }

        private static string GetRoleText(
            string? role)
        {
            return NormalizeRole(role) switch
            {
                "Admin" => "Quản trị viên",
                "Staff" => "Nhân viên",
                _ => "Khách hàng"
            };
        }

        private static string GetDisplayName(
            User user)
        {
            if (!string.IsNullOrWhiteSpace(
                    user.FullName))
            {
                return user.FullName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(
                    user.UserName))
            {
                return user.UserName.Trim();
            }

            return $"ID {user.Id}";
        }
    }
}