using System.ComponentModel.DataAnnotations;

namespace VDK_BookRental.ViewModels
{
    public class ProfilePageViewModel
    {
        public int UserId { get; set; }

        public string UserName { get; set; } =
            string.Empty;

        public string FullName { get; set; } =
            string.Empty;

        public string Email { get; set; } =
            string.Empty;

        public string Phone { get; set; } =
            string.Empty;

        public string Role { get; set; } =
            string.Empty;

        public bool IsLocked { get; set; }

        public int TotalRentals { get; set; }

        public int BorrowingRentals { get; set; }

        public int ReturnedRentals { get; set; }

        public decimal TotalSpent { get; set; }
    }

    public class UpdateProfileViewModel
    {
        [Required(
            ErrorMessage =
                "Vui lòng nhập họ và tên.")]
        [StringLength(
            100,
            MinimumLength = 2,
            ErrorMessage =
                "Họ và tên phải từ 2 đến 100 ký tự."
        )]
        public string FullName { get; set; } =
            string.Empty;

        [Required(
            ErrorMessage =
                "Vui lòng nhập email.")]
        [EmailAddress(
            ErrorMessage =
                "Email không đúng định dạng.")]
        [StringLength(
            150,
            ErrorMessage =
                "Email không được vượt quá 150 ký tự."
        )]
        public string Email { get; set; } =
            string.Empty;

        [RegularExpression(
            @"^(0|\+84)[0-9]{9}$",
            ErrorMessage =
                "Số điện thoại phải có 10 số và bắt đầu bằng 0."
        )]
        public string? Phone { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required(
            ErrorMessage =
                "Vui lòng nhập mật khẩu hiện tại.")]
        public string CurrentPassword { get; set; } =
            string.Empty;

        [Required(
            ErrorMessage =
                "Vui lòng nhập mật khẩu mới.")]
        [StringLength(
            100,
            MinimumLength = 6,
            ErrorMessage =
                "Mật khẩu mới phải có ít nhất 6 ký tự."
        )]
        public string NewPassword { get; set; } =
            string.Empty;

        [Required(
            ErrorMessage =
                "Vui lòng xác nhận mật khẩu mới.")]
        [Compare(
            nameof(NewPassword),
            ErrorMessage =
                "Mật khẩu xác nhận không khớp."
        )]
        public string ConfirmPassword { get; set; } =
            string.Empty;
    }
}