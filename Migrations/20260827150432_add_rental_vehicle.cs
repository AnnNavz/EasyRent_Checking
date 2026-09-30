using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
    /// <inheritdoc />
    public partial class add_rental_vehicle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transit_RentalID",
                table: "Transit");

            migrationBuilder.AddColumn<int>(
                name: "RentalVehicleId",
                table: "Transit",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RentalVehicle",
                columns: table => new
                {
                    RentalVehicleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RentalId = table.Column<int>(type: "int", nullable: false),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    LineBaseAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineSucceedingFeeTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentalVehicle", x => x.RentalVehicleId);
                    table.ForeignKey(
                        name: "FK_RentalVehicle_Rental_RentalId",
                        column: x => x.RentalId,
                        principalTable: "Rental",
                        principalColumn: "RentalId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RentalVehicle_Vehicle_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicle",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transit_RentalID_VehicleID",
                table: "Transit",
                columns: new[] { "RentalID", "VehicleID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transit_RentalVehicleId",
                table: "Transit",
                column: "RentalVehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_RentalVehicle_RentalId_VehicleId",
                table: "RentalVehicle",
                columns: new[] { "RentalId", "VehicleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RentalVehicle_VehicleId",
                table: "RentalVehicle",
                column: "VehicleId");

            // Backfill one vehicle line per existing rental.
            migrationBuilder.Sql("""
                INSERT INTO RentalVehicle (RentalId, VehicleId, LineBaseAmount, LineSucceedingFeeTotal, LineTotalAmount, SortOrder)
                SELECT d.RentalID, d.VehicleId,
                       ISNULL(r.TotalAmount, 0) - ISNULL(r.SucceedingFeeTotal, 0),
                       ISNULL(r.SucceedingFeeTotal, 0),
                       ISNULL(r.TotalAmount, 0),
                       0
                FROM RentalDetails d
                INNER JOIN Rental r ON r.RentalId = d.RentalID
                WHERE NOT EXISTS (
                    SELECT 1 FROM RentalVehicle rv
                    WHERE rv.RentalId = d.RentalID AND rv.VehicleId = d.VehicleId
                );
                """);

            migrationBuilder.Sql("""
                UPDATE t
                SET t.RentalVehicleId = rv.RentalVehicleId
                FROM Transit t
                INNER JOIN RentalVehicle rv
                    ON rv.RentalId = t.RentalID AND rv.VehicleId = t.VehicleID;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_Transit_RentalVehicle_RentalVehicleId",
                table: "Transit",
                column: "RentalVehicleId",
                principalTable: "RentalVehicle",
                principalColumn: "RentalVehicleId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transit_RentalVehicle_RentalVehicleId",
                table: "Transit");

            migrationBuilder.DropTable(
                name: "RentalVehicle");

            migrationBuilder.DropIndex(
                name: "IX_Transit_RentalID_VehicleID",
                table: "Transit");

            migrationBuilder.DropIndex(
                name: "IX_Transit_RentalVehicleId",
                table: "Transit");

            migrationBuilder.DropColumn(
                name: "RentalVehicleId",
                table: "Transit");

            migrationBuilder.CreateIndex(
                name: "IX_Transit_RentalID",
                table: "Transit",
                column: "RentalID",
                unique: true);
        }
    }
}
