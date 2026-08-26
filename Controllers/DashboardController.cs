using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Data;
using EasyRent_Checking.Models;
using EasyRent_Checking.Services;
using EasyRent_Checking.ViewModels;

namespace EasyRent_Checking.Controllers
{
	/// <summary>Admin home dashboard (KPIs, trends, fleet snapshot).</summary>
	[Authorize(Policy = "StaffArea")]
	[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
	public class DashboardController : Controller
	{
		private static readonly string[] CostColors = ["#1E3A8A", "#0F766E", "#F5B301", "#9CA3AF"];

		private readonly EasyRent_CheckingContext _context;

		public DashboardController(EasyRent_CheckingContext context)
		{
			_context = context;
		}

		public async Task<IActionResult> Index(string? period)
		{
			var now = DateTime.Now;
			var monthStart = new DateTime(now.Year, now.Month, 1);
			var nextMonthStart = monthStart.AddMonths(1);
			var prevMonthStart = monthStart.AddMonths(-1);
			var useMonth = string.Equals(period, "month", StringComparison.OrdinalIgnoreCase);
			var weekStart = StartOfWeek(now.Date);
			var weekEnd = weekStart.AddDays(6);
			var trendStart = useMonth
				? DateOnly.FromDateTime(monthStart)
				: DateOnly.FromDateTime(weekStart);
			var trendEnd = useMonth
				? DateOnly.FromDateTime(nextMonthStart.AddDays(-1))
				: DateOnly.FromDateTime(weekEnd);

			var model = new DashboardViewModel
			{
				Period = useMonth ? "month" : "week",
				PeriodLabel = useMonth ? "This Month" : "This Week",
				RangeLabel = FormatRange(trendStart, trendEnd)
			};

			model.MonthlyRevenue = await _context.Payments
				.AsNoTracking()
				.Where(p => p.PaymentDate >= monthStart)
				.SumAsync(p => (decimal?)p.AmountPaid) ?? 0m;

			var prevMonthRevenue = await _context.Payments
				.AsNoTracking()
				.Where(p => p.PaymentDate >= prevMonthStart && p.PaymentDate < monthStart)
				.SumAsync(p => (decimal?)p.AmountPaid) ?? 0m;

			model.RevenueChange = PctChange(model.MonthlyRevenue, prevMonthRevenue);

			model.ActiveTrips = await _context.Transits
				.AsNoTracking()
				.CountAsync(t => t.TripStatus == TripStatus.Scheduled || t.TripStatus == TripStatus.InTransit);

			var thisMonthActivePickups = await CountTripsForPickupRangeAsync(
				DateOnly.FromDateTime(monthStart),
				DateOnly.FromDateTime(nextMonthStart.AddDays(-1)));
			var lastMonthActivePickups = await CountTripsForPickupRangeAsync(
				DateOnly.FromDateTime(prevMonthStart),
				DateOnly.FromDateTime(monthStart.AddDays(-1)));
			model.ActiveTripsChange = PctChange(thisMonthActivePickups, lastMonthActivePickups, perAddedItem: true);

			model.BookingRequests = await _context.Rentals
				.AsNoTracking()
				.CountAsync(r => r.RentalStatus == RentalStatus.Pending);

			var lastMonthPending = await CountRentalsAsync(
				DateOnly.FromDateTime(prevMonthStart),
				DateOnly.FromDateTime(monthStart.AddDays(-1)),
				RentalStatus.Pending);
			model.BookingRequestsChange = PctChange(model.BookingRequests, lastMonthPending, perAddedItem: true);

			model.TotalBookings = await CountRentalsAsync(
				DateOnly.FromDateTime(monthStart),
				DateOnly.FromDateTime(nextMonthStart.AddDays(-1)));
			var lastMonthBookings = await CountRentalsAsync(
				DateOnly.FromDateTime(prevMonthStart),
				DateOnly.FromDateTime(monthStart.AddDays(-1)));
			model.TotalBookingsChange = PctChange(model.TotalBookings, lastMonthBookings, perAddedItem: true);

			var bookingsByDay = await _context.RentalDetails
				.AsNoTracking()
				.Where(d => d.PickupDate >= trendStart && d.PickupDate <= trendEnd)
				.GroupBy(d => d.PickupDate)
				.Select(g => new { Date = g.Key, Count = g.Count() })
				.ToListAsync();

			if (useMonth)
			{
				var cursor = trendStart;
				var weekIndex = 1;
				while (cursor <= trendEnd)
				{
					var bucketEnd = cursor.AddDays(6);
					if (bucketEnd > trendEnd)
					{
						bucketEnd = trendEnd;
					}

					model.TrendLabels.Add("Week " + weekIndex);
					model.TrendValues.Add(bookingsByDay
						.Where(b => b.Date >= cursor && b.Date <= bucketEnd)
						.Sum(b => b.Count));
					cursor = bucketEnd.AddDays(1);
					weekIndex++;
				}
			}
			else
			{
				for (var day = trendStart; day <= trendEnd; day = day.AddDays(1))
				{
					model.TrendLabels.Add(day.ToDateTime(TimeOnly.MinValue).ToString("ddd"));
					model.TrendValues.Add(bookingsByDay.FirstOrDefault(b => b.Date == day)?.Count ?? 0);
				}
			}

			var vehicles = await _context.Vehicles.AsNoTracking().ToListAsync();
			model.FleetRented = vehicles.Count(v => v.Status == VehicleStatus.Rented);
			model.FleetAvailable = vehicles.Count(v => v.Status == VehicleStatus.Available || v.Status == VehicleStatus.Unavailable);
			model.FleetMaintenance = vehicles.Count(v => v.Status == VehicleStatus.InMaintenance);
			model.TotalFleet = vehicles.Count;

			var typeCounts = vehicles
				.GroupBy(v => string.IsNullOrWhiteSpace(v.Type) ? "Other" : v.Type.Trim(), StringComparer.OrdinalIgnoreCase)
				.ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
			var typeLabels = new List<string>();
			var typeValues = new List<int>();
			foreach (var preset in VehicleTypes.Presets)
			{
				typeLabels.Add(preset);
				typeValues.Add(typeCounts.TryGetValue(preset, out var count) ? count : 0);
				typeCounts.Remove(preset);
			}
			foreach (var extra in typeCounts.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
			{
				typeLabels.Add(extra.Key);
				typeValues.Add(extra.Value);
			}
			model.FleetTypeLabels = typeLabels.ToArray();
			model.FleetTypeValues = typeValues.ToArray();

			await FillMaintenanceAsync(model);

			var costStart = monthStart;
			var costLogs = await _context.MaintenanceLogs
				.AsNoTracking()
				.Where(l => l.Status == MaintenanceStatus.Completed && l.Cost != null && l.Cost > 0)
				.Where(l => (l.CompletedAt ?? l.CreatedAt) >= costStart)
				.Select(l => new { l.Type, Amount = l.Cost ?? 0m })
				.ToListAsync();

			model.MaintenanceCostTotal = costLogs.Sum(l => l.Amount);
			var groupedCosts = costLogs
				.GroupBy(l => l.Type)
				.Select(g => new { Type = g.Key, Amount = g.Sum(x => x.Amount) })
				.OrderByDescending(g => g.Amount)
				.ToList();

			var slices = new List<DashboardCostSlice>();
			foreach (var row in groupedCosts.Take(3))
			{
				slices.Add(new DashboardCostSlice
				{
					Label = CheckupShort(row.Type),
					Amount = row.Amount,
					Percent = PercentOf(row.Amount, model.MaintenanceCostTotal)
				});
			}

			var otherAmount = groupedCosts.Skip(3).Sum(g => g.Amount);
			if (otherAmount > 0 || slices.Count == 0)
			{
				if (otherAmount > 0 || groupedCosts.Count == 0)
				{
					slices.Add(new DashboardCostSlice
					{
						Label = groupedCosts.Count == 0 ? "No costs yet" : "Others",
						Amount = otherAmount,
						Percent = PercentOf(otherAmount, model.MaintenanceCostTotal)
					});
				}
			}

			for (var i = 0; i < slices.Count; i++)
			{
				slices[i].Color = CostColors[Math.Min(i, CostColors.Length - 1)];
			}

			model.MaintenanceCosts = slices;

			var incidentStart = trendStart.ToDateTime(TimeOnly.MinValue);
			var incidentEndExclusive = trendEnd.ToDateTime(TimeOnly.MinValue).AddDays(1);
			var incidentStatuses = await _context.IncidentReports
				.AsNoTracking()
				.Where(i => i.CreatedAt >= incidentStart && i.CreatedAt < incidentEndExclusive)
				.Select(i => i.Status)
				.ToListAsync();

			model.IncidentNew = incidentStatuses.Count(s => s == IncidentStatus.Reported);
			model.IncidentInProgress = incidentStatuses.Count(s => s is IncidentStatus.UnderReview or IncidentStatus.InRepair);
			model.IncidentClosed = incidentStatuses.Count(s => s is IncidentStatus.Closed or IncidentStatus.Dismissed);

			return View(model);
		}

		private async Task FillMaintenanceAsync(DashboardViewModel model)
		{
			var plans = await _context.MaintenancePlans
				.AsNoTracking()
				.Include(p => p.Vehicle)
				.Include(p => p.Logs)
				.ToListAsync();

			var items = plans
				.Where(p => p.Vehicle != null)
				.Select(p =>
				{
					var vehicle = p.Vehicle!;
					var open = p.Logs.Any(l => l.Status == MaintenanceStatus.InProgress);
					return new
					{
						VehicleId = vehicle.VehicleId,
						VehicleName = $"{vehicle.Brand} {vehicle.Model}".Trim(),
						PlateNumber = vehicle.PlateNumber,
						ImagePath = vehicle.ImagePath,
						Type = p.Type,
						DueKind = PmsRules.Classify(p, vehicle, open),
						NextDue = IntervalShort(p)
					};
				})
				.ToList();

			model.MaintOverdue = items.Count(i => i.DueKind == PmsDueKind.Overdue);
			model.MaintDueNow = items.Count(i => i.DueKind == PmsDueKind.DueNow);
			model.MaintDueSoon = items.Count(i => i.DueKind == PmsDueKind.DueSoon);
			model.MaintInProgress = items.Count(i => i.DueKind == PmsDueKind.InProgress);
			model.MaintUpToDate = items.Count(i => i.DueKind == PmsDueKind.Upcoming);

			model.MaintenanceRows = items
				.GroupBy(i => i.VehicleId)
				.Select(g =>
				{
					var first = g.First();
					var ordered = g.OrderBy(i => KindOrder(i.DueKind)).ToList();
					var worst = ordered[0].DueKind;
					var urgent = ordered
						.Where(i => i.DueKind is PmsDueKind.Overdue or PmsDueKind.DueNow or PmsDueKind.DueSoon or PmsDueKind.InProgress)
						.Select(i => CheckupShort(i.Type))
						.Distinct()
						.ToList();
					if (urgent.Count == 0)
					{
						urgent = ordered.Select(i => CheckupShort(i.Type)).Distinct().Take(3).ToList();
					}

					return new DashboardMaintenanceRow
					{
						VehicleId = g.Key,
						VehicleName = first.VehicleName,
						PlateNumber = first.PlateNumber,
						ImagePath = first.ImagePath,
						CheckupSummary = urgent.Count == 0 ? "—" : string.Join(", ", urgent),
						NextDue = ordered[0].NextDue,
						Status = worst
					};
				})
				.Where(r => r.Status is PmsDueKind.Overdue or PmsDueKind.DueNow or PmsDueKind.DueSoon or PmsDueKind.InProgress)
				.OrderBy(r => KindOrder(r.Status))
				.ThenBy(r => r.PlateNumber)
				.Take(5)
				.ToList();
		}

		private async Task<int> CountRentalsAsync(DateOnly start, DateOnly end, RentalStatus? status = null)
		{
			var query =
				from d in _context.RentalDetails.AsNoTracking()
				join r in _context.Rentals.AsNoTracking() on d.RentalID equals r.RentalId
				where d.PickupDate >= start && d.PickupDate <= end
				select r;

			if (status == null)
			{
				return await query.CountAsync(r => r.RentalStatus != RentalStatus.Cancelled);
			}

			return await query.CountAsync(r => r.RentalStatus == status);
		}

		private async Task<int> CountTripsForPickupRangeAsync(DateOnly start, DateOnly end)
		{
			return await (
				from t in _context.Transits.AsNoTracking()
				join d in _context.RentalDetails.AsNoTracking() on t.RentalID equals d.RentalID
				where t.TripStatus != TripStatus.Cancelled
					&& d.PickupDate >= start
					&& d.PickupDate <= end
				select t
			).CountAsync();
		}

		private static decimal PctChange(decimal current, decimal previous, bool perAddedItem = false)
		{
			if (perAddedItem)
			{
				return Math.Round((current - previous) * 0.1m, 1);
			}

			if (previous > 0)
			{
				return Math.Round(((current - previous) / previous) * 100m, 1);
			}

			return current > 0 ? 0.1m : 0m;
		}

		private static decimal PercentOf(decimal amount, decimal total)
		{
			if (total <= 0) return 0m;
			return Math.Round((amount / total) * 100m, 0);
		}

		private static int KindOrder(PmsDueKind kind)
			=> kind switch
			{
				PmsDueKind.InProgress => 0,
				PmsDueKind.Overdue => 1,
				PmsDueKind.DueNow => 2,
				PmsDueKind.DueSoon => 3,
				_ => 4
			};

		private static string CheckupShort(MaintenanceType type)
			=> type switch
			{
				MaintenanceType.OilChange => "Oil Change",
				MaintenanceType.TireInspection => "Tires",
				MaintenanceType.BrakeInspection => "Brakes",
				MaintenanceType.GeneralCheckup => "Checkup",
				MaintenanceType.AirFilterReplacement => "Air Filter",
				_ => type.ToString()
			};

		private static string IntervalShort(MaintenancePlan plan)
		{
			if (plan.IntervalMonths is int months && months > 0)
			{
				return months == 1 ? "1 month" : $"{months} months";
			}

			if (plan.IntervalKilometers is int km && km > 0)
			{
				return $"{km:N0} km";
			}

			return "—";
		}

		private static DateTime StartOfWeek(DateTime date)
		{
			var diff = ((int)date.DayOfWeek + 6) % 7;
			return date.AddDays(-diff);
		}

		private static string FormatRange(DateOnly start, DateOnly end)
		{
			if (start.Year == end.Year && start.Month == end.Month)
			{
				return $"{start:MMM d} – {end:d, yyyy}";
			}

			if (start.Year == end.Year)
			{
				return $"{start:MMM d} – {end:MMM d, yyyy}";
			}

			return $"{start:MMM d, yyyy} – {end:MMM d, yyyy}";
		}
	}
}
