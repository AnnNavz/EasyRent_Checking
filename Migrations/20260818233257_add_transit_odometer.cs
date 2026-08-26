using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
    /// <inheritdoc />
    public partial class add_transit_odometer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OdometerEnd",
                table: "Transit",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OdometerStart",
                table: "Transit",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OdometerEnd",
                table: "Transit");

            migrationBuilder.DropColumn(
                name: "OdometerStart",
                table: "Transit");
        }
    }
}
