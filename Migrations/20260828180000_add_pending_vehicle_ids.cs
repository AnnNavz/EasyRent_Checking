using EasyRent_Checking.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
    [DbContext(typeof(EasyRent_CheckingContext))]
    [Migration("20260828180000_add_pending_vehicle_ids")]
    public class add_pending_vehicle_ids : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PendingVehicleIdsJson",
                table: "Rental",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            // Move existing pending line items into PendingVehicleIdsJson, then drop those lines.
            migrationBuilder.Sql("""
                ;WITH PendingLines AS (
                    SELECT rv.RentalId,
                           '[' + STRING_AGG(CAST(rv.VehicleId AS nvarchar(11)), ',') WITHIN GROUP (ORDER BY rv.SortOrder, rv.RentalVehicleId) + ']' AS JsonIds
                    FROM RentalVehicle rv
                    INNER JOIN Rental r ON r.RentalId = rv.RentalId
                    WHERE r.RentalStatus = 0
                    GROUP BY rv.RentalId
                )
                UPDATE r
                SET PendingVehicleIdsJson = p.JsonIds
                FROM Rental r
                INNER JOIN PendingLines p ON p.RentalId = r.RentalId;

                DELETE rv
                FROM RentalVehicle rv
                INNER JOIN Rental r ON r.RentalId = rv.RentalId
                WHERE r.RentalStatus = 0;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingVehicleIdsJson",
                table: "Rental");
        }
    }
}
