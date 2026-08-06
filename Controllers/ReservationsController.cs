using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Models;
using EasyRent_Checking.Data;
using EasyRent_Checking.Services;

namespace EasyRent_Checking.Controllers
{
	public class ReservationsController : Controller
	{
		private readonly EasyRent_CheckingContext _context;
		private readonly IWebHostEnvironment _webHostEnvironment;

		public ReservationsController(EasyRent_CheckingContext context, IWebHostEnvironment webHostEnvironment)
		{
			_context = context;
			_webHostEnvironment = webHostEnvironment;
		}

		// GET: Reservations
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

			var reservationsQuery = _context.Reservation
				.Include(r => r.Details)!
					.ThenInclude(d => d!.Vehicle)
				.AsQueryable();

			var today = DateOnly.FromDateTime(DateTime.Today);

			ViewData["TotalBookingsCount"] = await reservationsQuery.CountAsync();
			ViewData["PendingApprovalsCount"] = await reservationsQuery.CountAsync(r => r.ReservationStatus == ReservationStatus.Pending);
			ViewData["ActiveTripsCount"] = await reservationsQuery.CountAsync(r =>
				r.ReservationStatus == ReservationStatus.Approved
				&& r.Details != null
				&& r.Details.PickupDate <= today
				&& r.Details.ReturnDate >= today);

			if (!string.IsNullOrEmpty(searchString))
			{
				var term = searchString.Trim();
				var idToken = term.StartsWith("BK-", StringComparison.OrdinalIgnoreCase)
					? term[3..]
					: term;
				var hasBookingId = int.TryParse(idToken, out var bookingId);

				reservationsQuery = reservationsQuery.Where(r =>
					r.CustomerName.Contains(term)
					|| r.ContactNumber.Contains(term)
					|| (r.Details != null && r.Details.Vehicle != null && (
						r.Details.Vehicle.Brand.Contains(term)
						|| r.Details.Vehicle.Model.Contains(term)
						|| r.Details.Vehicle.PlateNumber.Contains(term)))
					|| (hasBookingId && r.ReservationId == bookingId));
			}

			if (!string.IsNullOrEmpty(currentFilter) && Enum.TryParse(currentFilter, true, out ReservationStatus filterStatus))
			{
				reservationsQuery = reservationsQuery.Where(r => r.ReservationStatus == filterStatus);
			}

			reservationsQuery = sortBy switch
			{
				"CustomerName" => reservationsQuery.OrderBy(r => r.CustomerName),
				"PickupDate" => reservationsQuery.OrderBy(r => r.Details!.PickupDate).ThenBy(r => r.Details!.PickupTime),
				"Status" => reservationsQuery.OrderBy(r => r.ReservationStatus),
				"Vehicle" => reservationsQuery.OrderBy(r => r.Details!.Vehicle!.Brand).ThenBy(r => r.Details!.Vehicle!.Model),
				_ => reservationsQuery.OrderByDescending(r => r.ReservationId)
			};

			var totalCount = await reservationsQuery.CountAsync();
			var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
			if (pageNumber > totalPages)
			{
				pageNumber = totalPages;
			}

			ViewData["PageIndex"] = pageNumber;
			ViewData["TotalPages"] = totalPages;
			ViewData["TotalCount"] = totalCount;
			ViewData["PageSize"] = pageSize;

