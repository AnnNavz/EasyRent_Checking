using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
    /// <inheritdoc />
    public partial class add_reservation_payment_confirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AmountSent",
                table: "Reservation",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayerAccountName",
                table: "Reservation",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentChannel",
                table: "Reservation",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentDateTime",
                table: "Reservation",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentNotes",
                table: "Reservation",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentProofPath",
                table: "Reservation",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentReference",
                table: "Reservation",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmountSent",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "PayerAccountName",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "PaymentChannel",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "PaymentDateTime",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "PaymentNotes",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "PaymentProofPath",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "PaymentReference",
                table: "Reservation");
        }
    }
}
