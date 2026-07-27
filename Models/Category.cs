using System.ComponentModel.DataAnnotations;

namespace VDK_BookRental.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public ICollection<Book>? Books { get; set; }
    }
}