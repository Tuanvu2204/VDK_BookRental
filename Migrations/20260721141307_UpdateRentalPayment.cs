using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VDK_BookRental.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRentalPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_RentalId",
                table: "Payments");

            migrationBuilder.AddColumn<int>(
                name: "RentalDays",
                table: "RentalDetails",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "SubTotal",
                table: "RentalDetails",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RentalId",
                table: "Payments",
                column: "RentalId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_RentalId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RentalDays",
                table: "RentalDetails");

            migrationBuilder.DropColumn(
                name: "SubTotal",
                table: "RentalDetails");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RentalId",
                table: "Payments",
                column: "RentalId");
        }
    }
}
