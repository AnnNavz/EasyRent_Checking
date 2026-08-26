namespace EasyRent_Checking.ViewModels
{
	public class HomeViewModel
	{
		public IList<int> CapacityOptions { get; set; } = new List<int>();
		public IList<HomeVehicleCard> PopularVehicles { get; set; } = new List<HomeVehicleCard>();
		public IList<HomeTestimonial> Testimonials { get; set; } = new List<HomeTestimonial>();
	}

	public class HomeVehicleCard
	{
		public int VehicleId { get; set; }
		public string Model { get; set; } = string.Empty;
		public string TypeLabel { get; set; } = string.Empty;
		public int PassengersCount { get; set; }
		public decimal BasePrice { get; set; }
		public decimal SucceedingFee { get; set; }
		public string? ImagePath { get; set; }
		public double? Rating { get; set; }
	}

	public class HomeTestimonial
	{
		public string CustomerName { get; set; } = string.Empty;
		public string Comment { get; set; } = string.Empty;
		public double Rating { get; set; }
	}
}
