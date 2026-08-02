namespace VDK_BookRental.ViewModels
{
    public class ReportViewModel
    {
        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public decimal TotalRevenue { get; set; }

        public int TotalRentals { get; set; }

        public int PaidPayments { get; set; }

        public int PendingPayments { get; set; }

        public int BorrowingRentals { get; set; }

        public int ReturnedRentals { get; set; }

        public int CancelledRentals { get; set; }

        public List<MonthlyRevenueItem> MonthlyRevenue { get; set; }
            = new List<MonthlyRevenueItem>();

        public List<RentalStatusItem> RentalStatuses { get; set; }
            = new List<RentalStatusItem>();

        public List<TopBookItem> TopBooks { get; set; }
            = new List<TopBookItem>();

        public List<RecentRevenueItem> RecentTransactions { get; set; }
            = new List<RecentRevenueItem>();
    }

    public class MonthlyRevenueItem
    {
        public string Label { get; set; } = string.Empty;

        public decimal Revenue { get; set; }

        public int RentalCount { get; set; }
    }

    public class RentalStatusItem
    {
        public string Status { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public int Count { get; set; }
    }

    public class TopBookItem
    {
        public int BookId { get; set; }

        public string Title { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public decimal Revenue { get; set; }
    }

    public class RecentRevenueItem
    {
        public int RentalId { get; set; }

        public int PaymentId { get; set; }

        public string CustomerName { get; set; } = string.Empty;

        public string PaymentMethod { get; set; } = string.Empty;

        public DateTime RentalDate { get; set; }

        public decimal Amount { get; set; }

        public string PaymentStatus { get; set; } = string.Empty;
    }
}
