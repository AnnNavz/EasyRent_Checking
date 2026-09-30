using EasyRent_Checking.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
	/// <inheritdoc />
	[DbContext(typeof(EasyRent_CheckingContext))]
	[Migration("20260829120000_add_transit_trip_photo_paths")]
	public class add_transit_trip_photo_paths : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
				name: "PostTripImagePathsJson",
				table: "Transit",
				type: "nvarchar(2000)",
				maxLength: 2000,
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "PreTripImagePathsJson",
				table: "Transit",
				type: "nvarchar(2000)",
				maxLength: 2000,
				nullable: true);

			migrationBuilder.Sql("""
				UPDATE Transit
				SET PreTripImagePathsJson = CONCAT('["', REPLACE(PreTripImagePath, '"', '\"'), '"]')
				WHERE PreTripImagePath IS NOT NULL AND LTRIM(RTRIM(PreTripImagePath)) <> '';

				UPDATE Transit
				SET PostTripImagePathsJson = CONCAT('["', REPLACE(PostTripImagePath, '"', '\"'), '"]')
				WHERE PostTripImagePath IS NOT NULL AND LTRIM(RTRIM(PostTripImagePath)) <> '';
				""");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "PostTripImagePathsJson",
				table: "Transit");

			migrationBuilder.DropColumn(
				name: "PreTripImagePathsJson",
				table: "Transit");
		}
	}
}
