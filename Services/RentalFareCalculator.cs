using EasyRent_Checking.Models;

namespace EasyRent_Checking.Services
{
	public readonly record struct RentalFareBreakdown(
		decimal BasePackageAmount,
		int SucceedingHours,
		decimal SucceedingFeeTotal,
		decimal DiscountAmount,
		decimal TotalAmount);

	public readonly record struct RentalVehicleFareLine(
		int VehicleId,
		decimal LineBaseAmount,
		decimal LineSucceedingFeeTotal,
		decimal LineTotalAmount,
		int SucceedingHours);

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
			var lines = CalculateLines(new[] { vehicle }, details);
			return Aggregate(lines, details.Discount == Discount.Yes);
		}

		public static IReadOnlyList<RentalVehicleFareLine> CalculateLines(
			IReadOnlyList<Vehicle> vehicles,
			RentalDetails details)
		{
			if (vehicles == null || vehicles.Count == 0)
			{
				return Array.Empty<RentalVehicleFareLine>();
			}

			var start = details.PickupDate.ToDateTime(details.PickupTime);
			var end = details.ReturnDate.ToDateTime(details.ReturnTime);
			var totalHours = end > start
				? Math.Max(0, (int)Math.Round((end - start).TotalHours))
				: 0;
			var succeedingHours = Math.Max(0, totalHours - BasePackageHours);

			var lines = new List<RentalVehicleFareLine>(vehicles.Count);
			foreach (var vehicle in vehicles)
			{
				var succeedingFeeTotal = Math.Round(succeedingHours * vehicle.SucceedingFee, 2);
				var basePackageAmount = vehicle.BasePrice;
				var lineSubtotal = basePackageAmount + succeedingFeeTotal;
				lines.Add(new RentalVehicleFareLine(
					VehicleId: vehicle.VehicleId,
					LineBaseAmount: basePackageAmount,
					LineSucceedingFeeTotal: succeedingFeeTotal,
					LineTotalAmount: lineSubtotal,
					SucceedingHours: succeedingHours));
			}

			return lines;
		}

		public static RentalFareBreakdown Aggregate(
			IReadOnlyList<RentalVehicleFareLine> lines,
			bool applyDiscount)
		{
			var basePackageAmount = lines.Sum(l => l.LineBaseAmount);
			var succeedingFeeTotal = lines.Sum(l => l.LineSucceedingFeeTotal);
			var succeedingHours = lines.Count > 0 ? lines[0].SucceedingHours : 0;
			var subtotal = basePackageAmount + succeedingFeeTotal;
			var discountAmount = applyDiscount
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
			ApplyTo(rental, new[] { vehicle }, details);
		}

		public static void ApplyTo(Rental rental, IReadOnlyList<Vehicle> vehicles, RentalDetails details)
		{
			var lines = CalculateLines(vehicles, details);
			var fare = Aggregate(lines, details.Discount == Discount.Yes);
			rental.SucceedingFeeTotal = fare.SucceedingFeeTotal;
			rental.TotalAmount = fare.TotalAmount;
		}

		/// <summary>
		/// Builds <see cref="RentalVehicle"/> rows with line amounts.
		/// Discount is applied once on the booking total (not per line).
		/// </summary>
		public static List<RentalVehicle> BuildRentalVehicles(
			IReadOnlyList<Vehicle> vehicles,
			RentalDetails details)
		{
			var lines = CalculateLines(vehicles, details);
			var result = new List<RentalVehicle>(lines.Count);
			for (var i = 0; i < lines.Count; i++)
			{
				var line = lines[i];
				result.Add(new RentalVehicle
				{
					VehicleId = line.VehicleId,
					LineBaseAmount = line.LineBaseAmount,
					LineSucceedingFeeTotal = line.LineSucceedingFeeTotal,
					LineTotalAmount = line.LineTotalAmount,
					SortOrder = i
				});
			}

			return result;
		}
	}
}
