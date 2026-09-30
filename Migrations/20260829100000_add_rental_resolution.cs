using EasyRent_Checking.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
	/// <inheritdoc />
	[DbContext(typeof(EasyRent_CheckingContext))]
	[Migration("20260829100000_add_rental_resolution")]
	public class add_rental_resolution : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
				name: "CompanyCancellationReason",
				table: "Rental",
				type: "nvarchar(500)",
				maxLength: 500,
				nullable: true);

			migrationBuilder.AddColumn<decimal>(
				name: "RefundAmount",
				table: "Rental",
				type: "decimal(18,2)",
				nullable: true);

			migrationBuilder.AddColumn<DateTime>(
				name: "RefundedAt",
				table: "Rental",
				type: "datetime2",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "RefundReceiptImagePath",
				table: "Rental",
				type: "nvarchar(255)",
				maxLength: 255,
				nullable: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "CompanyCancellationReason",
				table: "Rental");

			migrationBuilder.DropColumn(
				name: "RefundAmount",
				table: "Rental");

			migrationBuilder.DropColumn(
				name: "RefundedAt",
				table: "Rental");

			migrationBuilder.DropColumn(
				name: "RefundReceiptImagePath",
				table: "Rental");
		}
	}
}
