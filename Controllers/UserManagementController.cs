using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VDK_BookRental.Data;
using VDK_BookRental.ViewModels;

namespace VDK_BookRental.Controllers
{
    public class UserManagementController : Controller
    {
        private static readonly string[] AllowedRoles =
        {
            "Customer",
            "Staff",
            "Admin"
        };

        private readonly AppDbContext _context;

        public UserManagementController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? search,
            string? role,
            string? status)
        {
            var accessResult = CheckAdminAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            var currentUserId =
                GetCurrentUserId() ?? 0;

            var usersQuery =
                _context.Users
                    .AsNoTracking()
                    .AsQueryable();

            var normalizedSearch =
                search?.Trim() ?? string.Empty;

            var normalizedRole =
                role?.Trim() ?? string.Empty;

            var normalizedStatus =
                status?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                var searchPattern =
                    $"%{normalizedSearch}%";

                usersQuery = usersQuery.Where(user =>
                    EF.Functions.Like(
                        user.UserName,
                        searchPattern
                    )
                    ||
                    EF.Functions.Like(
                        user.FullName,
                        searchPattern
                    )
                    ||
                    EF.Functions.Like(
                        user.Email,
                        searchPattern
                    )
                    ||
                    EF.Functions.Like(
                        user.Phone,
                        searchPattern
                    ));
            }

            if (AllowedRoles.Contains(normalizedRole))
            {
                usersQuery = usersQuery.Where(user =>
                    user.Role == normalizedRole);
            }

            if (normalizedStatus == "Active")
            {
                usersQuery = usersQuery.Where(user =>
                    !user.IsLocked);
            }
            else if (normalizedStatus == "Locked")
            {
                usersQuery = usersQuery.Where(user =>
                    user.IsLocked);
            }

            var users = await usersQuery
                .OrderBy(user => user.IsLocked)
                .ThenBy(user => user.Role)
                .ThenBy(user => user.FullName)
                .ToListAsync();

            var userIds =
                users.Select(user => user.Id).ToList();

