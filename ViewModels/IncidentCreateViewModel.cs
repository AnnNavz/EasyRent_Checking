using EasyRent_Checking.Models;

namespace EasyRent_Checking.ViewModels;

public class IncidentCreateViewModel
{
	public IncidentReport Report { get; set; } = new();

	public IList<IncidentVehicleSelectionItem> Vehicles { get; set; } = new List<IncidentVehicleSelectionItem>();

	public Transit? Transit { get; set; }

	public bool LockVehicleSelection => Transit != null;
}

public class IncidentVehicleSelectionItem
{
	public int VehicleId { get; set; }

	public string Brand { get; set; } = string.Empty;

	public string Model { get; set; } = string.Empty;

	public string DisplayName => $"{Brand} {Model}".Trim();

	public string Type { get; set; } = string.Empty;

	public int PassengersCount { get; set; }

	public string PlateNumber { get; set; } = string.Empty;

	public string? ImagePath { get; set; }

	public string StatusLabel { get; set; } = string.Empty;

	public string StatusClass { get; set; } = string.Empty;

	public bool IsOnActiveTransit { get; set; }

	public string FleetStatusLabel { get; set; } = string.Empty;

	public string FleetStatusClass { get; set; } = string.Empty;

	public string? DriverName { get; set; }

	public string? DriverImagePath { get; set; }

	public string DriverInitials { get; set; } = "—";

	public string SearchKey { get; set; } = string.Empty;
}
