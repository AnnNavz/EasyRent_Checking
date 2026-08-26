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
	public class MaintenanceController : Controller
	{
		private readonly EasyRent_CheckingContext _context;
		private readonly IWebHostEnvironment _webHostEnvironment;
		private readonly SystemLogService _logs;

		public MaintenanceController(EasyRent_CheckingContext context, IWebHostEnvironment webHostEnvironment, SystemLogService logs)
		{
			_context = context;
			_webHostEnvironment = webHostEnvironment;
			_logs = logs;
		}

		public async Task<IActionResult> Index(string searchString, string sortBy, string currentFilter, string tab, int? page)
		{
			const int pageSize = 10;
			var pageNumber = page.GetValueOrDefault(1);
			if (pageNumber < 1)
			{
				pageNumber = 1;
			}

			tab = tab?.Trim().ToLowerInvariant() switch
			{
				"schedule" => "schedule",
				"history" => "history",
				_ => "overview"
			};

			ViewData["CurrentSearch"] = searchString;
			ViewData["CurrentSort"] = sortBy;
			ViewData["CurrentFilter"] = currentFilter;
			ViewData["CurrentTab"] = tab;

			await PmsPlanSeeder.AttachOrphanLogsAsync(_context);

			var unsetCount = await CountUnsetVehiclesAsync();
			ViewData["UnsetVehicleCount"] = unsetCount;

			var plans = await _context.MaintenancePlans
				.AsNoTracking()
				.Include(p => p.Vehicle)
				.Include(p => p.Logs)
				.ToListAsync();

			var items = plans
				.Where(p => p.Vehicle != null)
				.Select(p => ToDueItem(p, p.Vehicle!))
				.ToList();

			var overdueNow = items.Count(i => i.DueKind == PmsDueKind.Overdue);
			var dueNowNow = items.Count(i => i.DueKind == PmsDueKind.DueNow);
			var dueSoonNow = items.Count(i => i.DueKind == PmsDueKind.DueSoon);
			var inProgressNow = items.Count(i => i.DueKind == PmsDueKind.InProgress);
			ViewData["OverdueCount"] = overdueNow;
			ViewData["DueNowCount"] = dueNowNow;
			ViewData["DueSoonCount"] = dueSoonNow;
			ViewData["InProgressCount"] = inProgressNow;

			var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
			var monthStartDate = DateOnly.FromDateTime(monthStart);
			var overdueThen = items.Count(i =>
				i.DueKind != PmsDueKind.InProgress
				&& i.NextDueDate != null
				&& i.NextDueDate.Value < monthStartDate);
			var dueNowThen = items.Count(i =>
				i.DueKind != PmsDueKind.InProgress
				&& i.NextDueDate != null
				&& i.NextDueDate.Value == monthStartDate);
			var dueSoonThen = items.Count(i =>
				i.DueKind != PmsDueKind.InProgress
				&& i.NextDueDate != null
				&& i.NextDueDate.Value > monthStartDate
				&& i.NextDueDate.Value <= monthStartDate.AddDays(PmsRules.DueSoonDays));
			var inProgressThen = plans.SelectMany(p => p.Logs).Count(l =>
				(l.StartedAt ?? l.CreatedAt) < monthStart
				&& (l.Status == MaintenanceStatus.InProgress
					|| (l.CompletedAt != null && l.CompletedAt >= monthStart)));

			ViewData["OverdueChange"] = PctChange(overdueNow, overdueThen);
			ViewData["DueNowChange"] = PctChange(dueNowNow, dueNowThen);
			ViewData["DueSoonChange"] = PctChange(dueSoonNow, dueSoonThen);
			ViewData["InProgressChange"] = PctChange(inProgressNow, inProgressThen);

			var model = new MaintenanceIndexViewModel { Tab = tab };

			if (tab == "history")
			{
				var history = plans
					.SelectMany(p => p.Logs.Select(l => new { Plan = p, Log = l }))
					.Where(x => x.Log.Status == MaintenanceStatus.Completed && x.Plan.Vehicle != null)
					.Select(x => ToHistoryRow(x.Log, x.Plan.Vehicle!))
					.ToList();

				if (!string.IsNullOrWhiteSpace(searchString))
				{
					var term = searchString.Trim();
					history = history
						.Where(h =>
							h.PlateNumber.Contains(term, StringComparison.OrdinalIgnoreCase)
							|| h.VehicleName.Contains(term, StringComparison.OrdinalIgnoreCase)
							|| EnumDisplay(h.Type).Contains(term, StringComparison.OrdinalIgnoreCase)
							|| (h.WorkDone?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))
						.ToList();
				}

				history = sortBy switch
				{
					"Vehicle" => history.OrderBy(h => h.PlateNumber).ThenBy(h => h.VehicleName).ToList(),
					"Odometer" => history.OrderByDescending(h => h.Odometer ?? 0).ThenBy(h => h.PlateNumber).ToList(),
					_ => history.OrderByDescending(h => h.CompletedAt ?? DateTime.MinValue).ToList()
				};

				ApplyPaging(history.Count, pageSize, ref pageNumber);
				model.History = history
					.Skip((pageNumber - 1) * pageSize)
					.Take(pageSize)
					.ToList();
				return View(model);
			}

			IEnumerable<MaintenanceDueItem> filtered = items;
			if (!string.IsNullOrWhiteSpace(searchString))
			{
				var term = searchString.Trim();
				filtered = filtered.Where(i =>
					i.PlateNumber.Contains(term, StringComparison.OrdinalIgnoreCase)
					|| i.VehicleName.Contains(term, StringComparison.OrdinalIgnoreCase)
					|| EnumDisplay(i.Type).Contains(term, StringComparison.OrdinalIgnoreCase)
					|| i.ScheduleLabel.Contains(term, StringComparison.OrdinalIgnoreCase));
			}

			if (!string.IsNullOrEmpty(currentFilter)
				&& Enum.TryParse(currentFilter, true, out PmsDueKind filterKind))
			{
				filtered = filtered.Where(i => i.DueKind == filterKind);
			}

			var filteredItems = filtered.ToList();

			if (tab == "schedule")
			{
				var schedule = sortBy switch
				{
					"Vehicle" => filteredItems.OrderBy(i => i.PlateNumber).ThenBy(i => i.VehicleName).ThenBy(i => i.Type).ToList(),
					"Odometer" => filteredItems.OrderByDescending(i => i.CurrentOdometer).ThenBy(i => i.PlateNumber).ToList(),
					"NextDue" => filteredItems.OrderBy(i => i.NextDueDate ?? DateOnly.MaxValue).ThenBy(i => i.PlateNumber).ToList(),
					_ => filteredItems.OrderBy(i => KindOrder(i.DueKind)).ThenBy(i => i.PlateNumber).ThenBy(i => i.Type).ToList()
				};

				ApplyPaging(schedule.Count, pageSize, ref pageNumber);
				model.Schedule = schedule
					.Skip((pageNumber - 1) * pageSize)
					.Take(pageSize)
					.ToList();
				return View(model);
			}

			var matchingVehicleIds = filteredItems.Select(i => i.VehicleId).ToHashSet();
			var groups = items
				.GroupBy(i => i.VehicleId)
				.Where(g => matchingVehicleIds.Contains(g.Key))
				.Select(g =>
				{
					var first = g.First();
					var checkups = g
						.OrderBy(i => KindOrder(i.DueKind))
						.ThenBy(i => i.Type)
						.ToList();
					var lastDone = checkups
						.Where(c => c.LastCompletedDate != null)
						.Select(c => c.LastCompletedDate!.Value)
						.ToList();
					return new MaintenanceVehicleDueGroup
					{
						VehicleId = g.Key,
						VehicleName = first.VehicleName,
						PlateNumber = first.PlateNumber,
						ImagePath = first.ImagePath,
						VehicleStatus = first.VehicleStatus,
						CurrentOdometer = first.CurrentOdometer,
						OdometerUpdatedOn = lastDone.Count > 0 ? lastDone.Max() : first.RegistrationDate,
						WorstKind = checkups
							.OrderBy(i => KindOrder(i.DueKind))
							.Select(i => i.DueKind)
							.First(),
						OverdueCount = checkups.Count(i => i.DueKind == PmsDueKind.Overdue),
						DueNowCount = checkups.Count(i => i.DueKind == PmsDueKind.DueNow),
						DueSoonCount = checkups.Count(i => i.DueKind == PmsDueKind.DueSoon),
						InProgressCount = checkups.Count(i => i.DueKind == PmsDueKind.InProgress),
						NextCheckup = checkups.First(),
						Checkups = checkups
					};
				})
				.ToList();

			groups = sortBy switch
			{
				"Vehicle" => groups.OrderBy(g => g.PlateNumber).ThenBy(g => g.VehicleName).ToList(),
				"Odometer" => groups.OrderByDescending(g => g.CurrentOdometer).ThenBy(g => g.PlateNumber).ToList(),
				"NextDue" => groups
					.OrderBy(g => g.NextCheckup?.NextDueDate ?? DateOnly.MaxValue)
					.ThenBy(g => g.PlateNumber)
					.ToList(),
				_ => groups
					.OrderBy(g => KindOrder(g.WorstKind))
					.ThenBy(g => g.PlateNumber)
					.ToList()
			};

			ApplyPaging(groups.Count, pageSize, ref pageNumber);
			model.Vehicles = groups
				.Skip((pageNumber - 1) * pageSize)
				.Take(pageSize)
				.ToList();
			return View(model);
		}

		public async Task<IActionResult> Details(int? id)
		{
			if (id == null)
			{
				return NotFound();
			}

			var plan = await LoadPlanAsync(id.Value);
			if (plan?.Vehicle == null)
			{
				return NotFound();
			}

			return View(ToDetailsViewModel(plan, plan.Vehicle));
		}

		public IActionResult Create()
		{
			return RedirectToAction(nameof(Setup));
		}

		public async Task<IActionResult> Setup(int? vehicleId, string? preset)
		{
			var vehicles = await GetUnsetVehiclesAsync();
			if (vehicles.Count == 0)
			{
				TempData["ErrorMessage"] = "Every vehicle already has a maintenance schedule. Edit a vehicle's table from the due list.";
				return RedirectToAction(nameof(Index));
			}

			var selected = vehicles.FirstOrDefault(v => v.VehicleId == vehicleId) ?? vehicles[0];
			var selectedPreset = string.IsNullOrWhiteSpace(preset)
				? PmsRules.DefaultPreset(selected.Type)
				: preset;

			var input = new MaintenanceScheduleInput
			{
				VehicleId = selected.VehicleId,
				Preset = selectedPreset,
				Rows = RowsFromRules(PmsRules.ForPreset(selectedPreset))
			};

			PopulateSetupView(vehicles, input);
			return View(input);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Setup(MaintenanceScheduleInput input)
		{
			var vehicles = await GetUnsetVehiclesAsync();
			var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.VehicleId == input.VehicleId);
			if (vehicle == null)
			{
				return NotFound();
			}

			if (await _context.MaintenancePlans.AnyAsync(p => p.VehicleId == input.VehicleId))
			{
				TempData["ErrorMessage"] = "This vehicle already has a maintenance schedule.";
				return RedirectToAction(nameof(Index));
			}

			ValidateScheduleRows(input, requireIncluded: true);
			if (!ModelState.IsValid)
			{
				PopulateSetupView(vehicles.Count > 0 ? vehicles : new List<Vehicle> { vehicle }, input);
				return View(input);
			}

			foreach (var row in input.Rows.Where(r => r.Included))
			{
				var plan = new MaintenancePlan
				{
					VehicleId = vehicle.VehicleId,
					Type = row.Type,
					Trigger = row.Trigger,
					IntervalKilometers = row.Trigger == PmsTrigger.Mileage ? row.IntervalKilometers : null,
					IntervalMonths = row.Trigger == PmsTrigger.Time ? row.IntervalMonths : null
				};
				PmsRules.ApplyInitialDue(plan, vehicle);
				_context.MaintenancePlans.Add(plan);
			}

			await _context.SaveChangesAsync();
			_logs.Record(
				SystemLogAction.Created,
				SystemLogCategory.Maintenance,
				$"Created maintenance schedule for {vehicle.PlateNumber}.",
				"Vehicle",
				vehicle.VehicleId);
			await _context.SaveChangesAsync();
			TempData["SuccessMessage"] = "Maintenance schedule saved. This vehicle is now on the due list.";
			return RedirectToAction(nameof(Index));
		}

		public async Task<IActionResult> EditSchedule(int? id)
		{
			if (id == null)
			{
				return NotFound();
			}

			var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.VehicleId == id);
			if (vehicle == null)
			{
				return NotFound();
			}

			var plans = await _context.MaintenancePlans
				.Where(p => p.VehicleId == vehicle.VehicleId)
				.ToListAsync();
			if (plans.Count == 0)
			{
				return RedirectToAction(nameof(Setup), new { vehicleId = vehicle.VehicleId });
			}

			var input = new MaintenanceScheduleInput
			{
				VehicleId = vehicle.VehicleId,
				Preset = PmsRules.DefaultPreset(vehicle.Type),
				Rows = plans
					.OrderBy(p => p.Type)
					.Select(p => new MaintenanceScheduleRow
					{
						MaintenancePlanId = p.MaintenancePlanId,
						Type = p.Type,
						Trigger = p.Trigger,
						IntervalKilometers = p.IntervalKilometers,
						IntervalMonths = p.IntervalMonths,
						Included = true
					})
					.ToList()
			};

			ViewData["VehicleLabel"] = $"{vehicle.Brand} {vehicle.Model} ({vehicle.PlateNumber})";
			return View(input);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> EditSchedule(int id, MaintenanceScheduleInput input)
		{
			if (id != input.VehicleId)
			{
				return NotFound();
			}

			var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.VehicleId == id);
			if (vehicle == null)
			{
				return NotFound();
			}

			var plans = await _context.MaintenancePlans
				.Where(p => p.VehicleId == vehicle.VehicleId)
				.ToListAsync();

			ValidateScheduleRows(input, requireIncluded: false);
			if (!ModelState.IsValid)
			{
				ViewData["VehicleLabel"] = $"{vehicle.Brand} {vehicle.Model} ({vehicle.PlateNumber})";
				return View(input);
			}

			foreach (var row in input.Rows)
			{
				var plan = plans.FirstOrDefault(p => p.Type == row.Type);
				if (plan == null)
				{
					continue;
				}

				plan.Trigger = row.Trigger;
				plan.IntervalKilometers = row.Trigger == PmsTrigger.Mileage ? row.IntervalKilometers : null;
				plan.IntervalMonths = row.Trigger == PmsTrigger.Time ? row.IntervalMonths : null;
				PmsRules.RecalculateDue(plan, vehicle);
			}

			await _context.SaveChangesAsync();
			_logs.Record(
				SystemLogAction.Updated,
				SystemLogCategory.Maintenance,
				$"Updated maintenance schedule for {vehicle.PlateNumber}.",
				"Vehicle",
				vehicle.VehicleId);
			await _context.SaveChangesAsync();
			TempData["SuccessMessage"] = "Maintenance table updated. Next due dates were recalculated from the new intervals.";
			return RedirectToAction(nameof(Index));
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> StartWork(int id)
		{
			var plan = await LoadPlanAsync(id);
			if (plan?.Vehicle == null)
			{
				return NotFound();
			}

			if (plan.Logs.Any(l => l.Status == MaintenanceStatus.InProgress))
			{
				TempData["ErrorMessage"] = "This checkup is already in progress.";
				return RedirectToAction(nameof(Details), new { id });
			}

			var dueKind = PmsRules.Classify(plan, plan.Vehicle, inProgress: false);
			if (!PmsRules.CanStart(dueKind))
			{
				TempData["ErrorMessage"] = "This checkup is not due yet. Start it when it is due soon.";
				return RedirectToAction(nameof(Details), new { id });
			}

			if (plan.Vehicle.Status == VehicleStatus.Rented)
			{
				TempData["ErrorMessage"] = "This vehicle is currently rented. Complete the trip before starting a checkup.";
				return RedirectToAction(nameof(Details), new { id });
			}

			var today = DateOnly.FromDateTime(DateTime.Today);
			_context.MaintenanceLogs.Add(new MaintenanceLog
			{
				VehicleId = plan.VehicleId,
				MaintenancePlanId = plan.MaintenancePlanId,
				Type = plan.Type,
				Status = MaintenanceStatus.InProgress,
				ScheduledDate = today,
				StartedAt = DateTime.Now,
				CreatedAt = DateTime.Now
			});
			await _context.SaveChangesAsync();
			await VehicleStatusSync.ApplyAsync(_context, plan.VehicleId);

			_logs.Record(
				SystemLogAction.Started,
				SystemLogCategory.Maintenance,
				$"Started {plan.Type} checkup on {plan.Vehicle.PlateNumber}.",
				"MaintenancePlan",
				plan.MaintenancePlanId);
			await _context.SaveChangesAsync();

			TempData["SuccessMessage"] = "Checkup started. The vehicle is now In Maintenance.";
			return RedirectToAction(nameof(Details), new { id });
		}

		public async Task<IActionResult> Complete(int? id)
		{
			if (id == null)
			{
				return NotFound();
			}

			var plan = await LoadPlanAsync(id.Value);
			if (plan?.Vehicle == null)
			{
				return NotFound();
			}

			var log = plan.Logs.FirstOrDefault(l => l.Status == MaintenanceStatus.InProgress);
			if (log == null)
			{
				TempData["ErrorMessage"] = "Start this checkup before recording findings.";
				return RedirectToAction(nameof(Details), new { id });
			}

			log.Vehicle = plan.Vehicle;
			log.Plan = plan;
			log.Odometer ??= plan.Vehicle.Odometer;
			ViewData["Title"] = "Record Checkup";
			return View(log);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Complete(
			int id,
			[Bind("MaintenanceLogId,WorkDone,Cost,ImageFile,Odometer")] MaintenanceLog input)
		{
			var plan = await LoadPlanAsync(id);
			if (plan?.Vehicle == null)
			{
				return NotFound();
			}

			var log = plan.Logs.FirstOrDefault(l => l.Status == MaintenanceStatus.InProgress);
			if (log == null)
			{
				TempData["ErrorMessage"] = "Start this checkup before recording findings.";
				return RedirectToAction(nameof(Details), new { id });
			}

			if (string.IsNullOrWhiteSpace(input.WorkDone))
			{
				ModelState.AddModelError(nameof(input.WorkDone), "Findings are required.");
			}

			if (input.Odometer == null)
			{
				ModelState.AddModelError(nameof(input.Odometer), "Odometer reading is required.");
			}
			else if (input.Odometer < plan.Vehicle.Odometer)
			{
				ModelState.AddModelError(
					nameof(input.Odometer),
					$"Odometer cannot be lower than the vehicle's current reading ({plan.Vehicle.Odometer:N0} km).");
			}

			ModelState.Remove(nameof(MaintenanceLog.VehicleId));
			ModelState.Remove(nameof(MaintenanceLog.Type));
			ModelState.Remove(nameof(MaintenanceLog.ScheduledDate));
			ModelState.Remove(nameof(MaintenanceLog.Status));
			ModelState.Remove(nameof(MaintenanceLog.CreatedAt));
			ModelState.Remove(nameof(MaintenanceLog.MaintenancePlanId));

			if (!ModelState.IsValid)
			{
				log.WorkDone = input.WorkDone;
				log.Cost = input.Cost;
				log.Odometer = input.Odometer;
				log.Vehicle = plan.Vehicle;
				log.Plan = plan;
				ViewData["Title"] = "Record Checkup";
				return View(log);
			}

			log.WorkDone = input.WorkDone;
			log.Cost = input.Cost;
			log.Odometer = input.Odometer;

			if (input.ImageFile != null)
			{
				log.ImagePath = await ImageStorage.SaveAsync(
					_webHostEnvironment,
					input.ImageFile,
					ImageStorage.MaintenanceFolder);
			}

			var completedOn = DateOnly.FromDateTime(DateTime.Today);
			var odometer = input.Odometer!.Value;
			log.Status = MaintenanceStatus.Completed;
			log.CompletedAt = DateTime.Now;

			if (odometer > plan.Vehicle.Odometer)
			{
				plan.Vehicle.Odometer = odometer;
			}

			PmsRules.ApplyNextDue(plan, completedOn, odometer);
			_logs.Record(
				SystemLogAction.Completed,
				SystemLogCategory.Maintenance,
				$"Recorded {plan.Type} checkup on {plan.Vehicle.PlateNumber}.",
				"MaintenancePlan",
				plan.MaintenancePlanId);
			await _context.SaveChangesAsync();
			await VehicleStatusSync.ApplyAsync(_context, plan.VehicleId);

			return RedirectToAction(nameof(RecordSuccess), new { id });
		}

		public async Task<IActionResult> RecordSuccess(int? id)
		{
			if (id == null)
			{
				return NotFound();
			}

			var plan = await LoadPlanAsync(id.Value);
			if (plan?.Vehicle == null)
			{
				return NotFound();
			}

			var model = new CreateSuccessViewModel
			{
				PageTitle = "Record Checkup",
				ActivePage = "Maintenance",
				Heading = "Checkup Recorded",
				MessageHtml = "Findings were saved and the next due was updated from this vehicle's interval.",
				PrimaryActionText = "View Vehicle Maintenance",
				PrimaryActionUrl = Url.Action(nameof(Index)) ?? "",
				SecondaryActionText = "View Checkup Details",
				SecondaryActionUrl = Url.Action(nameof(Details), new { id }) ?? "",
				ShowSecondaryPlusIcon = false
			};

			return View("CreateSuccess", model);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Cancel(int id)
		{
			var plan = await LoadPlanAsync(id);
			if (plan == null)
			{
				return NotFound();
			}

			var log = plan.Logs.FirstOrDefault(l => l.Status == MaintenanceStatus.InProgress);
			if (log == null)
			{
				TempData["ErrorMessage"] = "There is no in-progress checkup to abort.";
				return RedirectToAction(nameof(Details), new { id });
			}

			log.Status = MaintenanceStatus.Cancelled;
			_logs.Record(
				SystemLogAction.Cancelled,
				SystemLogCategory.Maintenance,
				$"Aborted checkup on {plan.Vehicle?.PlateNumber ?? "vehicle"}.",
				"MaintenancePlan",
				plan.MaintenancePlanId);
			await _context.SaveChangesAsync();
			await VehicleStatusSync.ApplyAsync(_context, plan.VehicleId);

			TempData["SuccessMessage"] = "Checkup was aborted. The next due date was not changed.";
			return RedirectToAction(nameof(Details), new { id });
		}

		[HttpPost, ActionName("Delete")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteConfirmed(int id)
		{
			var log = await _context.MaintenanceLogs.FindAsync(id);
			if (log != null)
			{
				var vehicleId = log.VehicleId;
				var planId = log.MaintenancePlanId;
				_logs.Record(
					SystemLogAction.Deleted,
					SystemLogCategory.Maintenance,
					$"Deleted maintenance log #{log.MaintenanceLogId}.",
					"MaintenanceLog",
					log.MaintenanceLogId);
				_context.MaintenanceLogs.Remove(log);
				await _context.SaveChangesAsync();
				await VehicleStatusSync.ApplyAsync(_context, vehicleId);

				if (planId != null)
				{
					return RedirectToAction(nameof(Details), new { id = planId });
				}
			}

			return RedirectToAction(nameof(Index));
		}

		private async Task<MaintenancePlan?> LoadPlanAsync(int id)
		{
			return await _context.MaintenancePlans
				.Include(p => p.Vehicle)
				.Include(p => p.Logs)
				.FirstOrDefaultAsync(p => p.MaintenancePlanId == id);
		}

		private static MaintenanceDueItem ToDueItem(MaintenancePlan plan, Vehicle vehicle)
		{
			var openLog = plan.Logs.FirstOrDefault(l => l.Status == MaintenanceStatus.InProgress);
			return new MaintenanceDueItem
			{
				MaintenancePlanId = plan.MaintenancePlanId,
				VehicleId = plan.VehicleId,
				VehicleName = $"{vehicle.Brand} {vehicle.Model}".Trim(),
				PlateNumber = vehicle.PlateNumber,
				Type = plan.Type,
				Trigger = plan.Trigger,
				ScheduleLabel = PmsRules.FormatSchedule(plan),
				DueKind = PmsRules.Classify(plan, vehicle, openLog != null),
				NextDueDate = plan.NextDueDate,
				NextDueOdometer = plan.NextDueOdometer,
				CurrentOdometer = vehicle.Odometer,
				OpenLogId = openLog?.MaintenanceLogId,
				DueLabel = PmsRules.FormatDue(plan),
				LastCompletedDate = plan.LastCompletedDate,
				LastOdometer = plan.LastOdometer,
				RegistrationDate = vehicle.RegistrationDate == default
					? null
					: DateOnly.FromDateTime(vehicle.RegistrationDate),
				ImagePath = vehicle.ImagePath,
				VehicleStatus = vehicle.Status
			};
		}

		private static MaintenanceHistoryRow ToHistoryRow(MaintenanceLog log, Vehicle vehicle)
			=> new()
			{
				MaintenanceLogId = log.MaintenanceLogId,
				MaintenancePlanId = log.MaintenancePlanId,
				VehicleName = $"{vehicle.Brand} {vehicle.Model}".Trim(),
				PlateNumber = vehicle.PlateNumber,
				ImagePath = vehicle.ImagePath,
				Type = log.Type,
				CompletedAt = log.CompletedAt ?? log.CreatedAt,
				Odometer = log.Odometer,
				WorkDone = log.WorkDone,
				Cost = log.Cost
			};

		private static MaintenancePlanDetailsViewModel ToDetailsViewModel(MaintenancePlan plan, Vehicle vehicle)
		{
			var openLog = plan.Logs.FirstOrDefault(l => l.Status == MaintenanceStatus.InProgress);
			return new MaintenancePlanDetailsViewModel
			{
				Plan = plan,
				Vehicle = vehicle,
				OpenLog = openLog,
				DueKind = PmsRules.Classify(plan, vehicle, openLog != null),
				ScheduleLabel = PmsRules.FormatSchedule(plan),
				DueLabel = PmsRules.FormatDue(plan),
				History = plan.Logs
					.Where(l => l.Status == MaintenanceStatus.Completed)
					.OrderByDescending(l => l.CompletedAt ?? l.CreatedAt)
					.ToList()
			};
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

		private void ApplyPaging(int totalCount, int pageSize, ref int pageNumber)
		{
			var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
			if (pageNumber > totalPages)
			{
				pageNumber = totalPages;
			}

			ViewData["PageIndex"] = pageNumber;
			ViewData["TotalPages"] = totalPages;
			ViewData["TotalCount"] = totalCount;
			ViewData["PageSize"] = pageSize;
		}

		private static decimal PctChange(decimal current, decimal previous)
			=> Math.Round((current - previous) * 0.1m, 1);

		private static string EnumDisplay(Enum value)
		{
			var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
			var display = member?
				.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.DisplayAttribute), false)
				.OfType<System.ComponentModel.DataAnnotations.DisplayAttribute>()
				.FirstOrDefault()?
				.GetName();
			return display ?? value.ToString();
		}

		private async Task<List<Vehicle>> GetUnsetVehiclesAsync()
		{
			var scheduledIds = await _context.MaintenancePlans
				.Select(p => p.VehicleId)
				.Distinct()
				.ToListAsync();

			return await _context.Vehicles
				.AsNoTracking()
				.Where(v => !scheduledIds.Contains(v.VehicleId))
				.OrderBy(v => v.PlateNumber)
				.ToListAsync();
		}

		private async Task<int> CountUnsetVehiclesAsync()
		{
			var scheduledIds = await _context.MaintenancePlans
				.Select(p => p.VehicleId)
				.Distinct()
				.ToListAsync();

			return await _context.Vehicles.CountAsync(v => !scheduledIds.Contains(v.VehicleId));
		}

		private void PopulateSetupView(IReadOnlyList<Vehicle> vehicles, MaintenanceScheduleInput input)
		{
			ViewData["VehicleOptions"] = new SelectList(
				vehicles.Select(v => new
				{
					v.VehicleId,
					Label = v.Brand + " " + v.Model + " (" + v.PlateNumber + ")"
				}),
				"VehicleId",
				"Label",
				input.VehicleId);

			ViewData["VehicleTypesJson"] = System.Text.Json.JsonSerializer.Serialize(
				vehicles.ToDictionary(v => v.VehicleId.ToString(), v => PmsRules.DefaultPreset(v.Type)));
		}

		private void ValidateScheduleRows(MaintenanceScheduleInput input, bool requireIncluded)
		{
			if (input.Rows == null || input.Rows.Count == 0)
			{
				ModelState.AddModelError(string.Empty, "Add at least one checkup to the table.");
				return;
			}

			var included = input.Rows.Where(r => r.Included).ToList();
			if (requireIncluded && included.Count == 0)
			{
				ModelState.AddModelError(string.Empty, "Select at least one checkup for this vehicle.");
			}

			for (var i = 0; i < input.Rows.Count; i++)
			{
				var row = input.Rows[i];
				if (!row.Included)
				{
					continue;
				}

				if (row.Trigger == PmsTrigger.Mileage && (row.IntervalKilometers == null || row.IntervalKilometers <= 0))
				{
					ModelState.AddModelError($"Rows[{i}].IntervalKilometers", "Enter a kilometer interval.");
				}

				if (row.Trigger == PmsTrigger.Time && (row.IntervalMonths == null || row.IntervalMonths <= 0))
				{
					ModelState.AddModelError($"Rows[{i}].IntervalMonths", "Enter a month interval.");
				}
			}
		}

		private static List<MaintenanceScheduleRow> RowsFromRules(IEnumerable<PmsRule> rules)
			=> rules.Select(r => new MaintenanceScheduleRow
			{
				Type = r.Type,
				Trigger = r.Trigger,
				IntervalKilometers = r.IntervalKilometers,
				IntervalMonths = r.IntervalMonths,
				Included = true
			}).ToList();
	}
}
