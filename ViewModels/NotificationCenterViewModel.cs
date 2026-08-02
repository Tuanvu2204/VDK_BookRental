namespace VDK_BookRental.ViewModels
{
    public class NotificationCenterViewModel
    {
        public int PendingRentalCount { get; set; }

        public int AwaitingPaymentCount { get; set; }

        public int OverdueRentalCount { get; set; }

        public int LowStockBookCount { get; set; }

        public List<PendingRentalNotificationItem> PendingRentals { get; set; }
            = new List<PendingRentalNotificationItem>();

        public List<AwaitingPaymentNotificationItem> AwaitingPayments { get; set; }
            = new List<AwaitingPaymentNotificationItem>();

        public List<OverdueNotificationItem> OverdueRentals { get; set; }
            = new List<OverdueNotificationItem>();

        public List<LowStockNotificationItem> LowStockBooks { get; set; }
            = new List<LowStockNotificationItem>();
    }

    public class PendingRentalNotificationItem
    {
        public int RentalId { get; set; }

        public string CustomerName { get; set; } = string.Empty;

        public DateTime RentalDate { get; set; }

        public decimal TotalAmount { get; set; }
    }

    public class AwaitingPaymentNotificationItem
    {
        public int PaymentId { get; set; }

        public int RentalId { get; set; }

        public string CustomerName { get; set; } = string.Empty;

        public string PaymentMethod { get; set; } = string.Empty;

        public decimal Amount { get; set; }
    }

    public class OverdueNotificationItem
    {
        public int RentalId { get; set; }

        public string CustomerName { get; set; } = string.Empty;

        public DateTime ReturnDate { get; set; }

        public int OverdueDays { get; set; }
    }

    public class LowStockNotificationItem
    {
        public int BookId { get; set; }

        public string Title { get; set; } = string.Empty;

        public int Quantity { get; set; }
    }
}