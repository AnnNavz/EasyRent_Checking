using EasyRent_Checking.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(EasyRent_CheckingContext))]
    [Migration("20260826002000_add_login_email_otp")]
    public class add_login_email_otp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LoginOtpExpiresUtc",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoginOtpFailedCount",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LoginOtpHash",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LoginOtpSentAtUtc",
                table: "Users",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LoginOtpExpiresUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LoginOtpFailedCount",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LoginOtpHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LoginOtpSentAtUtc",
                table: "Users");
        }
    }
}
