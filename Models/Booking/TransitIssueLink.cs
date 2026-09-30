using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyRent_Checking.Models
{
	public enum TransitIssueSource
	{
		Incident,
		Maintenance
	}

	public class TransitIssueLink
	{
		[Key]
		public int TransitIssueLinkId { get; set; }

		[Required]
		public int TransitID { get; set; }

		[ForeignKey(nameof(TransitID))]
		public Transit? Transit { get; set; }

		[Required]
		public TransitIssueSource Source { get; set; }

		public int? IncidentReportId { get; set; }

		[ForeignKey(nameof(IncidentReportId))]
		public IncidentReport? IncidentReport { get; set; }

		public int? MaintenanceLogId { get; set; }

		[ForeignKey(nameof(MaintenanceLogId))]
		public MaintenanceLog? MaintenanceLog { get; set; }

		public bool BlocksTrip { get; set; }

		[Required]
		public DateTime LinkedAt { get; set; } = DateTime.Now;

		public DateTime? ResolvedAt { get; set; }

		[StringLength(50)]
		public string? ResolutionAction { get; set; }

		[StringLength(500)]
		public string? ResolutionNotes { get; set; }
	}
}
