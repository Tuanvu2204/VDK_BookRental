using VDK_BookRental.Models;

namespace VDK_BookRental.ViewModels
{
    public class RentalContractViewModel
    {
        public Rental Rental { get; set; } = null!;

        public string ContractNumber { get; set; } =
            string.Empty;

        public DateTime CreatedAt { get; set; }

        public string CustomerName { get; set; } =
            string.Empty;

        public string CustomerPhone { get; set; } =
            string.Empty;

        public string CustomerEmail { get; set; } =
            string.Empty;

        public string CustomerAddress { get; set; } =
            string.Empty;

        public string PaymentMethod { get; set; } =
            string.Empty;
    }
}