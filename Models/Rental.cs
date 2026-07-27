namespace VDK_BookRental.Models
{
    public class Rental
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public DateTime RentalDate { get; set; } = DateTime.Now;

        public DateTime ReturnDate { get; set; }

        public decimal TotalAmount { get; set; }

        public string Status { get; set; } = "Pending";

        public User? User { get; set; }

        public ICollection<RentalDetail> RentalDetails { get; set; } = new List<RentalDetail>();

        public Payment? Payment { get; set; }
    }
}