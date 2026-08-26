namespace EasyRent_Checking.Models
{
	public static class RentalRules
	{
		/// <summary>Unpaid reserves must be paid within this many days or they expire.</summary>
		public const int ReservePaymentWindowDays = 2;

		/// <summary>Customer cancellation fee when the linked trip is already InTransit.</summary>
		public const decimal CancellationFeeWhenInTransit = 2000m;
	}
}
