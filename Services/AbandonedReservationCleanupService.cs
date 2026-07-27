using EasyRent_Checking.Data;
using EasyRent_Checking.Models;
using Microsoft.EntityFrameworkCore;

namespace EasyRent_Checking.Services
{
	/// <summary>
	/// Releases abandoned soft locks when customers never submit payment proof.
	/// </summary>
	public class AbandonedReservationCleanupService : BackgroundService
	{
		private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

		private readonly IServiceScopeFactory _scopeFactory;
		private readonly ILogger<AbandonedReservationCleanupService> _logger;

		public AbandonedReservationCleanupService(
			IServiceScopeFactory scopeFactory,
			ILogger<AbandonedReservationCleanupService> logger)
		{
			_scopeFactory = scopeFactory;
			_logger = logger;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					await ReleaseExpiredSoftLocksAsync(stoppingToken);
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					_logger.LogError(ex, "Failed to release abandoned reservation soft locks.");
				}

				try
				{
					await Task.Delay(Interval, stoppingToken);
				}
				catch (OperationCanceledException)
				{
					break;
				}
			}
		}

		private async Task ReleaseExpiredSoftLocksAsync(CancellationToken cancellationToken)
		{
			using var scope = _scopeFactory.CreateScope();
			var context = scope.ServiceProvider.GetRequiredService<EasyRent_CheckingContext>();
			var now = DateTime.UtcNow;

			var expired = await context.Reservation
				.Where(r =>
					r.Status == ReservationStatus.Pending &&
					r.LockedUntilUtc != null &&
					r.LockedUntilUtc <= now)
				.ToListAsync(cancellationToken);

			if (expired.Count == 0)
			{
				return;
			}

			foreach (var reservation in expired)
			{
				reservation.Status = ReservationStatus.Cancelled;
				reservation.LockedUntilUtc = null;
				reservation.IsDraft = false;
			}

			await context.SaveChangesAsync(cancellationToken);
			_logger.LogInformation("Released {Count} abandoned reservation soft lock(s).", expired.Count);
		}
	}
}
