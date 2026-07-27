using System.ComponentModel.DataAnnotations;

namespace VDK_BookRental.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
        [StringLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tên tài khoản.")]
        [StringLength(50)]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [StringLength(150)]
        public string Email { get; set; } = string.Empty;

        [StringLength(20)]
        public string? Phone { get; set; }

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Role { get; set; } = "Customer";

        public bool IsLocked { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<Rental> Rentals { get; set; }
            = new List<Rental>();
    }
}