using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EasyRent_Checking.Data;

#nullable disable

namespace EasyRent_Checking.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(EasyRent_CheckingContext))]
    [Migration("20260824140000_vehicle_type_as_string")]
    public class vehicle_type_as_string : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TypeName",
                table: "Vehicle",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE Vehicle SET TypeName = CASE [Type]
    WHEN 0 THEN N'SUV'
    WHEN 1 THEN N'Van'
    ELSE N'SUV'
END;");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Vehicle");

            migrationBuilder.RenameColumn(
                name: "TypeName",
                table: "Vehicle",
                newName: "Type");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Vehicle",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "SUV",
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TypeInt",
                table: "Vehicle",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
UPDATE Vehicle SET TypeInt = CASE
    WHEN [Type] = N'Van' THEN 1
    ELSE 0
END;");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Vehicle");

            migrationBuilder.RenameColumn(
                name: "TypeInt",
                table: "Vehicle",
                newName: "Type");
        }
    }
}
