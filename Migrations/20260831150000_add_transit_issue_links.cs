using EasyRent_Checking.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyRent_Checking.Migrations
{
	[DbContext(typeof(EasyRent_CheckingContext))]
	[Migration("20260831150000_add_transit_issue_links")]
	public class add_transit_issue_links : Migration
	{
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "TransitIssueLink",
				columns: table => new
				{
					TransitIssueLinkId = table.Column<int>(type: "int", nullable: false)
						.Annotation("SqlServer:Identity", "1, 1"),
					TransitID = table.Column<int>(type: "int", nullable: false),
					Source = table.Column<int>(type: "int", nullable: false),
					IncidentReportId = table.Column<int>(type: "int", nullable: true),
					MaintenanceLogId = table.Column<int>(type: "int", nullable: true),
					BlocksTrip = table.Column<bool>(type: "bit", nullable: false),
					LinkedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
					ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
					ResolutionAction = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
					ResolutionNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_TransitIssueLink", x => x.TransitIssueLinkId);
					table.ForeignKey(
						name: "FK_TransitIssueLink_IncidentReport_IncidentReportId",
						column: x => x.IncidentReportId,
						principalTable: "IncidentReport",
						principalColumn: "IncidentReportId");
					table.ForeignKey(
						name: "FK_TransitIssueLink_MaintenanceLog_MaintenanceLogId",
						column: x => x.MaintenanceLogId,
						principalTable: "MaintenanceLog",
						principalColumn: "MaintenanceLogId");
					table.ForeignKey(
						name: "FK_TransitIssueLink_Transit_TransitID",
						column: x => x.TransitID,
						principalTable: "Transit",
						principalColumn: "TransitID",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateIndex(
				name: "IX_TransitIssueLink_IncidentReportId",
				table: "TransitIssueLink",
				column: "IncidentReportId");

			migrationBuilder.CreateIndex(
				name: "IX_TransitIssueLink_MaintenanceLogId",
				table: "TransitIssueLink",
				column: "MaintenanceLogId");

			migrationBuilder.CreateIndex(
				name: "IX_TransitIssueLink_TransitID",
				table: "TransitIssueLink",
				column: "TransitID");
		}

		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(name: "TransitIssueLink");
		}
	}
}