			var reservations = await reservationsQuery
				.Skip((pageNumber - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();

			return View(reservations);
		}

		// GET: Reservations/Details/5
		public async Task<IActionResult> Details(int? id)
		{
			if (id == null)
			{
				return NotFound();
			}

			var reservation = await _context.Reservation
				.Include(r => r.Details)!
					.ThenInclude(d => d!.Vehicle)
				.FirstOrDefaultAsync(m => m.ReservationId == id);
			if (reservation == null)
			{
				return NotFound();
			}

			var latestPayment = await _context.Payment
				.AsNoTracking()
				.Where(p => p.ReservationId == reservation.ReservationId)
				.OrderByDescending(p => p.PaymentDate)
				.FirstOrDefaultAsync();

			var transit = await _context.Transit
				.AsNoTracking()
				.Include(t => t.Driver)
				.FirstOrDefaultAsync(t => t.ReservationID == reservation.ReservationId);

			ViewData["LatestPayment"] = latestPayment;
			ViewData["Transit"] = transit;
			return View(reservation);
		}

		// POST: Reservations/Approve/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Approve(int id)
		{
			var reservation = await _context.Reservation.FindAsync(id);
			if (reservation == null)
			{
				return NotFound();
			}

			reservation.ReservationStatus = ReservationStatus.Approved;
			await _context.SaveChangesAsync();
			TempData["ShowPaymentSection"] = true;
			TempData["SuccessMessage"] = "Reservation approved. You can now add a payment.";
			return RedirectToAction(nameof(Details), new { id });
		}

		// POST: Reservations/Reject/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Reject(int id)
		{
			var reservation = await _context.Reservation.FindAsync(id);
			if (reservation == null)
			{
				return NotFound();
			}

			reservation.ReservationStatus = ReservationStatus.Cancelled;
			await _context.SaveChangesAsync();
			TempData["SuccessMessage"] = "Reservation rejected and marked as cancelled.";
			return RedirectToAction(nameof(Details), new { id });
		}

		// GET: Reservations/Create
		public async Task<IActionResult> Create(int? vehicleId)
		{
			await PopulateVehicleListAsync(vehicleId);
			var model = new ReservationInputModel
			{
				PickupTime = new TimeOnly(9, 0),
				ReturnTime = new TimeOnly(17, 0),
				PassengerCount = 1,
				Discount = Discount.No,
				ReservationStatus = ReservationStatus.Approved
			};
			if (vehicleId.HasValue && await _context.Vehicle.AnyAsync(v => v.VehicleId == vehicleId.Value))
			{
				model.VehicleId = vehicleId.Value;
			}

			ViewData["InitialUnavailableDates"] = await GetUnavailableDatesAsync(model.VehicleId);
			return View(model);
		}

		// POST: Reservations/Create
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create([Bind("ReservationId,ReservationDetailsID,VehicleId,CustomerName,ContactNumber,PickupLocation,DropoffLocation,PickupDate,ReturnDate,PickupTime,ReturnTime,PassengerCount,Notes,Discount,DiscountImagePath,DiscountImageFile,ReservationStatus")] ReservationInputModel model)
		{
			await ValidateReservationInputAsync(model, excludeReservationId: null);

			if (ModelState.IsValid)
			{
				await SaveDiscountImageAsync(model);

				var reservation = new Reservation();
				var details = new ReservationDetails();
				// Admin walk-in bookings are approved immediately.
				model.ReservationStatus = ReservationStatus.Approved;
				model.ApplyTo(reservation, details);

				_context.Reservation.Add(reservation);
				await _context.SaveChangesAsync();

				details.ReservationID = reservation.ReservationId;
				_context.ReservationDetails.Add(details);
				await _context.SaveChangesAsync();

				return RedirectToAction(nameof(Index));
			}

			await PopulateVehicleListAsync(model.VehicleId);
			ViewData["InitialUnavailableDates"] = await GetUnavailableDatesAsync(model.VehicleId);
			return View(model);
		}

		// GET: Reservations/Edit/5
		public async Task<IActionResult> Edit(int? id)
		{
			if (id == null)
			{
				return NotFound();
			}

			var reservation = await _context.Reservation
				.Include(r => r.Details)
				.FirstOrDefaultAsync(r => r.ReservationId == id);
			if (reservation == null)
			{
				return NotFound();
			}

			var model = ReservationInputModel.FromEntities(reservation);
			await PopulateVehicleListAsync(model.VehicleId);
			return View(model);
		}

		// POST: Reservations/Edit/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(int id, [Bind("ReservationId,ReservationDetailsID,VehicleId,CustomerName,ContactNumber,PickupLocation,DropoffLocation,PickupDate,ReturnDate,PickupTime,ReturnTime,PassengerCount,Notes,Discount,DiscountImagePath,DiscountImageFile,ReservationStatus")] ReservationInputModel model)
		{
			if (id != model.ReservationId)
			{
				return NotFound();
			}

			await ValidateReservationInputAsync(model, excludeReservationId: id);

			if (ModelState.IsValid)
			{
				try
				{
					var reservation = await _context.Reservation
						.Include(r => r.Details)
						.FirstOrDefaultAsync(r => r.ReservationId == id);
					if (reservation == null)
					{
						return NotFound();
					}

					var details = reservation.Details;
					if (details == null)
					{
						details = new ReservationDetails { ReservationID = reservation.ReservationId };
						_context.ReservationDetails.Add(details);
					}

					await SaveDiscountImageAsync(model);
					model.ApplyTo(reservation, details);
					await _context.SaveChangesAsync();
				}
				catch (DbUpdateConcurrencyException)
				{
					if (!ReservationExists(model.ReservationId))
					{
						return NotFound();
					}
					throw;
				}
				return RedirectToAction(nameof(Index));
			}

			await PopulateVehicleListAsync(model.VehicleId);
			return View(model);
		}

		// GET: Reservations/Delete/5
		public async Task<IActionResult> Delete(int? id)
		{
			if (id == null)
			{
				return NotFound();
			}

			var reservation = await _context.Reservation
				.Include(r => r.Details)!
					.ThenInclude(d => d!.Vehicle)
				.FirstOrDefaultAsync(m => m.ReservationId == id);
			if (reservation == null)
			{
				return NotFound();
			}

			return View(reservation);
		}

		// POST: Reservations/Delete/5
		[HttpPost, ActionName("Delete")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteConfirmed(int id)
		{
			var reservation = await _context.Reservation
				.Include(r => r.Details)
				.FirstOrDefaultAsync(r => r.ReservationId == id);
			if (reservation != null)
			{
				_context.Reservation.Remove(reservation);
			}

			await _context.SaveChangesAsync();
			return RedirectToAction(nameof(Index));
		}

		// GET: Reservations/Availability?vehicleId=1
		[HttpGet]
		public async Task<IActionResult> Availability(int vehicleId)
		{
			var unavailableDates = await GetUnavailableDatesAsync(vehicleId);
			return Json(new { unavailableDates });
		}

		private async Task<List<string>> GetUnavailableDatesAsync(int vehicleId)
		{
			if (vehicleId <= 0)
			{
				return new List<string>();
			}

			// Join explicitly so status filtering stays reliable after ReservationDetails split.
			var ranges = await (
				from details in _context.ReservationDetails.AsNoTracking()
				join reservation in _context.Reservation.AsNoTracking()
					on details.ReservationID equals reservation.ReservationId
				where details.VehicleId == vehicleId
					&& reservation.ReservationStatus != ReservationStatus.Cancelled
				select new { details.PickupDate, details.ReturnDate }
			).ToListAsync();

			var unavailableDates = new List<string>();
			foreach (var range in ranges)
			{
				for (var day = range.PickupDate; day <= range.ReturnDate; day = day.AddDays(1))
				{
					unavailableDates.Add(day.ToString("yyyy-MM-dd"));
				}
			}

			return unavailableDates.Distinct().OrderBy(d => d).ToList();
		}

		private async Task ValidateReservationInputAsync(ReservationInputModel model, int? excludeReservationId)
		{
			var today = DateOnly.FromDateTime(DateTime.Today);
			if (model.PickupDate < today)
			{
				ModelState.AddModelError(nameof(model.PickupDate), "Please select a pick-up date on the calendar.");
			}

			if (model.ReturnDate < model.PickupDate)
			{
				ModelState.AddModelError(nameof(model.ReturnDate), "Return date cannot be earlier than pick-up date.");
			}

			if (model.VehicleId > 0
				&& model.PickupDate >= today
				&& model.ReturnDate >= model.PickupDate)
			{
				var conflictQuery =
					from details in _context.ReservationDetails
					join reservation in _context.Reservation
						on details.ReservationID equals reservation.ReservationId
					where details.VehicleId == model.VehicleId
						&& reservation.ReservationStatus != ReservationStatus.Cancelled
						&& details.PickupDate <= model.ReturnDate
						&& details.ReturnDate >= model.PickupDate
					select details;

				if (excludeReservationId.HasValue)
				{
					conflictQuery = conflictQuery.Where(d => d.ReservationID != excludeReservationId.Value);
				}

				if (await conflictQuery.AnyAsync())
				{
					ModelState.AddModelError(nameof(model.PickupDate), "Selected dates overlap an existing rental for this vehicle.");
				}
			}

			if (model.Discount == Discount.Yes && model.DiscountImageFile == null && string.IsNullOrEmpty(model.DiscountImagePath))
			{
				ModelState.AddModelError(nameof(model.DiscountImageFile), "Please upload a Senior/PWD ID image.");
			}
		}

		private async Task SaveDiscountImageAsync(ReservationInputModel model)
		{
			if (model.DiscountImageFile != null)
			{
				model.DiscountImagePath = await ImageStorage.SaveAsync(
					_webHostEnvironment,
					model.DiscountImageFile,
					ImageStorage.ReservationsFolder);
			}

			if (model.Discount == Discount.No)
			{
				model.DiscountImagePath = null;
			}
		}

		private bool ReservationExists(int id)
		{
			return _context.Reservation.Any(e => e.ReservationId == id);
		}

		private async Task PopulateVehicleListAsync(int? selectedVehicleId = null)
		{
			var vehicles = await _context.Vehicle
				.OrderBy(v => v.Brand)
				.ThenBy(v => v.Model)
				.ToListAsync();

			ViewBag.Vehicles = vehicles;
			ViewData["VehicleId"] = new SelectList(
				vehicles.Select(v => new
				{
					v.VehicleId,
					Label = $"{v.Brand} {v.Model} ({v.PlateNumber})"
				}),
				"VehicleId",
				"Label",
				selectedVehicleId);
		}
	}
}
