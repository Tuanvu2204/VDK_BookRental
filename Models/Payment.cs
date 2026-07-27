namespace VDK_BookRental.Models
{
    public class Payment
    {
        public int Id { get; set; }

        public int RentalId { get; set; }

        public decimal Amount { get; set; }

        public string PaymentMethod { get; set; } = "QR";

        public string? QrCodeUrl { get; set; }

        public string? TransferContent { get; set; }

        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Rental? Rental { get; set; }
    }
}