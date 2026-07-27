namespace VDK_BookRental.Models
{
    public class RentalDetail
    {
        public int Id { get; set; }

        public int RentalId { get; set; }

        public int BookId { get; set; }

        // Số lượng sách thuê
        public int Quantity { get; set; } = 1;

        // Giá thuê tại thời điểm đặt
        public decimal Price { get; set; }

        // Số ngày thuê
        public int RentalDays { get; set; }

        // Thành tiền
        public decimal SubTotal { get; set; }

        public Rental? Rental { get; set; }

        public Book? Book { get; set; }
    }
}