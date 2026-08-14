using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
    /// <inheritdoc />
    public partial class remove_payment_status : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Payment");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PaymentStatus",
                table: "Payment",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
