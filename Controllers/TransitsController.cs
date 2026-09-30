using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Models;
using EasyRent_Checking.Data;
using EasyRent_Checking.Services;
using EasyRent_Checking.ViewModels;

[Authorize(Policy = "StaffArea")]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class TransitsController : Controller
{
	private readonly EasyRent_CheckingContext _context;
	private readonly IWebHostEnvironment _webHostEnvironment;
	private readonly BookingEmailService _bookingEmailService;
	private readonly SystemLogService _logs;

	public TransitsController(
		EasyRent_CheckingContext context,
		IWebHostEnvironment webHostEnvironment,
		BookingEmailService bookingEmailService,
		SystemLogService logs)
	{
		_context = context;
		_webHostEnvironment = webHostEnvironment;
		_bookingEmailService = bookingEmailService;
		_logs = logs;
	}

	// GET: TRANSITS
	public async Task<IActionResult> Index(string searchString, string sortBy, string currentFilter, int? page)
	{
		await SyncMissingTransitsAsync();
		await ConsolidateDuplicateTransitsAsync();

		const int pageSize = 10;
		var pageNumber = page.GetValueOrDefault(1);
		if (pageNumber < 1)
		{
			pageNumber = 1;
		}

		ViewData["CurrentSearch"] = searchString;
		ViewData["CurrentSort"] = sortBy;
		ViewData["CurrentFilter"] = currentFilter;

		var transitsQuery = _context.Transits
			.AsNoTracking()
			.Include(t => t.Vehicle)
			.Include(t => t.Rental)
				.ThenInclude(r => r!.Customer).ThenInclude(c => c!.User)
			.Include(t => t.Rental)
				.ThenInclude(r => r!.RentalVehicles)
					.ThenInclude(d => d!.Vehicle)
			.AsQueryable();

		ViewData["TotalBookingsCount"] = await transitsQuery.Select(t => t.RentalID).Distinct().CountAsync();
		ViewData["ApprovedCount"] = await transitsQuery
			.Where(t => t.Rental != null && t.Rental.RentalStatus == RentalStatus.Approved)
			.Select(t => t.RentalID)
			.Distinct()
			.CountAsync();
		ViewData["CompletedCount"] = await transitsQuery
			.Where(t => t.TripStatus == TripStatus.Completed)
			.Select(t => t.RentalID)
			.Distinct()
			.CountAsync();
		ViewData["OngoingCount"] = await transitsQuery
			.Where(t => t.TripStatus == TripStatus.InTransit)
			.Select(t => t.RentalID)
			.Distinct()
			.CountAsync();

		if (!string.IsNullOrEmpty(searchString))
		{
			var term = searchString.Trim();
			var idToken = term.StartsWith("BK-", StringComparison.OrdinalIgnoreCase)
				? term[3..]
				: term;
			var hasBookingId = int.TryParse(idToken, out var bookingId);

			transitsQuery = transitsQuery.Where(t =>
				(t.Rental != null && (
					t.Rental.CustomerName.Contains(term)
					|| t.Rental.ContactNumber.Contains(term)))
				|| (hasBookingId && t.RentalID == bookingId)
				|| t.TransitID == bookingId);
		}

		if (!string.IsNullOrEmpty(currentFilter) && Enum.TryParse(currentFilter, true, out TripStatus filterStatus))
		{
			transitsQuery = transitsQuery.Where(t => t.TripStatus == filterStatus);
		}

		var transits = await transitsQuery.ToListAsync();

		static int TripStatusPriority(TripStatus status) => status switch
		{
			TripStatus.InTransit => 5,
			TripStatus.Scheduled => 4,
			TripStatus.Delayed => 3,
			TripStatus.Completed => 2,
			TripStatus.Cancelled => 1,
			_ => 0
		};

		var groupedItems = transits
			.GroupBy(t => t.RentalID)
			.Select(g =>
			{
				var primary = g
					.OrderByDescending(t => TripStatusPriority(t.TripStatus))
					.ThenBy(t => t.TransitID)
					.First();
				return new TransitIndexItemViewModel
				{
					Transit = primary,
					VehicleCount = g.Select(t => t.VehicleID).Distinct().Count(),
					VehicleLabel = BuildVehicleLabel(g, primary.Rental)
				};
			});

		groupedItems = sortBy switch
		{
			"CustomerName" => groupedItems.OrderBy(i => i.Transit.Rental?.CustomerName),
			"TripStatus" => groupedItems.OrderBy(i => i.Transit.TripStatus),
			"BookingId" => groupedItems.OrderBy(i => i.Transit.RentalID),
			_ => groupedItems.OrderByDescending(i => i.Transit.TransitID)
		};

		var items = groupedItems.ToList();
		var totalCount = items.Count;
		var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
		if (pageNumber > totalPages)
		{
			pageNumber = totalPages;
		}

		ViewData["PageIndex"] = pageNumber;
		ViewData["TotalPages"] = totalPages;
		ViewData["TotalCount"] = totalCount;
		ViewData["PageSize"] = pageSize;

		var pageItems = items
			.Skip((pageNumber - 1) * pageSize)
			.Take(pageSize)
			.ToList();

		return View(pageItems);
	}

	private static string BuildVehicleLabel(IEnumerable<Transit> trips, Rental? rental)
	{
		var vehicles = trips
			.Select(t => t.Vehicle)
			.Where(v => v != null)
			.GroupBy(v => v!.VehicleId)
			.Select(group => group.First()!)
			.ToList();

		if (vehicles.Count > 1)
		{
			var first = vehicles[0];
			return $"{first.Brand} {first.Model} + {vehicles.Count - 1} more";
		}

		if (vehicles.Count == 1)
		{
			return $"{vehicles[0].Brand} {vehicles[0].Model}";
		}

		var detailsVehicle = rental?.RentalVehicles?
			.OrderBy(rv => rv.SortOrder)
			.Select(rv => rv.Vehicle)
			.FirstOrDefault();
		if (detailsVehicle != null)
		{
			return $"{detailsVehicle.Brand} {detailsVehicle.Model}";
		}

		return "Unassigned";
	}

	// GET: TRANSITS/Details/5
	public async Task<IActionResult> Details(int? transitid)
	{
		if (transitid == null)
		{
			return NotFound();
		}

		var transit = await LoadTransitAsync(transitid.Value);
		if (transit == null)
		{
			return NotFound();
		}

		var model = await BuildDetailsViewModelAsync(transit);
		return View(model);
	}

	// POST: TRANSITS/AssignDriver/5
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> AssignDriver(int transitid, int driverId)
	{
		var transit = await _context.Transits
			.Include(t => t.Rental)!
				.ThenInclude(r => r!.RentalVehicles)
			.Include(t => t.Vehicle)
			.FirstOrDefaultAsync(t => t.TransitID == transitid);
		if (transit == null)
		{
			return NotFound();
		}

		if (transit.TripStatus is TripStatus.Completed or TripStatus.Cancelled)
		{
			TempData["ErrorMessage"] = "Cannot assign a driver to a completed or cancelled trip.";
			return RedirectToAction(nameof(Details), new { transitid });
		}

		var vehicleAlert = await TripVehicleAvailability.EvaluateAsync(_context, transit);
		if (vehicleAlert.BlocksDispatch)
		{
			TempData["ErrorMessage"] = vehicleAlert.Message;
			return RedirectToAction(nameof(Details), new { transitid });
		}

		if (driverId <= 0)
		{
			TempData["ErrorMessage"] = "Please select a valid driver.";
			return RedirectToAction(nameof(Details), new { transitid });
		}

		var driver = await _context.Drivers.AsNoTracking().FirstOrDefaultAsync(d => d.DriverId == driverId);
		if (driver == null)
		{
			TempData["ErrorMessage"] = "Please select a valid driver.";
			return RedirectToAction(nameof(Details), new { transitid });
		}

		if (!driver.IsActive)
		{
			TempData["ErrorMessage"] = "Cannot assign a deactivated driver.";
			return RedirectToAction(nameof(Details), new { transitid });
		}

		var systemDate = DateOnly.FromDateTime(DateTime.Today);
		if (driver.ExpiryDate < systemDate)
		{
			TempData["ErrorMessage"] = "Cannot assign a driver with an expired license.";
			return RedirectToAction(nameof(Details), new { transitid });
		}

		var tripWindow = GetTripWindow(transit.Rental);
		if (tripWindow == null)
		{
			TempData["ErrorMessage"] = "This trip has no pickup/return schedule, so a driver cannot be assigned yet.";
			return RedirectToAction(nameof(Details), new { transitid });
		}

		var conflictingRentalId = await FindOverlappingDriverAssignmentAsync(
			driverId,
			excludeTransitId: transit.TransitID,
			tripWindow.Value.Start,
			tripWindow.Value.End);

		if (conflictingRentalId.HasValue)
		{
			TempData["ErrorMessage"] =
				$"This driver is already assigned to BK-{conflictingRentalId.Value:D5} during the same schedule.";
			return RedirectToAction(nameof(Details), new { transitid });
		}

		transit.DriverID = driverId;
		_logs.Record(
			SystemLogAction.Assigned,
			SystemLogCategory.Trip,
			$"Assigned driver {driver.Name} to trip for BK-{transit.RentalID:D5}.",
			"Transit",
			transit.TransitID);
		await _context.SaveChangesAsync();
		TempData["SuccessMessage"] = "Driver assigned successfully.";
		return RedirectToAction(nameof(Details), new { transitid });
	}

	// GET: TRANSITS/StartTrip/5
	// Displays vehicle preparation and driver assignment form
	public async Task<IActionResult> StartTrip(int? transitid)
	{
		// Step 1: Validate transit ID
		if (transitid == null)
		{
			return NotFound();
		}

		// Step 2: Load the transit
		var transit = await LoadTransitAsync(transitid.Value);
		if (transit == null)
		{
			return NotFound();
		}

		// Step 3: Check if trip can be prepared
		if (!CanOpenPreparation(transit))
		{
			TempData["ErrorMessage"] = "This trip cannot be prepared in its current status.";
			return RedirectToAction(nameof(Details), new { transitid });
		}

		// Step 4: Set default departure time from rental details
		if (transit.DepartureTime == null && transit.Rental != null)
		{
			transit.DepartureTime = transit.Rental.PickupTime;
		}

		// Step 5: Set default odometer start from vehicle's current reading
		transit.OdometerStart ??= transit.Vehicle?.Odometer;

		// Step 6: Display preparation form
		ViewData["Title"] = "Vehicle Preparation & Driver Assignment";
		return View(await BuildPreparationViewModelAsync(transit));
	}

	// POST: TRANSITS/StartTrip/5
	// Processes vehicle preparation and driver assignment
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> StartTrip(
		int transitid,
		int driverId,
		[Bind("TransitID,DepartureTime,FuelLevelStart,OdometerStart,VehicleConditionStart,PreTripImageFile", Prefix = "Transit")] Transit input)
	{
		// Step 1: Load the transit
		var transit = await LoadTransitAsync(transitid);
		if (transit == null)
		{
			return NotFound();
		}

		// Step 2: Check if trip can be prepared
		if (!CanOpenPreparation(transit))
		{
			TempData["ErrorMessage"] = "This trip cannot be prepared in its current status.";
			return RedirectToAction(nameof(Details), new { transitid });
		}

		// Step 3: Clear model state for validation
		ModelState.Clear();
		
		// Step 4: Validate driver selection
		if (driverId <= 0)
		{
			ModelState.AddModelError("driverId", "Please select a driver.");
		}

		// Step 5: Validate departure time
		if (input.DepartureTime == null)
		{
			ModelState.AddModelError(nameof(input.DepartureTime), "Departure time is required.");
		}

		// Step 6: Validate fuel level
		if (input.FuelLevelStart == null)
		{
			ModelState.AddModelError(nameof(input.FuelLevelStart), "Starting fuel level is required.");
		}

		// Step 7: Validate odometer start reading
		if (input.OdometerStart == null)
		{
			ModelState.AddModelError(nameof(input.OdometerStart), "Starting odometer reading is required.");
		}
		else if (transit.Vehicle != null && input.OdometerStart < transit.Vehicle.Odometer)
		{
			ModelState.AddModelError(
				nameof(input.OdometerStart),
				$"Odometer cannot be lower than the vehicle's current reading ({transit.Vehicle.Odometer:N0} km).");
		}

		// Step 8: Validate driver assignment
		Driver? selectedDriver = null;
		if (driverId > 0)
		{
			var assignError = await ValidateDriverAssignmentAsync(transit, driverId);
			if (assignError != null)
			{
				ModelState.AddModelError("driverId", assignError);
			}
			else
			{
				selectedDriver = await _context.Drivers.AsNoTracking()
					.FirstOrDefaultAsync(d => d.DriverId == driverId);
			}
		}

		// Step 9: If validation fails, redisplay form with errors
		if (!ModelState.IsValid)
		{
			transit.DepartureTime = input.DepartureTime;
			transit.FuelLevelStart = input.FuelLevelStart;
			transit.OdometerStart = input.OdometerStart;
			transit.VehicleConditionStart = input.VehicleConditionStart;
			ViewData["Title"] = "Vehicle Preparation & Driver Assignment";
			return View(await BuildPreparationViewModelAsync(transit, driverId > 0 ? driverId : null));
		}

		// Step 10: Assign driver to transit if changed
		if (selectedDriver != null && transit.DriverID != driverId)
		{
			transit.DriverID = driverId;
			_logs.Record(
				SystemLogAction.Assigned,
				SystemLogCategory.Trip,
				$"Assigned driver {selectedDriver.Name} to trip for BK-{transit.RentalID:D5}.",
				"Transit",
				transit.TransitID);
		}

		// Step 11: Save trip preparation details
		transit.DepartureTime = input.DepartureTime;
		transit.FuelLevelStart = input.FuelLevelStart;
		transit.OdometerStart = input.OdometerStart;
		transit.VehicleConditionStart = input.VehicleConditionStart;
		
		// Step 12: Update vehicle's master odometer
		ApplyVehicleOdometer(transit.Vehicle, input.OdometerStart!.Value);

		// Step 13: Save pre-trip photos if uploaded
		var preTripFiles = TransitTripPhotos.CollectFiles(Request, "Transit.PreTripImageFile", "PreTripImageFiles");
		if (preTripFiles.Count > 0)
		{
			await SaveTripPhotosAsync(transit, preTripFiles, isPreTrip: true);
		}

		// Step 14: Log the preparation action
		_logs.Record(
			SystemLogAction.Updated,
			SystemLogCategory.Trip,
			$"Saved vehicle preparation for BK-{transit.RentalID:D5}.",
			"Transit",
			transit.TransitID);
		await _context.SaveChangesAsync();

		// Step 15: Show success message and redirect
		TempData["SuccessMessage"] = "Driver assigned and vehicle preparation saved. Start the trip when ready to dispatch.";
		return RedirectToAction(nameof(Details), new { transitid });
	}

	// POST: TRANSITS/DispatchTrip/5
	// Dispatches the trip and marks vehicles as in use
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> DispatchTrip(int transitid)
	{
		// Step 1: Load the primary transit with rental details
		var primaryTransit = await _context.Transits
			.Include(t => t.Rental)!
				.ThenInclude(r => r!.RentalVehicles)
			.FirstOrDefaultAsync(t => t.TransitID == transitid);
		if (primaryTransit == null)
		{
			return NotFound();
		}

		// Step 2: Load all transits for this rental (multi-vehicle support)
		var allTransits = await _context.Transits
			.Include(t => t.Vehicle)
			.Include(t => t.Driver)
			.Where(t => t.RentalID == primaryTransit.RentalID)
			.OrderBy(t => t.TransitID)
			.ToListAsync();

		// Step 3: Check which trips are ready to dispatch
		var tripsToDispatch = allTransits.Where(CanDispatchTrip).ToList();
		if (tripsToDispatch.Count == 0)
		{
			TempData["ErrorMessage"] = "Complete vehicle preparation before starting this trip.";
			return RedirectToAction(nameof(Details), new { transitid });
		}

		// Step 4: Ensure all vehicles are prepared before dispatching
		var awaitingDispatch = allTransits.Where(CanOpenPreparation).ToList();
		if (tripsToDispatch.Count != awaitingDispatch.Count)
		{
			TempData["ErrorMessage"] = "Prepare all vehicles before starting the trip.";
			return RedirectToAction(nameof(Details), new { transitid });
		}

		// Step 5: Check vehicle availability for all trips
		foreach (var transit in tripsToDispatch)
		{
			var vehicleAlert = await TripVehicleAvailability.EvaluateAsync(_context, transit);
			if (vehicleAlert.BlocksDispatch)
			{
				TempData["ErrorMessage"] = vehicleAlert.Message;
				return RedirectToAction(nameof(Details), new { transitid });
			}
		}

		// Step 6: Update trip status to InTransit for all vehicles
		foreach (var transit in tripsToDispatch)
		{
			transit.TripStatus = TripStatus.InTransit;

			_logs.Record(
				SystemLogAction.Started,
				SystemLogCategory.Trip,
				$"Started trip for BK-{transit.RentalID:D5}.",
				"Transit",
				transit.TransitID);
		}

		// Step 7: Save changes to database
		await _context.SaveChangesAsync();

		// Step 8: Send trip started notification email
		if (primaryTransit.Rental != null)
		{
			await _bookingEmailService.SendTripStartedAsync(primaryTransit.Rental);
		}

		// Step 9: Show success message and redirect
		TempData["SuccessMessage"] = tripsToDispatch.Count > 1
			? $"Trip started for {tripsToDispatch.Count} vehicles."
			: "Trip started.";
		return RedirectToAction(nameof(Details), new { transitid });
	}

	// GET: TRANSITS/CompleteTrip/5
	// Displays trip completion form
	public async Task<IActionResult> CompleteTrip(int? transitid)
	{
		// Step 1: Validate transit ID
		if (transitid == null)
		{
			return NotFound();
		}

		// Step 2: Load the transit
		var transit = await LoadTransitAsync(transitid.Value);
		if (transit == null)
		{
			return NotFound();
		}

		// Step 3: Check if trip is in transit (only in-transit trips can be completed)
		if (transit.TripStatus != TripStatus.InTransit)
		{
			TempData["ErrorMessage"] = "Only in-transit trips can be completed.";
			return RedirectToAction(nameof(Details), new { transitid });
		}

		// Step 4: Set default return time from rental details
		if (transit.ReturnTime == null && transit.Rental != null)
		{
			transit.ReturnTime = transit.Rental.ReturnTime;
		}

		// Step 5: Set default odometer end from start reading or vehicle's current reading
		transit.OdometerEnd ??= transit.OdometerStart ?? transit.Vehicle?.Odometer;

		// Step 6: Display completion form
		ViewData["Title"] = "Complete Trip";
		return View(transit);
	}

	// POST: TRANSITS/CompleteTrip/5
	// Processes trip completion and updates vehicle status
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> CompleteTrip(
		int transitid,
		[Bind("TransitID,ReturnTime,FuelLevelEnd,OdometerEnd,VehicleConditionEnd,PostTripImageFile")] Transit input)
	{
		// Step 1: Load the transit
		var transit = await LoadTransitAsync(transitid);
		if (transit == null)
		{
			return NotFound();
		}

		// Step 2: Check if trip is in transit (only in-transit trips can be completed)
		if (transit.TripStatus != TripStatus.InTransit)
		{
			TempData["ErrorMessage"] = "Only in-transit trips can be completed.";
			return RedirectToAction(nameof(Details), new { transitid });
		}

		// Step 3: Clear model state for validation
		ModelState.Clear();
		
		// Step 4: Validate return time
		if (input.ReturnTime == null)
		{
			ModelState.AddModelError(nameof(input.ReturnTime), "Return time is required.");
		}

		// Step 5: Validate fuel level
		if (input.FuelLevelEnd == null)
		{
			ModelState.AddModelError(nameof(input.FuelLevelEnd), "Ending fuel level is required.");
		}

		// Step 6: Validate odometer end reading
		if (input.OdometerEnd == null)
		{
			ModelState.AddModelError(nameof(input.OdometerEnd), "Ending odometer reading is required.");
		}
		else
		{
			var minimumKm = transit.OdometerStart ?? transit.Vehicle?.Odometer ?? 0;
			if (input.OdometerEnd < minimumKm)
			{
				ModelState.AddModelError(
					nameof(input.OdometerEnd),
					$"Odometer cannot be lower than the start reading ({minimumKm:N0} km).");
			}
		}

		// Step 7: If validation fails, redisplay form with errors
		if (!ModelState.IsValid)
		{
			transit.ReturnTime = input.ReturnTime;
			transit.FuelLevelEnd = input.FuelLevelEnd;
			transit.OdometerEnd = input.OdometerEnd;
			transit.VehicleConditionEnd = input.VehicleConditionEnd;
			ViewData["Title"] = "Complete Trip";
			return View(transit);
		}

		// Step 8: Save trip completion details
		transit.ReturnTime = input.ReturnTime;
		transit.FuelLevelEnd = input.FuelLevelEnd;
		transit.OdometerEnd = input.OdometerEnd;
		transit.VehicleConditionEnd = input.VehicleConditionEnd;

		// Step 9: Update trip status to Completed
		transit.TripStatus = TripStatus.Completed;
		
		// Step 10: Update vehicle's master odometer
		ApplyVehicleOdometer(transit.Vehicle, input.OdometerEnd.Value);

		// Step 11: Save post-trip photos if uploaded
		var postTripFiles = TransitTripPhotos.CollectFiles(Request, "PostTripImageFile", "PostTripImageFiles");
		if (postTripFiles.Count > 0)
		{
			await SaveTripPhotosAsync(transit, postTripFiles, isPreTrip: false);
		}

		// Step 12: Log the completion action
		_logs.Record(
			SystemLogAction.Completed,
			SystemLogCategory.Trip,
			$"Completed trip for BK-{transit.RentalID:D5}.",
			"Transit",
			transit.TransitID);
		await _context.SaveChangesAsync();
		
		// Step 13: Show success message and redirect
		TempData["SuccessMessage"] = "Trip completed.";
		return RedirectToAction(nameof(Details), new { transitid });
	}

	// GET: TRANSITS/Delete/5
	public async Task<IActionResult> Delete(int? transitid)
	{
		if (transitid == null)
		{
			return NotFound();
		}

		var transit = await LoadTransitAsync(transitid.Value);
		if (transit == null)
		{
			return NotFound();
		}

		return View(transit);
	}

	// POST: TRANSITS/Delete/5
	[HttpPost, ActionName("Delete")]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> DeleteConfirmed(int? transitid)
	{
		var transit = await _context.Transits.FindAsync(transitid);
		if (transit != null)
		{
			_logs.Record(
				SystemLogAction.Deleted,
				SystemLogCategory.Trip,
				$"Deleted trip for BK-{transit.RentalID:D5}.",
				"Transit",
				transit.TransitID);
			_context.Transits.Remove(transit);
			await _context.SaveChangesAsync();
		}

		return RedirectToAction(nameof(Index));
	}

	private async Task<Transit?> LoadTransitAsync(int transitId)
	{
		return await _context.Transits
			.Include(t => t.Rental)!
				.ThenInclude(r => r!.RentalVehicles)
					.ThenInclude(rv => rv.Vehicle)
			.Include(t => t.Driver)
			.Include(t => t.Vehicle)
			.FirstOrDefaultAsync(t => t.TransitID == transitId);
	}

	private async Task<TransitDetailsViewModel> BuildDetailsViewModelAsync(Transit primaryTransit)
	{
		var allTransits = await _context.Transits
			.AsNoTracking()
			.Include(t => t.Vehicle)
			.Include(t => t.Driver)
			.Include(t => t.Rental)!
				.ThenInclude(r => r!.RentalVehicles)
			.Where(t => t.RentalID == primaryTransit.RentalID)
			.OrderBy(t => t.TransitID)
			.ToListAsync();

		if (!allTransits.Any(t => t.TransitID == primaryTransit.TransitID))
		{
			allTransits.Insert(0, primaryTransit);
		}

		var rentalVehicles = await _context.RentalVehicles
			.AsNoTracking()
			.Include(rv => rv.Vehicle)
			.Where(rv => rv.RentalId == primaryTransit.RentalID)
			.OrderBy(rv => rv.SortOrder)
			.ThenBy(rv => rv.RentalVehicleId)
			.ToListAsync();

		if (rentalVehicles.Count == 0 && primaryTransit.Vehicle != null)
		{
			rentalVehicles.Add(new RentalVehicle
			{
				RentalId = primaryTransit.RentalID,
				VehicleId = primaryTransit.VehicleID,
				Vehicle = primaryTransit.Vehicle,
				SortOrder = 0
			});
		}

		var payment = await _context.Payments
			.AsNoTracking()
			.Where(p => p.RentalId == primaryTransit.RentalID)
			.OrderByDescending(p => p.PaymentId)
			.FirstOrDefaultAsync();

		await TransitIssueSync.SyncForRentalAsync(_context, primaryTransit.RentalID);

		var issueAlerts = await TransitIssueSync.LoadAlertsForRentalAsync(_context, primaryTransit.RentalID);
		foreach (var alert in issueAlerts)
		{
			alert.SourceUrl = alert.Source switch
			{
				TransitIssueSource.Incident when alert.IncidentReportId.HasValue
					=> Url.Action("Details", "Incidents", new { id = alert.IncidentReportId.Value }) ?? string.Empty,
				TransitIssueSource.Maintenance when alert.MaintenancePlanId.HasValue
					=> Url.Action("Details", "Maintenance", new { id = alert.MaintenancePlanId.Value }) ?? string.Empty,
				_ => string.Empty
			};
		}

		var vehicleTrips = new List<TransitVehicleTripViewModel>();
		TripVehicleAvailabilityResult? topAlert = null;

		foreach (var trip in allTransits)
		{
			var alert = await ApplyVehicleAvailabilityAlertAsync(trip);
			var canPrepareBase = CanPrepareTrip(trip);
			var canDispatchBase = CanDispatchTrip(trip);
			vehicleTrips.Add(new TransitVehicleTripViewModel
			{
				Transit = trip,
				VehicleAlert = alert,
				CanPrepareTrip = canPrepareBase,
				CanDispatchTrip = canDispatchBase,
				CanCompleteTrip = trip.TripStatus == TripStatus.InTransit
			});

			if (alert.HasAlert && (topAlert == null || (alert.BlocksDispatch && !topAlert.BlocksDispatch)))
			{
				topAlert = alert;
			}
		}

		return new TransitDetailsViewModel
		{
			PrimaryTransit = primaryTransit,
			AllTransits = allTransits,
			RentalVehicles = rentalVehicles,
			VehicleTrips = vehicleTrips,
			LatestPayment = payment,
			VehicleAvailabilityAlert = topAlert,
			IssueAlerts = issueAlerts,
			CanResolveBooking = primaryTransit.Rental != null
				&& RentalResolution.CanResolve(primaryTransit.Rental, allTransits),
			CanDispatchAllTrips = vehicleTrips.Count > 0
				&& vehicleTrips
					.Where(v => CanOpenPreparation(v.Transit))
					.All(v => v.CanDispatchTrip)
				&& vehicleTrips.Any(v => v.CanDispatchTrip)
		};
	}

	private async Task<TripVehicleAvailabilityResult> ApplyVehicleAvailabilityAlertAsync(Transit transit)
	{
		var alert = await TripVehicleAvailability.EvaluateAsync(_context, transit);
		if (alert.IncidentReportId.HasValue)
		{
			alert.SourceUrl = Url.Action("Details", "Incidents", new { id = alert.IncidentReportId.Value });
		}
		else if (alert.MaintenancePlanId.HasValue)
		{
			alert.SourceUrl = Url.Action("Details", "Maintenance", new { id = alert.MaintenancePlanId.Value });
		}
		else if (alert.Source == TripVehicleAvailabilitySource.Fleet && transit.VehicleID > 0)
		{
			alert.SourceUrl = Url.Action("Details", "Vehicles", new { id = transit.VehicleID });
		}

		return alert;
	}

	private static (DateTime Start, DateTime End)? GetTripWindow(Rental? rental)
	{
		if (rental == null)
		{
			return null;
		}

		var start = rental.PickupDate.ToDateTime(rental.PickupTime);
		var end = rental.ReturnDate.ToDateTime(rental.ReturnTime);
		if (end <= start)
		{
			return null;
		}

		return (start, end);
	}

	private async Task<HashSet<int>> GetBusyDriverIdsAsync(DateTime start, DateTime end, int excludeTransitId)
	{
		var assignedTrips = await _context.Transits
			.AsNoTracking()
			.Where(t => t.DriverID != null
				&& t.TransitID != excludeTransitId
				&& t.TripStatus != TripStatus.Completed
				&& t.TripStatus != TripStatus.Cancelled)
			.Select(t => new
			{
				DriverId = t.DriverID!.Value,
				PickupDate = t.Rental!.PickupDate,
				PickupTime = t.Rental.PickupTime,
				ReturnDate = t.Rental.ReturnDate,
				ReturnTime = t.Rental.ReturnTime
			})
			.ToListAsync();

		return assignedTrips
			.Where(t =>
			{
				var otherStart = t.PickupDate.ToDateTime(t.PickupTime);
				var otherEnd = t.ReturnDate.ToDateTime(t.ReturnTime);
				return start < otherEnd && otherStart < end;
			})
			.Select(t => t.DriverId)
			.ToHashSet();
	}

	private async Task<int?> FindOverlappingDriverAssignmentAsync(
		int driverId,
		int excludeTransitId,
		DateTime start,
		DateTime end)
	{
		var otherTrips = await _context.Transits
			.AsNoTracking()
			.Where(t => t.DriverID == driverId
				&& t.TransitID != excludeTransitId
				&& t.TripStatus != TripStatus.Completed
				&& t.TripStatus != TripStatus.Cancelled)
			.Select(t => new
			{
				t.RentalID,
				PickupDate = t.Rental!.PickupDate,
				PickupTime = t.Rental.PickupTime,
				ReturnDate = t.Rental.ReturnDate,
				ReturnTime = t.Rental.ReturnTime
			})
			.ToListAsync();

		foreach (var trip in otherTrips)
		{
			var otherStart = trip.PickupDate.ToDateTime(trip.PickupTime);
			var otherEnd = trip.ReturnDate.ToDateTime(trip.ReturnTime);
			if (start < otherEnd && otherStart < end)
			{
				return trip.RentalID;
			}
		}

		return null;
	}

	private static bool IsTripPrepared(Transit transit)
		=> TransitPreparation.IsPrepared(transit);

	private static bool CanOpenPreparation(Transit transit)
		=> transit.TripStatus is TripStatus.Scheduled or TripStatus.Delayed;

	private static bool CanPrepareTrip(Transit transit)
		=> CanOpenPreparation(transit) && !IsTripPrepared(transit);

	private static bool CanDispatchTrip(Transit transit)
		=> CanOpenPreparation(transit) && IsTripPrepared(transit);

	private static bool CanStartTrip(Transit transit)
		=> CanDispatchTrip(transit);

	private async Task<string?> ValidateDriverAssignmentAsync(Transit transit, int driverId)
	{
		var driver = await _context.Drivers.AsNoTracking().FirstOrDefaultAsync(d => d.DriverId == driverId);
		if (driver == null)
		{
			return "Please select a valid driver.";
		}

		if (!driver.IsActive)
		{
			return "Cannot assign a deactivated driver.";
		}

		var systemDate = DateOnly.FromDateTime(DateTime.Today);
		if (driver.ExpiryDate < systemDate)
		{
			return "Cannot assign a driver with an expired license.";
		}

		var eligibleIds = (await GetEligibleDriversAsync(transit))
			.Select(d => d.DriverId)
			.ToHashSet();
		if (!eligibleIds.Contains(driverId))
		{
			return "This driver is not available for the scheduled trip window.";
		}

		var tripWindow = GetTripWindow(transit.Rental);
		if (tripWindow == null)
		{
			return "This trip has no pickup/return schedule, so a driver cannot be assigned yet.";
		}

		var conflictingRentalId = await FindOverlappingDriverAssignmentAsync(
			driverId,
			excludeTransitId: transit.TransitID,
			tripWindow.Value.Start,
			tripWindow.Value.End);

		if (conflictingRentalId.HasValue)
		{
			return $"This driver is already assigned to BK-{conflictingRentalId.Value:D5} during the same schedule.";
		}

		return null;
	}

	private async Task<IReadOnlyList<Driver>> GetEligibleDriversAsync(Transit transit)
	{
		var systemDate = DateOnly.FromDateTime(DateTime.Today);
		var driversQuery = _context.Drivers.AsNoTracking()
			.Where(d => d.IsActive && d.ExpiryDate >= systemDate);

		var eligibleDrivers = await driversQuery
			.OrderBy(d => d.Name)
			.ToListAsync();

		var tripWindow = GetTripWindow(transit.Rental);
		if (tripWindow == null)
		{
			return eligibleDrivers;
		}

		var busyDriverIds = await GetBusyDriverIdsAsync(
			tripWindow.Value.Start,
			tripWindow.Value.End,
			excludeTransitId: transit.TransitID);

		return eligibleDrivers
			.Where(d => d.DriverId == transit.DriverID || !busyDriverIds.Contains(d.DriverId))
			.ToList();
	}

	private async Task<TransitPreparationViewModel> BuildPreparationViewModelAsync(
		Transit transit,
		int? selectedDriverId = null)
	{
		var eligibleDrivers = await GetEligibleDriversAsync(transit);
		var driverIds = eligibleDrivers.Select(d => d.DriverId).ToList();

		var reviewRows = driverIds.Count == 0
			? new List<(int DriverId, int Professionalism, int Driving, int Courtesy)>()
			: (await _context.Feedbacks
				.AsNoTracking()
				.Where(f => f.Transit != null
					&& f.Transit.DriverID != null
					&& driverIds.Contains(f.Transit.DriverID.Value)
					&& f.DriverProfessionalism != null
					&& f.DriverDriving != null
					&& f.DriverCourtesy != null)
				.Select(f => new
				{
					DriverId = f.Transit!.DriverID!.Value,
					Professionalism = f.DriverProfessionalism!.Value,
					Driving = f.DriverDriving!.Value,
					Courtesy = f.DriverCourtesy!.Value
				})
				.ToListAsync())
				.Select(f => (f.DriverId, f.Professionalism, f.Driving, f.Courtesy))
				.ToList();

		var reviewGroups = reviewRows
			.GroupBy(r => r.DriverId)
			.ToDictionary(g => g.Key, g => g.ToList());

		var drivers = eligibleDrivers.Select(driver =>
		{
			reviewGroups.TryGetValue(driver.DriverId, out var reviews);
			reviews ??= new List<(int DriverId, int Professionalism, int Driving, int Courtesy)>();
			var overallStars = reviews
				.Select(r => RatingScale.OverallStars(r.Professionalism, r.Driving, r.Courtesy))
				.ToList();

			return new DriverSelectionItem
			{
				DriverId = driver.DriverId,
				Name = driver.Name,
				IsActive = driver.IsActive,
				ExpiryDate = driver.ExpiryDate,
				ImagePath = driver.ImagePath,
				Initials = GetDriverInitials(driver.Name),
				RatingTen = overallStars.Count == 0 ? 0d : RatingScale.ToTen(overallStars),
				ReviewCount = reviews.Count
			};
		}).ToList();

		return new TransitPreparationViewModel
		{
			Transit = transit,
			Drivers = drivers,
			SelectedDriverId = selectedDriverId ?? transit.DriverID
		};
	}

	private static string GetDriverInitials(string? name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return "?";
		}

		var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 1)
		{
			return parts[0][..1].ToUpperInvariant();
		}

		return (parts[0][0].ToString() + parts[^1][0]).ToUpperInvariant();
	}

	private async Task SaveTripPhotosAsync(Transit transit, IReadOnlyList<IFormFile> files, bool isPreTrip)
	{
		var paths = new List<string>();
		foreach (var file in files.Take(TransitTripPhotos.MaxPhotos))
		{
			paths.Add(await ImageStorage.SaveAsync(
				_webHostEnvironment,
				file,
				ImageStorage.TransitsFolder));
		}

		if (paths.Count == 0)
		{
			return;
		}

		var json = TransitTripPhotos.Serialize(paths);
		if (isPreTrip)
		{
			transit.PreTripImagePath = paths[0];
			transit.PreTripImagePathsJson = json;
		}
		else
		{
			transit.PostTripImagePath = paths[0];
			transit.PostTripImagePathsJson = json;
		}
	}

	private static void ApplyVehicleOdometer(Vehicle? vehicle, int reading)
	{
		if (vehicle != null && reading > vehicle.Odometer)
		{
			vehicle.Odometer = reading;
		}
	}

	/// <summary>
	/// Removes duplicate transit rows for the same booking.
	/// Keeps the most relevant transit per rental/vehicle pair, or one transit for single-vehicle bookings.
	/// </summary>
	private async Task ConsolidateDuplicateTransitsAsync()
	{
		var transits = await _context.Transits.ToListAsync();
		if (transits.Count == 0)
		{
			return;
		}

		static int TripStatusPriority(TripStatus status) => status switch
		{
			TripStatus.InTransit => 5,
			TripStatus.Scheduled => 4,
			TripStatus.Delayed => 3,
			TripStatus.Completed => 2,
			TripStatus.Cancelled => 1,
			_ => 0
		};

		var toRemove = new List<Transit>();

		foreach (var rentalGroup in transits.GroupBy(t => t.RentalID))
		{
			var groupTransits = rentalGroup.ToList();
			var distinctVehicleCount = groupTransits.Select(t => t.VehicleID).Distinct().Count();

			if (distinctVehicleCount <= 1 && groupTransits.Count > 1)
			{
				var keep = groupTransits
					.OrderByDescending(t => TripStatusPriority(t.TripStatus))
					.ThenBy(t => t.TransitID)
					.First();
				toRemove.AddRange(groupTransits.Where(t => t.TransitID != keep.TransitID));
				continue;
			}

			foreach (var vehicleGroup in groupTransits.GroupBy(t => t.VehicleID))
			{
				var vehicleTransits = vehicleGroup.ToList();
				if (vehicleTransits.Count <= 1)
				{
					continue;
				}

				var keep = vehicleTransits
					.OrderByDescending(t => TripStatusPriority(t.TripStatus))
					.ThenBy(t => t.TransitID)
					.First();
				toRemove.AddRange(vehicleTransits.Where(t => t.TransitID != keep.TransitID));
			}
		}

		if (toRemove.Count == 0)
		{
			return;
		}

		_context.Transits.RemoveRange(toRemove);
		await _context.SaveChangesAsync();
	}

	/// <summary>
	/// Creates transit rows for approved payments that do not have one yet (backfill).
	/// One transit per rental vehicle line.
	/// </summary>
	private async Task SyncMissingTransitsAsync()
	{
		var approvedRentalIds = await _context.Rentals
			.AsNoTracking()
			.Where(r => r.RentalStatus == RentalStatus.Approved
				&& _context.Payments.Any(p => p.RentalId == r.RentalId))
			.Select(r => r.RentalId)
			.ToListAsync();

		if (approvedRentalIds.Count == 0)
		{
			return;
		}

		var rentalVehicles = await _context.RentalVehicles
			.AsNoTracking()
			.Where(rv => approvedRentalIds.Contains(rv.RentalId))
			.ToListAsync();

		var existingPairs = await _context.Transits
			.AsNoTracking()
			.Where(t => approvedRentalIds.Contains(t.RentalID))
			.Select(t => new { t.RentalID, t.VehicleID })
			.ToListAsync();
		var existingKeys = existingPairs
			.Select(p => (p.RentalID, p.VehicleID))
			.ToHashSet();
		var transitCountByRental = existingPairs
			.GroupBy(p => p.RentalID)
			.ToDictionary(g => g.Key, g => g.Count());

		var vehicleLineCountByRental = rentalVehicles
			.GroupBy(rv => rv.RentalId)
			.ToDictionary(g => g.Key, g => g.Select(rv => rv.VehicleId).Distinct().Count());

		var added = 0;
		foreach (var line in rentalVehicles)
		{
			if (existingKeys.Contains((line.RentalId, line.VehicleId)))
			{
				continue;
			}

			var lineCount = vehicleLineCountByRental.GetValueOrDefault(line.RentalId, 1);
			var existingCount = transitCountByRental.GetValueOrDefault(line.RentalId, 0);
			if (lineCount <= 1 && existingCount > 0)
			{
				continue;
			}

			_context.Transits.Add(new Transit
			{
				RentalID = line.RentalId,
				VehicleID = line.VehicleId,
				RentalVehicleId = line.RentalVehicleId,
				TripStatus = TripStatus.Scheduled
			});
			existingKeys.Add((line.RentalId, line.VehicleId));
			transitCountByRental[line.RentalId] = existingCount + 1;
			added++;
		}

		if (added > 0)
		{
			await _context.SaveChangesAsync();
		}
	}
}
