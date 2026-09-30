using EasyRent_Checking.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
	/// <inheritdoc />
	[DbContext(typeof(EasyRent_CheckingContext))]
	[Migration("20260828150000_add_vehicle_is_active")]
	public class add_vehicle_is_active : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<bool>(
				name: "IsActive",
				table: "Vehicle",
				type: "bit",
				nullable: false,
				defaultValue: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "IsActive",
				table: "Vehicle");
		}
	}
}
