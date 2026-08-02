using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VDK_BookRental.Data;
using VDK_BookRental.ViewModels;

namespace VDK_BookRental.Controllers
{
    public class ProfileController : Controller
    {
        private readonly AppDbContext _context;

        private readonly IWebHostEnvironment _environment;

        // Ảnh sau khi nén không được vượt quá 5 MB.
        private const long MaximumAvatarFileSize =
            5L * 1024 * 1024;

        // Cho phép request tối đa 10 MB để hỗ trợ fallback
        // trong trường hợp JavaScript không hoạt động.
        private const long MaximumRequestSize =
            10L * 1024 * 1024;

        public ProfileController(
            AppDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // =====================================================
        // TRANG HỒ SƠ
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId =
                GetCurrentUserId();

            if (userId == null)
            {
                TempData["ErrorMessage"] =
                    "Vui lòng đăng nhập để xem hồ sơ.";

                return RedirectToAction(
                    "Login",
                    "Account");
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
                    "Account");
            }

            var rentals = await _context.Rentals
                .AsNoTracking()
                .Where(rental =>
                    rental.UserId == userId.Value)
                .Include(rental =>
                    rental.Payment)
                .ToListAsync();

            var model =
                new ProfilePageViewModel
                {
                    UserId =
                        user.Id,

                    UserName =
                        user.UserName,

                    FullName =
                        user.FullName,

                    Email =
                        user.Email,

                    Phone =
                        user.Phone ??
                        string.Empty,

                    Role =
                        user.Role,

                    IsLocked =
                        user.IsLocked,

                    AvatarUrl =
                        user.AvatarUrl ??
                        string.Empty,

                    Address =
                        user.Address ??
                        string.Empty,

                    DateOfBirth =
                        user.DateOfBirth,

                    Gender =
                        user.Gender ??
                        string.Empty,

                    TotalRentals =
                        rentals.Count,

                    PendingRentals =
                        rentals.Count(rental =>
                            rental.Status == "Pending" ||
                            rental.Status == "Approved"),

                    BorrowingRentals =
                        rentals.Count(rental =>
                            rental.Status == "Borrowing"),

                    ReturnedRentals =
                        rentals.Count(rental =>
                            rental.Status == "Returned"),

                    TotalSpent =
                        rentals
                            .Where(rental =>
                                rental.Payment != null &&
                                (
                                    rental.Payment.Status ==
                                    "Paid"
                                    ||
                                    rental.Payment.Status ==
                                    "Completed"
                                ))
                            .Sum(rental =>
                                rental.TotalAmount)
                };

            return View(
                "~/Views/Profile/Index.cshtml",
                model);
        }

