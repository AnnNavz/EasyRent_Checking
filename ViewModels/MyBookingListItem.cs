using EasyRent_Checking.Models;

namespace EasyRent_Checking.ViewModels
{
	public class MyBookingVehicleItem
	{
		public string Brand { get; set; } = string.Empty;
		public string Model { get; set; } = string.Empty;
		public string TypeLabel { get; set; } = string.Empty;
		public string? ImagePath { get; set; }
		public int PassengersCount { get; set; }
	}

	public class MyBookingListItem
	{
		public int RentalId { get; set; }
		public string BookingLabel { get; set; } = string.Empty;
		public RentalStatus RentalStatus { get; set; }
		public RentalOption RentalOption { get; set; }
		public string VehicleTitle { get; set; } = string.Empty;
		public string VehicleTypeLabel { get; set; } = string.Empty;
		public int PassengersCount { get; set; }
		public string? VehicleImagePath { get; set; }
		public List<MyBookingVehicleItem> Vehicles { get; set; } = new();
		public DateOnly PickupDate { get; set; }
		public TimeOnly PickupTime { get; set; }
		public TimeOnly ReturnTime { get; set; }
		public decimal TotalFare { get; set; }
		public decimal AmountPaid { get; set; }
		public decimal RemainingBalance { get; set; }
		public string? PaymentMethod { get; set; }
		public string? DriverName { get; set; }
		public string? DriverInitials { get; set; }
		public string? DriverImagePath { get; set; }
		public bool IsPast { get; set; }
		public TripStatus? TripStatus { get; set; }
		public bool CanRate { get; set; }
		public bool HasFeedback { get; set; }
		public bool CanPay { get; set; }
		public string ProgressLabel { get; set; } = string.Empty;
		public string ProgressTone { get; set; } = "muted";
	}

	public class MyBookingsPageViewModel
	{
		public string FullName { get; set; } = string.Empty;
		public string AvatarInitials { get; set; } = "U";
		public string? ProfileImagePath { get; set; }
		public string Filter { get; set; } = "all";
		public string Sort { get; set; } = "newest";
		public List<MyBookingListItem> Items { get; set; } = new();
	}
}
