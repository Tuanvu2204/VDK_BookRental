using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VDK_BookRental.Data;
using VDK_BookRental.ViewModels;

namespace VDK_BookRental.Controllers
{
    public class ProfileController : Controller
    {
        private const long MaxAvatarSize =
            5L * 1024 * 1024;

        private static readonly HashSet<string>
            AllowedAvatarExtensions =
                new(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ".jpg",
                    ".jpeg",
                    ".png",
                    ".webp"
                };

        private readonly AppDbContext _context;
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(
            AppDbContext context,
            ILogger<ProfileController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // =========================================================
        // HỒ SƠ CÁ NHÂN
        // GET: /Profile
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();

            if (userId == null)
            {
                TempData["ErrorMessage"] =
                    "Vui lòng đăng nhập để xem hồ sơ cá nhân.";

                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.Id == userId.Value);

            if (user == null)
            {
                HttpContext.Session.Clear();

                TempData["ErrorMessage"] =
                    "Tài khoản không còn tồn tại.";

                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }

            // Đồng bộ lại avatar vào Session mỗi lần mở Profile.
            RefreshAvatarSession(user.Id);

            var rentals = await _context.Rentals
                .AsNoTracking()
                .Where(rental =>
                    rental.UserId == userId.Value)
                .Include(rental => rental.Payment)
                .ToListAsync();

            var model = new ProfilePageViewModel
            {
                UserId = user.Id,

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
                    user.Role ??
                    string.Empty,

                IsLocked =
                    user.IsLocked,

                TotalRentals =
                    rentals.Count,

                BorrowingRentals =
                    rentals.Count(rental =>
                        string.Equals(
                            rental.Status,
                            "Borrowing",
                            StringComparison.OrdinalIgnoreCase
                        )
                    ),

                ReturnedRentals =
                    rentals.Count(rental =>
                        string.Equals(
                            rental.Status,
                            "Returned",
                            StringComparison.OrdinalIgnoreCase
                        )
                    ),

                TotalSpent =
                    rentals
                        .Where(rental =>
                            rental.Payment != null &&
                            (
                                string.Equals(
                                    rental.Payment.Status,
                                    "Paid",
                                    StringComparison.OrdinalIgnoreCase
                                )
                                ||
                                string.Equals(
                                    rental.Payment.Status,
                                    "Completed",
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                        )
                        .Sum(rental =>
                            rental.TotalAmount)
            };

            return View(
                "~/Views/Profile/Index.cshtml",
                model
            );
        }

        // =========================================================
        // CẬP NHẬT THÔNG TIN CÁ NHÂN
        // POST: /Profile/Update
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(
            UpdateProfileViewModel model)
        {
            var userId = GetCurrentUserId();

            if (userId == null)
            {
                TempData["ErrorMessage"] =
                    "Phiên đăng nhập đã hết hạn.";

                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] =
                    GetFirstModelError(
                        "Thông tin cập nhật không hợp lệ."
                    );

                return RedirectToAction(
                    nameof(Index)
                );
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(item =>
                    item.Id == userId.Value);

            if (user == null)
            {
                HttpContext.Session.Clear();

                TempData["ErrorMessage"] =
                    "Không tìm thấy tài khoản.";

                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }

            var normalizedEmail =
                model.Email.Trim();

            var duplicatedEmail =
                await _context.Users
                    .AnyAsync(item =>
                        item.Id != user.Id &&
                        item.Email == normalizedEmail);

            if (duplicatedEmail)
            {
                TempData["ErrorMessage"] =
                    "Email này đã được sử dụng bởi tài khoản khác.";

                return RedirectToAction(
                    nameof(Index)
                );
            }

            user.FullName =
                model.FullName.Trim();

            user.Email =
                normalizedEmail;

            user.Phone =
                string.IsNullOrWhiteSpace(model.Phone)
                    ? string.Empty
                    : model.Phone.Trim();

            await _context.SaveChangesAsync();

            HttpContext.Session.SetString(
                "FullName",
                user.FullName
            );

            TempData["SuccessMessage"] =
                "Cập nhật hồ sơ cá nhân thành công.";

            return RedirectToAction(
                nameof(Index)
            );
        }

        // =========================================================
        // NẾU TRUY CẬP TRỰC TIẾP URL UPLOAD BẰNG GET
        // -> đưa về trang Profile thay vì để người dùng mắc ở URL POST.
        // GET: /Profile/UploadAvatar
        // =========================================================
        [HttpGet]
        public IActionResult UploadAvatar()
        {
            return RedirectToAction(
                nameof(Index)
            );
        }

        // =========================================================
        // UPLOAD ẢNH ĐẠI DIỆN
        //
        // Ảnh KHÔNG lưu trong thư mục source/wwwroot.
        // Lưu ở:
        // %LOCALAPPDATA%\VDK_BookRental\avatars
        //
        // Cách này tránh việc Visual Studio/Hot Reload theo dõi
        // file mới trong project rồi làm gián đoạn web khi upload.
        //
        // POST: /Profile/UploadAvatar
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(6L * 1024 * 1024)]
        [RequestFormLimits(
            MultipartBodyLengthLimit =
                6L * 1024 * 1024)]
        public async Task<IActionResult> UploadAvatar(
            IFormFile? avatarFile)
        {
            var userId = GetCurrentUserId();

            if (userId == null)
            {
                TempData["ErrorMessage"] =
                    "Phiên đăng nhập đã hết hạn.";

                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }

            if (avatarFile == null ||
                avatarFile.Length <= 0)
            {
                TempData["ErrorMessage"] =
                    "Vui lòng chọn ảnh đại diện.";

                return RedirectToAction(
                    nameof(Index)
                );
            }

            if (avatarFile.Length > MaxAvatarSize)
            {
                TempData["ErrorMessage"] =
                    "Ảnh đại diện không được vượt quá 5 MB.";

                return RedirectToAction(
                    nameof(Index)
                );
            }

            var extension =
                Path.GetExtension(
                    avatarFile.FileName
                );

            if (string.IsNullOrWhiteSpace(extension) ||
                !AllowedAvatarExtensions.Contains(
                    extension))
            {
                TempData["ErrorMessage"] =
                    "Chỉ hỗ trợ JPG, JPEG, PNG hoặc WEBP.";

                return RedirectToAction(
                    nameof(Index)
                );
            }

            try
            {
                var userExists =
                    await _context.Users
                        .AsNoTracking()
                        .AnyAsync(item =>
                            item.Id == userId.Value);

                if (!userExists)
                {
                    HttpContext.Session.Clear();

                    TempData["ErrorMessage"] =
                        "Không tìm thấy tài khoản.";

                    return RedirectToAction(
                        "Login",
                        "Account"
                    );
                }

                var validSignature =
                    await HasValidImageSignatureAsync(
                        avatarFile,
                        extension
                    );

                if (!validSignature)
                {
                    TempData["ErrorMessage"] =
                        "Nội dung tệp không phải ảnh hợp lệ.";

                    return RedirectToAction(
                        nameof(Index)
                    );
                }

                var avatarDirectory =
                    GetAvatarDirectory();

                Directory.CreateDirectory(
                    avatarDirectory
                );

                var safeExtension =
                    extension.ToLowerInvariant();

                var finalFileName =
                    $"avatar_{userId.Value}{safeExtension}";

                var finalPath =
                    Path.Combine(
                        avatarDirectory,
                        finalFileName
                    );

                var tempPath =
                    Path.Combine(
                        avatarDirectory,
                        $"upload_{userId.Value}_{Guid.NewGuid():N}.tmp"
                    );

                try
                {
                    await using (
                        var output =
                            new FileStream(
                                tempPath,
                                FileMode.CreateNew,
                                FileAccess.Write,
                                FileShare.None,
                                bufferSize: 81920,
                                useAsync: true
                            )
                    )
                    {
                        await avatarFile.CopyToAsync(
                            output
                        );

                        await output.FlushAsync();
                    }

                    // Xóa avatar cũ của chính user.
                    foreach (
                        var oldPath
                        in Directory.EnumerateFiles(
                            avatarDirectory,
                            $"avatar_{userId.Value}.*"
                        )
                    )
                    {
                        if (!string.Equals(
                                oldPath,
                                finalPath,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            TryDeleteFile(oldPath);
                        }
                    }

                    System.IO.File.Move(
                        tempPath,
                        finalPath,
                        overwrite: true
                    );
                }
                finally
                {
                    // Nếu Copy/Move lỗi thì dọn file tạm.
                    TryDeleteFile(tempPath);
                }

                RefreshAvatarSession(
                    userId.Value
                );

                TempData["SuccessMessage"] =
                    "Cập nhật ảnh đại diện thành công.";

                return RedirectToAction(
                    nameof(Index)
                );
            }
            catch (Exception exception)
            {
                // Bắt lỗi để request upload không làm văng người dùng
                // ra khỏi toàn bộ hệ thống.
                _logger.LogError(
                    exception,
                    "Upload avatar thất bại cho UserId {UserId}.",
                    userId.Value
                );

                TempData["ErrorMessage"] =
                    "Không thể tải ảnh đại diện lên. " +
                    "Vui lòng thử lại bằng ảnh JPG/PNG/WEBP dưới 5 MB.";

                return RedirectToAction(
                    nameof(Index)
                );
            }
        }

        // =========================================================
        // TRẢ FILE AVATAR CHO TRÌNH DUYỆT
        // GET: /Profile/Avatar?userId=1
        // =========================================================
        [HttpGet]
        [ResponseCache(
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public IActionResult Avatar(
            int userId)
        {
            var currentUserId =
                GetCurrentUserId();

            if (currentUserId == null ||
                currentUserId.Value != userId)
            {
                return NotFound();
            }

            var avatarPath =
                FindAvatarFile(userId);

            if (avatarPath == null)
            {
                return NotFound();
            }

            var contentType =
                GetImageContentType(
                    Path.GetExtension(avatarPath)
                );

            return PhysicalFile(
                avatarPath,
                contentType,
                enableRangeProcessing: false
            );
        }

        // =========================================================
        // ĐỔI MẬT KHẨU
        // POST: /Profile/ChangePassword
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(
            ChangePasswordViewModel model)
        {
            var userId = GetCurrentUserId();

            if (userId == null)
            {
                TempData["ErrorMessage"] =
                    "Phiên đăng nhập đã hết hạn.";

                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] =
                    GetFirstModelError(
                        "Thông tin đổi mật khẩu không hợp lệ."
                    );

                return RedirectToAction(
                    nameof(Index)
                );
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(item =>
                    item.Id == userId.Value);

            if (user == null)
            {
                HttpContext.Session.Clear();

                TempData["ErrorMessage"] =
                    "Không tìm thấy tài khoản.";

                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }

            var currentPasswordIsValid =
                BCrypt.Net.BCrypt.Verify(
                    model.CurrentPassword,
                    user.PasswordHash
                );

            if (!currentPasswordIsValid)
            {
                TempData["ErrorMessage"] =
                    "Mật khẩu hiện tại không chính xác.";

                return RedirectToAction(
                    nameof(Index)
                );
            }

            var sameAsCurrentPassword =
                BCrypt.Net.BCrypt.Verify(
                    model.NewPassword,
                    user.PasswordHash
                );

            if (sameAsCurrentPassword)
            {
                TempData["ErrorMessage"] =
                    "Mật khẩu mới phải khác mật khẩu hiện tại.";

                return RedirectToAction(
                    nameof(Index)
                );
            }

            user.PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(
                    model.NewPassword
                );

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Đổi mật khẩu thành công.";

            return RedirectToAction(
                nameof(Index)
            );
        }

        // =========================================================
        // SESSION USER ID
        // =========================================================
        private int? GetCurrentUserId()
        {
            var userIdValue =
                HttpContext.Session.GetString(
                    "UserId"
                );

            if (string.IsNullOrWhiteSpace(
                    userIdValue))
            {
                return null;
            }

            if (!int.TryParse(
                    userIdValue,
                    out var userId))
            {
                return null;
            }

            return userId;
        }

        // =========================================================
        // AVATAR STORAGE
        // =========================================================
        private static string GetAvatarDirectory()
        {
            var localAppData =
                Environment.GetFolderPath(
                    Environment.SpecialFolder
                        .LocalApplicationData
                );

            if (string.IsNullOrWhiteSpace(
                    localAppData))
            {
                localAppData =
                    Path.GetTempPath();
            }

            return Path.Combine(
                localAppData,
                "VDK_BookRental",
                "avatars"
            );
        }

        private static string? FindAvatarFile(
            int userId)
        {
            var directory =
                GetAvatarDirectory();

            if (!Directory.Exists(directory))
            {
                return null;
            }

            var preferredExtensions =
                new[]
                {
                    ".jpg",
                    ".jpeg",
                    ".png",
                    ".webp"
                };

            foreach (
                var extension
                in preferredExtensions)
            {
                var candidate =
                    Path.Combine(
                        directory,
                        $"avatar_{userId}{extension}"
                    );

                if (System.IO.File.Exists(
                        candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private void RefreshAvatarSession(
            int userId)
        {
            var avatarPath =
                FindAvatarFile(userId);

            if (avatarPath == null)
            {
                HttpContext.Session.Remove(
                    "AvatarUrl"
                );

                return;
            }

            var version =
                System.IO.File
                    .GetLastWriteTimeUtc(
                        avatarPath)
                    .Ticks;

            var avatarUrl =
                Url.Action(
                    nameof(Avatar),
                    "Profile",
                    new
                    {
                        userId,
                        v = version
                    }
                );

            if (!string.IsNullOrWhiteSpace(
                    avatarUrl))
            {
                HttpContext.Session.SetString(
                    "AvatarUrl",
                    avatarUrl
                );
            }
        }

        // =========================================================
        // KIỂM TRA CHỮ KÝ FILE ẢNH
        // =========================================================
        private static async Task<bool>
            HasValidImageSignatureAsync(
                IFormFile file,
                string extension)
        {
            var header =
                new byte[12];

            await using var stream =
                file.OpenReadStream();

            var bytesRead =
                await stream.ReadAsync(
                    header.AsMemory(
                        0,
                        header.Length)
                );

            if (bytesRead < 4)
            {
                return false;
            }

            if (extension.Equals(
                    ".jpg",
                    StringComparison.OrdinalIgnoreCase)
                ||
                extension.Equals(
                    ".jpeg",
                    StringComparison.OrdinalIgnoreCase))
            {
                return
                    header[0] == 0xFF &&
                    header[1] == 0xD8 &&
                    header[2] == 0xFF;
            }

            if (extension.Equals(
                    ".png",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (bytesRead < 8)
                {
                    return false;
                }

                return
                    header[0] == 0x89 &&
                    header[1] == 0x50 &&
                    header[2] == 0x4E &&
                    header[3] == 0x47 &&
                    header[4] == 0x0D &&
                    header[5] == 0x0A &&
                    header[6] == 0x1A &&
                    header[7] == 0x0A;
            }

            if (extension.Equals(
                    ".webp",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (bytesRead < 12)
                {
                    return false;
                }

                return
                    header[0] == (byte)'R' &&
                    header[1] == (byte)'I' &&
                    header[2] == (byte)'F' &&
                    header[3] == (byte)'F' &&
                    header[8] == (byte)'W' &&
                    header[9] == (byte)'E' &&
                    header[10] == (byte)'B' &&
                    header[11] == (byte)'P';
            }

            return false;
        }

        private static string GetImageContentType(
            string extension)
        {
            return extension
                .ToLowerInvariant() switch
            {
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }

        private static void TryDeleteFile(
            string path)
        {
            try
            {
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }
            catch
            {
                // Không để lỗi dọn file phụ
                // làm hỏng request chính.
            }
        }

        // =========================================================
        // LỖI VALIDATION ĐẦU TIÊN
        // =========================================================
        private string GetFirstModelError(
            string defaultMessage)
        {
            var error =
                ModelState.Values
                    .SelectMany(value =>
                        value.Errors)
                    .Select(item =>
                        item.ErrorMessage)
                    .FirstOrDefault(message =>
                        !string.IsNullOrWhiteSpace(
                            message));

            return
                error ??
                defaultMessage;
        }
    }
}