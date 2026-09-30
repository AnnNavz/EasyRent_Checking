using EasyRent_Checking.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
	[DbContext(typeof(EasyRent_CheckingContext))]
	[Migration("20260831130000_add_customer_cancellation_refund")]
	public class add_customer_cancellation_refund : Migration
	{
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
				name: "CustomerCancellationReason",
				table: "Rental",
				type: "nvarchar(500)",
				maxLength: 500,
				nullable: true);

			migrationBuilder.AddColumn<DateTime>(
				name: "RefundRequestedAt",
				table: "Rental",
				type: "datetime2",
				nullable: true);

			migrationBuilder.AddColumn<decimal>(
				name: "RefundRequestedAmount",
				table: "Rental",
				type: "decimal(18,2)",
				nullable: true);

			migrationBuilder.AddColumn<DateTime>(
				name: "CancellationFeePaidAt",
				table: "Rental",
				type: "datetime2",
				nullable: true);
		}

		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(name: "CustomerCancellationReason", table: "Rental");
			migrationBuilder.DropColumn(name: "RefundRequestedAt", table: "Rental");
			migrationBuilder.DropColumn(name: "RefundRequestedAmount", table: "Rental");
			migrationBuilder.DropColumn(name: "CancellationFeePaidAt", table: "Rental");
		}
	}
}
