using System.ComponentModel.DataAnnotations;

namespace EasyRent_Checking.Models
{
	public enum PmsTrigger
	{
		Mileage,
		Time,
		[Display(Name = "Time / Mileage")]
		Both
	}

	public enum MaintenanceType
	{
		[Display(Name = "Engine Oil Change")]
		OilChange,
		[Display(Name = "Tire Inspection")]
		TireInspection,
		[Display(Name = "Brake Inspection")]
		BrakeInspection,
		[Display(Name = "General Checkup")]
		GeneralCheckup,
		[Display(Name = "Air Filter Replacement")]
		AirFilterReplacement
	}

	public enum MaintenanceStatus
	{
		Scheduled,
		[Display(Name = "In Progress")]
		InProgress,
		Completed,
		Cancelled
	}

	public enum PmsDueKind
	{
		[Display(Name = "In Progress")]
		InProgress,
		Overdue,
		[Display(Name = "Due Now")]
		DueNow,
		[Display(Name = "Due Soon")]
		DueSoon,
		Upcoming
	}
}
