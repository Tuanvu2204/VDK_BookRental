using System.ComponentModel.DataAnnotations;

namespace VDK_BookRental.Models
{
    public class User
    {
        // =====================================================
        // KHÓA CHÍNH
        // =====================================================

        public int Id { get; set; }

        // =====================================================
        // THÔNG TIN TÀI KHOẢN
        // =====================================================

        [Required(
            ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
        [StringLength(
            50,
            MinimumLength = 3,
            ErrorMessage =
                "Tên đăng nhập phải từ 3 đến 50 ký tự.")]
        public string UserName { get; set; } =
            string.Empty;

        [Required(
            ErrorMessage = "Vui lòng nhập họ và tên.")]
        [StringLength(
            100,
            MinimumLength = 2,
            ErrorMessage =
                "Họ và tên phải từ 2 đến 100 ký tự.")]
        public string FullName { get; set; } =
            string.Empty;

        [Required(
            ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(
            ErrorMessage = "Email không đúng định dạng.")]
        [StringLength(
            150,
            ErrorMessage =
                "Email không được vượt quá 150 ký tự.")]
        public string Email { get; set; } =
            string.Empty;

        [RegularExpression(
            @"^(0[0-9]{9}|\+84[0-9]{9})$",
            ErrorMessage =
                "Số điện thoại phải có 10 số và bắt đầu bằng 0.")]
        [StringLength(
            12,
            ErrorMessage =
                "Số điện thoại không được vượt quá 12 ký tự.")]
        public string? Phone { get; set; }

        [Required]
        public string PasswordHash { get; set; } =
            string.Empty;

        [Required]
        [StringLength(20)]
        public string Role { get; set; } =
            "Customer";

        public bool IsLocked { get; set; } =
            false;

        public DateTime CreatedAt { get; set; } =
            DateTime.Now;

        // =====================================================
        // ẢNH ĐẠI DIỆN
        // DÙNG CHUNG ADMIN, STAFF VÀ CUSTOMER
        // =====================================================

        [StringLength(
            500,
            ErrorMessage =
                "Đường dẫn ảnh không được vượt quá 500 ký tự.")]
        public string? AvatarUrl { get; set; }

        // =====================================================
        // HỒ SƠ KHÁCH HÀNG
        // CHỈ HIỂN THỊ KHI ROLE = CUSTOMER
        // =====================================================

        [StringLength(
            250,
            ErrorMessage =
                "Địa chỉ không được vượt quá 250 ký tự.")]
        public string? Address { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [StringLength(
            20,
            ErrorMessage =
                "Giới tính không được vượt quá 20 ký tự.")]
        public string? Gender { get; set; }

        // =====================================================
        // QUAN HỆ
        // =====================================================

        public ICollection<Rental> Rentals { get; set; } =
            new List<Rental>();
    }
}