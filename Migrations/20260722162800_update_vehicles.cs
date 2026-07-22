using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
    /// <inheritdoc />
    public partial class update_vehicles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BasePrice",
                table: "Vehicle",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 800m);

            migrationBuilder.AddColumn<int>(
                name: "PassengersCount",
                table: "Vehicle",
                type: "int",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Vehicle",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BasePrice",
                table: "Vehicle");

            migrationBuilder.DropColumn(
                name: "PassengersCount",
                table: "Vehicle");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Vehicle");
        }
    }
}
