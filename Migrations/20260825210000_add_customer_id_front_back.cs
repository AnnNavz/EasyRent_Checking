using EasyRent_Checking.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(EasyRent_CheckingContext))]
    [Migration("20260825210000_add_customer_id_front_back")]
    public class add_customer_id_front_back : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FrontValidIDImagePath",
                table: "CustomerProfiles",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BackValidIDImagePath",
                table: "CustomerProfiles",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE CustomerProfiles
SET FrontValidIDImagePath = ValidIDImagePath
WHERE ValidIDImagePath IS NOT NULL AND ValidIDImagePath <> N'';");

            migrationBuilder.DropColumn(
                name: "ValidIDImagePath",
                table: "CustomerProfiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ValidIDImagePath",
                table: "CustomerProfiles",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE CustomerProfiles
SET ValidIDImagePath = FrontValidIDImagePath
WHERE FrontValidIDImagePath IS NOT NULL AND FrontValidIDImagePath <> N'';");

            migrationBuilder.DropColumn(
                name: "FrontValidIDImagePath",
                table: "CustomerProfiles");

            migrationBuilder.DropColumn(
                name: "BackValidIDImagePath",
                table: "CustomerProfiles");
        }
    }
}
