using EasyRent_Checking.Data;
using EasyRent_Checking.Models;
using Microsoft.EntityFrameworkCore;

namespace EasyRent_Checking.Services
{
	public static class RentalConfirmation
	{
		/// <summary>
		/// Payment confirms the booking: reserve vehicles, create trip records, and notify the customer.
		/// Unpaid pay-later holds stay pending until this runs.
		/// </summary>
		public static async Task ConfirmPaidBookingAsync(
			EasyRent_CheckingContext context,
			SystemLogService logs,
			BookingEmailService emails,
			Rental rental)
		{
			if (rental.RentalStatus is RentalStatus.Cancelled
				or RentalStatus.Expired
				or RentalStatus.Refunded
				or RentalStatus.RefundRejected)
			{
				return;
			}

			if (RentalResolution.HasPendingRefundRequest(rental))
			{
				return;
			}

			var vehicleIds = RentalVehicleWorkflow.ResolveVehicleIds(rental, rental.RentalVehicles);
			if (vehicleIds.Count > 0)
			{
				await RentalVehicleWorkflow.MaterializeAsync(context, rental, vehicleIds);
			}

			var alreadyApproved = rental.RentalStatus == RentalStatus.Approved;
			rental.RentalStatus = RentalStatus.Approved;
			rental.PaymentDueAt = null;
			rental.RentalOption = RentalOption.Book;

			if (!alreadyApproved)
			{
				logs.Record(
					SystemLogAction.Approved,
					SystemLogCategory.Booking,
					$"Confirmed booking BK-{rental.RentalId:D5} for {rental.CustomerName}.",
					"Rental",
					rental.RentalId);
			}

			await context.SaveChangesAsync();
			await EnsureTransitsAsync(context, rental.RentalId);

			if (!alreadyApproved)
			{
				await emails.SendRentalConfirmedAsync(rental);
			}
		}

		public static async Task EnsureTransitsAsync(EasyRent_CheckingContext context, int rentalId)
		{
			var rentalVehicles = await context.RentalVehicles
				.AsNoTracking()
				.Where(rv => rv.RentalId == rentalId)
				.OrderBy(rv => rv.SortOrder)
				.ThenBy(rv => rv.RentalVehicleId)
				.ToListAsync();

			if (rentalVehicles.Count == 0)
			{
				return;
			}

			var distinctLines = rentalVehicles
				.GroupBy(rv => rv.VehicleId)
				.Select(g => g.First())
				.ToList();

			var existingVehicleIds = await context.Transits
				.AsNoTracking()
				.Where(t => t.RentalID == rentalId)
				.Select(t => t.VehicleID)
				.ToListAsync();

			if (distinctLines.Count <= 1 && existingVehicleIds.Count > 0)
			{
				return;
			}

			foreach (var line in distinctLines)
			{
				if (existingVehicleIds.Contains(line.VehicleId))
				{
					continue;
				}

				context.Transits.Add(new Transit
				{
					RentalID = rentalId,
					VehicleID = line.VehicleId,
					RentalVehicleId = line.RentalVehicleId > 0 ? line.RentalVehicleId : null,
					TripStatus = TripStatus.Scheduled
				});
			}

			await context.SaveChangesAsync();
		}
	}
}
