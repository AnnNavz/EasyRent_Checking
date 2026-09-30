using EasyRent_Checking.Data;
using EasyRent_Checking.Models;
using EasyRent_Checking.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EasyRent_Checking.Services
{
	public static class TransitIssueSync
	{
		public static async Task SyncForIncidentAsync(EasyRent_CheckingContext context, int incidentReportId)
		{
			var incident = await context.IncidentReports
				.AsNoTracking()
				.FirstOrDefaultAsync(i => i.IncidentReportId == incidentReportId);
			if (incident == null)
			{
				return;
			}

			if (incident.Status is IncidentStatus.Closed or IncidentStatus.Dismissed)
			{
				return;
			}

			var blocksTrip = incident.IsUndrivable || incident.Type == IncidentType.Breakdown;
			var transits = await GetAffectedTransitsAsync(context, incident.VehicleId, incident.TransitID);

			foreach (var transit in transits)
			{
				await EnsureLinkAsync(
					context,
					transit.TransitID,
					TransitIssueSource.Incident,
					incidentReportId,
					null,
					blocksTrip);
			}

			await context.SaveChangesAsync();
		}

		public static async Task SyncForMaintenanceLogAsync(EasyRent_CheckingContext context, int maintenanceLogId)
		{
			var log = await context.MaintenanceLogs
				.AsNoTracking()
				.FirstOrDefaultAsync(m => m.MaintenanceLogId == maintenanceLogId);
			if (log == null)
			{
				return;
			}

			if (log.Status != MaintenanceStatus.InProgress)
			{
				return;
			}

			var transits = await GetAffectedTransitsAsync(context, log.VehicleId, null);
			foreach (var transit in transits)
			{
				await EnsureLinkAsync(
					context,
					transit.TransitID,
					TransitIssueSource.Maintenance,
					null,
					maintenanceLogId,
					blocksTrip: true);
			}

			await context.SaveChangesAsync();
		}

		public static async Task SyncForRentalAsync(EasyRent_CheckingContext context, int rentalId)
		{
			var transits = await context.Transits
				.AsNoTracking()
				.Where(t => t.RentalID == rentalId
					&& t.TripStatus != TripStatus.Cancelled
					&& t.TripStatus != TripStatus.Completed)
				.ToListAsync();

			foreach (var transit in transits)
			{
				await SyncForTransitVehicleAsync(context, transit.TransitID, transit.VehicleID);
			}

			await context.SaveChangesAsync();
		}

		public static async Task<bool> HasBlockingIssueForRentalAsync(
			EasyRent_CheckingContext context,
			int rentalId,
			int? transitId = null)
		{
			var query = context.TransitIssueLinks
				.AsNoTracking()
				.Where(l => l.ResolvedAt == null
					&& l.BlocksTrip
					&& l.Transit!.RentalID == rentalId);

			if (transitId.HasValue)
			{
				query = query.Where(l => l.TransitID == transitId.Value);
			}

			return await query.AnyAsync();
		}

		public static async Task ResolveForRentalAsync(
			EasyRent_CheckingContext context,
			int rentalId,
			int? transitId,
			string resolutionAction,
			string? notes)
		{
			var query = context.TransitIssueLinks
				.Where(l => l.ResolvedAt == null && l.Transit!.RentalID == rentalId);

			if (transitId.HasValue)
			{
				query = query.Where(l => l.TransitID == transitId.Value);
			}

			var links = await query.ToListAsync();
			if (links.Count == 0)
			{
				return;
			}

			var now = DateTime.Now;
			foreach (var link in links)
			{
				link.ResolvedAt = now;
				link.ResolutionAction = resolutionAction;
				link.ResolutionNotes = notes;
			}

			await context.SaveChangesAsync();
		}

		public static async Task<List<TransitIssueAlertViewModel>> LoadAlertsForRentalAsync(
			EasyRent_CheckingContext context,
			int rentalId)
		{
			var links = await context.TransitIssueLinks
				.AsNoTracking()
				.Include(l => l.IncidentReport)
				.Include(l => l.MaintenanceLog)!
					.ThenInclude(m => m!.Vehicle)
				.Include(l => l.Transit)!
					.ThenInclude(t => t!.Vehicle)
				.Where(l => l.ResolvedAt == null && l.Transit!.RentalID == rentalId)
				.OrderByDescending(l => l.BlocksTrip)
				.ThenByDescending(l => l.LinkedAt)
				.ToListAsync();

			return links
				.Select(MapAlert)
				.ToList();
		}

		private static async Task SyncForTransitVehicleAsync(
			EasyRent_CheckingContext context,
			int transitId,
			int vehicleId)
		{
			var openIncidents = await context.IncidentReports
				.AsNoTracking()
				.Where(i => i.VehicleId == vehicleId
					&& (i.Status == IncidentStatus.Reported
						|| i.Status == IncidentStatus.UnderReview
						|| i.Status == IncidentStatus.InRepair))
				.ToListAsync();

			foreach (var incident in openIncidents)
			{
				var blocksTrip = incident.IsUndrivable || incident.Type == IncidentType.Breakdown;
				await EnsureLinkAsync(
					context,
					transitId,
					TransitIssueSource.Incident,
					incident.IncidentReportId,
					null,
					blocksTrip);
			}

			var inProgressLog = await context.MaintenanceLogs
				.AsNoTracking()
				.Where(m => m.VehicleId == vehicleId && m.Status == MaintenanceStatus.InProgress)
				.OrderByDescending(m => m.StartedAt)
				.FirstOrDefaultAsync();

			if (inProgressLog != null)
			{
				await EnsureLinkAsync(
					context,
					transitId,
					TransitIssueSource.Maintenance,
					null,
					inProgressLog.MaintenanceLogId,
					blocksTrip: true);
			}
		}

		private static async Task<List<Transit>> GetAffectedTransitsAsync(
			EasyRent_CheckingContext context,
			int vehicleId,
			int? preferredTransitId)
		{
			var transits = await context.Transits
				.AsNoTracking()
				.Where(t => t.VehicleID == vehicleId
					&& t.TripStatus != TripStatus.Cancelled
					&& t.TripStatus != TripStatus.Completed)
				.ToListAsync();

			if (preferredTransitId.HasValue
				&& transits.All(t => t.TransitID != preferredTransitId.Value))
			{
				var preferred = await context.Transits
					.AsNoTracking()
					.FirstOrDefaultAsync(t => t.TransitID == preferredTransitId.Value
						&& t.TripStatus != TripStatus.Cancelled
						&& t.TripStatus != TripStatus.Completed);
				if (preferred != null)
				{
					transits.Add(preferred);
				}
			}

			return transits;
		}

		private static async Task EnsureLinkAsync(
			EasyRent_CheckingContext context,
			int transitId,
			TransitIssueSource source,
			int? incidentReportId,
			int? maintenanceLogId,
			bool blocksTrip)
		{
			var exists = await context.TransitIssueLinks.AnyAsync(l =>
				l.TransitID == transitId
				&& l.ResolvedAt == null
				&& l.Source == source
				&& ((incidentReportId != null && l.IncidentReportId == incidentReportId)
					|| (maintenanceLogId != null && l.MaintenanceLogId == maintenanceLogId)));

			if (exists)
			{
				return;
			}

			context.TransitIssueLinks.Add(new TransitIssueLink
			{
				TransitID = transitId,
				Source = source,
				IncidentReportId = incidentReportId,
				MaintenanceLogId = maintenanceLogId,
				BlocksTrip = blocksTrip,
				LinkedAt = DateTime.Now
			});
		}

		private static TransitIssueAlertViewModel MapAlert(TransitIssueLink link)
		{
			if (link.Source == TransitIssueSource.Incident && link.IncidentReport != null)
			{
				var incident = link.IncidentReport;
				var vehicle = link.Transit?.Vehicle;
				var vehicleLabel = vehicle != null
					? $"{vehicle.Brand} {vehicle.Model} ({vehicle.PlateNumber})".Trim()
					: $"Vehicle #{incident.VehicleId}";

				return new TransitIssueAlertViewModel
				{
					TransitIssueLinkId = link.TransitIssueLinkId,
					TransitId = link.TransitID,
					Source = TransitIssueSource.Incident,
					IncidentReportId = incident.IncidentReportId,
					SourceLabel = "Incident Report",
					ReferenceLabel = IncidentReport.FormatReference(incident.IncidentReportId),
					VehicleLabel = vehicleLabel,
					IssueDescription = incident.Description,
					ReportedAt = incident.OccurredAt,
					CurrentCondition = FormatIncidentCondition(incident),
					BlocksTrip = link.BlocksTrip
				};
			}

			var log = link.MaintenanceLog;
			var logVehicle = log?.Vehicle ?? link.Transit?.Vehicle;
			var maintenanceVehicleLabel = logVehicle != null
				? $"{logVehicle.Brand} {logVehicle.Model} ({logVehicle.PlateNumber})".Trim()
				: $"Vehicle #{log?.VehicleId}";

			return new TransitIssueAlertViewModel
			{
				TransitIssueLinkId = link.TransitIssueLinkId,
				TransitId = link.TransitID,
				Source = TransitIssueSource.Maintenance,
				MaintenanceLogId = log?.MaintenanceLogId,
				MaintenancePlanId = log?.MaintenancePlanId,
				SourceLabel = "Maintenance Log",
				ReferenceLabel = log != null ? $"MNT-{log.MaintenanceLogId:D5}" : null,
				VehicleLabel = maintenanceVehicleLabel,
				IssueDescription = string.IsNullOrWhiteSpace(log?.Description)
					? $"{log?.Type} service in progress."
					: log!.Description!,
				ReportedAt = log?.StartedAt ?? log?.CreatedAt ?? link.LinkedAt,
				CurrentCondition = log?.Status.ToString() ?? "In maintenance",
				BlocksTrip = link.BlocksTrip
			};
		}

		private static string FormatIncidentCondition(IncidentReport incident)
		{
			if (incident.IsUndrivable || incident.Type == IncidentType.Breakdown)
			{
				return "Not fit for transit";
			}

			return $"{incident.Status} · {incident.Severity} severity";
		}
	}
}
