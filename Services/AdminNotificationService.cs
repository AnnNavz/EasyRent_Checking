using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Data;
using EasyRent_Checking.Models;
using EasyRent_Checking.ViewModels;

namespace EasyRent_Checking.Services
{
	public class AdminNotificationService
	{
		private readonly EasyRent_CheckingContext _context;

		public AdminNotificationService(EasyRent_CheckingContext context)
		{
			_context = context;
		}

		public async Task<AdminNotificationFeed> GetFeedAsync()
		{
			var now = DateTime.Now;
			var feed = new AdminNotificationFeed();
			var recent = new List<(DateTime At, AdminNotificationItem Item)>();

			var plans = await _context.MaintenancePlans
				.AsNoTracking()
				.Include(p => p.Vehicle)
				.Include(p => p.Logs)
				.Where(p => p.Vehicle != null)
				.ToListAsync();

			var classified = plans
				.Select(p =>
				{
					var vehicle = p.Vehicle!;
					var open = p.Logs.Any(l => l.Status == MaintenanceStatus.InProgress);
					return new
					{
						Plan = p,
						Vehicle = vehicle,
						Kind = PmsRules.Classify(p, vehicle, open)
					};
				})
				.ToList();

			var overdue = classified.Where(x => x.Kind == PmsDueKind.Overdue).ToList();
			if (overdue.Count > 0)
			{
				feed.Critical.Add(Item(
					"maint-overdue-summary",
					"maintenance",
					"danger",
					"bi-exclamation-triangle",
					overdue.Count == 1 ? "1 maintenance overdue" : $"{overdue.Count} maintenance overdue",
					"Vehicles require immediate attention",
					now,
					"/Maintenance/Index?currentFilter=Overdue"));

				foreach (var row in overdue.Take(5))
				{
					var due = row.Plan.NextDueDate;
					var detail = due != null
						? $"{CheckupShort(row.Plan.Type)} was due on {due.Value.ToDateTime(TimeOnly.MinValue):MMMM d, yyyy}."
						: $"{CheckupShort(row.Plan.Type)} is past the scheduled interval.";
					var at = due?.ToDateTime(TimeOnly.MinValue) ?? now;
					feed.Critical.Add(Item(
						"maint-overdue-" + row.Plan.MaintenancePlanId,
						"maintenance",
						"danger",
						"bi-exclamation-triangle",
						$"Vehicle {row.Vehicle.PlateNumber} is overdue",
						detail,
						at,
						"/Maintenance/Details/" + row.Plan.MaintenancePlanId));
				}
			}

			foreach (var row in classified.Where(x => x.Kind == PmsDueKind.DueNow).Take(4))
			{
				feed.Critical.Add(Item(
					"maint-duenow-" + row.Plan.MaintenancePlanId,
					"maintenance",
					"danger",
					"bi-exclamation-triangle",
					$"Vehicle {row.Vehicle.PlateNumber} is due now",
					$"{CheckupShort(row.Plan.Type)} needs attention today.",
					now,
					"/Maintenance/Details/" + row.Plan.MaintenancePlanId));
			}

			var pendingRentals = await _context.Rentals
				.AsNoTracking()
				.Include(r => r.Details)
					.ThenInclude(d => d!.Vehicle)
				.Where(r => r.RentalStatus == RentalStatus.Pending)
				.OrderByDescending(r => r.RentalId)
				.Take(8)
				.ToListAsync();

			foreach (var rental in pendingRentals)
			{
				var plate = rental.Details?.Vehicle?.PlateNumber ?? "a vehicle";
				var pickup = rental.Details == null
					? now
					: rental.Details.PickupDate.ToDateTime(rental.Details.PickupTime);
				recent.Add((pickup, Item(
					"booking-" + rental.RentalId,
					"booking",
					"booking",
					"bi-calendar-check",
					"New booking received",
					$"Booking #{rental.RentalId} — {rental.CustomerName} requested {plate}.",
					pickup,
					"/Rentals/Details/" + rental.RentalId)));
			}

			foreach (var row in classified.Where(x => x.Kind == PmsDueKind.DueSoon).Take(6))
			{
				var due = row.Plan.NextDueDate;
				var at = due?.ToDateTime(TimeOnly.MinValue) ?? now;
				var when = due != null
					? due.Value.ToDateTime(TimeOnly.MinValue).ToString("MMMM d, yyyy")
					: "soon";
				recent.Add((at, Item(
					"maint-soon-" + row.Plan.MaintenancePlanId,
					"maintenance",
					"warn",
					"bi-exclamation-circle",
					"Upcoming maintenance",
					$"{row.Vehicle.PlateNumber} — {CheckupShort(row.Plan.Type)} is due {when}.",
					at,
					"/Maintenance/Details/" + row.Plan.MaintenancePlanId)));
			}

			var recentLogs = await _context.MaintenanceLogs
				.AsNoTracking()
				.Include(l => l.Vehicle)
				.Where(l => l.Status == MaintenanceStatus.InProgress
					|| (l.Status == MaintenanceStatus.Completed && (l.CompletedAt ?? l.CreatedAt) >= now.AddDays(-14))
					|| (l.Status == MaintenanceStatus.Scheduled && l.CreatedAt >= now.AddDays(-14)))
				.OrderByDescending(l => l.CompletedAt ?? l.StartedAt ?? l.CreatedAt)
				.Take(10)
				.ToListAsync();

			foreach (var log in recentLogs)
			{
				var plate = log.Vehicle?.PlateNumber ?? "Vehicle";
				var url = log.MaintenancePlanId != null
					? "/Maintenance/Details/" + log.MaintenancePlanId
					: "/Maintenance/Index";
				if (log.Status == MaintenanceStatus.Completed)
				{
					var at = log.CompletedAt ?? log.CreatedAt;
					recent.Add((at, Item(
						"maint-done-" + log.MaintenanceLogId,
						"maintenance",
						"success",
						"bi-check-circle",
						"Completed maintenance",
						$"{CheckupShort(log.Type)} for {plate} was completed.",
						at,
						url)));
				}
				else if (log.Status == MaintenanceStatus.InProgress)
				{
					var at = log.StartedAt ?? log.CreatedAt;
					recent.Add((at, Item(
						"maint-progress-" + log.MaintenanceLogId,
						"maintenance",
						"maint",
						"bi-wrench",
						"Maintenance in progress",
						$"{CheckupShort(log.Type)} for {plate} is currently being serviced.",
						at,
						url)));
				}
				else
				{
					recent.Add((log.CreatedAt, Item(
						"maint-sched-" + log.MaintenanceLogId,
						"maintenance",
						"maint",
						"bi-wrench",
						"Scheduled maintenance",
						$"{CheckupShort(log.Type)} for {plate} is scheduled on {log.ScheduledDate.ToDateTime(TimeOnly.MinValue):MMMM d, yyyy}.",
						log.CreatedAt,
						url)));
				}
			}

			var incidents = await _context.IncidentReports
				.AsNoTracking()
				.Include(i => i.Vehicle)
				.Where(i => i.Status == IncidentStatus.Reported || i.Status == IncidentStatus.UnderReview)
				.OrderByDescending(i => i.CreatedAt)
				.Take(6)
				.ToListAsync();

			foreach (var incident in incidents)
			{
				var plate = incident.Vehicle?.PlateNumber ?? "a vehicle";
				var title = incident.Status == IncidentStatus.Reported
					? "New incident reported"
					: "Incident under review";
				recent.Add((incident.CreatedAt, Item(
					"incident-" + incident.IncidentReportId,
					"system",
					"warn",
					"bi-exclamation-triangle",
					title,
					$"{incident.Type} involving {plate} at {incident.Location}.",
					incident.CreatedAt,
					"/Incidents/Details/" + incident.IncidentReportId)));
			}

			var pendingCustomers = await _context.CustomerProfiles
				.AsNoTracking()
				.Include(c => c.User)
				.Where(c => c.Status == Status.Pending)
				.OrderByDescending(c => c.User != null ? c.User.CreatedAt : DateTime.MinValue)
				.Take(5)
				.ToListAsync();

			foreach (var customer in pendingCustomers)
			{
				var at = customer.User?.CreatedAt ?? now;
				recent.Add((at, Item(
					"customer-" + customer.CustomerId,
					"system",
					"system",
					"bi-person-plus",
					"Customer pending approval",
					$"{customer.FullName} registered and is waiting for account approval.",
					at,
					"/Customers/Details?customerid=" + customer.CustomerId)));
			}

			feed.Items = recent
				.OrderByDescending(x => x.At)
				.Select(x => x.Item)
				.Take(16)
				.ToList();

			return feed;
		}