        // =====================================================
        // CẬP NHẬT THÔNG TIN
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(
            UpdateProfileViewModel model)
        {
            var userId =
                GetCurrentUserId();

            if (userId == null)
            {
                TempData["ErrorMessage"] =
                    "Phiên đăng nhập đã hết hạn.";

                return RedirectToAction(
                    "Login",
                    "Account");
            }

            if (model.DateOfBirth.HasValue &&
                model.DateOfBirth.Value.Date >
                DateTime.Today)
            {
                ModelState.AddModelError(
                    nameof(model.DateOfBirth),
                    "Ngày sinh không được lớn hơn ngày hiện tại.");
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] =
                    GetFirstModelError(
                        "Thông tin cập nhật không hợp lệ.");

                return RedirectToAction(
                    nameof(Index));
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
                    "Account");
            }

            var normalizedEmail =
                model.Email
                    .Trim()
                    .ToLowerInvariant();

            var emailExists =
                await _context.Users
                    .AsNoTracking()
                    .AnyAsync(item =>
                        item.Id != user.Id &&
                        item.Email.ToLower() ==
                        normalizedEmail);

            if (emailExists)
            {
                TempData["ErrorMessage"] =
                    "Email này đã được tài khoản khác sử dụng.";

                return RedirectToAction(
                    nameof(Index));
            }

            user.FullName =
                model.FullName.Trim();

            user.Email =
                normalizedEmail;

            user.Phone =
                string.IsNullOrWhiteSpace(
                    model.Phone)
                    ? null
                    : model.Phone.Trim();

            if (string.Equals(
                    user.Role,
                    "Customer",
                    StringComparison.OrdinalIgnoreCase))
            {
                user.Address =
                    string.IsNullOrWhiteSpace(
                        model.Address)
                        ? null
                        : model.Address.Trim();

                user.DateOfBirth =
                    model.DateOfBirth?.Date;

                user.Gender =
                    NormalizeGender(
                        model.Gender);
            }

            await _context.SaveChangesAsync();

            HttpContext.Session.SetString(
                "FullName",
                user.FullName);

            TempData["SuccessMessage"] =
                "Cập nhật hồ sơ thành công.";

            return RedirectToAction(
                nameof(Index));
        }

        // =====================================================
        // UPLOAD ẢNH ĐẠI DIỆN
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaximumRequestSize)]
        [RequestFormLimits(
            MultipartBodyLengthLimit =
                MaximumRequestSize)]
        public async Task<IActionResult> UploadAvatar(
            UploadAvatarViewModel model)
        {
            var userId =
                GetCurrentUserId();

            if (userId == null)
            {
                return CreateUploadResponse(
                    false,
                    "Phiên đăng nhập đã hết hạn.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            ModelState.Clear();

            var validation =
                await ValidateAvatarAsync(
                    model.AvatarFile);

            if (!validation.IsValid)
            {
                return CreateUploadResponse(
                    false,
                    validation.Message);
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(item =>
                    item.Id == userId.Value);

            if (user == null)
            {
                HttpContext.Session.Clear();

                return CreateUploadResponse(
                    false,
                    "Không tìm thấy tài khoản.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            var oldAvatarUrl =
                user.AvatarUrl;

            string? newAvatarUrl = null;

            try
            {
                newAvatarUrl =
                    await SaveAvatarSafelyAsync(
                        user.Id,
                        model.AvatarFile!,
                        validation.Extension!);

                user.AvatarUrl =
                    newAvatarUrl;

                await _context.SaveChangesAsync();

                // Chỉ xóa ảnh cũ sau khi DB lưu thành công.
                DeleteAvatarFile(
                    oldAvatarUrl);

                HttpContext.Session.SetString(
                    "AvatarUrl",
                    newAvatarUrl);

                return CreateUploadResponse(
                    true,
                    "Cập nhật ảnh đại diện thành công.",
                    newAvatarUrl);
            }
            catch (DbUpdateException exception)
            {
                DeleteAvatarFile(
                    newAvatarUrl);

                Console.Error.WriteLine(
                    $"Lỗi database khi lưu avatar: {exception}");

                return CreateUploadResponse(
                    false,
                    "Database không thể lưu ảnh đại diện. " +
                    "Vui lòng thử lại.");
            }
            catch (UnauthorizedAccessException exception)
            {
                DeleteAvatarFile(
                    newAvatarUrl);

                Console.Error.WriteLine(
                    $"Không có quyền ghi avatar: {exception}");

                return CreateUploadResponse(
                    false,
                    "Ứng dụng không có quyền ghi vào thư mục ảnh.");
            }
            catch (IOException exception)
            {
                DeleteAvatarFile(
                    newAvatarUrl);

                Console.Error.WriteLine(
                    $"Lỗi tệp avatar: {exception}");

                return CreateUploadResponse(
                    false,
                    "Không thể lưu tệp ảnh. " +
                    "Tệp có thể đang bị khóa hoặc thư mục không khả dụng.");
            }
            catch (Exception exception)
            {
                DeleteAvatarFile(
                    newAvatarUrl);

                Console.Error.WriteLine(
                    $"Lỗi không xác định khi upload avatar: {exception}");

                return CreateUploadResponse(
                    false,
                    "Đã xảy ra lỗi khi cập nhật ảnh đại diện.");
            }
        }

        // =====================================================
        // XÓA ẢNH ĐẠI DIỆN
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAvatar()
        {
            var userId =
                GetCurrentUserId();

            if (userId == null)
            {
                TempData["ErrorMessage"] =
                    "Phiên đăng nhập đã hết hạn.";

                return RedirectToAction(
                    "Login",
                    "Account");
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
                    "Account");
            }

            if (string.IsNullOrWhiteSpace(
                    user.AvatarUrl))
            {
                TempData["InfoMessage"] =
                    "Tài khoản chưa có ảnh đại diện.";

                return RedirectToAction(
                    nameof(Index));
            }

            var oldAvatarUrl =
                user.AvatarUrl;

            user.AvatarUrl = null;

            await _context.SaveChangesAsync();

            DeleteAvatarFile(
                oldAvatarUrl);

            HttpContext.Session.Remove(
                "AvatarUrl");

            TempData["SuccessMessage"] =
                "Đã xóa ảnh đại diện.";

            return RedirectToAction(
                nameof(Index));
        }

        // =====================================================
        // ĐỔI MẬT KHẨU
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(
            ChangePasswordViewModel model)
        {
            var userId =
                GetCurrentUserId();

            if (userId == null)
            {
                TempData["ErrorMessage"] =
                    "Phiên đăng nhập đã hết hạn.";

                return RedirectToAction(
                    "Login",
                    "Account");
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] =
                    GetFirstModelError(
                        "Thông tin đổi mật khẩu không hợp lệ.");

                return RedirectToAction(
                    nameof(Index));
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
                    "Account");
            }

            var currentPasswordIsValid =
                BCrypt.Net.BCrypt.Verify(
                    model.CurrentPassword,
                    user.PasswordHash);

            if (!currentPasswordIsValid)
            {
                TempData["ErrorMessage"] =
                    "Mật khẩu hiện tại không chính xác.";

                return RedirectToAction(
                    nameof(Index));
            }

            var sameAsCurrentPassword =
                BCrypt.Net.BCrypt.Verify(
                    model.NewPassword,
                    user.PasswordHash);

            if (sameAsCurrentPassword)
            {
                TempData["ErrorMessage"] =
                    "Mật khẩu mới phải khác mật khẩu hiện tại.";

                return RedirectToAction(
                    nameof(Index));
            }

            user.PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(
                    model.NewPassword);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Đổi mật khẩu thành công.";

            return RedirectToAction(
                nameof(Index));
        }

        // =====================================================
        // KIỂM TRA ẢNH THẬT BẰNG FILE SIGNATURE
        // =====================================================

        private static async Task<AvatarValidationResult>
            ValidateAvatarAsync(
                IFormFile? avatarFile)
        {
            if (avatarFile == null)
            {
                return AvatarValidationResult.Fail(
                    "Vui lòng chọn ảnh đại diện.");
            }

            if (avatarFile.Length <= 0)
            {
                return AvatarValidationResult.Fail(
                    "Tệp ảnh không có dữ liệu.");
            }

            if (avatarFile.Length >
                MaximumAvatarFileSize)
            {
                return AvatarValidationResult.Fail(
                    "Ảnh sau khi xử lý không được vượt quá 5 MB.");
            }

            string? detectedExtension;

            try
            {
                detectedExtension =
                    await DetectImageExtensionAsync(
                        avatarFile);
            }
            catch
            {
                return AvatarValidationResult.Fail(
                    "Không thể đọc nội dung tệp ảnh.");
            }

            if (detectedExtension == null)
            {
                return AvatarValidationResult.Fail(
                    "Tệp không phải ảnh JPG, PNG hoặc WEBP hợp lệ.");
            }

            return AvatarValidationResult.Success(
                detectedExtension);
        }

        private static async Task<string?>
            DetectImageExtensionAsync(
                IFormFile file)
        {
            var header =
                new byte[12];

            await using var stream =
                file.OpenReadStream();

            var bytesRead =
                await stream.ReadAsync(
                    header.AsMemory(
                        0,
                        header.Length));

            // JPEG: FF D8 FF
            if (bytesRead >= 3 &&
                header[0] == 0xFF &&
                header[1] == 0xD8 &&
                header[2] == 0xFF)
            {
                return ".jpg";
            }

            // PNG: 89 50 4E 47 0D 0A 1A 0A
            if (bytesRead >= 8 &&
                header[0] == 0x89 &&
                header[1] == 0x50 &&
                header[2] == 0x4E &&
                header[3] == 0x47 &&
                header[4] == 0x0D &&
                header[5] == 0x0A &&
                header[6] == 0x1A &&
                header[7] == 0x0A)
            {
                return ".png";
            }

            // WEBP: RIFF....WEBP
            if (bytesRead >= 12 &&
                header[0] == 0x52 &&
                header[1] == 0x49 &&
                header[2] == 0x46 &&
                header[3] == 0x46 &&
                header[8] == 0x57 &&
                header[9] == 0x45 &&
                header[10] == 0x42 &&
                header[11] == 0x50)
            {
                return ".webp";
            }

            return null;
        }

        // =====================================================
        // LƯU ẢNH BẰNG FILE TẠM
        // =====================================================

        private async Task<string>
            SaveAvatarSafelyAsync(
                int userId,
                IFormFile avatarFile,
                string extension)
        {
            var avatarDirectory =
                GetAvatarDirectory();

            Directory.CreateDirectory(
                avatarDirectory);

            var uniqueName =
                $"avatar_{userId}_" +
                $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_" +
                $"{Guid.NewGuid():N}{extension}";

            var finalPath =
                Path.Combine(
                    avatarDirectory,
                    uniqueName);

            var temporaryPath =
                finalPath + ".uploading";

            try
            {
                await using (
                    var stream =
                        new FileStream(
                            temporaryPath,
                            FileMode.CreateNew,
                            FileAccess.Write,
                            FileShare.None,
                            bufferSize: 81920,
                            useAsync: true))
                {
                    await avatarFile.CopyToAsync(
                        stream);

                    await stream.FlushAsync();
                }

                System.IO.File.Move(
                    temporaryPath,
                    finalPath);

                return
                    $"/images/avatars/{uniqueName}";
            }
            catch
            {
                if (System.IO.File.Exists(
                        temporaryPath))
                {
                    System.IO.File.Delete(
                        temporaryPath);
                }

                throw;
            }
        }

        // =====================================================
        // XÓA ẢNH AN TOÀN
        // =====================================================

        private void DeleteAvatarFile(
            string? avatarUrl)
        {
            if (string.IsNullOrWhiteSpace(
                    avatarUrl))
            {
                return;
            }

            try
            {
                var cleanUrl =
                    avatarUrl.Split(
                        new[] { '?', '#' },
                        2)[0];

                var fileName =
                    Path.GetFileName(
                        cleanUrl);

                if (string.IsNullOrWhiteSpace(
                        fileName))
                {
                    return;
                }

                var avatarDirectory =
                    GetAvatarDirectory();

                var fullDirectory =
                    Path.GetFullPath(
                        avatarDirectory +
                        Path.DirectorySeparatorChar);

                var fullFilePath =
                    Path.GetFullPath(
                        Path.Combine(
                            avatarDirectory,
                            fileName));

                if (!fullFilePath.StartsWith(
                        fullDirectory,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (System.IO.File.Exists(
                        fullFilePath))
                {
                    System.IO.File.Delete(
                        fullFilePath);
                }
            }
            catch (Exception exception)
            {
                // Không làm sập request chỉ vì ảnh cũ
                // không thể xóa.
                Console.Error.WriteLine(
                    $"Không thể xóa avatar cũ: {exception.Message}");
            }
        }

        private string GetAvatarDirectory()
        {
            var webRootPath =
                !string.IsNullOrWhiteSpace(
                    _environment.WebRootPath)
                    ? _environment.WebRootPath
                    : Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot");

            return Path.Combine(
                webRootPath,
                "images",
                "avatars");
        }

        // =====================================================
        // PHẢN HỒI AJAX HOẶC FORM THÔNG THƯỜNG
        // =====================================================

        private IActionResult CreateUploadResponse(
            bool success,
            string message,
            string? avatarUrl = null,
            int statusCode =
                StatusCodes.Status200OK)
        {
            if (IsAjaxRequest())
            {
                Response.StatusCode =
                    statusCode;

                return Json(new
                {
                    success,
                    message,
                    avatarUrl
                });
            }

            TempData[
                success
                    ? "SuccessMessage"
                    : "ErrorMessage"
            ] = message;

            if (statusCode ==
                StatusCodes.Status401Unauthorized)
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            return RedirectToAction(
                nameof(Index));
        }

        private bool IsAjaxRequest()
        {
            return string.Equals(
                Request.Headers[
                    "X-Requested-With"],
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase);
        }

        private int? GetCurrentUserId()
        {
            var userIdValue =
                HttpContext.Session.GetString(
                    "UserId");

            return int.TryParse(
                userIdValue,
                out var userId)
                    ? userId
                    : null;
        }

        private string GetFirstModelError(
            string defaultMessage)
        {
            return ModelState.Values
                .SelectMany(value =>
                    value.Errors)
                .Select(error =>
                    error.ErrorMessage)
                .FirstOrDefault(message =>
                    !string.IsNullOrWhiteSpace(
                        message))
                ?? defaultMessage;
        }

        private static string? NormalizeGender(
            string? gender)
        {
            return gender?.Trim() switch
            {
                "Nam" => "Nam",
                "Nữ" => "Nữ",
                "Khác" => "Khác",
                _ => null
            };
        }

        private sealed class AvatarValidationResult
        {
            public bool IsValid { get; init; }

            public string Message { get; init; } =
                string.Empty;

            public string? Extension { get; init; }

            public static AvatarValidationResult Success(
                string extension)
            {
                return new AvatarValidationResult
                {
                    IsValid = true,
                    Extension = extension
                };
            }

            public static AvatarValidationResult Fail(
                string message)
            {
                return new AvatarValidationResult
                {
                    IsValid = false,
                    Message = message
                };
            }
        }
    }
}