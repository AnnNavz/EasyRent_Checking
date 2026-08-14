using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
    /// <inheritdoc />
    public partial class add_rental_payment_due_and_expired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentDueAt",
                table: "Rental",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentDueAt",
                table: "Rental");
        }
    }
}
