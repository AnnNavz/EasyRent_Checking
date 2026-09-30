using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyRent_Checking.Models
{
	public class IncidentReport
	{
		[Key]
		public int IncidentReportId { get; set; }

		[Required(ErrorMessage = "Vehicle is required.")]
		[Display(Name = "Vehicle")]
		public int VehicleId { get; set; }

		[ForeignKey(nameof(VehicleId))]
		public Vehicle? Vehicle { get; set; }

		[Display(Name = "Transit")]
		public int? TransitID { get; set; }

		[ForeignKey(nameof(TransitID))]
		public Transit? Transit { get; set; }

		[Display(Name = "Driver")]
		public int? DriverID { get; set; }

		[ForeignKey(nameof(DriverID))]
		public Driver? Driver { get; set; }

		[Required(ErrorMessage = "Type is required.")]
		[Display(Name = "Type")]
		public IncidentType Type { get; set; }

		[Required(ErrorMessage = "Severity is required.")]
		[Display(Name = "Severity")]
		public IncidentSeverity Severity { get; set; }

		[Required(ErrorMessage = "Status is required.")]
		[Display(Name = "Status")]
		public IncidentStatus Status { get; set; } = IncidentStatus.Reported;

		[Required(ErrorMessage = "When it happened is required.")]
		[DataType(DataType.DateTime)]
		[Display(Name = "Occurred At")]
		public DateTime OccurredAt { get; set; } = DateTime.Now;

		[Required(ErrorMessage = "Location is required.")]
		[StringLength(200, ErrorMessage = "Location cannot exceed 200 characters.")]
		[Display(Name = "Location")]
		public string Location { get; set; } = string.Empty;

		[Required(ErrorMessage = "Description is required.")]
		[StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
		[DataType(DataType.MultilineText)]
		[Display(Name = "Description")]
		public string Description { get; set; } = string.Empty;

		[Display(Name = "Vehicle cannot go out")]
		public bool IsUndrivable { get; set; }

		[StringLength(255)]
		[Display(Name = "Photo")]
		public string? ImagePath { get; set; }

		[NotMapped]
		[Display(Name = "Upload Photo")]
		[DataType(DataType.Upload)]
		public IFormFile? ImageFile { get; set; }

		[DataType(DataType.DateTime)]
		[Display(Name = "Repair Started At")]
		public DateTime? RepairStartedAt { get; set; }

		[StringLength(1000, ErrorMessage = "Work done cannot exceed 1000 characters.")]
		[DataType(DataType.MultilineText)]
		[Display(Name = "Work Done")]
		public string? WorkDone { get; set; }

		[Column(TypeName = "decimal(18,2)")]
		[Range(0, 999999.99, ErrorMessage = "Cost cannot be negative.")]
		[Display(Name = "Cost")]
		public decimal? Cost { get; set; }

		[StringLength(1000, ErrorMessage = "Remarks cannot exceed 1000 characters.")]
		[DataType(DataType.MultilineText)]
		[Display(Name = "Remarks")]
		public string? Remarks { get; set; }

		[Required]
		[DataType(DataType.DateTime)]
		[Display(Name = "Created At")]
		public DateTime CreatedAt { get; set; } = DateTime.Now;

		[DataType(DataType.DateTime)]
		[Display(Name = "Closed At")]
		public DateTime? ClosedAt { get; set; }

		public const string ReferencePrefix = "IRT";

		public static string FormatReference(int incidentReportId)
			=> $"{ReferencePrefix}-{incidentReportId:D5}";
	}
}
