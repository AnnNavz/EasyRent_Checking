using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyRent_Checking.Models
{
	public class MaintenanceLog
	{
		[Key]
		public int MaintenanceLogId { get; set; }

		[Required(ErrorMessage = "Vehicle is required.")]
		[Display(Name = "Vehicle")]
		public int VehicleId { get; set; }

		[ForeignKey(nameof(VehicleId))]
		public Vehicle? Vehicle { get; set; }

		[Display(Name = "PMS Plan")]
		public int? MaintenancePlanId { get; set; }

		[ForeignKey(nameof(MaintenancePlanId))]
		public MaintenancePlan? Plan { get; set; }

		[Required(ErrorMessage = "Type is required.")]
		[Display(Name = "Type")]
		public MaintenanceType Type { get; set; }

		[Required(ErrorMessage = "Status is required.")]
		[Display(Name = "Status")]
		public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Scheduled;

		[Required(ErrorMessage = "Scheduled date is required.")]
		[DataType(DataType.Date)]
		[Display(Name = "Scheduled Date")]
		public DateOnly ScheduledDate { get; set; }

		[DataType(DataType.DateTime)]
		[Display(Name = "Started At")]
		public DateTime? StartedAt { get; set; }

		[DataType(DataType.DateTime)]
		[Display(Name = "Completed At")]
		public DateTime? CompletedAt { get; set; }

		[Display(Name = "Odometer (km)")]
		[Range(0, 9999999, ErrorMessage = "Odometer must be between 0 and 9,999,999 km.")]
		public int? Odometer { get; set; }

		[StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
		[DataType(DataType.MultilineText)]
		[Display(Name = "Description")]
		public string? Description { get; set; }

		[StringLength(1000, ErrorMessage = "Work done cannot exceed 1000 characters.")]
		[DataType(DataType.MultilineText)]
		[Display(Name = "Work Done / Findings")]
		public string? WorkDone { get; set; }

		[Column(TypeName = "decimal(18,2)")]
		[Range(0, 999999.99, ErrorMessage = "Cost cannot be negative.")]
		[Display(Name = "Cost")]
		public decimal? Cost { get; set; }

		[StringLength(255)]
		[Display(Name = "Photo")]
		public string? ImagePath { get; set; }

		[NotMapped]
		[Display(Name = "Upload Photo")]
		[DataType(DataType.Upload)]
		public IFormFile? ImageFile { get; set; }

		[StringLength(1000, ErrorMessage = "Remarks cannot exceed 1000 characters.")]
		[DataType(DataType.MultilineText)]
		[Display(Name = "Remarks")]
		public string? Remarks { get; set; }

		[Required]
		[DataType(DataType.DateTime)]
		[Display(Name = "Created At")]
		public DateTime CreatedAt { get; set; } = DateTime.Now;
	}
}
