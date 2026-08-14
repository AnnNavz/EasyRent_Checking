using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
    /// <inheritdoc />
    public partial class add_rental_customer_id : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                table: "Rental",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE r
                SET r.CustomerId = c.CustomerId
                FROM Rental r
                INNER JOIN CustomerProfiles c ON r.ContactNumber = c.ContactNumber
                WHERE r.CustomerId IS NULL
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Rental_CustomerId",
                table: "Rental",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Rental_CustomerProfiles_CustomerId",
                table: "Rental",
                column: "CustomerId",
                principalTable: "CustomerProfiles",
                principalColumn: "CustomerId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rental_CustomerProfiles_CustomerId",
                table: "Rental");

            migrationBuilder.DropIndex(
                name: "IX_Rental_CustomerId",
                table: "Rental");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Rental");
        }
    }
}
