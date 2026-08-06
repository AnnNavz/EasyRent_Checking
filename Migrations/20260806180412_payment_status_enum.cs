using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
    /// <inheritdoc />
    public partial class payment_status_enum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PaymentStatusEnum",
                table: "Payment",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Pending=0, Approved=1, Rejected=2
            migrationBuilder.Sql("""
                UPDATE Payment
                SET PaymentStatusEnum = CASE
                    WHEN PaymentStatus = 'Approved' THEN 1
                    WHEN PaymentStatus = 'Rejected' THEN 2
                    ELSE 0
                END
                """);

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Payment");

            migrationBuilder.RenameColumn(
                name: "PaymentStatusEnum",
                table: "Payment",
                newName: "PaymentStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentStatusText",
                table: "Payment",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.Sql("""
                UPDATE Payment
                SET PaymentStatusText = CASE PaymentStatus
                    WHEN 1 THEN 'Approved'
                    WHEN 2 THEN 'Rejected'
                    ELSE 'Pending'
                END
                """);

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Payment");

            migrationBuilder.RenameColumn(
                name: "PaymentStatusText",
                table: "Payment",
                newName: "PaymentStatus");
        }
    }
}
