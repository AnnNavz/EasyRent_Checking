namespace EasyRent_Checking.ViewModels;

public class MaintenanceSetupViewModel
{
	public MaintenanceScheduleInput Schedule { get; set; } = new();

	public IList<IncidentVehicleSelectionItem> Vehicles { get; set; } = new List<IncidentVehicleSelectionItem>();
}

public class VehicleSelectionPickerViewModel
{
	public IList<IncidentVehicleSelectionItem> Vehicles { get; set; } = new List<IncidentVehicleSelectionItem>();

	public int SelectedVehicleId { get; set; }

	public string InputName { get; set; } = "VehicleId";

	public string ValidationKey { get; set; } = "VehicleId";

	public string EmptyMessage { get; set; } = "No vehicles are available.";

	public string TabEmptyMessage { get; set; } = "No vehicles match your search in this tab.";
}
