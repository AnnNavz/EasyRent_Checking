using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
	/// <inheritdoc />
	public partial class rename_reservation_to_rental : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropForeignKey(
				name: "FK_Payment_Reservation_ReservationId",
				table: "Payment");

			migrationBuilder.DropForeignKey(
				name: "FK_Transit_Reservation_ReservationID",
				table: "Transit");

			migrationBuilder.DropForeignKey(
				name: "FK_ReservationDetails_Reservation_ReservationID",
				table: "ReservationDetails");

			migrationBuilder.DropForeignKey(
				name: "FK_ReservationDetails_Vehicle_VehicleId",
				table: "ReservationDetails");

			migrationBuilder.RenameTable(
				name: "Reservation",
				newName: "Rental");

			migrationBuilder.RenameColumn(
				name: "ReservationId",
				table: "Rental",
				newName: "RentalId");

			migrationBuilder.RenameColumn(
				name: "ReservationStatus",
				table: "Rental",
				newName: "RentalStatus");

			migrationBuilder.AddColumn<int>(
				name: "RentalOption",
				table: "Rental",
				type: "int",
				nullable: false,
				defaultValue: 0);

			migrationBuilder.RenameTable(
				name: "ReservationDetails",
				newName: "RentalDetails");

			migrationBuilder.RenameColumn(
				name: "ReservationDetailsID",
				table: "RentalDetails",
				newName: "RentalDetailsID");

			migrationBuilder.RenameColumn(
				name: "ReservationID",
				table: "RentalDetails",
				newName: "RentalID");

			migrationBuilder.RenameIndex(
				name: "IX_ReservationDetails_ReservationID",
				table: "RentalDetails",
				newName: "IX_RentalDetails_RentalID");

			migrationBuilder.RenameIndex(
				name: "IX_ReservationDetails_VehicleId",
				table: "RentalDetails",
				newName: "IX_RentalDetails_VehicleId");

			migrationBuilder.RenameColumn(
				name: "ReservationId",
				table: "Payment",
				newName: "RentalId");

			migrationBuilder.RenameIndex(
				name: "IX_Payment_ReservationId",
				table: "Payment",
				newName: "IX_Payment_RentalId");

			migrationBuilder.RenameColumn(
				name: "ReservationID",
				table: "Transit",
				newName: "RentalID");

			migrationBuilder.RenameIndex(
				name: "IX_Transit_ReservationID",
				table: "Transit",
				newName: "IX_Transit_RentalID");

			migrationBuilder.AddForeignKey(
				name: "FK_RentalDetails_Rental_RentalID",
				table: "RentalDetails",
				column: "RentalID",
				principalTable: "Rental",
				principalColumn: "RentalId",
				onDelete: ReferentialAction.Cascade);

			migrationBuilder.AddForeignKey(
				name: "FK_RentalDetails_Vehicle_VehicleId",
				table: "RentalDetails",
				column: "VehicleId",
				principalTable: "Vehicle",
				principalColumn: "VehicleId",
				onDelete: ReferentialAction.Restrict);

			migrationBuilder.AddForeignKey(
				name: "FK_Payment_Rental_RentalId",
				table: "Payment",
				column: "RentalId",
				principalTable: "Rental",
				principalColumn: "RentalId",
				onDelete: ReferentialAction.Restrict);

			migrationBuilder.AddForeignKey(
				name: "FK_Transit_Rental_RentalID",
				table: "Transit",
				column: "RentalID",
				principalTable: "Rental",
				principalColumn: "RentalId",
				onDelete: ReferentialAction.Restrict);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropForeignKey(
				name: "FK_Payment_Rental_RentalId",
				table: "Payment");

			migrationBuilder.DropForeignKey(
				name: "FK_Transit_Rental_RentalID",
				table: "Transit");

			migrationBuilder.DropForeignKey(
				name: "FK_RentalDetails_Rental_RentalID",
				table: "RentalDetails");

			migrationBuilder.DropForeignKey(
				name: "FK_RentalDetails_Vehicle_VehicleId",
				table: "RentalDetails");

			migrationBuilder.DropColumn(
				name: "RentalOption",
				table: "Rental");

			migrationBuilder.RenameColumn(
				name: "RentalID",
				table: "Transit",
				newName: "ReservationID");

			migrationBuilder.RenameIndex(
				name: "IX_Transit_RentalID",
				table: "Transit",
				newName: "IX_Transit_ReservationID");

			migrationBuilder.RenameColumn(
				name: "RentalId",
				table: "Payment",
				newName: "ReservationId");

			migrationBuilder.RenameIndex(
				name: "IX_Payment_RentalId",
				table: "Payment",
				newName: "IX_Payment_ReservationId");

			migrationBuilder.RenameColumn(
				name: "RentalID",
				table: "RentalDetails",
				newName: "ReservationID");

			migrationBuilder.RenameColumn(
				name: "RentalDetailsID",
				table: "RentalDetails",
				newName: "ReservationDetailsID");

			migrationBuilder.RenameIndex(
				name: "IX_RentalDetails_RentalID",
				table: "RentalDetails",
				newName: "IX_ReservationDetails_ReservationID");

			migrationBuilder.RenameIndex(
				name: "IX_RentalDetails_VehicleId",
				table: "RentalDetails",
				newName: "IX_ReservationDetails_VehicleId");

			migrationBuilder.RenameTable(
				name: "RentalDetails",
				newName: "ReservationDetails");

			migrationBuilder.RenameColumn(
				name: "RentalStatus",
				table: "Rental",
				newName: "ReservationStatus");

			migrationBuilder.RenameColumn(
				name: "RentalId",
				table: "Rental",
				newName: "ReservationId");

			migrationBuilder.RenameTable(
				name: "Rental",
				newName: "Reservation");

			migrationBuilder.AddForeignKey(
				name: "FK_ReservationDetails_Reservation_ReservationID",
				table: "ReservationDetails",
				column: "ReservationID",
				principalTable: "Reservation",
				principalColumn: "ReservationId",
				onDelete: ReferentialAction.Cascade);

			migrationBuilder.AddForeignKey(
				name: "FK_ReservationDetails_Vehicle_VehicleId",
				table: "ReservationDetails",
				column: "VehicleId",
				principalTable: "Vehicle",
				principalColumn: "VehicleId",
				onDelete: ReferentialAction.Restrict);

			migrationBuilder.AddForeignKey(
				name: "FK_Payment_Reservation_ReservationId",
				table: "Payment",
				column: "ReservationId",
				principalTable: "Reservation",
				principalColumn: "ReservationId",
				onDelete: ReferentialAction.Restrict);

			migrationBuilder.AddForeignKey(
				name: "FK_Transit_Reservation_ReservationID",
				table: "Transit",
				column: "ReservationID",
				principalTable: "Reservation",
				principalColumn: "ReservationId",
				onDelete: ReferentialAction.Restrict);
		}
	}
}