		private static AdminNotificationItem Item(
			string id,
			string category,
			string tone,
			string icon,
			string title,
			string detail,
			DateTime occurredAt,
			string url)
			=> new()
			{
				Id = id,
				Category = category,
				Tone = tone,
				Icon = icon,
				Title = title,
				Detail = detail,
				TimeAgo = FormatRelative(occurredAt),
				Url = url
			};

		private static string CheckupShort(MaintenanceType type)
			=> type switch
			{
				MaintenanceType.OilChange => "Oil Change",
				MaintenanceType.TireInspection => "Tire Inspection",
				MaintenanceType.BrakeInspection => "Brake Inspection",
				MaintenanceType.GeneralCheckup => "General Checkup",
				MaintenanceType.AirFilterReplacement => "Air Filter Replacement",
				_ => type.ToString()
			};

		private static string FormatRelative(DateTime at)
		{
			var now = DateTime.Now;
			if (at > now.AddMinutes(1))
			{
				return at.ToString("MMM d, yyyy");
			}

			var delta = now - at;
			if (delta.TotalSeconds < 45) return "Just now";
			if (delta.TotalMinutes < 2) return "1 minute ago";
			if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes} minutes ago";
			if (delta.TotalHours < 2) return "1 hour ago";
			if (delta.TotalHours < 24) return $"{(int)delta.TotalHours} hours ago";
			if (delta.TotalDays < 2) return "Yesterday";
			if (delta.TotalDays < 7) return $"{(int)delta.TotalDays} days ago";
			return at.ToString("MMM d, yyyy");
		}
	}
}
