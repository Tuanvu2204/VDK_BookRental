using Microsoft.EntityFrameworkCore;
using VDK_BookRental.Models;

namespace VDK_BookRental.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(
            DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;

        public DbSet<Book> Books { get; set; } = null!;

        public DbSet<Category> Categories { get; set; } = null!;

        public DbSet<Rental> Rentals { get; set; } = null!;

        public DbSet<RentalDetail> RentalDetails { get; set; } = null!;

        public DbSet<Payment> Payments { get; set; } = null!;

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ========================================
            // ĐỘ CHÍNH XÁC CỦA CÁC TRƯỜNG TIỀN
            // ========================================

            modelBuilder.Entity<Book>()
                .Property(book => book.RentalPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Rental>()
                .Property(rental => rental.TotalAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<RentalDetail>()
                .Property(detail => detail.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<RentalDetail>()
                .Property(detail => detail.SubTotal)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Payment>()
                .Property(payment => payment.Amount)
                .HasPrecision(18, 2);

            // ========================================
            // KHÔNG CHO TRÙNG TÀI KHOẢN VÀ EMAIL
            // ========================================

            modelBuilder.Entity<User>()
                .HasIndex(user => user.UserName)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(user => user.Email)
                .IsUnique();

            // Một đơn thuê chỉ có một thanh toán
            modelBuilder.Entity<Payment>()
                .HasIndex(payment => payment.RentalId)
                .IsUnique();

            // ========================================
            // RENTAL - PAYMENT: QUAN HỆ 1 - 1
            // ========================================

            modelBuilder.Entity<Rental>()
                .HasOne(rental => rental.Payment)
                .WithOne(payment => payment.Rental)
                .HasForeignKey<Payment>(
                    payment => payment.RentalId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}