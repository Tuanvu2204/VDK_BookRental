using System.ComponentModel.DataAnnotations;

namespace VDK_BookRental.Models
{
    public class Book
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        public string Author { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int CategoryId { get; set; }

        public decimal RentalPrice { get; set; }

        public int Quantity { get; set; }

        public string? ImageUrl { get; set; }

        public string Status { get; set; } = "Available";

        public Category? Category { get; set; }
    }
}