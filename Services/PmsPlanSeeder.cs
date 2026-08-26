using EasyRent_Checking.Data;
using EasyRent_Checking.Models;
using Microsoft.EntityFrameworkCore;

namespace EasyRent_Checking.Services
{
	public static class PmsPlanSeeder
	{
		public static async Task AttachOrphanLogsAsync(EasyRent_CheckingContext db)
		{
			var orphanLogs = await db.MaintenanceLogs
				.Where(l => l.MaintenancePlanId == null)
				.ToListAsync();
			if (orphanLogs.Count == 0)
			{
				return;
			}

			var plans = await db.MaintenancePlans.ToListAsync();
			var planLookup = plans.ToDictionary(p => (p.VehicleId, p.Type));
			foreach (var log in orphanLogs)
			{
				if (planLookup.TryGetValue((log.VehicleId, log.Type), out var plan))
				{
					log.MaintenancePlanId = plan.MaintenancePlanId;
				}
			}

			await db.SaveChangesAsync();
		}
	}
}
