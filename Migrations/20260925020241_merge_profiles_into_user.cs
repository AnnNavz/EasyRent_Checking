using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
    /// <inheritdoc />
    public partial class merge_profiles_into_user : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContactNumber",
                table: "Users",
                type: "nvarchar(11)",
                maxLength: 11,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Users",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfileImagePath",
                table: "Users",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsSelfDeactivated",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(@"
UPDATE u SET
    u.FullName = c.FullName,
    u.ContactNumber = c.ContactNumber,
    u.ProfileImagePath = c.ProfileImagePath,
    u.Status = c.Status,
    u.IsSelfDeactivated = c.IsSelfDeactivated
FROM Users u
INNER JOIN CustomerProfiles c ON c.CustomerId = u.UserId;

UPDATE u SET
    u.FullName = a.FullName,
    u.ContactNumber = a.ContactNumber,
    u.Address = a.Address,
    u.ProfileImagePath = a.ProfileImagePath,
    u.Status = 0,
    u.IsSelfDeactivated = a.IsSelfDeactivated
FROM Users u
INNER JOIN AdminProfiles a ON a.AdminId = u.UserId;

UPDATE u SET
    u.FullName = s.FullName,
    u.ContactNumber = s.ContactNumber,
    u.Address = s.Address,
    u.ProfileImagePath = s.ProfileImagePath,
    u.Status = 0,
    u.IsSelfDeactivated = s.IsSelfDeactivated
FROM Users u
INNER JOIN StaffProfiles s ON s.StaffId = u.UserId;

UPDATE Users SET FullName = LEFT(Email, 100) WHERE FullName = '';
");

            migrationBuilder.DropTable(
                name: "AdminProfiles");

            migrationBuilder.DropTable(
                name: "StaffProfiles");

            migrationBuilder.DropColumn(
                name: "ContactNumber",
                table: "CustomerProfiles");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "CustomerProfiles");

            migrationBuilder.DropColumn(
                name: "IsSelfDeactivated",
                table: "CustomerProfiles");

            migrationBuilder.DropColumn(
                name: "ProfileImagePath",
                table: "CustomerProfiles");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "CustomerProfiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContactNumber",
                table: "CustomerProfiles",
                type: "nvarchar(11)",
                maxLength: 11,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "CustomerProfiles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsSelfDeactivated",
                table: "CustomerProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ProfileImagePath",
                table: "CustomerProfiles",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "CustomerProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AdminProfiles",
                columns: table => new
                {
                    AdminId = table.Column<int>(type: "int", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContactNumber = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsSelfDeactivated = table.Column<bool>(type: "bit", nullable: false),
                    ProfileImagePath = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminProfiles", x => x.AdminId);
                    table.ForeignKey(
                        name: "FK_AdminProfiles_Users_AdminId",
                        column: x => x.AdminId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StaffProfiles",
                columns: table => new
                {
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContactNumber = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsSelfDeactivated = table.Column<bool>(type: "bit", nullable: false),
                    ProfileImagePath = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffProfiles", x => x.StaffId);
                    table.ForeignKey(
                        name: "FK_StaffProfiles_Users_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(@"
UPDATE c SET
    c.FullName = u.FullName,
    c.ContactNumber = u.ContactNumber,
    c.ProfileImagePath = u.ProfileImagePath,
    c.Status = u.Status,
    c.IsSelfDeactivated = u.IsSelfDeactivated
FROM CustomerProfiles c
INNER JOIN Users u ON u.UserId = c.CustomerId;

INSERT INTO AdminProfiles (AdminId, FullName, ContactNumber, Address, ProfileImagePath, IsSelfDeactivated, CreatedAt)
SELECT UserId, FullName, ContactNumber, ISNULL(Address, ''), ProfileImagePath, IsSelfDeactivated, CreatedAt
FROM Users WHERE Role = 0;

INSERT INTO StaffProfiles (StaffId, FullName, ContactNumber, Address, ProfileImagePath, IsSelfDeactivated, CreatedAt)
SELECT UserId, FullName, ContactNumber, ISNULL(Address, ''), ProfileImagePath, IsSelfDeactivated, CreatedAt
FROM Users WHERE Role = 2;
");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ContactNumber",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsSelfDeactivated",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProfileImagePath",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Users");
        }
    }
}
