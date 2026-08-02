using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using VDK_BookRental.Models;

namespace VDK_BookRental.ViewModels
{
    public class AdminBookListViewModel
    {
        public string Search { get; set; } = string.Empty;

        public int? CategoryId { get; set; }

        public string StockStatus { get; set; } = string.Empty;

        public int PageNumber { get; set; } = 1;

        public int PageSize { get; set; } = 8;

        public int TotalItems { get; set; }

        public int TotalPages { get; set; } = 1;

        public int TotalBooks { get; set; }

        public int AvailableBooks { get; set; }

        public int LowStockBooks { get; set; }

        public int OutOfStockBooks { get; set; }

        public List<Book> Books { get; set; } = new();

        public List<Category> Categories { get; set; } = new();

        public bool HasPreviousPage =>
            PageNumber > 1;

        public bool HasNextPage =>
            PageNumber < TotalPages;
    }

    public class AdminBookFormViewModel
    {
        public int Id { get; set; }

        [Required(
            ErrorMessage = "Vui lòng nhập tên sách.")]
        [StringLength(
            200,
            MinimumLength = 2,
            ErrorMessage = "Tên sách phải từ 2 đến 200 ký tự.")]
        [Display(Name = "Tên sách")]
        public string Title { get; set; } = string.Empty;

        [Required(
            ErrorMessage = "Vui lòng nhập tên tác giả.")]
        [StringLength(
            150,
            MinimumLength = 2,
            ErrorMessage = "Tên tác giả phải từ 2 đến 150 ký tự.")]
        [Display(Name = "Tác giả")]
        public string Author { get; set; } = string.Empty;

        [Range(
            1,
            int.MaxValue,
            ErrorMessage = "Vui lòng chọn thể loại.")]
        [Display(Name = "Thể loại")]
        public int CategoryId { get; set; }

        [Range(
            typeof(decimal),
            "0",
            "100000000",
            ErrorMessage =
                "Giá thuê phải từ 0 đến 100.000.000 VNĐ.")]
        [Display(Name = "Giá thuê mỗi ngày")]
        public decimal RentalPrice { get; set; }

        [Range(
            0,
            1000000,
            ErrorMessage =
                "Số lượng phải từ 0 đến 1.000.000.")]
        [Display(Name = "Số lượng trong kho")]
        public int Quantity { get; set; }

        [Display(Name = "Ảnh bìa")]
        public IFormFile? ImageFile { get; set; }

        public string ExistingImageUrl { get; set; } =
            "/images/books/default-book.jpg";

        public List<SelectListItem> Categories { get; set; } =
            new();
    }
}