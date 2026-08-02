using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VDK_BookRental.Models
{
    public class Book
    {
        public int Id { get; set; }

        // =====================================================
        // TÊN SÁCH
        // =====================================================

        [Required(ErrorMessage = "Vui lòng nhập tên sách.")]
        [StringLength(
            200,
            MinimumLength = 2,
            ErrorMessage = "Tên sách phải từ 2 đến 200 ký tự.")]
        [Display(Name = "Tên sách")]
        public string Title { get; set; } = string.Empty;

        // =====================================================
        // TÁC GIẢ
        // =====================================================

        [Required(ErrorMessage = "Vui lòng nhập tên tác giả.")]
        [StringLength(
            150,
            MinimumLength = 2,
            ErrorMessage = "Tên tác giả phải từ 2 đến 150 ký tự.")]
        [Display(Name = "Tác giả")]
        public string Author { get; set; } = string.Empty;

        // =====================================================
        // MÔ TẢ
        // =====================================================

        [StringLength(
            2000,
            ErrorMessage = "Mô tả không được vượt quá 2.000 ký tự.")]
        [Display(Name = "Mô tả sách")]
        public string? Description { get; set; }

        // =====================================================
        // THỂ LOẠI
        // =====================================================

        [Range(
            1,
            int.MaxValue,
            ErrorMessage = "Vui lòng chọn thể loại sách.")]
        [Display(Name = "Thể loại")]
        public int CategoryId { get; set; }

        // =====================================================
        // GIÁ THUÊ
        // =====================================================

        [Range(
            typeof(decimal),
            "0",
            "100000000",
            ErrorMessage = "Giá thuê phải từ 0 đến 100.000.000 VNĐ.")]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Giá thuê mỗi ngày")]
        public decimal RentalPrice { get; set; }

        // =====================================================
        // SỐ LƯỢNG
        // =====================================================

        [Range(
            0,
            1000000,
            ErrorMessage = "Số lượng phải từ 0 đến 1.000.000.")]
        [Display(Name = "Số lượng trong kho")]
        public int Quantity { get; set; }

        // =====================================================
        // ẢNH BÌA
        // =====================================================

        [StringLength(
            500,
            ErrorMessage = "Đường dẫn ảnh không được vượt quá 500 ký tự.")]
        [Display(Name = "Ảnh bìa")]
        public string? ImageUrl { get; set; }

        // =====================================================
        // TRẠNG THÁI
        // =====================================================

        [Required]
        [StringLength(
            30,
            ErrorMessage = "Trạng thái không được vượt quá 30 ký tự.")]
        [Display(Name = "Trạng thái")]
        public string Status { get; set; } = "Available";

        // =====================================================
        // QUAN HỆ VỚI CATEGORY
        // =====================================================

        public Category? Category { get; set; }

        // =====================================================
        // THUỘC TÍNH HỖ TRỢ, KHÔNG LƯU DATABASE
        // =====================================================

        [NotMapped]
        public bool IsAvailable =>
            Quantity > 0 &&
            string.Equals(
                Status,
                "Available",
                StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public bool IsLowStock =>
            Quantity > 0 &&
            Quantity <= 3;

        [NotMapped]
        public string DisplayImageUrl =>
            string.IsNullOrWhiteSpace(ImageUrl)
                ? "/images/books/default-book.jpg"
                : ImageUrl;

        [NotMapped]
        public string StockDisplayText
        {
            get
            {
                if (Quantity <= 0)
                {
                    return "Hết sách";
                }

                if (Quantity <= 3)
                {
                    return "Sắp hết";
                }

                return "Còn sách";
            }
        }
    }
}