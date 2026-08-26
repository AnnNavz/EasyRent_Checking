using EasyRent_Checking.Models;
using EasyRent_Checking.Services;

namespace EasyRent_Checking.ViewModels
{
	public class MaintenanceDueItem
	{
		public int MaintenancePlanId { get; set; }
		public int VehicleId { get; set; }
		public string VehicleName { get; set; } = "";
		public string PlateNumber { get; set; } = "";
		public MaintenanceType Type { get; set; }
		public PmsTrigger Trigger { get; set; }
		public string ScheduleLabel { get; set; } = "";
		public PmsDueKind DueKind { get; set; }
		public DateOnly? NextDueDate { get; set; }
		public int? NextDueOdometer { get; set; }
		public int CurrentOdometer { get; set; }
		public int? OpenLogId { get; set; }
		public string DueLabel { get; set; } = "";
		public DateOnly? LastCompletedDate { get; set; }
		public int? LastOdometer { get; set; }
		public DateOnly? RegistrationDate { get; set; }
		public string? ImagePath { get; set; }
		public VehicleStatus VehicleStatus { get; set; }
	}

	public class MaintenanceVehicleDueGroup
	{
		public int VehicleId { get; set; }
		public string VehicleName { get; set; } = "";
		public string PlateNumber { get; set; } = "";
		public string? ImagePath { get; set; }
		public VehicleStatus VehicleStatus { get; set; }
		public int CurrentOdometer { get; set; }
		public DateOnly? OdometerUpdatedOn { get; set; }
		public PmsDueKind WorstKind { get; set; }
		public int OverdueCount { get; set; }
		public int DueNowCount { get; set; }
		public int DueSoonCount { get; set; }
		public int InProgressCount { get; set; }
		public MaintenanceDueItem? NextCheckup { get; set; }
		public IList<MaintenanceDueItem> Checkups { get; set; } = new List<MaintenanceDueItem>();
	}

	public class MaintenanceHistoryRow
	{
		public int MaintenanceLogId { get; set; }
		public int? MaintenancePlanId { get; set; }
		public string VehicleName { get; set; } = "";
		public string PlateNumber { get; set; } = "";
		public string? ImagePath { get; set; }
		public MaintenanceType Type { get; set; }
		public DateTime? CompletedAt { get; set; }
		public int? Odometer { get; set; }
		public string? WorkDone { get; set; }
		public decimal? Cost { get; set; }
	}

	public class MaintenanceIndexViewModel
	{
		public string Tab { get; set; } = "overview";
		public IList<MaintenanceVehicleDueGroup> Vehicles { get; set; } = new List<MaintenanceVehicleDueGroup>();
		public IList<MaintenanceDueItem> Schedule { get; set; } = new List<MaintenanceDueItem>();
		public IList<MaintenanceHistoryRow> History { get; set; } = new List<MaintenanceHistoryRow>();
	}

	public class MaintenancePlanDetailsViewModel
	{
		public MaintenancePlan Plan { get; set; } = null!;
		public Vehicle Vehicle { get; set; } = null!;
		public MaintenanceLog? OpenLog { get; set; }
		public PmsDueKind DueKind { get; set; }
		public string ScheduleLabel { get; set; } = "";
		public string DueLabel { get; set; } = "";
		public IList<MaintenanceLog> History { get; set; } = new List<MaintenanceLog>();
	}

	public class MaintenanceScheduleRow
	{
		public int? MaintenancePlanId { get; set; }
		public MaintenanceType Type { get; set; }
		public PmsTrigger Trigger { get; set; }
		public int? IntervalKilometers { get; set; }
		public int? IntervalMonths { get; set; }
		public bool Included { get; set; } = true;
	}

	public class MaintenanceScheduleInput
	{
		public int VehicleId { get; set; }
		public string Preset { get; set; } = PmsRules.PresetSuv;
		public List<MaintenanceScheduleRow> Rows { get; set; } = new();
	}
}
