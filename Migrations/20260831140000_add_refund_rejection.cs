using EasyRent_Checking.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
	[DbContext(typeof(EasyRent_CheckingContext))]
	[Migration("20260831140000_add_refund_rejection")]
	public class add_refund_rejection : Migration
	{
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<DateTime>(
				name: "RefundRejectedAt",
				table: "Rental",
				type: "datetime2",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "RefundRejectionReason",
				table: "Rental",
				type: "nvarchar(500)",
				maxLength: 500,
				nullable: true);
		}

		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(name: "RefundRejectedAt", table: "Rental");
			migrationBuilder.DropColumn(name: "RefundRejectionReason", table: "Rental");
		}
	}
}