            var rentalCounts =
                userIds.Count == 0
                    ? new Dictionary<int, int>()
                    : await _context.Rentals
                        .AsNoTracking()
                        .Where(rental =>
                            userIds.Contains(rental.UserId))
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
                            item => item.Count
                        );

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
                new UserManagementPageViewModel
                {
                    Search = normalizedSearch,

                    RoleFilter =
                        AllowedRoles.Contains(normalizedRole)
                            ? normalizedRole
                            : string.Empty,

                    StatusFilter =
                        normalizedStatus == "Active" ||
                        normalizedStatus == "Locked"
                            ? normalizedStatus
                            : string.Empty,

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

                    CustomerUsers =
                        allUsers.Count(user =>
                            user.Role == "Customer"),

                    StaffUsers =
                        allUsers.Count(user =>
                            user.Role == "Staff"),

                    AdminUsers =
                        allUsers.Count(user =>
                            user.Role == "Admin"),

                    Users = users.Select(user =>
                        new UserManagementItemViewModel
                        {
                            Id = user.Id,

                            UserName =
                                user.UserName ?? string.Empty,

                            FullName =
                                user.FullName ?? string.Empty,

                            Email =
                                user.Email ?? string.Empty,

                            Phone =
                                user.Phone ?? string.Empty,

                            Role =
                                user.Role ?? "Customer",

                            IsLocked =
                                user.IsLocked,

                            RentalCount =
                                rentalCounts.TryGetValue(
                                    user.Id,
                                    out var count)
                                        ? count
                                        : 0,

                            IsCurrentUser =
                                user.Id == currentUserId
                        })
                        .ToList()
                };

            return View(
                "~/Views/UserManagement/Index.cshtml",
                model
            );
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLock(int id)
        {
            var accessResult = CheckAdminAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            var currentUserId =
                GetCurrentUserId();

            if (currentUserId == id)
            {
                TempData["ErrorMessage"] =
                    "Bạn không thể tự khóa tài khoản đang đăng nhập.";

                return RedirectToAction(nameof(Index));
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(item =>
                    item.Id == id);

            if (user == null)
            {
                TempData["ErrorMessage"] =
                    "Không tìm thấy tài khoản.";

                return RedirectToAction(nameof(Index));
            }

            if (!user.IsLocked &&
                user.Role == "Admin")
            {
                var activeAdminCount =
                    await _context.Users.CountAsync(item =>
                        item.Role == "Admin" &&
                        !item.IsLocked);

                if (activeAdminCount <= 1)
                {
                    TempData["ErrorMessage"] =
                        "Không thể khóa quản trị viên hoạt động cuối cùng.";

                    return RedirectToAction(nameof(Index));
                }
            }

            user.IsLocked =
                !user.IsLocked;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                user.IsLocked
                    ? $"Đã khóa tài khoản {user.UserName}."
                    : $"Đã mở khóa tài khoản {user.UserName}.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeRole(
            int id,
            string? role)
        {
            var accessResult = CheckAdminAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            var normalizedRole =
                role?.Trim() ?? string.Empty;

            if (!AllowedRoles.Contains(normalizedRole))
            {
                TempData["ErrorMessage"] =
                    "Quyền tài khoản không hợp lệ.";

                return RedirectToAction(nameof(Index));
            }

            var currentUserId =
                GetCurrentUserId();

            if (currentUserId == id)
            {
                TempData["ErrorMessage"] =
                    "Bạn không thể tự thay đổi quyền của tài khoản đang đăng nhập.";

                return RedirectToAction(nameof(Index));
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(item =>
                    item.Id == id);

            if (user == null)
            {
                TempData["ErrorMessage"] =
                    "Không tìm thấy tài khoản.";

                return RedirectToAction(nameof(Index));
            }

            if (user.Role == "Admin" &&
                normalizedRole != "Admin" &&
                !user.IsLocked)
            {
                var activeAdminCount =
                    await _context.Users.CountAsync(item =>
                        item.Role == "Admin" &&
                        !item.IsLocked);

                if (activeAdminCount <= 1)
                {
                    TempData["ErrorMessage"] =
                        "Không thể hạ quyền quản trị viên hoạt động cuối cùng.";

                    return RedirectToAction(nameof(Index));
                }
            }

            if (user.Role == normalizedRole)
            {
                TempData["ErrorMessage"] =
                    "Tài khoản đã có quyền này.";

                return RedirectToAction(nameof(Index));
            }

            user.Role =
                normalizedRole;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Đã đổi quyền của {user.UserName} thành {GetRoleText(normalizedRole)}.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int id)
        {
            var accessResult = CheckAdminAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            var currentUserId =
                GetCurrentUserId();

            if (currentUserId == id)
            {
                TempData["ErrorMessage"] =
                    "Hãy đổi mật khẩu của bạn trong trang Hồ sơ cá nhân.";

                return RedirectToAction(nameof(Index));
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(item =>
                    item.Id == id);

            if (user == null)
            {
                TempData["ErrorMessage"] =
                    "Không tìm thấy tài khoản.";

                return RedirectToAction(nameof(Index));
            }

            var temporaryPassword =
                GenerateTemporaryPassword();

            user.PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(
                    temporaryPassword
                );

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Đã đặt lại mật khẩu cho {user.UserName}.";

            TempData["TemporaryPassword"] =
                temporaryPassword;

            TempData["TemporaryPasswordUser"] =
                user.UserName ?? string.Empty;

            return RedirectToAction(nameof(Index));
        }

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
                    "Account"
                );
            }

            if (!string.Equals(
                    userRole,
                    "Admin",
                    StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] =
                    "Chỉ quản trị viên mới được quản lý tài khoản.";

                return RedirectToAction(
                    "AccessDenied",
                    "Home"
                );
            }

            return null;
        }

        private int? GetCurrentUserId()
        {
            var value =
                HttpContext.Session.GetString("UserId");

            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return int.TryParse(
                value,
                out var userId)
                    ? userId
                    : null;
        }

        private static string GetRoleText(string role)
        {
            return role switch
            {
                "Admin" => "Quản trị viên",
                "Staff" => "Nhân viên",
                _ => "Khách hàng"
            };
        }

        private static string GenerateTemporaryPassword()
        {
            const string upper =
                "ABCDEFGHJKLMNPQRSTUVWXYZ";

            const string lower =
                "abcdefghijkmnopqrstuvwxyz";

            const string digits =
                "23456789";

            const string symbols =
                "@#$%";

            const string all =
                upper + lower + digits + symbols;

            var characters =
                new List<char>
                {
                    upper[RandomNumberGenerator.GetInt32(upper.Length)],
                    lower[RandomNumberGenerator.GetInt32(lower.Length)],
                    digits[RandomNumberGenerator.GetInt32(digits.Length)],
                    symbols[RandomNumberGenerator.GetInt32(symbols.Length)]
                };

            while (characters.Count < 12)
            {
                characters.Add(
                    all[
                        RandomNumberGenerator.GetInt32(
                            all.Length
                        )
                    ]
                );
            }

            for (var index =
                    characters.Count - 1;
                 index > 0;
                 index--)
            {
                var swapIndex =
                    RandomNumberGenerator.GetInt32(
                        index + 1
                    );

                (
                    characters[index],
                    characters[swapIndex]
                )
                =
                (
                    characters[swapIndex],
                    characters[index]
                );
            }

            return new string(
                characters.ToArray()
            );
        }
    }
}