using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
    /// <inheritdoc />
    public partial class add_feedback_category_ratings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DriverCourtesy",
                table: "Feedback",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DriverDriving",
                table: "Feedback",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DriverProfessionalism",
                table: "Feedback",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VehicleComfort",
                table: "Feedback",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VehiclePerformance",
                table: "Feedback",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VehicleSafety",
                table: "Feedback",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE Feedback
                SET VehicleComfort = Rating,
                    VehiclePerformance = Rating,
                    VehicleSafety = Rating
                WHERE VehicleComfort = 0 AND VehiclePerformance = 0 AND VehicleSafety = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DriverCourtesy",
                table: "Feedback");

            migrationBuilder.DropColumn(
                name: "DriverDriving",
                table: "Feedback");

            migrationBuilder.DropColumn(
                name: "DriverProfessionalism",
                table: "Feedback");

            migrationBuilder.DropColumn(
                name: "VehicleComfort",
                table: "Feedback");

            migrationBuilder.DropColumn(
                name: "VehiclePerformance",
                table: "Feedback");

            migrationBuilder.DropColumn(
                name: "VehicleSafety",
                table: "Feedback");
        }
    }
}
