using EasyRent_Checking.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
	/// <inheritdoc />
	[DbContext(typeof(EasyRent_CheckingContext))]
	[Migration("20260911170000_add_admin_staff_contact_address")]
	public class add_admin_staff_contact_address : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			AddProfileColumns(migrationBuilder, "AdminProfiles", "AdminId");
			AddProfileColumns(migrationBuilder, "StaffProfiles", "StaffId");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			DropProfileColumns(migrationBuilder, "AdminProfiles");
			DropProfileColumns(migrationBuilder, "StaffProfiles");
		}

		private static void AddProfileColumns(MigrationBuilder migrationBuilder, string table, string idColumn)
		{
			migrationBuilder.AddColumn<string>(
				name: "ContactNumber",
				table: table,
				type: "nvarchar(11)",
				maxLength: 11,
				nullable: false,
				defaultValue: "");

			migrationBuilder.AddColumn<string>(
				name: "Address",
				table: table,
				type: "nvarchar(255)",
				maxLength: 255,
				nullable: false,
				defaultValue: "");

			migrationBuilder.AddColumn<DateTime>(
				name: "CreatedAt",
				table: table,
				type: "datetime2",
				nullable: false,
				defaultValue: new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified));

			migrationBuilder.Sql($"""
				UPDATE p
				SET p.CreatedAt = u.CreatedAt
				FROM {table} p
				INNER JOIN Users u ON u.UserId = p.{idColumn}
				WHERE p.CreatedAt = '0001-01-01T00:00:00';
				""");
		}

		private static void DropProfileColumns(MigrationBuilder migrationBuilder, string table)
		{
			migrationBuilder.DropColumn(name: "ContactNumber", table: table);
			migrationBuilder.DropColumn(name: "Address", table: table);
			migrationBuilder.DropColumn(name: "CreatedAt", table: table);
		}
	}
}
