using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Data;
using EasyRent_Checking.Models;
using EasyRent_Checking.Services;
using EasyRent_Checking.ViewModels;

namespace EasyRent_Checking.Controllers
{
	[Authorize(Policy = "StaffArea")]
	[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
	public class IncidentsController : Controller
	{
		private readonly EasyRent_CheckingContext _context;
		private readonly IWebHostEnvironment _webHostEnvironment;
		private readonly SystemLogService _logs;

		public IncidentsController(EasyRent_CheckingContext context, IWebHostEnvironment webHostEnvironment, SystemLogService logs)
		{
			_context = context;
			_webHostEnvironment = webHostEnvironment;
			_logs = logs;
		}

		public async Task<IActionResult> Index(string searchString, string sortBy, string currentFilter, int? page)
		{
			const int pageSize = 10;
			var pageNumber = page.GetValueOrDefault(1);
			if (pageNumber < 1)
			{
				pageNumber = 1;
			}

			ViewData["CurrentSearch"] = searchString;
			ViewData["CurrentSort"] = sortBy;
			ViewData["CurrentFilter"] = currentFilter;

			var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
			var metricsQuery = _context.IncidentReports.AsNoTracking();

			var underReviewCount = await metricsQuery.CountAsync(i => i.Status == IncidentStatus.UnderReview);
			var inRepairCount = await metricsQuery.CountAsync(i => i.Status == IncidentStatus.InRepair);
			var closedCount = await metricsQuery.CountAsync(i => i.Status == IncidentStatus.Closed);

			var underReviewAtMonthStart = await metricsQuery.CountAsync(i =>
				i.Status == IncidentStatus.UnderReview
				&& i.CreatedAt < monthStart);
			var inRepairAtMonthStart = await metricsQuery.CountAsync(i =>
				i.Status == IncidentStatus.InRepair
				&& (i.RepairStartedAt ?? i.CreatedAt) < monthStart);
			var closedAtMonthStart = await metricsQuery.CountAsync(i =>
				i.Status == IncidentStatus.Closed
				&& (i.ClosedAt ?? i.CreatedAt) < monthStart);

			ViewData["UnderReviewCount"] = underReviewCount;
			ViewData["InRepairCount"] = inRepairCount;
			ViewData["ClosedCount"] = closedCount;
			ViewData["UnderReviewChange"] = PctChange(underReviewCount, underReviewAtMonthStart);
			ViewData["InRepairChange"] = PctChange(inRepairCount, inRepairAtMonthStart);
			ViewData["ClosedChange"] = PctChange(closedCount, closedAtMonthStart);

			var query = _context.IncidentReports
				.AsNoTracking()
				.Include(i => i.Vehicle)
				.Include(i => i.Driver)
				.Include(i => i.Transit)
					.ThenInclude(t => t!.Rental)
				.AsQueryable();

			if (!string.IsNullOrEmpty(currentFilter)
				&& Enum.TryParse(currentFilter, true, out IncidentStatus filterStatus))
			{
				query = query.Where(i => i.Status == filterStatus);
			}

			if (!string.IsNullOrWhiteSpace(searchString))
			{
				var term = searchString.Trim();
				query = query.Where(i =>
					(i.Vehicle != null && (i.Vehicle.PlateNumber.Contains(term) || i.Vehicle.Model.Contains(term) || i.Vehicle.Brand.Contains(term)))
					|| (i.Driver != null && i.Driver.Name.Contains(term))
					|| i.Location.Contains(term)
					|| i.Description.Contains(term)
					|| (i.Transit != null && i.Transit.RentalID.ToString().Contains(term)));
			}

			query = sortBy switch
			{
				"Vehicle" => query.OrderBy(i => i.Vehicle != null ? i.Vehicle.PlateNumber : ""),
				"Severity" => query.OrderByDescending(i => i.Severity).ThenByDescending(i => i.OccurredAt),
				"Status" => query.OrderBy(i => i.Status).ThenByDescending(i => i.OccurredAt),
				_ => query.OrderByDescending(i => i.OccurredAt)
			};

			var totalCount = await query.CountAsync();
			var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
			if (pageNumber > totalPages)
			{
				pageNumber = totalPages;
			}

			ViewData["PageIndex"] = pageNumber;
			ViewData["TotalPages"] = totalPages;
			ViewData["TotalCount"] = totalCount;
			ViewData["PageSize"] = pageSize;

			var items = await query
				.Skip((pageNumber - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();

			return View(items);
		}

		public async Task<IActionResult> Details(int? id)
		{
			if (id == null)
			{
				return NotFound();
			}

			var report = await LoadReportAsync(id.Value);
			if (report == null)
			{
				return NotFound();
			}

			return View(report);
		}

		public async Task<IActionResult> Create(int? transitid)
		{
			var report = new IncidentReport
			{
				OccurredAt = DateTime.Now,
				Status = IncidentStatus.Reported
			};

			Transit? transit = null;
			if (transitid != null)
			{
				transit = await LoadTransitAsync(transitid.Value);
				if (transit == null)
				{
					return NotFound();
				}

				if (!CanReportFromTransit(transit))
				{
					TempData["ErrorMessage"] = "Report Accident is available when the trip is in transit or completed.";
					return RedirectToAction("Details", "Transits", new { transitid });
				}

				report.TransitID = transit.TransitID;
				report.VehicleId = transit.VehicleID;
				report.DriverID = transit.DriverID;
				report.Location = transit.Rental?.DropoffLocation ?? string.Empty;
			}

			return View(await BuildCreateViewModelAsync(report, transit));
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(int? transitid, IncidentReport report)
		{
			StripIgnoredCreateFields(report);
			ModelState.Remove(nameof(IncidentReport.Status));
			ModelState.Remove(nameof(IncidentReport.CreatedAt));
			ModelState.Remove(nameof(IncidentReport.WorkDone));
			ModelState.Remove(nameof(IncidentReport.Cost));
			ModelState.Remove(nameof(IncidentReport.RepairStartedAt));
			ModelState.Remove(nameof(IncidentReport.ClosedAt));
			ModelState.Remove(nameof(IncidentReport.ImagePath));
			ModelState.Remove(nameof(IncidentReport.Vehicle));
			ModelState.Remove(nameof(IncidentReport.Transit));
			ModelState.Remove(nameof(IncidentReport.Driver));

			Transit? transit = null;
			if (transitid != null)
			{
				transit = await LoadTransitAsync(transitid.Value);
				if (transit == null)
				{
					return NotFound();
				}

				if (!CanReportFromTransit(transit))
				{
					TempData["ErrorMessage"] = "Report Accident is available when the trip is in transit or completed.";
					return RedirectToAction("Details", "Transits", new { transitid });
				}

				report.TransitID = transit.TransitID;
				report.VehicleId = transit.VehicleID;
				report.DriverID = transit.DriverID;
				ModelState.Remove(nameof(IncidentReport.VehicleId));
			}

			if (report.VehicleId <= 0)
			{
				ModelState.AddModelError("Report.VehicleId", "Vehicle is required.");
			}
			else if (!await _context.Vehicles.AnyAsync(v => v.VehicleId == report.VehicleId))
			{
				ModelState.AddModelError("Report.VehicleId", "Selected vehicle was not found.");
			}

			if (!ModelState.IsValid)
			{
				return View(await BuildCreateViewModelAsync(report, transit));
			}

			if (report.ImageFile != null && report.ImageFile.Length > 0)
			{
				report.ImagePath = await ImageStorage.SaveAsync(
					_webHostEnvironment,
					report.ImageFile,
					ImageStorage.IncidentsFolder);
			}

			report.Status = IncidentStatus.Reported;
			report.CreatedAt = DateTime.Now;
			if (report.Type == IncidentType.Breakdown)
			{
				report.IsUndrivable = true;
			}
			report.RepairStartedAt = null;
			report.ClosedAt = null;
			report.WorkDone = null;
			report.Cost = null;
			report.Remarks = null;

			_context.IncidentReports.Add(report);
			await _context.SaveChangesAsync();
			await VehicleStatusSync.ApplyAsync(_context, report.VehicleId);
			await TransitIssueSync.SyncForIncidentAsync(_context, report.IncidentReportId);

			var vehicle = await _context.Vehicles.AsNoTracking()
				.FirstOrDefaultAsync(v => v.VehicleId == report.VehicleId);
			var plate = vehicle?.PlateNumber ?? "the vehicle";
			_logs.Record(
				SystemLogAction.Created,
				SystemLogCategory.Incident,
				$"Filed incident {IncidentReport.FormatReference(report.IncidentReportId)} for {plate}.",
				"Incident",
				report.IncidentReportId);
			await _context.SaveChangesAsync();

			return View("CreateSuccess", new CreateSuccessViewModel
			{
				PageTitle = "Record Incident",
				ActivePage = "Incidents",
				Heading = "Incident Recorded Successfully",
				MessageHtml = "The vehicle incident report has been successfully logged into the system for review and administrative tracking.",
				PrimaryActionText = "View Incident Details",
				PrimaryActionUrl = Url.Action(nameof(Details), new { id = report.IncidentReportId }) ?? "",
				SecondaryActionText = transit != null ? "Back to Trip" : "Report Another Incident",
				SecondaryActionUrl = transit != null
					? Url.Action("Details", "Transits", new { transitid = transit.TransitID }) ?? ""
					: Url.Action(nameof(Create)) ?? "",
				ShowSecondaryPlusIcon = transit == null
			});
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Review(int id)
		{
			var report = await LoadReportAsync(id);
			if (report == null)
			{
				return NotFound();
			}

			if (report.Status != IncidentStatus.Reported)
			{
				TempData["ErrorMessage"] = "Only a reported incident can be moved to under review.";
				return RedirectToAction(nameof(Details), new { id });
			}

			report.Status = IncidentStatus.UnderReview;
			_logs.Record(
				SystemLogAction.Updated,
				SystemLogCategory.Incident,
				$"Moved {IncidentReport.FormatReference(report.IncidentReportId)} under review.",
				"Incident",
				report.IncidentReportId);
			await _context.SaveChangesAsync();
			await TransitIssueSync.SyncForIncidentAsync(_context, report.IncidentReportId);
			TempData["SuccessMessage"] = "Incident is now under review.";
			return RedirectToAction(nameof(Details), new { id });
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> StartRepair(int id)
		{
			var report = await LoadReportAsync(id);
			if (report == null)
			{
				return NotFound();
			}

			if (report.Status != IncidentStatus.UnderReview)
			{
				TempData["ErrorMessage"] = "Confirm the incident under review before starting repair.";
				return RedirectToAction(nameof(Details), new { id });
			}

			report.Status = IncidentStatus.InRepair;
			report.RepairStartedAt = DateTime.Now;
			_logs.Record(
				SystemLogAction.Started,
				SystemLogCategory.Incident,
				$"Started repair for {IncidentReport.FormatReference(report.IncidentReportId)}.",
				"Incident",
				report.IncidentReportId);
			await _context.SaveChangesAsync();
			await VehicleStatusSync.ApplyAsync(_context, report.VehicleId);
			await TransitIssueSync.SyncForIncidentAsync(_context, report.IncidentReportId);

			TempData["SuccessMessage"] = report.IsUndrivable
				? "Repair started. The vehicle stays Unavailable until this report is closed."
				: "Repair started.";
			return RedirectToAction(nameof(Details), new { id });
		}

		public async Task<IActionResult> CompleteRepair(int? id)
		{
			if (id == null)
			{
				return NotFound();
			}

			var report = await LoadReportAsync(id.Value);
			if (report == null)
			{
				return NotFound();
			}

			if (report.Status != IncidentStatus.InRepair)
			{
				TempData["ErrorMessage"] = "Start repair before recording work done.";
				return RedirectToAction(nameof(Details), new { id });
			}

			return View(report);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CompleteRepair(
			int id,
			string? workDone,
			decimal? cost,
			string? remarks,
			IFormFile? imageFile)
		{
			var report = await LoadReportAsync(id);
			if (report == null)
			{
				return NotFound();
			}

			if (report.Status != IncidentStatus.InRepair)
			{
				TempData["ErrorMessage"] = "Start repair before recording work done.";
				return RedirectToAction(nameof(Details), new { id });
			}

			if (string.IsNullOrWhiteSpace(workDone))
			{
				ModelState.AddModelError(nameof(IncidentReport.WorkDone), "Work done is required to close a repair.");
			}

			if (cost < 0)
			{
				ModelState.AddModelError(nameof(IncidentReport.Cost), "Cost cannot be negative.");
			}

			if (!string.IsNullOrWhiteSpace(remarks) && remarks.Length > 1000)
			{
				ModelState.AddModelError(nameof(IncidentReport.Remarks), "Remarks cannot exceed 1000 characters.");
			}

			if (!ModelState.IsValid)
			{
				report.WorkDone = workDone;
				report.Cost = cost;
				report.Remarks = remarks;
				return View(report);
			}

			if (imageFile != null && imageFile.Length > 0)
			{
				report.ImagePath = await ImageStorage.SaveAsync(
					_webHostEnvironment,
					imageFile,
					ImageStorage.IncidentsFolder);
			}

			report.WorkDone = workDone?.Trim();
			report.Cost = cost;
			report.Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();

			report.Status = IncidentStatus.Closed;
			report.ClosedAt = DateTime.Now;
			_logs.Record(
				SystemLogAction.Completed,
				SystemLogCategory.Incident,
				$"Completed repair and closed {IncidentReport.FormatReference(report.IncidentReportId)}.",
				"Incident",
				report.IncidentReportId);
			await _context.SaveChangesAsync();
			await VehicleStatusSync.ApplyAsync(_context, report.VehicleId);
			await TransitIssueSync.SyncForIncidentAsync(_context, report.IncidentReportId);

			TempData["SuccessMessage"] = "Repair recorded and the incident was closed.";
			return RedirectToAction(nameof(Details), new { id });
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Close(int id)
		{
			var report = await LoadReportAsync(id);
			if (report == null)
			{
				return NotFound();
			}

			if (report.Status != IncidentStatus.UnderReview)
			{
				TempData["ErrorMessage"] = "Close without repair only after the incident is under review.";
				return RedirectToAction(nameof(Details), new { id });
			}

			report.Status = IncidentStatus.Closed;
			report.ClosedAt = DateTime.Now;
			_logs.Record(
				SystemLogAction.Closed,
				SystemLogCategory.Incident,
				$"Closed {IncidentReport.FormatReference(report.IncidentReportId)} without shop work.",
				"Incident",
				report.IncidentReportId);
			await _context.SaveChangesAsync();
			await VehicleStatusSync.ApplyAsync(_context, report.VehicleId);
			await TransitIssueSync.SyncForIncidentAsync(_context, report.IncidentReportId);

			TempData["SuccessMessage"] = "Incident closed without shop work.";
			return RedirectToAction(nameof(Details), new { id });
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Dismiss(int id)
		{
			var report = await LoadReportAsync(id);
			if (report == null)
			{
				return NotFound();
			}

			if (report.Status is not (IncidentStatus.Reported or IncidentStatus.UnderReview))
			{
				TempData["ErrorMessage"] = "Only a reported or under-review incident can be dismissed.";
				return RedirectToAction(nameof(Details), new { id });
			}

			report.Status = IncidentStatus.Dismissed;
			report.ClosedAt = DateTime.Now;
			_logs.Record(
				SystemLogAction.Dismissed,
				SystemLogCategory.Incident,
				$"Dismissed {IncidentReport.FormatReference(report.IncidentReportId)}.",
				"Incident",
				report.IncidentReportId);
			await _context.SaveChangesAsync();
			await VehicleStatusSync.ApplyAsync(_context, report.VehicleId);
			await TransitIssueSync.SyncForIncidentAsync(_context, report.IncidentReportId);

			TempData["SuccessMessage"] = "Incident dismissed.";
			return RedirectToAction(nameof(Details), new { id });
		}

		private async Task<IncidentReport?> LoadReportAsync(int id)
		{
			return await _context.IncidentReports
				.Include(i => i.Vehicle)
				.Include(i => i.Driver)
				.Include(i => i.Transit)!
					.ThenInclude(t => t!.Rental)!
						.ThenInclude(r => r!.RentalVehicles)
				.FirstOrDefaultAsync(i => i.IncidentReportId == id);
		}

		private async Task<Transit?> LoadTransitAsync(int transitId)
		{
			return await _context.Transits
				.Include(t => t.Vehicle)
				.Include(t => t.Driver)
				.Include(t => t.Rental)!
					.ThenInclude(r => r!.RentalVehicles)
				.FirstOrDefaultAsync(t => t.TransitID == transitId);
		}

		private static bool CanReportFromTransit(Transit transit)
			=> transit.TripStatus is TripStatus.InTransit or TripStatus.Completed;

		private async Task<IncidentCreateViewModel> BuildCreateViewModelAsync(IncidentReport report, Transit? transit)
		{
			var vehicles = await _context.Vehicles.AsNoTracking()
				.OrderBy(v => v.PlateNumber)
				.ThenBy(v => v.Brand)
				.ToListAsync();

			var activeTransits = await _context.Transits.AsNoTracking()
				.Include(t => t.Driver)
				.Where(t => t.TripStatus == TripStatus.InTransit || t.TripStatus == TripStatus.Scheduled)
				.ToListAsync();

			var transitByVehicle = activeTransits
				.GroupBy(t => t.VehicleID)
				.ToDictionary(
					g => g.Key,
					g => g.OrderByDescending(t => t.TripStatus == TripStatus.InTransit).First());

			var vehicleItems = vehicles
				.Select(v =>
				{
					transitByVehicle.TryGetValue(v.VehicleId, out var activeTransit);
					var (fleetStatusLabel, fleetStatusClass) = ResolveFleetVehicleStatus(v);
					var driver = activeTransit?.Driver;
					var isOnActiveTransit = activeTransit != null;

					return new IncidentVehicleSelectionItem
					{
						VehicleId = v.VehicleId,
						Brand = v.Brand,
						Model = v.Model,
						Type = VehicleTypes.Display(v.Type),
						PassengersCount = v.PassengersCount,
						PlateNumber = v.PlateNumber,
						ImagePath = v.ImagePath,
						IsOnActiveTransit = isOnActiveTransit,
						FleetStatusLabel = fleetStatusLabel,
						FleetStatusClass = fleetStatusClass,
						StatusLabel = isOnActiveTransit ? "In Transit" : fleetStatusLabel,
						StatusClass = isOnActiveTransit ? "is-in-transit" : fleetStatusClass,
						DriverName = driver?.Name,
						DriverImagePath = driver?.ImagePath,
						DriverInitials = BuildInitials(driver?.Name),
						SearchKey = string.Join(' ',
							v.PlateNumber,
							v.Brand,
							v.Model,
							fleetStatusLabel,
							driver?.Name,
							isOnActiveTransit ? "In Transit" : string.Empty).ToLowerInvariant()
					};
				})
				.ToList();

			if (transit != null)
			{
				var transitVehicle = vehicles.FirstOrDefault(v => v.VehicleId == transit.VehicleID);
				vehicleItems = vehicleItems
					.Where(v => v.VehicleId == transit.VehicleID)
					.Select(v =>
					{
						if (transit.Driver != null)
						{
							v.DriverName = transit.Driver.Name;
							v.DriverImagePath = transit.Driver.ImagePath;
							v.DriverInitials = BuildInitials(transit.Driver.Name);
						}

						if (transitVehicle != null)
						{
							var (fleetStatusLabel, fleetStatusClass) = ResolveFleetVehicleStatus(transitVehicle);
							v.FleetStatusLabel = fleetStatusLabel;
							v.FleetStatusClass = fleetStatusClass;
							v.IsOnActiveTransit = transit.TripStatus is TripStatus.InTransit or TripStatus.Scheduled;
							v.StatusLabel = v.IsOnActiveTransit ? "In Transit" : fleetStatusLabel;
							v.StatusClass = v.IsOnActiveTransit ? "is-in-transit" : fleetStatusClass;
						}

						return v;
					})
					.ToList();
			}

			return new IncidentCreateViewModel
			{
				Report = report,
				Transit = transit,
				Vehicles = vehicleItems
			};
		}

		private static (string Label, string CssClass) ResolveFleetVehicleStatus(Vehicle vehicle)
		{
			if (!vehicle.IsActive
				|| vehicle.Status is VehicleStatus.Unavailable or VehicleStatus.InMaintenance)
			{
				return ("Unavailable", "is-unavailable");
			}

			if (vehicle.Status == VehicleStatus.Available)
			{
				return ("Available", "is-available");
			}

			return ("Unavailable", "is-unavailable");
		}

		private static string BuildInitials(string? name)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				return "—";
			}

			var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length == 1)
			{
				return parts[0].Length >= 2
					? parts[0][..2].ToUpperInvariant()
					: parts[0].ToUpperInvariant();
			}

			return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
		}

		private static void StripIgnoredCreateFields(IncidentReport report)
		{
			report.IncidentReportId = 0;
			report.Status = IncidentStatus.Reported;
			report.RepairStartedAt = null;
			report.ClosedAt = null;
			report.WorkDone = null;
			report.Cost = null;
			report.Remarks = null;
			report.CreatedAt = DateTime.Now;
		}

		private static decimal PctChange(decimal current, decimal previous)
			=> Math.Round((current - previous) * 0.1m, 1);
	}
}
