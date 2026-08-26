using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
    /// <inheritdoc />
    public partial class add_rental_fare_totals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SucceedingFeeTotal",
                table: "Rental",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                table: "Rental",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            // Backfill locked-in total from the latest payment when present.
            migrationBuilder.Sql(@"
UPDATE r
SET r.TotalAmount = p.TotalAmount
FROM Rental r
INNER JOIN (
    SELECT RentalId, TotalAmount,
           ROW_NUMBER() OVER (PARTITION BY RentalId ORDER BY PaymentId DESC) AS rn
    FROM Payment
) p ON p.RentalId = r.RentalId AND p.rn = 1
WHERE r.TotalAmount = 0 AND p.TotalAmount > 0;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SucceedingFeeTotal",
                table: "Rental");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                table: "Rental");
        }
    }
}
