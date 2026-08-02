using Microsoft.AspNetCore.Http;
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

        private readonly ILogger<AdminController>
            _logger;

        private static readonly string[] AllowedRoles =
        {
            "Admin",
            "Staff",
            "Customer"
        };

        public AdminController(
            AppDbContext context,
            ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
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