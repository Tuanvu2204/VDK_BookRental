using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VDK_BookRental.Models
{
    public class Book
    {
        // =====================================================
        // CONSTANT STATUS
        // =====================================================

        public const string AvailableStatus = "Available";
        public const string UnavailableStatus = "Unavailable";

        // =====================================================
        // ID
        // =====================================================

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
            ErrorMessage =
                "Giá thuê phải từ 0 đến 100.000.000 VNĐ.")]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Giá thuê mỗi ngày")]
        public decimal RentalPrice { get; set; }

        // =====================================================
        // SỐ LƯỢNG
        // =====================================================

        [Range(
            0,
            1000000,
            ErrorMessage =
                "Số lượng phải từ 0 đến 1.000.000.")]
        [Display(Name = "Số lượng trong kho")]
        public int Quantity { get; set; }

        // =====================================================
        // ẢNH BÌA
        // =====================================================

        [StringLength(
            500,
            ErrorMessage =
                "Đường dẫn ảnh không được vượt quá 500 ký tự.")]
        [Display(Name = "Ảnh bìa")]
        public string? ImageUrl { get; set; }

        // =====================================================
        // TRẠNG THÁI
        // =====================================================

        [Required]
        [StringLength(
            30,
            ErrorMessage =
                "Trạng thái không được vượt quá 30 ký tự.")]
        [Display(Name = "Trạng thái")]
        public string Status { get; set; } = AvailableStatus;

        // =====================================================
        // QUAN HỆ CATEGORY
        // =====================================================

        public Category? Category { get; set; }

        // =====================================================
        // THUỘC TÍNH HỖ TRỢ
        // KHÔNG LƯU DATABASE
        // =====================================================

        /// <summary>
        /// Sách có thể thuê hay không.
        /// </summary>
        [NotMapped]
        public bool IsAvailable =>
            Quantity > 0 &&
            string.Equals(
                Status,
                AvailableStatus,
                StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Sách sắp hết khi còn từ 1 - 3 cuốn.
        /// </summary>
        [NotMapped]
        public bool IsLowStock =>
            Quantity is > 0 and <= 3;

        /// <summary>
        /// Ảnh hiển thị.
        /// Nếu chưa có ảnh sẽ dùng ảnh mặc định.
        /// </summary>
        [NotMapped]
        public string DisplayImageUrl =>
            string.IsNullOrWhiteSpace(ImageUrl)
                ? "/images/books/default-book.jpg"
                : ImageUrl;

        /// <summary>
        /// Nội dung hiển thị tình trạng kho.
        /// </summary>
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

        /// <summary>
        /// Hiển thị trạng thái bằng tiếng Việt.
        /// </summary>
        [NotMapped]
        public string StatusDisplayText
        {
            get
            {
                if (Quantity <= 0)
                {
                    return "Hết sách";
                }

                return string.Equals(
                    Status,
                    AvailableStatus,
                    StringComparison.OrdinalIgnoreCase)
                    ? "Đang cho thuê"
                    : "Ngừng cho thuê";
            }
        }

        /// <summary>
        /// Bootstrap class dùng cho badge trạng thái.
        /// </summary>
        [NotMapped]
        public string StatusBadgeClass
        {
            get
            {
                if (Quantity <= 0)
                {
                    return "bg-danger";
                }

                if (IsLowStock)
                {
                    return "bg-warning text-dark";
                }

                if (IsAvailable)
                {
                    return "bg-success";
                }

                return "bg-secondary";
            }
        }

        // =====================================================
        // HÀM HỖ TRỢ
        // =====================================================

        /// <summary>
        /// Đồng bộ trạng thái dựa trên số lượng.
        /// </summary>
        public void SyncStockStatus()
        {
            if (Quantity <= 0)
            {
                Quantity = 0;
                Status = UnavailableStatus;
            }
            else if (string.IsNullOrWhiteSpace(Status))
            {
                Status = AvailableStatus;
            }
        }

        /// <summary>
        /// Chuẩn hóa dữ liệu trước khi lưu.
        /// </summary>
        public void Normalize()
        {
            Title = Title.Trim();
            Author = Author.Trim();

            Description =
                string.IsNullOrWhiteSpace(Description)
                    ? null
                    : Description.Trim();

            ImageUrl =
                string.IsNullOrWhiteSpace(ImageUrl)
                    ? null
                    : ImageUrl.Trim();

            Status =
                string.IsNullOrWhiteSpace(Status)
                    ? AvailableStatus
                    : Status.Trim();

            SyncStockStatus();
        }
    }
}