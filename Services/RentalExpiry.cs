using EasyRent_Checking.Data;
using EasyRent_Checking.Models;
using Microsoft.EntityFrameworkCore;

namespace EasyRent_Checking.Services
{
	public static class RentalExpiry
	{
		public static async Task ExpireOverdueReservesAsync(EasyRent_CheckingContext context, CancellationToken ct = default)
		{
			var now = DateTime.Now;
			var overdue = await context.Rentals
				.Where(r =>
					r.RentalOption == RentalOption.Reserve
					&& r.RentalStatus == RentalStatus.Pending
					&& r.PaymentDueAt != null
					&& r.PaymentDueAt < now
					&& !context.Payments.Any(p => p.RentalId == r.RentalId))
				.ToListAsync(ct);

			if (overdue.Count == 0)
			{
				return;
			}

			foreach (var rental in overdue)
			{
				rental.RentalStatus = RentalStatus.Expired;
			}

			await context.SaveChangesAsync(ct);
		}

		public static bool IsActiveHoldStatus(RentalStatus status)
			=> status is not RentalStatus.Cancelled and not RentalStatus.Expired;
	}
}
