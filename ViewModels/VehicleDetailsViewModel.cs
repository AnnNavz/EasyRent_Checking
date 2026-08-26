using EasyRent_Checking.Models;

namespace EasyRent_Checking.ViewModels
{
	public class VehicleBookingHistoryItem
	{
		public int RentalId { get; set; }
		public string BookingLabel { get; set; } = string.Empty;
		public string CustomerName { get; set; } = string.Empty;
		public string TripDates { get; set; } = string.Empty;
		public string Duration { get; set; } = string.Empty;
		public DateTime SortDate { get; set; }
		public string Status { get; set; } = string.Empty;
		public string StatusClass { get; set; } = string.Empty;
	}

	public class VehicleDetailsViewModel
	{
		public Vehicle Vehicle { get; set; } = null!;
		public IReadOnlyList<MaintenanceDueItem> MaintenanceItems { get; set; } = Array.Empty<MaintenanceDueItem>();
		public IReadOnlyList<VehicleBookingHistoryItem> BookingHistory { get; set; } = Array.Empty<VehicleBookingHistoryItem>();
	}
}
