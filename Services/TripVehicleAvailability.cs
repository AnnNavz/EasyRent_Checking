using EasyRent_Checking.Data;
using EasyRent_Checking.Models;
using Microsoft.EntityFrameworkCore;

namespace EasyRent_Checking.Services
{
	public enum TripVehicleAvailabilityLevel
	{
		None,
		Warning,
		Blocked
	}

	public enum TripVehicleAvailabilitySource
	{
		None,
		Fleet,
		Maintenance,
		Incident,
		PmsDue
	}

	public sealed class TripVehicleAvailabilityResult
	{
		public TripVehicleAvailabilityLevel Level { get; init; } = TripVehicleAvailabilityLevel.None;

		public TripVehicleAvailabilitySource Source { get; init; } = TripVehicleAvailabilitySource.None;

		public string Title { get; init; } = string.Empty;

		public string Message { get; init; } = string.Empty;

		public string? SourceLabel { get; init; }

		public string? SourceUrl { get; set; }

		public int? MaintenancePlanId { get; init; }

		public int? MaintenanceLogId { get; init; }

		public int? IncidentReportId { get; init; }

		public bool BlocksDispatch => Level == TripVehicleAvailabilityLevel.Blocked;

		public bool HasAlert => Level != TripVehicleAvailabilityLevel.None;

		public static TripVehicleAvailabilityResult Clear() => new();
	}

	public static class TripVehicleAvailability
	{
		/// <summary>Warn when pickup is within this many days and PMS is due before/on pickup.</summary>
		public const int EarlyWarningLeadDays = 7;

		public static async Task<TripVehicleAvailabilityResult> EvaluateAsync(
			EasyRent_CheckingContext context,
			Transit transit)
		{
			if (transit.TripStatus is TripStatus.Cancelled)
			{
				return TripVehicleAvailabilityResult.Clear();
			}

			if (transit.Rental?.RentalStatus is RentalStatus.Cancelled or RentalStatus.Expired)
			{
				return TripVehicleAvailabilityResult.Clear();
			}

			var canBlockDispatch = transit.TripStatus is TripStatus.Scheduled or TripStatus.Delayed;

			var vehicleId = transit.VehicleID > 0
				? transit.VehicleID
				: transit.Rental == null
					? 0
					: RentalVehicleWorkflow.ResolveVehicleIds(transit.Rental).FirstOrDefault();
			if (vehicleId <= 0)
			{
				return Blocked(
					TripVehicleAvailabilitySource.Fleet,
					"Vehicle unavailable for this booking",
					"No vehicle is assigned to this trip yet.",
					sourceLabel: null);
			}

			var vehicle = transit.Vehicle
				?? await context.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.VehicleId == vehicleId);
			if (vehicle == null)
			{
				return Blocked(
					TripVehicleAvailabilitySource.Fleet,
					"Vehicle unavailable for this booking",
					"The assigned vehicle could not be found.",
					sourceLabel: null);
			}

			var vehicleLabel = FormatVehicleLabel(vehicle);
			var rental = transit.Rental;
			var pickupDate = rental?.PickupDate;
			var pickupTime = rental?.PickupTime;
			var scheduleLine = pickupDate != null
				? $"Pickup: {pickupDate.Value:MMM d, yyyy}{(pickupTime != null ? $" · {pickupTime:h:mm tt}" : "")}"
				: null;

			if (!vehicle.IsActive)
			{
				return Blocked(
					TripVehicleAvailabilitySource.Fleet,
					"Vehicle unavailable for this booking",
					$"{vehicleLabel} has been deactivated and cannot be dispatched.{Suffix(scheduleLine)}",
					sourceLabel: "View vehicle");
			}

			var inProgressLog = await context.MaintenanceLogs
				.AsNoTracking()
				.Where(m => m.VehicleId == vehicle.VehicleId && m.Status == MaintenanceStatus.InProgress)
				.OrderByDescending(m => m.StartedAt)
				.ThenByDescending(m => m.MaintenanceLogId)
				.FirstOrDefaultAsync();

			if (inProgressLog != null || vehicle.Status == VehicleStatus.InMaintenance)
			{
				var typeLabel = inProgressLog?.Type.ToString() ?? "maintenance";
				return Blocked(
					TripVehicleAvailabilitySource.Maintenance,
					"Vehicle unavailable for this booking",
					$"{vehicleLabel} is in maintenance ({typeLabel}). Trip preparation and dispatch are blocked until service is completed.{Suffix(scheduleLine)}",
					sourceLabel: inProgressLog?.MaintenancePlanId != null ? "View checkup" : "View maintenance",
					maintenancePlanId: inProgressLog?.MaintenancePlanId,
					maintenanceLogId: inProgressLog?.MaintenanceLogId);
			}

			var openIncidents = await context.IncidentReports
				.AsNoTracking()
				.Where(i => i.VehicleId == vehicle.VehicleId
					&& (i.Status == IncidentStatus.Reported
						|| i.Status == IncidentStatus.UnderReview
						|| i.Status == IncidentStatus.InRepair))
				.OrderByDescending(i => i.CreatedAt)
				.ThenByDescending(i => i.IncidentReportId)
				.ToListAsync();

