using EasyRent_Checking.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
	/// <inheritdoc />
	[DbContext(typeof(EasyRent_CheckingContext))]
	[Migration("20260911150000_add_staff_profile")]
	public class add_staff_profile : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "StaffProfiles",
				columns: table => new
				{
					StaffId = table.Column<int>(type: "int", nullable: false),
					FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
					ProfileImagePath = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
					IsSelfDeactivated = table.Column<bool>(type: "bit", nullable: false)
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

			// Move existing staff rows out of AdminProfiles into StaffProfiles.
			migrationBuilder.Sql("""
				INSERT INTO StaffProfiles (StaffId, FullName, ProfileImagePath, IsSelfDeactivated)
				SELECT ap.AdminId, ap.FullName, ap.ProfileImagePath, ap.IsSelfDeactivated
				FROM AdminProfiles ap
				INNER JOIN Users u ON u.UserId = ap.AdminId
				WHERE u.Role = 2;

				DELETE ap
				FROM AdminProfiles ap
				INNER JOIN Users u ON u.UserId = ap.AdminId
				WHERE u.Role = 2;
				""");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql("""
				INSERT INTO AdminProfiles (AdminId, FullName, ProfileImagePath, IsSelfDeactivated)
				SELECT sp.StaffId, sp.FullName, sp.ProfileImagePath, sp.IsSelfDeactivated
				FROM StaffProfiles sp
				INNER JOIN Users u ON u.UserId = sp.StaffId
				WHERE u.Role = 2;
				""");

			migrationBuilder.DropTable(
				name: "StaffProfiles");
		}
	}
}
