using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
    /// <inheritdoc />
    public partial class transit_dispatch_flow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transit_Driver_DriverID",
                table: "Transit");

            migrationBuilder.DropForeignKey(
                name: "FK_Transit_Reservation_ReservationID",
                table: "Transit");

            migrationBuilder.DropForeignKey(
                name: "FK_Transit_Vehicle_VehicleID",
                table: "Transit");

            migrationBuilder.DropIndex(
                name: "IX_Transit_ReservationID",
                table: "Transit");

            migrationBuilder.AlterColumn<TimeOnly>(
                name: "ReturnTime",
                table: "Transit",
                type: "time",
                nullable: true,
                oldClrType: typeof(TimeOnly),
                oldType: "time");

            migrationBuilder.AlterColumn<int>(
                name: "FuelLevelStart",
                table: "Transit",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "DriverID",
                table: "Transit",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<TimeOnly>(
                name: "DepartureTime",
                table: "Transit",
                type: "time",
                nullable: true,
                oldClrType: typeof(TimeOnly),
                oldType: "time");

            migrationBuilder.AddColumn<string>(
                name: "PreTripImagePath",
                table: "Transit",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleConditionEnd",
                table: "Transit",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transit_ReservationID",
                table: "Transit",
                column: "ReservationID",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Transit_Driver_DriverID",
                table: "Transit",
                column: "DriverID",
                principalTable: "Driver",
                principalColumn: "DriverId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Transit_Reservation_ReservationID",
                table: "Transit",
                column: "ReservationID",
                principalTable: "Reservation",
                principalColumn: "ReservationId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transit_Vehicle_VehicleID",
                table: "Transit",
                column: "VehicleID",
                principalTable: "Vehicle",
                principalColumn: "VehicleId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transit_Driver_DriverID",
                table: "Transit");

            migrationBuilder.DropForeignKey(
                name: "FK_Transit_Reservation_ReservationID",
                table: "Transit");

            migrationBuilder.DropForeignKey(
                name: "FK_Transit_Vehicle_VehicleID",
                table: "Transit");

            migrationBuilder.DropIndex(
                name: "IX_Transit_ReservationID",
                table: "Transit");

            migrationBuilder.DropColumn(
                name: "PreTripImagePath",
                table: "Transit");

            migrationBuilder.DropColumn(
                name: "VehicleConditionEnd",
                table: "Transit");

            migrationBuilder.AlterColumn<TimeOnly>(
                name: "ReturnTime",
                table: "Transit",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0),
                oldClrType: typeof(TimeOnly),
                oldType: "time",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "FuelLevelStart",
                table: "Transit",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "DriverID",
                table: "Transit",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<TimeOnly>(
                name: "DepartureTime",
                table: "Transit",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0),
                oldClrType: typeof(TimeOnly),
                oldType: "time",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transit_ReservationID",
                table: "Transit",
                column: "ReservationID");

            migrationBuilder.AddForeignKey(
                name: "FK_Transit_Driver_DriverID",
                table: "Transit",
                column: "DriverID",
                principalTable: "Driver",
                principalColumn: "DriverId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Transit_Reservation_ReservationID",
                table: "Transit",
                column: "ReservationID",
                principalTable: "Reservation",
                principalColumn: "ReservationId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Transit_Vehicle_VehicleID",
                table: "Transit",
                column: "VehicleID",
                principalTable: "Vehicle",
                principalColumn: "VehicleId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
