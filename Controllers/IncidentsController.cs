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

			var query = _context.IncidentReports
				.AsNoTracking()
				.Include(i => i.Vehicle)
				.Include(i => i.Driver)
				.Include(i => i.Transit)
					.ThenInclude(t => t!.Rental)
				.AsQueryable();

			ViewData["UnderReviewCount"] = await query.CountAsync(i => i.Status == IncidentStatus.UnderReview);
			ViewData["InRepairCount"] = await query.CountAsync(i => i.Status == IncidentStatus.InRepair);
			ViewData["ClosedCount"] = await query.CountAsync(i => i.Status == IncidentStatus.Closed);

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

			if (transitid != null)
			{
				var transit = await LoadTransitAsync(transitid.Value);
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
				report.Location = transit.Rental?.Details?.DropoffLocation ?? string.Empty;
				ViewBag.Transit = transit;
			}

			await PopulateVehicleListAsync(report.VehicleId == 0 ? null : report.VehicleId);
			return View(report);
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
				ViewBag.Transit = transit;
				ModelState.Remove(nameof(IncidentReport.VehicleId));
			}

			if (report.VehicleId <= 0)
			{
				ModelState.AddModelError(nameof(IncidentReport.VehicleId), "Vehicle is required.");
			}
			else if (!await _context.Vehicles.AnyAsync(v => v.VehicleId == report.VehicleId))
			{
				ModelState.AddModelError(nameof(IncidentReport.VehicleId), "Selected vehicle was not found.");
			}

			if (!ModelState.IsValid)
			{
				await PopulateVehicleListAsync(report.VehicleId == 0 ? null : report.VehicleId);
				return View(report);
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
			report.RepairStartedAt = null;
			report.ClosedAt = null;
			report.WorkDone = null;
			report.Cost = null;
			report.Remarks = null;

			_context.IncidentReports.Add(report);
			await _context.SaveChangesAsync();
			await VehicleStatusSync.ApplyAsync(_context, report.VehicleId);

			var vehicle = await _context.Vehicles.AsNoTracking()
				.FirstOrDefaultAsync(v => v.VehicleId == report.VehicleId);
			var plate = vehicle?.PlateNumber ?? "the vehicle";
			_logs.Record(
				SystemLogAction.Created,
				SystemLogCategory.Incident,
				$"Filed incident INC-{report.IncidentReportId:D5} for {plate}.",
				"Incident",
				report.IncidentReportId);
			await _context.SaveChangesAsync();

			return View("CreateSuccess", new CreateSuccessViewModel
			{
				PageTitle = "Incident Reported",
				ActivePage = "Incidents",
				Heading = "Incident Reported",
				MessageHtml = $"The report for <strong>{plate}</strong> was saved. Review it next, then start repair or close it.",
				PrimaryActionText = "View Report",
				PrimaryActionUrl = Url.Action(nameof(Details), new { id = report.IncidentReportId }) ?? "",
				SecondaryActionText = transit != null ? "Back to Trip" : "All Incidents",
				SecondaryActionUrl = transit != null
					? Url.Action("Details", "Transits", new { transitid = transit.TransitID }) ?? ""
					: Url.Action(nameof(Index)) ?? "",
				ShowSecondaryPlusIcon = false
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
				$"Moved INC-{report.IncidentReportId:D5} under review.",
				"Incident",
				report.IncidentReportId);
			await _context.SaveChangesAsync();
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
				$"Started repair for INC-{report.IncidentReportId:D5}.",
				"Incident",
				report.IncidentReportId);
			await _context.SaveChangesAsync();
			await VehicleStatusSync.ApplyAsync(_context, report.VehicleId);

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

			if (!ModelState.IsValid)
			{
				report.WorkDone = workDone;
				report.Cost = cost;
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

			report.Status = IncidentStatus.Closed;
			report.ClosedAt = DateTime.Now;
			_logs.Record(
				SystemLogAction.Completed,
				SystemLogCategory.Incident,
				$"Completed repair and closed INC-{report.IncidentReportId:D5}.",
				"Incident",
				report.IncidentReportId);
			await _context.SaveChangesAsync();
			await VehicleStatusSync.ApplyAsync(_context, report.VehicleId);

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
				$"Closed INC-{report.IncidentReportId:D5} without shop work.",
				"Incident",
				report.IncidentReportId);
			await _context.SaveChangesAsync();
			await VehicleStatusSync.ApplyAsync(_context, report.VehicleId);

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
				$"Dismissed INC-{report.IncidentReportId:D5}.",
				"Incident",
				report.IncidentReportId);
			await _context.SaveChangesAsync();
			await VehicleStatusSync.ApplyAsync(_context, report.VehicleId);

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
						.ThenInclude(r => r!.Details)
				.FirstOrDefaultAsync(i => i.IncidentReportId == id);
		}

		private async Task<Transit?> LoadTransitAsync(int transitId)
		{
			return await _context.Transits
				.Include(t => t.Vehicle)
				.Include(t => t.Driver)
				.Include(t => t.Rental)!
					.ThenInclude(r => r!.Details)
				.FirstOrDefaultAsync(t => t.TransitID == transitId);
		}

		private static bool CanReportFromTransit(Transit transit)
			=> transit.TripStatus is TripStatus.InTransit or TripStatus.Completed;

		private async Task PopulateVehicleListAsync(int? selectedId)
		{
			ViewBag.VehicleId = new SelectList(
				await _context.Vehicles.AsNoTracking()
					.OrderBy(v => v.PlateNumber)
					.Select(v => new
					{
						v.VehicleId,
						Label = v.PlateNumber + " — " + v.Brand + " " + v.Model
					})
					.ToListAsync(),
				"VehicleId",
				"Label",
				selectedId);
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
	}
}
