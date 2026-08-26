using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
    /// <inheritdoc />
    public partial class add_pms_maintenance_plans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Odometer",
                table: "Vehicle",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaintenancePlanId",
                table: "MaintenanceLog",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Odometer",
                table: "MaintenanceLog",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE MaintenanceLog SET [Type] = CASE [Type]
    WHEN 0 THEN 0
    WHEN 1 THEN 2
    WHEN 2 THEN 1
    WHEN 3 THEN 0
    WHEN 4 THEN 0
    WHEN 5 THEN 0
    WHEN 6 THEN 3
    WHEN 7 THEN 3
    WHEN 8 THEN 3
    WHEN 9 THEN 0
    ELSE 0
END;
");

            migrationBuilder.CreateTable(
                name: "MaintenancePlan",
                columns: table => new
                {
                    MaintenancePlanId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Trigger = table.Column<int>(type: "int", nullable: false),
                    IntervalKilometers = table.Column<int>(type: "int", nullable: true),
                    IntervalMonths = table.Column<int>(type: "int", nullable: true),
                    LastCompletedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LastOdometer = table.Column<int>(type: "int", nullable: true),
                    NextDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NextDueOdometer = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenancePlan", x => x.MaintenancePlanId);
                    table.ForeignKey(
                        name: "FK_MaintenancePlan_Vehicle_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicle",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceLog_MaintenancePlanId",
                table: "MaintenanceLog",
                column: "MaintenancePlanId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenancePlan_VehicleId_Type",
                table: "MaintenancePlan",
                columns: new[] { "VehicleId", "Type" },
                unique: true);

            migrationBuilder.Sql(@"
INSERT INTO MaintenancePlan (VehicleId, [Type], [Trigger], IntervalKilometers, IntervalMonths, NextDueDate, NextDueOdometer)
SELECT VehicleId, 0, 0, 5000, NULL, NULL, Odometer + 5000 FROM Vehicle;

INSERT INTO MaintenancePlan (VehicleId, [Type], [Trigger], IntervalKilometers, IntervalMonths, NextDueDate, NextDueOdometer)
SELECT VehicleId, 1, 1, NULL, 1, DATEADD(month, 1, CAST(RegistrationDate AS date)), NULL FROM Vehicle;

INSERT INTO MaintenancePlan (VehicleId, [Type], [Trigger], IntervalKilometers, IntervalMonths, NextDueDate, NextDueOdometer)
SELECT VehicleId, 2, 0, 10000, NULL, NULL, Odometer + 10000 FROM Vehicle;

INSERT INTO MaintenancePlan (VehicleId, [Type], [Trigger], IntervalKilometers, IntervalMonths, NextDueDate, NextDueOdometer)
SELECT VehicleId, 3, 1, NULL, 6, DATEADD(month, 6, CAST(RegistrationDate AS date)), NULL FROM Vehicle;

INSERT INTO MaintenancePlan (VehicleId, [Type], [Trigger], IntervalKilometers, IntervalMonths, NextDueDate, NextDueOdometer)
SELECT VehicleId, 4, 0, 15000, NULL, NULL, Odometer + 15000 FROM Vehicle;
");

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceLog_MaintenancePlan_MaintenancePlanId",
                table: "MaintenanceLog",
                column: "MaintenancePlanId",
                principalTable: "MaintenancePlan",
                principalColumn: "MaintenancePlanId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceLog_MaintenancePlan_MaintenancePlanId",
                table: "MaintenanceLog");

            migrationBuilder.DropTable(
                name: "MaintenancePlan");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceLog_MaintenancePlanId",
                table: "MaintenanceLog");

            migrationBuilder.DropColumn(
                name: "Odometer",
                table: "Vehicle");

            migrationBuilder.DropColumn(
                name: "MaintenancePlanId",
                table: "MaintenanceLog");

            migrationBuilder.DropColumn(
                name: "Odometer",
                table: "MaintenanceLog");
        }
    }
}
