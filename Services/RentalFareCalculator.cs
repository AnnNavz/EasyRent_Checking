using EasyRent_Checking.Models;

namespace EasyRent_Checking.Services
/*
 * Rental fare calculation
 */
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

	/*
	 * Rental fare is computed here at booking time and saved on the rental.
	 * Payment uses that saved amount, not the vehicle's current price.
	 */
	public static class RentalFareCalculator
	{
		// Hours covered by Vehicle.BasePrice before succeeding fees apply (company 8-hour package).
		public const int BasePackageHours = 8;

		// Single-vehicle shortcut. Computes the line for one vehicle, then applies Senior/PWD discount
		// if rental.Discount is Yes. Use this when the booking has only one vehicle.
		public static RentalFareBreakdown Calculate(Vehicle vehicle, Rental rental)
		{
			var lines = CalculateLines(new[] { vehicle }, rental);
			return Aggregate(lines, rental.Discount == Discount.Yes);
		}

		// Per-vehicle fare before discount.
		// Gets pickup and return from the rental, rounds duration to whole hours, then extra hours = max(0, totalHours - 8).
		// Every vehicle on the booking uses that same extra-hour count.
		// Line total = BasePrice + (extra hours × that vehicle's SucceedingFee). Discount is not applied here.
		public static IReadOnlyList<RentalVehicleFareLine> CalculateLines(
			IReadOnlyList<Vehicle> vehicles,
			Rental rental)
		{
			if (vehicles == null || vehicles.Count == 0)
			{
				return Array.Empty<RentalVehicleFareLine>();
			}

			var start = rental.PickupDate.ToDateTime(rental.PickupTime);
			var end = rental.ReturnDate.ToDateTime(rental.ReturnTime);
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

		// Combines all vehicle lines into one booking total.
		// Sums base amounts and succeeding fees, then if applyDiscount is true takes 10% off the subtotal (Senior/PWD).
		// Discount is once on the whole booking, not per vehicle.
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

		// Single-vehicle overload. Writes the computed fare onto the rental record.
		public static void ApplyTo(Rental rental, Vehicle vehicle)
		{
			ApplyTo(rental, new[] { vehicle });
		}

		// Saves SucceedingFeeTotal and TotalAmount on the rental so payment uses this snapshot
		// even if vehicle prices change later. Discount follows rental.Discount.
		public static void ApplyTo(Rental rental, IReadOnlyList<Vehicle> vehicles)
		{
			var lines = CalculateLines(vehicles, rental);
			var fare = Aggregate(lines, rental.Discount == Discount.Yes);
			rental.SucceedingFeeTotal = fare.SucceedingFeeTotal;
			rental.TotalAmount = fare.TotalAmount;
		}

		// Creates one RentalVehicle row per selected vehicle (base, succeeding fee, line total, sort order).
		// Line amounts are undiscounted; Senior/PWD 10% is only on the rental total from ApplyTo / Aggregate.
		public static List<RentalVehicle> BuildRentalVehicles(
			IReadOnlyList<Vehicle> vehicles,
			Rental rental)
		{
			var fareLines = CalculateLines(vehicles, rental);
			var result = new List<RentalVehicle>(vehicles.Count);
			for (var i = 0; i < vehicles.Count; i++)
			{
				var vehicle = vehicles[i];
				var fare = fareLines.FirstOrDefault(l => l.VehicleId == vehicle.VehicleId);
				result.Add(new RentalVehicle
				{
					VehicleId = vehicle.VehicleId,
					LineBaseAmount = fare.LineBaseAmount,
					LineSucceedingFeeTotal = fare.LineSucceedingFeeTotal,
					LineTotalAmount = fare.LineTotalAmount,
					SortOrder = i
				});
			}

			return result;
		}
	}
}
