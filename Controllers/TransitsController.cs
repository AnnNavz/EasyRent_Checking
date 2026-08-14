using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Models;
using EasyRent_Checking.Data;
using EasyRent_Checking.Services;

public class TransitsController : Controller
{
	private readonly EasyRent_CheckingContext _context;
	private readonly IWebHostEnvironment _webHostEnvironment;
	private readonly BookingEmailService _bookingEmailService;

	public TransitsController(
		EasyRent_CheckingContext context,
		IWebHostEnvironment webHostEnvironment,
		BookingEmailService bookingEmailService)
	{
		_context = context;
		_webHostEnvironment = webHostEnvironment;
		_bookingEmailService = bookingEmailService;
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
		var transit = await _context.Transits.FirstOrDefaultAsync(t => t.TransitID == transitid);
		if (transit == null)
		{
			return NotFound();
		}

		if (transit.TripStatus is TripStatus.Completed or TripStatus.Cancelled)
		{
			TempData["ErrorMessage"] = "Cannot assign a driver to a completed or cancelled trip.";
			return RedirectToAction(nameof(Details), new { transitid });
		}

		if (driverId <= 0 || !await _context.Drivers.AnyAsync(d => d.DriverId == driverId))
		{
			TempData["ErrorMessage"] = "Please select a valid driver.";
			return RedirectToAction(nameof(Details), new { transitid });
		}

		transit.DriverID = driverId;
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

		ViewData["Title"] = "Start Trip";
		return View(transit);
	}

	// POST: TRANSITS/StartTrip/5
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> StartTrip(
		int transitid,
		[Bind("TransitID,DepartureTime,FuelLevelStart,VehicleConditionStart,PreTripImageFile,Remarks")] Transit input)
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

		if (!ModelState.IsValid)
		{
			transit.DepartureTime = input.DepartureTime;
			transit.FuelLevelStart = input.FuelLevelStart;
			transit.VehicleConditionStart = input.VehicleConditionStart;
			transit.Remarks = input.Remarks;
			ViewData["Title"] = "Start Trip";
			return View(transit);
		}

		transit.DepartureTime = input.DepartureTime;
		transit.FuelLevelStart = input.FuelLevelStart;
		transit.VehicleConditionStart = input.VehicleConditionStart;
		transit.Remarks = input.Remarks;
		transit.TripStatus = TripStatus.InTransit;

		if (input.PreTripImageFile != null)
		{
			transit.PreTripImagePath = await ImageStorage.SaveAsync(
				_webHostEnvironment,
				input.PreTripImageFile,
				ImageStorage.TransitsFolder);
		}

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

		ViewData["Title"] = "Complete Trip";
		return View(transit);
	}

	// POST: TRANSITS/CompleteTrip/5
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> CompleteTrip(
		int transitid,
		[Bind("TransitID,ReturnTime,FuelLevelEnd,VehicleConditionEnd,PostTripImageFile,Remarks")] Transit input)
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

		if (!ModelState.IsValid)
		{
			transit.ReturnTime = input.ReturnTime;
			transit.FuelLevelEnd = input.FuelLevelEnd;
			transit.VehicleConditionEnd = input.VehicleConditionEnd;
			transit.Remarks = input.Remarks;
			ViewData["Title"] = "Complete Trip";
			return View(transit);
		}

		transit.ReturnTime = input.ReturnTime;
		transit.FuelLevelEnd = input.FuelLevelEnd;
		transit.VehicleConditionEnd = input.VehicleConditionEnd;
		if (!string.IsNullOrWhiteSpace(input.Remarks))
		{
			transit.Remarks = input.Remarks;
		}

		transit.TripStatus = TripStatus.Completed;

		if (input.PostTripImageFile != null)
		{
			transit.PostTripImagePath = await ImageStorage.SaveAsync(
				_webHostEnvironment,
				input.PostTripImageFile,
				ImageStorage.TransitsFolder);
		}

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
		ViewBag.DriverList = new SelectList(
			await _context.Drivers.AsNoTracking().OrderBy(d => d.Name).ToListAsync(),
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

	private static bool CanStartTrip(Transit transit)
		=> transit.DriverID != null
			&& transit.TripStatus is TripStatus.Scheduled or TripStatus.Delayed;

	/// <summary>
	/// Creates transit rows for approved payments that do not have one yet (backfill).
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

		var existingRentalIds = await _context.Transits
			.AsNoTracking()
			.Where(t => approvedRentalIds.Contains(t.RentalID))
			.Select(t => t.RentalID)
			.ToListAsync();

		var missingIds = approvedRentalIds.Except(existingRentalIds).ToList();
		if (missingIds.Count == 0)
		{
			return;
		}

		var detailsList = await _context.RentalDetails
			.AsNoTracking()
			.Where(d => missingIds.Contains(d.RentalID))
			.ToListAsync();

		foreach (var details in detailsList)
		{
			_context.Transits.Add(new Transit
			{
				RentalID = details.RentalID,
				VehicleID = details.VehicleId,
				TripStatus = TripStatus.Scheduled
			});
		}

		if (detailsList.Count > 0)
		{
			await _context.SaveChangesAsync();
		}
	}
}
