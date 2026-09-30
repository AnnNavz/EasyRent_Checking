using EasyRent_Checking.Models;

namespace EasyRent_Checking.ViewModels;

	public class TransitIssueAlertViewModel
	{
		public int TransitIssueLinkId { get; set; }

		public int TransitId { get; set; }

		public TransitIssueSource Source { get; set; }

		public int? IncidentReportId { get; set; }

		public int? MaintenanceLogId { get; set; }

		public int? MaintenancePlanId { get; set; }

	public string SourceLabel { get; set; } = string.Empty;

	public string? ReferenceLabel { get; set; }

	public string VehicleLabel { get; set; } = string.Empty;

	public string IssueDescription { get; set; } = string.Empty;

	public DateTime ReportedAt { get; set; }

	public string CurrentCondition { get; set; } = string.Empty;

	public bool BlocksTrip { get; set; }

	public string SourceUrl { get; set; } = string.Empty;
}
