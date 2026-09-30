using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
    /// <inheritdoc />
    public partial class hatdog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing hosted rows can be +63XXXXXXXXXX. Shrink only after normalizing to 09XXXXXXXXX.
            migrationBuilder.Sql(@"
UPDATE Rental SET ContactNumber = REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(ContactNumber)), N'+', N''), N' ', N''), N'-', N'');
UPDATE Rental SET ContactNumber = N'0' + SUBSTRING(ContactNumber, 3, 10)
WHERE ContactNumber LIKE N'63%' AND LEN(ContactNumber) = 12;
UPDATE Rental SET ContactNumber = RIGHT(ContactNumber, 11) WHERE LEN(ContactNumber) > 11;
UPDATE Rental SET ContactNumber = N'09000000000'
WHERE ContactNumber IS NULL OR LTRIM(RTRIM(ContactNumber)) = N'' OR LEN(ContactNumber) < 11;

UPDATE CustomerProfiles SET ContactNumber = REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(ContactNumber)), N'+', N''), N' ', N''), N'-', N'');
UPDATE CustomerProfiles SET ContactNumber = N'0' + SUBSTRING(ContactNumber, 3, 10)
WHERE ContactNumber LIKE N'63%' AND LEN(ContactNumber) = 12;
UPDATE CustomerProfiles SET ContactNumber = RIGHT(ContactNumber, 11) WHERE LEN(ContactNumber) > 11;
UPDATE CustomerProfiles SET ContactNumber = N'09000000000'
WHERE ContactNumber IS NULL OR LTRIM(RTRIM(ContactNumber)) = N'' OR LEN(ContactNumber) < 11;

UPDATE Driver SET ContactNo = REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(ContactNo)), N'+', N''), N' ', N''), N'-', N'');
UPDATE Driver SET ContactNo = N'0' + SUBSTRING(ContactNo, 3, 10)
WHERE ContactNo LIKE N'63%' AND LEN(ContactNo) = 12;
UPDATE Driver SET ContactNo = RIGHT(ContactNo, 11) WHERE LEN(ContactNo) > 11;
UPDATE Driver SET ContactNo = N'09000000000'
WHERE ContactNo IS NULL OR LTRIM(RTRIM(ContactNo)) = N'' OR LEN(ContactNo) < 11;
");

            migrationBuilder.AlterColumn<string>(
                name: "ContactNumber",
                table: "Rental",
                type: "nvarchar(11)",
                maxLength: 11,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "ContactNo",
                table: "Driver",
                type: "nvarchar(11)",
                maxLength: 11,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "ContactNumber",
                table: "CustomerProfiles",
                type: "nvarchar(11)",
                maxLength: 11,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ContactNumber",
                table: "Rental",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(11)",
                oldMaxLength: 11);

            migrationBuilder.AlterColumn<string>(
                name: "ContactNo",
                table: "Driver",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(11)",
                oldMaxLength: 11);

            migrationBuilder.AlterColumn<string>(
                name: "ContactNumber",
                table: "CustomerProfiles",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(11)",
                oldMaxLength: 11);
        }
    }
}
