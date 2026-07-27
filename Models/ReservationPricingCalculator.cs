namespace EasyRent_Checking.Models
{
	public static class ReservationPricingCalculator
	{
		private const int BaseHoursIncluded = 8;
		private const decimal DiscountRate = 0.10m;

		public static ReservationPricingSummary Calculate(Vehicle vehicle, Reservation reservation)
		{
			var pickup = reservation.PickupDate.ToDateTime(reservation.PickupTime);
			var returnDateTime = reservation.ReturnDate.ToDateTime(reservation.ReturnTime);
			var totalHours = Math.Max(0, (returnDateTime - pickup).TotalHours);
			var billedHours = Math.Max(1, (int)Math.Ceiling(totalHours));

			var baseFee = vehicle.BasePrice;
			var succeedingHourlyRate = Math.Round(vehicle.BasePrice / 2m, 2);
			var succeedingHours = Math.Max(0, billedHours - BaseHoursIncluded);
			var succeedingFee = succeedingHours * succeedingHourlyRate;
			var subtotal = baseFee + succeedingFee;
			var hasDiscount = reservation.Discount == 1;
			var discountAmount = hasDiscount ? Math.Round(subtotal * DiscountRate, 2) : 0m;
			var total = subtotal - discountAmount;

			return new ReservationPricingSummary
			{
				TotalHours = totalHours,
				DurationLabel = FormatDuration(billedHours),
				BaseFee = baseFee,
				SucceedingHours = succeedingHours,
				SucceedingHourlyRate = succeedingHourlyRate,
				SucceedingFee = succeedingFee,
				Subtotal = subtotal,
				HasDiscount = hasDiscount,
				DiscountAmount = discountAmount,
				Total = total
			};
		}

		public static string FormatDuration(int billedHours)
		{
			return billedHours == 1 ? "1 hour" : $"{billedHours} hours";
		}
	}

}
