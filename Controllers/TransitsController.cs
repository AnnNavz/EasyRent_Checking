using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Models;
using EasyRent_Checking.Data;
using EasyRent_Checking.Services;

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
			.Include(t => t.Rental)
				.ThenInclude(r => r!.Customer)
			.AsQueryable();

		ViewData["TotalBookingsCount"] = await transitsQuery.CountAsync();
		ViewData["ApprovedCount"] = await transitsQuery.CountAsync(t =>
			t.Rental != null && t.Rental.RentalStatus == RentalStatus.Approved);
		ViewData["CompletedCount"] = await transitsQuery.CountAsync(t => t.TripStatus == TripStatus.Completed);
		ViewData["OngoingCount"] = await transitsQuery.CountAsync(t => t.TripStatus == TripStatus.InTransit);

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

		transitsQuery = sortBy switch
		{
			"CustomerName" => transitsQuery.OrderBy(t => t.Rental!.CustomerName),
			"TripStatus" => transitsQuery.OrderBy(t => t.TripStatus),
			"BookingId" => transitsQuery.OrderBy(t => t.RentalID),
			_ => transitsQuery.OrderByDescending(t => t.TransitID)
		};

		var totalCount = await transitsQuery.CountAsync();
		var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
		if (pageNumber > totalPages)
		{
			pageNumber = totalPages;
		}

		ViewData["PageIndex"] = pageNumber;
		ViewData["TotalPages"] = totalPages;
		ViewData["TotalCount"] = totalCount;
		ViewData["PageSize"] = pageSize;

		var transits = await transitsQuery
			.Skip((pageNumber - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync();

		var reservationIds = transits.Select(t => t.RentalID).Distinct().ToList();
		var payments = await _context.Payments
			.AsNoTracking()
			.Where(p => reservationIds.Contains(p.RentalId))
			.OrderByDescending(p => p.PaymentId)
			.ToListAsync();

		ViewBag.PaymentsByRentalId = payments
			.GroupBy(p => p.RentalId)
			.ToDictionary(g => g.Key, g => g.First());

		return View(transits);
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

		await PopulateDetailsContextAsync(transit);
		return View(transit);
	}

	// POST: TRANSITS/AssignDriver/5
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> AssignDriver(int transitid, int driverId)
	{
		var transit = await _context.Transits
			.Include(t => t.Rental)!
				.ThenInclude(r => r!.Details)
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

		var tripWindow = GetTripWindow(transit.Rental?.Details);
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
	public async Task<IActionResult> StartTrip(int? transitid)
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

		if (!CanStartTrip(transit))
		{
			TempData["ErrorMessage"] = transit.DriverID == null
				? "Assign a driver before starting the trip."
				: "This trip cannot be started in its current status.";
			return RedirectToAction(nameof(Details), new { transitid });
		}

		if (transit.DepartureTime == null && transit.Rental?.Details != null)
		{
			transit.DepartureTime = transit.Rental.Details.PickupTime;
		}

		transit.OdometerStart ??= transit.Vehicle?.Odometer;

		ViewData["Title"] = "Start Trip";
		return View(transit);
	}

	// POST: TRANSITS/StartTrip/5
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> StartTrip(
		int transitid,
		[Bind("TransitID,DepartureTime,FuelLevelStart,OdometerStart,VehicleConditionStart,PreTripImageFile")] Transit input)
	{
		var transit = await LoadTransitAsync(transitid);
		if (transit == null)
		{
			return NotFound();
		}

		if (!CanStartTrip(transit))
		{
			TempData["ErrorMessage"] = "This trip cannot be started in its current status.";
			return RedirectToAction(nameof(Details), new { transitid });
		}

		ModelState.Clear();
		if (input.DepartureTime == null)
		{
			ModelState.AddModelError(nameof(input.DepartureTime), "Departure time is required.");
		}

		if (input.FuelLevelStart == null)
		{
			ModelState.AddModelError(nameof(input.FuelLevelStart), "Starting fuel level is required.");
		}

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

		if (!ModelState.IsValid)
		{
			transit.DepartureTime = input.DepartureTime;
			transit.FuelLevelStart = input.FuelLevelStart;
			transit.OdometerStart = input.OdometerStart;
			transit.VehicleConditionStart = input.VehicleConditionStart;
			ViewData["Title"] = "Start Trip";
			return View(transit);
		}

		transit.DepartureTime = input.DepartureTime;
		transit.FuelLevelStart = input.FuelLevelStart;
		transit.OdometerStart = input.OdometerStart;
		transit.VehicleConditionStart = input.VehicleConditionStart;
		transit.TripStatus = TripStatus.InTransit;
		ApplyVehicleOdometer(transit.Vehicle, input.OdometerStart.Value);

		if (input.PreTripImageFile != null)
		{
			transit.PreTripImagePath = await ImageStorage.SaveAsync(
				_webHostEnvironment,
				input.PreTripImageFile,
				ImageStorage.TransitsFolder);
		}

		_logs.Record(
			SystemLogAction.Started,
			SystemLogCategory.Trip,
			$"Started trip for BK-{transit.RentalID:D5}.",
			"Transit",
			transit.TransitID);
		await _context.SaveChangesAsync();

		if (transit.Rental != null)
		{
			await _bookingEmailService.SendTripStartedAsync(transit.Rental, transit.Rental.Details);
		}

		TempData["SuccessMessage"] = "Trip started.";
		return RedirectToAction(nameof(Details), new { transitid });
	}

	// GET: TRANSITS/CompleteTrip/5
	public async Task<IActionResult> CompleteTrip(int? transitid)
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

		if (transit.TripStatus != TripStatus.InTransit)
		{
			TempData["ErrorMessage"] = "Only in-transit trips can be completed.";
			return RedirectToAction(nameof(Details), new { transitid });
		}

		if (transit.ReturnTime == null && transit.Rental?.Details != null)
		{
			transit.ReturnTime = transit.Rental.Details.ReturnTime;
		}

		transit.OdometerEnd ??= transit.OdometerStart ?? transit.Vehicle?.Odometer;

		ViewData["Title"] = "Complete Trip";
		return View(transit);
	}

	// POST: TRANSITS/CompleteTrip/5
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> CompleteTrip(
		int transitid,
		[Bind("TransitID,ReturnTime,FuelLevelEnd,OdometerEnd,VehicleConditionEnd,PostTripImageFile")] Transit input)
	{
		var transit = await LoadTransitAsync(transitid);
		if (transit == null)
		{
			return NotFound();
		}

		if (transit.TripStatus != TripStatus.InTransit)
		{
			TempData["ErrorMessage"] = "Only in-transit trips can be completed.";
			return RedirectToAction(nameof(Details), new { transitid });
		}

		ModelState.Clear();
		if (input.ReturnTime == null)
		{
			ModelState.AddModelError(nameof(input.ReturnTime), "Return time is required.");
		}

		if (input.FuelLevelEnd == null)
		{
			ModelState.AddModelError(nameof(input.FuelLevelEnd), "Ending fuel level is required.");
		}

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

		if (!ModelState.IsValid)
		{
			transit.ReturnTime = input.ReturnTime;
			transit.FuelLevelEnd = input.FuelLevelEnd;
			transit.OdometerEnd = input.OdometerEnd;
			transit.VehicleConditionEnd = input.VehicleConditionEnd;
			ViewData["Title"] = "Complete Trip";
			return View(transit);
		}

		transit.ReturnTime = input.ReturnTime;
		transit.FuelLevelEnd = input.FuelLevelEnd;
		transit.OdometerEnd = input.OdometerEnd;
		transit.VehicleConditionEnd = input.VehicleConditionEnd;

		transit.TripStatus = TripStatus.Completed;
		ApplyVehicleOdometer(transit.Vehicle, input.OdometerEnd.Value);

		if (input.PostTripImageFile != null)
		{
			transit.PostTripImagePath = await ImageStorage.SaveAsync(
				_webHostEnvironment,
				input.PostTripImageFile,
				ImageStorage.TransitsFolder);
		}

		_logs.Record(
			SystemLogAction.Completed,
			SystemLogCategory.Trip,
			$"Completed trip for BK-{transit.RentalID:D5}.",
			"Transit",
			transit.TransitID);
		await _context.SaveChangesAsync();
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
				.ThenInclude(r => r!.Details)!
					.ThenInclude(d => d!.Vehicle)
			.Include(t => t.Driver)
			.Include(t => t.Vehicle)
			.FirstOrDefaultAsync(t => t.TransitID == transitId);
	}

	private async Task PopulateDetailsContextAsync(Transit transit)
	{
		var systemDate = DateOnly.FromDateTime(DateTime.Today);
		var tripWindow = GetTripWindow(transit.Rental?.Details);

		var driversQuery = _context.Drivers.AsNoTracking()
			.Where(d => d.IsActive && d.ExpiryDate >= systemDate);

		var eligibleDrivers = await driversQuery
			.OrderBy(d => d.Name)
			.ToListAsync();

		if (tripWindow != null)
		{
			var busyDriverIds = await GetBusyDriverIdsAsync(
				tripWindow.Value.Start,
				tripWindow.Value.End,
				excludeTransitId: transit.TransitID);

			eligibleDrivers = eligibleDrivers
				.Where(d => d.DriverId == transit.DriverID || !busyDriverIds.Contains(d.DriverId))
				.ToList();
		}

		ViewBag.DriverList = new SelectList(
			eligibleDrivers,
			nameof(Driver.DriverId),
			nameof(Driver.Name),
			transit.DriverID);

		var payment = await _context.Payments
			.AsNoTracking()
			.Where(p => p.RentalId == transit.RentalID)
			.OrderByDescending(p => p.PaymentId)
			.FirstOrDefaultAsync();

		ViewBag.LatestPayment = payment;
		ViewBag.CanAssignDriver = transit.TripStatus is TripStatus.Scheduled or TripStatus.Delayed;
		ViewBag.CanStartTrip = CanStartTrip(transit);
		ViewBag.CanCompleteTrip = transit.TripStatus == TripStatus.InTransit;
	}

	private static (DateTime Start, DateTime End)? GetTripWindow(RentalDetails? details)
	{
		if (details == null)
		{
			return null;
		}

		var start = details.PickupDate.ToDateTime(details.PickupTime);
		var end = details.ReturnDate.ToDateTime(details.ReturnTime);
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
				PickupDate = t.Rental!.Details!.PickupDate,
				PickupTime = t.Rental.Details.PickupTime,
				ReturnDate = t.Rental.Details.ReturnDate,
				ReturnTime = t.Rental.Details.ReturnTime
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
				PickupDate = t.Rental!.Details!.PickupDate,
				PickupTime = t.Rental.Details.PickupTime,
				ReturnDate = t.Rental.Details.ReturnDate,
				ReturnTime = t.Rental.Details.ReturnTime
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

	private static bool CanStartTrip(Transit transit)
		=> transit.DriverID != null
			&& transit.TripStatus is TripStatus.Scheduled or TripStatus.Delayed;

	private static void ApplyVehicleOdometer(Vehicle? vehicle, int reading)
	{
		if (vehicle != null && reading > vehicle.Odometer)
		{
			vehicle.Odometer = reading;
		}
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

		var added = 0;
		foreach (var line in rentalVehicles)
		{
			if (existingKeys.Contains((line.RentalId, line.VehicleId)))
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
			added++;
		}

		// Legacy rentals without RentalVehicle rows yet.
		var rentalsWithLines = rentalVehicles.Select(rv => rv.RentalId).ToHashSet();
		var legacyDetails = await _context.RentalDetails
			.AsNoTracking()
			.Where(d => approvedRentalIds.Contains(d.RentalID) && !rentalsWithLines.Contains(d.RentalID))
			.ToListAsync();

		foreach (var details in legacyDetails)
		{
			if (existingKeys.Contains((details.RentalID, details.VehicleId)))
			{
				continue;
			}

			_context.Transits.Add(new Transit
			{
				RentalID = details.RentalID,
				VehicleID = details.VehicleId,
				TripStatus = TripStatus.Scheduled
			});
			added++;
		}

		if (added > 0)
		{
			await _context.SaveChangesAsync();
		}
	}
}
