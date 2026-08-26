using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyRent_Checking.Models
{
	public class MaintenancePlan
	{
		[Key]
		public int MaintenancePlanId { get; set; }

		[Required(ErrorMessage = "Vehicle is required.")]
		[Display(Name = "Vehicle")]
		public int VehicleId { get; set; }

		[ForeignKey(nameof(VehicleId))]
		public Vehicle? Vehicle { get; set; }

		[Required(ErrorMessage = "Checkup type is required.")]
		[Display(Name = "Checkup")]
		public MaintenanceType Type { get; set; }

		[Required(ErrorMessage = "Schedule basis is required.")]
		[Display(Name = "Based On")]
		public PmsTrigger Trigger { get; set; }

		[Display(Name = "Interval (km)")]
		[Range(1, 200000, ErrorMessage = "Interval must be between 1 and 200,000 km.")]
		public int? IntervalKilometers { get; set; }

		[Display(Name = "Interval (months)")]
		[Range(1, 120, ErrorMessage = "Interval must be between 1 and 120 months.")]
		public int? IntervalMonths { get; set; }

		[DataType(DataType.Date)]
		[Display(Name = "Last Completed")]
		public DateOnly? LastCompletedDate { get; set; }

		[Display(Name = "Last Odometer")]
		[Range(0, 9999999, ErrorMessage = "Odometer must be between 0 and 9,999,999 km.")]
		public int? LastOdometer { get; set; }

		[DataType(DataType.Date)]
		[Display(Name = "Next Due Date")]
		public DateOnly? NextDueDate { get; set; }

		[Display(Name = "Next Due (km)")]
		[Range(0, 9999999, ErrorMessage = "Odometer must be between 0 and 9,999,999 km.")]
		public int? NextDueOdometer { get; set; }

		public ICollection<MaintenanceLog> Logs { get; set; } = new List<MaintenanceLog>();
	}
}
