using EasyRent_Checking.Data;
using EasyRent_Checking.Models;
using EasyRent_Checking.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EasyRent_Checking.Services
{
	public static class RentalResolution
	{
		public const string CancellationFeePaymentType = "Cancellation Fee";

		public static async Task<decimal> GetAmountPaidAsync(EasyRent_CheckingContext context, int rentalId)
			=> await context.Payments
				.AsNoTracking()
				.Where(p => p.RentalId == rentalId && p.PaymentType != CancellationFeePaymentType)
				.SumAsync(p => p.AmountPaid);

		public static async Task<Payment?> GetLatestRentalPaymentAsync(EasyRent_CheckingContext context, int rentalId)
			=> await context.Payments
				.AsNoTracking()
				.Where(p => p.RentalId == rentalId && p.PaymentType != CancellationFeePaymentType)
				.OrderByDescending(p => p.PaymentDate)
				.ThenByDescending(p => p.PaymentId)
				.FirstOrDefaultAsync();

		public static async Task<Payment?> GetCancellationFeePaymentAsync(EasyRent_CheckingContext context, int rentalId)
			=> await context.Payments
				.AsNoTracking()
				.Where(p => p.RentalId == rentalId && p.PaymentType == CancellationFeePaymentType)
				.OrderByDescending(p => p.PaymentDate)
				.ThenByDescending(p => p.PaymentId)
				.FirstOrDefaultAsync();

		public static bool CanResolve(Rental rental, IEnumerable<Transit> transits)
		{
			if (rental.RentalStatus != RentalStatus.Approved)
			{
				return false;
			}

			return transits.Any(t => t.TripStatus is TripStatus.Scheduled or TripStatus.Delayed);
		}

		public static async Task<List<ResolveBookingVehicleOption>> GetReplacementCandidatesAsync(
			EasyRent_CheckingContext context,
			Rental rental,
			int currentVehicleId)
		{
			var vehicles = await context.Vehicles.AsNoTracking()
				.Where(v => v.IsActive
					&& v.Status == VehicleStatus.Available
					&& v.VehicleId != currentVehicleId
					&& v.PassengersCount >= rental.PassengerCount)
				.OrderBy(v => v.Brand)
				.ThenBy(v => v.Model)
				.ToListAsync();

			var candidates = new List<ResolveBookingVehicleOption>();
			foreach (var vehicle in vehicles)
			{
				var hasConflict = await RentalVehicleWorkflow.HasScheduleConflictAsync(
					context,
					[vehicle.VehicleId],
					rental.PickupDate,
					rental.ReturnDate,
					rental.RentalId);

				if (hasConflict)
				{
					continue;
				}

				candidates.Add(new ResolveBookingVehicleOption
				{
					VehicleId = vehicle.VehicleId,
					Title = $"{vehicle.Brand} {vehicle.Model} ({vehicle.PlateNumber})".Trim(),
					TypeLabel = VehicleTypes.DisplayUpper(vehicle.Type),
					PassengersCount = vehicle.PassengersCount,
					ImagePath = vehicle.ImagePath
				});
			}

			return candidates;
		}

		public static async Task<(bool Success, string? Error, Vehicle? NewVehicle)> ReplaceVehicleAsync(
			EasyRent_CheckingContext context,
			Rental rental,
			Transit? transit,
			int currentVehicleId,
			int newVehicleId,
			string reason)
		{
			if (newVehicleId <= 0 || newVehicleId == currentVehicleId)
			{
				return (false, "Please select a different replacement vehicle.", null);
			}

			var newVehicle = await context.Vehicles.FirstOrDefaultAsync(v => v.VehicleId == newVehicleId);
			if (newVehicle == null)
			{
				return (false, "Selected replacement vehicle was not found.", null);
			}

			if (!newVehicle.IsActive || newVehicle.Status != VehicleStatus.Available)
			{
				return (false, "Selected vehicle is not available for dispatch.", null);
			}

			if (newVehicle.PassengersCount < rental.PassengerCount)
			{
				return (false, "Replacement vehicle does not have enough passenger capacity.", null);
			}

			var hasConflict = await RentalVehicleWorkflow.HasScheduleConflictAsync(
				context,
				[newVehicleId],
				rental.PickupDate,
				rental.ReturnDate,
				rental.RentalId);

			if (hasConflict)
			{
				return (false, "Replacement vehicle is already booked for the same schedule.", null);
			}

			var rentalVehicleLine = await context.RentalVehicles
				.FirstOrDefaultAsync(rv => rv.RentalId == rental.RentalId && rv.VehicleId == currentVehicleId);

			if (rentalVehicleLine != null)
			{
				rentalVehicleLine.VehicleId = newVehicleId;
			}

			var transits = transit != null
				? await context.Transits
					.Where(t => t.TransitID == transit.TransitID)
					.ToListAsync()
				: await context.Transits
					.Where(t => t.RentalID == rental.RentalId && t.VehicleID == currentVehicleId)
					.ToListAsync();

			foreach (var trip in transits)
			{
				if (trip.TripStatus is TripStatus.Scheduled or TripStatus.Delayed)
				{
					trip.VehicleID = newVehicleId;

					// Reset preparation fields for the new vehicle
					trip.DepartureTime = null;
					trip.FuelLevelStart = null;
					trip.OdometerStart = null;
					trip.VehicleConditionStart = null;
					trip.PreTripImageFile = null;
				}
			}

			var noteLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm}] Vehicle replaced ({reason}): {currentVehicleId} → {newVehicleId}. Preparation data reset for new vehicle.";
			rental.Notes = string.IsNullOrWhiteSpace(rental.Notes)
				? noteLine
				: rental.Notes.TrimEnd() + Environment.NewLine + noteLine;

			await context.SaveChangesAsync();
			return (true, null, newVehicle);
		}

		public static bool HasPendingRefundRequest(Rental rental)
			=> rental.RefundRequestedAt != null
				&& rental.RefundedAt == null
				&& rental.RefundRejectedAt == null
				&& rental.RentalStatus == RentalStatus.Cancelled;

		public static bool IsRefunded(Rental rental)
			=> rental.RentalStatus == RentalStatus.Refunded || rental.RefundedAt != null;

		public static bool IsRefundRejected(Rental rental)
			=> rental.RentalStatus == RentalStatus.RefundRejected || rental.RefundRejectedAt != null;

		public static async Task ProcessCustomerRefundAsync(
			EasyRent_CheckingContext context,
			Rental rental,
			decimal refundAmount,
			string? receiptPath,
			string? notes)
		{
			if (!HasPendingRefundRequest(rental))
			{
				throw new InvalidOperationException("This refund request is no longer pending review.");
			}

			if (IsRefunded(rental))
			{
				throw new InvalidOperationException("This refund has already been processed.");
			}

			rental.RefundAmount = refundAmount > 0 ? refundAmount : null;
			rental.RefundedAt = DateTime.Now;
			rental.RefundReceiptImagePath = receiptPath;
			rental.RentalStatus = RentalStatus.Refunded;

			var noteLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm}] Customer refund approved: ₱{refundAmount:N2}.";
			if (!string.IsNullOrWhiteSpace(notes))
			{
				noteLine += $" {notes.Trim()}";
			}

			rental.Notes = string.IsNullOrWhiteSpace(rental.Notes)
				? noteLine
				: rental.Notes.TrimEnd() + Environment.NewLine + noteLine;

			await context.SaveChangesAsync();
		}

		public static async Task RejectCustomerRefundAsync(
			EasyRent_CheckingContext context,
			Rental rental,
			string rejectionReason)
		{
			if (!HasPendingRefundRequest(rental))
			{
				throw new InvalidOperationException("This refund request is no longer pending review.");
			}

			if (IsRefunded(rental) || IsRefundRejected(rental))
			{
				throw new InvalidOperationException("This refund has already been decided.");
			}

			rental.RentalStatus = RentalStatus.RefundRejected;
			rental.RefundRejectedAt = DateTime.Now;
			rental.RefundRejectionReason = rejectionReason.Trim();

			var noteLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm}] Customer refund rejected: {rejectionReason.Trim()}.";
			rental.Notes = string.IsNullOrWhiteSpace(rental.Notes)
				? noteLine
				: rental.Notes.TrimEnd() + Environment.NewLine + noteLine;

			await context.SaveChangesAsync();
		}

		public static async Task CancelWithRefundAsync(
			EasyRent_CheckingContext context,
			Rental rental,
			decimal refundAmount,
			string reason,
			string? receiptPath,
			string? notes)
		{
			rental.RentalStatus = RentalStatus.Cancelled;
			rental.CancelledAt = DateTime.Now;
			rental.CancellationFee = 0m;
			rental.CompanyCancellationReason = reason;
			rental.RefundAmount = refundAmount > 0 ? refundAmount : null;
			rental.RefundedAt = refundAmount > 0 ? DateTime.Now : null;
			rental.RefundReceiptImagePath = receiptPath;
			if (refundAmount > 0)
			{
				rental.RentalStatus = RentalStatus.Refunded;
				rental.RefundRequestedAt = null;
				rental.RefundRequestedAmount = null;
			}

			var noteLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm}] Company cancellation ({reason}). Refund: ₱{refundAmount:N2}.";
			if (!string.IsNullOrWhiteSpace(notes))
			{
				noteLine += $" {notes.Trim()}";
			}

			rental.Notes = string.IsNullOrWhiteSpace(rental.Notes)
				? noteLine
				: rental.Notes.TrimEnd() + Environment.NewLine + noteLine;

			var transits = await context.Transits
				.Where(t => t.RentalID == rental.RentalId)
				.ToListAsync();

			foreach (var trip in transits)
			{
				if (trip.TripStatus is not TripStatus.Completed)
				{
					trip.TripStatus = TripStatus.Cancelled;
				}
			}

			await context.SaveChangesAsync();
		}
	}
}
