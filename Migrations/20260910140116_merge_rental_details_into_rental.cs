using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
    /// <inheritdoc />
    public partial class merge_rental_details_into_rental : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Discount",
                table: "Rental",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DiscountImagePath",
                table: "Rental",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DropoffLocation",
                table: "Rental",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PassengerCount",
                table: "Rental",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PickupDate",
                table: "Rental",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<string>(
                name: "PickupLocation",
                table: "Rental",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "PickupTime",
                table: "Rental",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReturnDate",
                table: "Rental",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ReturnTime",
                table: "Rental",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.Sql("""
                UPDATE r
                SET
                    r.PickupLocation = d.PickupLocation,
                    r.DropoffLocation = d.DropoffLocation,
                    r.PickupDate = d.PickupDate,
                    r.ReturnDate = d.ReturnDate,
                    r.PickupTime = d.PickupTime,
                    r.ReturnTime = d.ReturnTime,
                    r.PassengerCount = d.PassengerCount,
                    r.Discount = d.Discount,
                    r.DiscountImagePath = d.DiscountImagePath
                FROM Rental r
                INNER JOIN RentalDetails d ON d.RentalID = r.RentalId;
                """);

            migrationBuilder.DropTable(
                name: "RentalDetails");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Discount",
                table: "Rental");

            migrationBuilder.DropColumn(
                name: "DiscountImagePath",
                table: "Rental");

            migrationBuilder.DropColumn(
                name: "DropoffLocation",
                table: "Rental");

            migrationBuilder.DropColumn(
                name: "PassengerCount",
                table: "Rental");

            migrationBuilder.DropColumn(
                name: "PickupDate",
                table: "Rental");

            migrationBuilder.DropColumn(
                name: "PickupLocation",
                table: "Rental");

            migrationBuilder.DropColumn(
                name: "PickupTime",
                table: "Rental");

            migrationBuilder.DropColumn(
                name: "ReturnDate",
                table: "Rental");

            migrationBuilder.DropColumn(
                name: "ReturnTime",
                table: "Rental");

            migrationBuilder.CreateTable(
                name: "RentalDetails",
                columns: table => new
                {
                    RentalDetailsID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RentalID = table.Column<int>(type: "int", nullable: false),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    Discount = table.Column<int>(type: "int", nullable: false),
                    DiscountImagePath = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DropoffLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PassengerCount = table.Column<int>(type: "int", nullable: false),
                    PickupDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PickupLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PickupTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    ReturnDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReturnTime = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentalDetails", x => x.RentalDetailsID);
                    table.ForeignKey(
                        name: "FK_RentalDetails_Rental_RentalID",
                        column: x => x.RentalID,
                        principalTable: "Rental",
                        principalColumn: "RentalId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RentalDetails_Vehicle_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicle",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RentalDetails_RentalID",
                table: "RentalDetails",
                column: "RentalID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RentalDetails_VehicleId",
                table: "RentalDetails",
                column: "VehicleId");
        }
    }
}
