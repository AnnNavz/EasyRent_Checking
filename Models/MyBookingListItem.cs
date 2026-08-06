namespace EasyRent_Checking.Models
{
	public class MyBookingListItem
	{
		public int ReservationId { get; set; }
		public string BookingLabel { get; set; } = string.Empty;
		public ReservationStatus ReservationStatus { get; set; }
		public string VehicleTitle { get; set; } = string.Empty;
		public string? VehicleImagePath { get; set; }
		public DateOnly PickupDate { get; set; }
		public TimeOnly PickupTime { get; set; }
		public TimeOnly ReturnTime { get; set; }
		public decimal AmountPaid { get; set; }
		public string? PaymentMethod { get; set; }
		public PaymentStatus? PaymentStatus { get; set; }
		public string? DriverName { get; set; }
		public bool IsPast { get; set; }
	}
}
