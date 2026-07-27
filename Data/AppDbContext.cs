using Microsoft.EntityFrameworkCore;
using VDK_BookRental.Models;

namespace VDK_BookRental.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;

        public DbSet<Book> Books { get; set; } = null!;

        public DbSet<Category> Categories { get; set; } = null!;

        public DbSet<Rental> Rentals { get; set; } = null!;

        public DbSet<RentalDetail> RentalDetails { get; set; } = null!;

        public DbSet<Payment> Payments { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ==========================
            // Book
            // ==========================
            modelBuilder.Entity<Book>()
                .Property(b => b.RentalPrice)
                .HasPrecision(18, 2);

            // ==========================
            // Payment
            // ==========================
            modelBuilder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasPrecision(18, 2);

            // ==========================
            // Rental
            // ==========================
            modelBuilder.Entity<Rental>()
                .Property(r => r.TotalAmount)
                .HasPrecision(18, 2);

            // ==========================
            // RentalDetail
            // ==========================
            modelBuilder.Entity<RentalDetail>()
                .Property(rd => rd.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<RentalDetail>()
                .Property(rd => rd.SubTotal)
                .HasPrecision(18, 2);
        }
    }
}