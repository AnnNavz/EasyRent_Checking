using EasyRent_Checking.Models;

namespace EasyRent_Checking.ViewModels;

public class TransitPreparationViewModel
{
	public Transit Transit { get; set; } = null!;

	public IList<DriverSelectionItem> Drivers { get; set; } = new List<DriverSelectionItem>();

	public int? SelectedDriverId { get; set; }
}

public class DriverSelectionItem
{
	public int DriverId { get; set; }

	public string Name { get; set; } = string.Empty;

	public bool IsActive { get; set; }

	public DateOnly ExpiryDate { get; set; }

	public string? ImagePath { get; set; }

	public string Initials { get; set; } = "?";

	public double RatingTen { get; set; }

	public int ReviewCount { get; set; }
}
