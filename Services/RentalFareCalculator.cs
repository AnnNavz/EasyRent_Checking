using EasyRent_Checking.Models;

namespace EasyRent_Checking.Services
{
	public readonly record struct RentalFareBreakdown(
		decimal BasePackageAmount,
		int SucceedingHours,
		decimal SucceedingFeeTotal,
		decimal DiscountAmount,
		decimal TotalAmount);

	/// <summary>
	/// Snapshots rental fare for payment.
	/// SucceedingFeeTotal = (hours beyond the base package) × vehicle succeeding rate — not the per-hour rate alone.
	/// </summary>
	public static class RentalFareCalculator
	{
		/// <summary>Hours covered by <see cref="Vehicle.BasePrice"/> before succeeding fees apply.</summary>
		public const int BasePackageHours = 8;

		public static RentalFareBreakdown Calculate(Vehicle vehicle, RentalDetails details)
		{
			var start = details.PickupDate.ToDateTime(details.PickupTime);
			var end = details.ReturnDate.ToDateTime(details.ReturnTime);
			var totalHours = end > start
				? Math.Max(0, (int)Math.Round((end - start).TotalHours))
				: 0;

			// Only hours past the base package are charged at SucceedingFee (₱/hr).
			var succeedingHours = Math.Max(0, totalHours - BasePackageHours);
			var succeedingFeePerHour = vehicle.SucceedingFee;
			var succeedingFeeTotal = Math.Round(succeedingHours * succeedingFeePerHour, 2);

			var basePackageAmount = vehicle.BasePrice;
			var subtotal = basePackageAmount + succeedingFeeTotal;
			var discountAmount = details.Discount == Discount.Yes
				? Math.Round(subtotal * 0.10m, 2)
				: 0m;
			var totalAmount = Math.Round(subtotal - discountAmount, 2);

			return new RentalFareBreakdown(
				BasePackageAmount: basePackageAmount,
				SucceedingHours: succeedingHours,
				SucceedingFeeTotal: succeedingFeeTotal,
				DiscountAmount: discountAmount,
				TotalAmount: totalAmount);
		}

		public static void ApplyTo(Rental rental, Vehicle vehicle, RentalDetails details)
		{
			var fare = Calculate(vehicle, details);
			rental.SucceedingFeeTotal = fare.SucceedingFeeTotal;
			rental.TotalAmount = fare.TotalAmount;
		}
	}
}
