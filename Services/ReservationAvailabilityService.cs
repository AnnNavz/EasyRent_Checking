using EasyRent_Checking.Data;
using EasyRent_Checking.Models;
using Microsoft.EntityFrameworkCore;

namespace EasyRent_Checking.Services
{
	public class ReservationAvailabilityService
	{
		public static readonly TimeSpan SoftLockDuration = TimeSpan.FromMinutes(30);

		private readonly EasyRent_CheckingContext _context;

		public ReservationAvailabilityService(EasyRent_CheckingContext context)
		{
			_context = context;
		}

		public async Task<bool> HasConflictAsync(
			int vehicleId,
			DateOnly pickupDate,
			DateOnly returnDate,
			int? excludeReservationId = null,
			CancellationToken cancellationToken = default)
		{
			var now = DateTime.UtcNow;
			var query = BlockingReservationsQuery(vehicleId, now);

			if (excludeReservationId.HasValue)
			{
				query = query.Where(r => r.ReservationID != excludeReservationId.Value);
			}

			return await query.AnyAsync(
				r => r.PickupDate <= returnDate && r.ReturnDate >= pickupDate,
				cancellationToken);
		}

		public async Task<IReadOnlyList<(DateOnly PickupDate, DateOnly ReturnDate)>> GetBlockedRangesAsync(
			int vehicleId,
			int? excludeReservationId = null,
			CancellationToken cancellationToken = default)
		{
			var now = DateTime.UtcNow;
			var query = BlockingReservationsQuery(vehicleId, now);

			if (excludeReservationId.HasValue)
			{
				query = query.Where(r => r.ReservationID != excludeReservationId.Value);
			}

			var rows = await query
				.Select(r => new { r.PickupDate, r.ReturnDate })
				.ToListAsync(cancellationToken);

			return rows.Select(r => (r.PickupDate, r.ReturnDate)).ToList();
		}

		private IQueryable<Reservation> BlockingReservationsQuery(int vehicleId, DateTime utcNow)
		{
			return _context.Reservation.Where(r =>
				r.VehicleId == vehicleId &&
				(
					r.Status == ReservationStatus.Confirmed ||
					r.Status == ReservationStatus.InProgress ||
					(
						r.Status == ReservationStatus.Pending &&
						(r.LockedUntilUtc == null || r.LockedUntilUtc > utcNow)
					)
				));
		}
	}
}