			var blockingIncident = openIncidents.FirstOrDefault(i =>
				i.IsUndrivable || i.Type == IncidentType.Breakdown);
			var openIncident = blockingIncident ?? openIncidents.FirstOrDefault();

			if (openIncident != null || vehicle.Status == VehicleStatus.Unavailable)
			{
				var incidentLabel = openIncident != null
					? $"{IncidentReport.FormatReference(openIncident.IncidentReportId)} ({openIncident.Type})"
					: "open incident";
				var blocksThisTrip = canBlockDispatch
					&& (blockingIncident != null || vehicle.Status == VehicleStatus.Unavailable);
				var linkedToTrip = openIncident?.TransitID == transit.TransitID;

				if (blocksThisTrip)
				{
					return Blocked(
						TripVehicleAvailabilitySource.Incident,
						"Vehicle unavailable for this booking",
						$"{vehicleLabel} is unavailable due to {incidentLabel}. Resolve the incident or replace the vehicle before dispatch.{Suffix(scheduleLine)}",
						sourceLabel: openIncident != null ? "View incident" : null,
						incidentReportId: openIncident?.IncidentReportId);
				}

				if (openIncident != null && (canBlockDispatch || linkedToTrip || transit.TripStatus == TripStatus.InTransit))
				{
					return Warning(
						"Vehicle incident on record",
						$"{vehicleLabel} has {incidentLabel}. Review the incident before dispatching this vehicle.{Suffix(scheduleLine)}",
						sourceLabel: "View incident",
						incidentReportId: openIncident.IncidentReportId);
				}
			}

			if (!canBlockDispatch)
			{
				return TripVehicleAvailabilityResult.Clear();
			}

			if (pickupDate == null)
			{
				return TripVehicleAvailabilityResult.Clear();
			}

			var today = DateOnly.FromDateTime(DateTime.Today);
			var daysUntilPickup = pickupDate.Value.DayNumber - today.DayNumber;
			if (daysUntilPickup < 0)
			{
				return TripVehicleAvailabilityResult.Clear();
			}

			var plans = await context.MaintenancePlans
				.AsNoTracking()
				.Where(p => p.VehicleId == vehicle.VehicleId)
				.ToListAsync();

			foreach (var plan in plans)
			{
				if (plan.NextDueDate == pickupDate)
				{
					return Warning(
						"Maintenance due on pickup day",
						$"{vehicleLabel} has {plan.Type} scheduled on the rental pickup date ({pickupDate.Value:MMM d, yyyy}). Complete service early or plan a vehicle replacement before dispatch.",
						sourceLabel: "View checkup",
						maintenancePlanId: plan.MaintenancePlanId);
				}
			}

			if (daysUntilPickup <= EarlyWarningLeadDays)
			{
				foreach (var plan in plans)
				{
					var dueKind = PmsRules.Classify(plan, vehicle, inProgress: false);
					if (dueKind is not (PmsDueKind.Overdue or PmsDueKind.DueNow or PmsDueKind.DueSoon))
					{
						continue;
					}

					if (plan.NextDueDate != null && plan.NextDueDate <= pickupDate)
					{
						var dueText = PmsRules.FormatDue(plan);
						return Warning(
							"Maintenance due before pickup",
							$"{vehicleLabel} has {plan.Type} {dueText} and pickup is in {daysUntilPickup} day{(daysUntilPickup == 1 ? "" : "s")} ({pickupDate.Value:MMM d, yyyy}). Service the vehicle or replace it before the rental date.",
							sourceLabel: "View checkup",
							maintenancePlanId: plan.MaintenancePlanId);
					}
				}
			}

			return TripVehicleAvailabilityResult.Clear();
		}

		private static TripVehicleAvailabilityResult Blocked(
			TripVehicleAvailabilitySource source,
			string title,
			string message,
			string? sourceLabel,
			int? maintenancePlanId = null,
			int? maintenanceLogId = null,
			int? incidentReportId = null)
			=> new()
			{
				Level = TripVehicleAvailabilityLevel.Blocked,
				Source = source,
				Title = title,
				Message = message,
				SourceLabel = sourceLabel,
				MaintenancePlanId = maintenancePlanId,
				MaintenanceLogId = maintenanceLogId,
				IncidentReportId = incidentReportId
			};

		private static TripVehicleAvailabilityResult Warning(
			string title,
			string message,
			string? sourceLabel,
			int? maintenancePlanId = null,
			int? incidentReportId = null)
			=> new()
			{
				Level = TripVehicleAvailabilityLevel.Warning,
				Source = incidentReportId != null
					? TripVehicleAvailabilitySource.Incident
					: TripVehicleAvailabilitySource.PmsDue,
				Title = title,
				Message = message,
				SourceLabel = sourceLabel,
				MaintenancePlanId = maintenancePlanId,
				IncidentReportId = incidentReportId
			};

		private static string FormatVehicleLabel(Vehicle vehicle)
		{
			var name = $"{vehicle.Brand} {vehicle.Model}".Trim();
			return string.IsNullOrEmpty(name)
				? vehicle.PlateNumber
				: $"{name} ({vehicle.PlateNumber})";
		}

		private static string Suffix(string? scheduleLine)
			=> string.IsNullOrWhiteSpace(scheduleLine) ? string.Empty : $" {scheduleLine}.";
	}
}
