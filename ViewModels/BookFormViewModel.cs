using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace VDK_BookRental.ViewModels
{
    public class BookFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên sách.")]
        [StringLength(
            200,
            MinimumLength = 2,
            ErrorMessage = "Tên sách phải có từ 2 đến 200 ký tự.")]
        [Display(Name = "Tên sách")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tên tác giả.")]
        [StringLength(
            150,
            MinimumLength = 2,
            ErrorMessage = "Tên tác giả phải có từ 2 đến 150 ký tự.")]
        [Display(Name = "Tác giả")]
        public string Author { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn danh mục.")]
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn danh mục.")]
        [Display(Name = "Danh mục")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập giá thuê.")]
        [Range(
            0,
            100000000,
            ErrorMessage = "Giá thuê phải từ 0 đến 100.000.000 VNĐ.")]
        [Display(Name = "Giá thuê mỗi ngày")]
        public decimal RentalPrice { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số lượng.")]
        [Range(
            0,
            100000,
            ErrorMessage = "Số lượng phải từ 0 đến 100.000.")]
        [Display(Name = "Số lượng")]
        public int Quantity { get; set; }

        [StringLength(
            3000,
            ErrorMessage = "Mô tả không được vượt quá 3.000 ký tự.")]
        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        /*
         * File ảnh được người dùng chọn bằng File Explorer.
         * Thuộc tính này không được lưu trực tiếp vào database.
         */
        [Display(Name = "Ảnh bìa sách")]
        public IFormFile? ImageFile { get; set; }

        /*
         * Dùng khi sửa sách:
         * lưu lại đường dẫn ảnh hiện tại nếu không chọn ảnh mới.
         */
        public string? ExistingImageUrl { get; set; }
    }
}