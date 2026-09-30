using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Data;
using EasyRent_Checking.Models;

namespace EasyRent_Checking.Services
{
	public static class VehicleStatusSync
	{
		public static async Task ApplyAsync(EasyRent_CheckingContext context, int vehicleId)
		{
			var vehicle = await context.Vehicles.FirstOrDefaultAsync(v => v.VehicleId == vehicleId);
			if (vehicle == null)
			{
				return;
			}

			var hasPmsInProgress = await context.MaintenanceLogs.AnyAsync(m =>
				m.VehicleId == vehicleId && m.Status == MaintenanceStatus.InProgress);

			var hasOpenUndrivableIncident = await context.IncidentReports.AnyAsync(i =>
				i.VehicleId == vehicleId
				&& (i.IsUndrivable || i.Type == IncidentType.Breakdown)
				&& (i.Status == IncidentStatus.Reported
					|| i.Status == IncidentStatus.UnderReview
					|| i.Status == IncidentStatus.InRepair));

			if (hasPmsInProgress)
			{
				vehicle.Status = VehicleStatus.InMaintenance;
			}
			else if (hasOpenUndrivableIncident)
			{
				vehicle.Status = VehicleStatus.Unavailable;
			}
			else if (vehicle.Status is VehicleStatus.InMaintenance or VehicleStatus.Unavailable)
			{
				vehicle.Status = VehicleStatus.Available;
			}

			await context.SaveChangesAsync();
		}
	}
}
