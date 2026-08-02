using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
    /// <inheritdoc />
    public partial class add_vehicle_succeeding_fee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SucceedingFee",
                table: "Vehicle",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 400m);

            migrationBuilder.Sql("UPDATE Vehicle SET SucceedingFee = ROUND(BasePrice / 2.0, 2) WHERE SucceedingFee = 400");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SucceedingFee",
                table: "Vehicle");
        }
    }
}
