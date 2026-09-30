using EasyRent_Checking.Models;

namespace EasyRent_Checking.ViewModels;

public class TransitIndexItemViewModel
{
	public Transit Transit { get; set; } = null!;

	public int VehicleCount { get; set; } = 1;

	public string VehicleLabel { get; set; } = "Unassigned";
}
