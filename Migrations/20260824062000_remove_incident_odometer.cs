using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EasyRent_Checking.Data;

#nullable disable

namespace EasyRent_Checking.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(EasyRent_CheckingContext))]
    [Migration("20260824062000_remove_incident_odometer")]
    public class remove_incident_odometer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Odometer",
                table: "IncidentReport");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Odometer",
                table: "IncidentReport",
                type: "int",
                nullable: true);
        }
    }
}
