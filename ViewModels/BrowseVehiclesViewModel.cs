namespace EasyRent_Checking.ViewModels
{
	public class BrowseVehiclesViewModel
	{
		public IList<HomeVehicleCard> Vehicles { get; set; } = new List<HomeVehicleCard>();
		public IList<int> CapacityOptions { get; set; } = new List<int>();

		public IList<string> Categories { get; set; } = new List<string> { "All", "SUV", "Sedan", "Van" };

		public string CurrentCategory { get; set; } = "All";
		public string CurrentSort { get; set; } = "Default";
		public int? Capacity { get; set; }
		public DateOnly? PickupDate { get; set; }
		public DateOnly? ReturnDate { get; set; }
		public string? Query { get; set; }

		public int Page { get; set; } = 1;
		public int TotalPages { get; set; } = 1;
		public int TotalCount { get; set; }
	}
}
