using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
    /// <inheritdoc />
    public partial class split_reservation_details : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReservationDetails",
                columns: table => new
                {
                    ReservationDetailsID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReservationID = table.Column<int>(type: "int", nullable: false),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    PickupLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DropoffLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PickupDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReturnDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PickupTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    ReturnTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    PassengerCount = table.Column<int>(type: "int", nullable: false),
                    Discount = table.Column<int>(type: "int", nullable: false),
                    DiscountImagePath = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationDetails", x => x.ReservationDetailsID);
                    table.ForeignKey(
                        name: "FK_ReservationDetails_Reservation_ReservationID",
                        column: x => x.ReservationID,
                        principalTable: "Reservation",
                        principalColumn: "ReservationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReservationDetails_Vehicle_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicle",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Restrict);
                });

            // Preserve existing trip fields into ReservationDetails before dropping columns.
            migrationBuilder.Sql(@"
INSERT INTO [ReservationDetails]
(
    [ReservationID],
    [VehicleId],
    [PickupLocation],
    [DropoffLocation],
    [PickupDate],
    [ReturnDate],
    [PickupTime],
    [ReturnTime],
    [PassengerCount],
    [Discount],
    [DiscountImagePath]
)
SELECT
    [ReservationId],
    [VehicleId],
    [PickupLocation],
    [DropoffLocation],
    [PickupDate],
    [ReturnDate],
    [PickupTime],
    [ReturnTime],
    [PassengerCount],
    [Discount],
    [DiscountImagePath]
FROM [Reservation];
");

            migrationBuilder.DropForeignKey(
                name: "FK_Reservation_Vehicle_VehicleId",
                table: "Reservation");

            migrationBuilder.DropIndex(
                name: "IX_Reservation_VehicleId",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "Discount",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "DiscountImagePath",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "DropoffLocation",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "PassengerCount",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "PickupDate",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "PickupLocation",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "PickupTime",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "ReturnDate",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "ReturnTime",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "VehicleId",
                table: "Reservation");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationDetails_ReservationID",
                table: "ReservationDetails",
                column: "ReservationID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReservationDetails_VehicleId",
                table: "ReservationDetails",
                column: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Discount",
                table: "Reservation",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DiscountImagePath",
                table: "Reservation",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DropoffLocation",
                table: "Reservation",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PassengerCount",
                table: "Reservation",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PickupDate",
                table: "Reservation",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<string>(
                name: "PickupLocation",
                table: "Reservation",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "PickupTime",
                table: "Reservation",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReturnDate",
                table: "Reservation",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ReturnTime",
                table: "Reservation",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddColumn<int>(
                name: "VehicleId",
                table: "Reservation",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
UPDATE r
SET
    r.[VehicleId] = d.[VehicleId],
    r.[PickupLocation] = d.[PickupLocation],
    r.[DropoffLocation] = d.[DropoffLocation],
    r.[PickupDate] = d.[PickupDate],
    r.[ReturnDate] = d.[ReturnDate],
    r.[PickupTime] = d.[PickupTime],
    r.[ReturnTime] = d.[ReturnTime],
    r.[PassengerCount] = d.[PassengerCount],
    r.[Discount] = d.[Discount],
    r.[DiscountImagePath] = d.[DiscountImagePath]
FROM [Reservation] r
INNER JOIN [ReservationDetails] d ON d.[ReservationID] = r.[ReservationId];
");

            migrationBuilder.DropTable(
                name: "ReservationDetails");

            migrationBuilder.CreateIndex(
                name: "IX_Reservation_VehicleId",
                table: "Reservation",
                column: "VehicleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reservation_Vehicle_VehicleId",
                table: "Reservation",
                column: "VehicleId",
                principalTable: "Vehicle",
                principalColumn: "VehicleId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
