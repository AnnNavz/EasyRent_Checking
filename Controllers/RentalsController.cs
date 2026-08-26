using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Models;
using EasyRent_Checking.ViewModels;
using EasyRent_Checking.Data;
using EasyRent_Checking.Services;

namespace EasyRent_Checking.Controllers
{
	[Authorize(Policy = "StaffArea")]
	[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
	public class RentalsController : Controller
	{
		private readonly EasyRent_CheckingContext _context;
		private readonly IWebHostEnvironment _webHostEnvironment;
		private readonly BookingEmailService _bookingEmailService;
		private readonly SystemLogService _logs;

		public RentalsController(
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

		// GET: Rentals
		public async Task<IActionResult> Index(string searchString, string sortBy, string currentFilter, int? page)
		{
			await RentalExpiry.ExpireOverdueReservesAsync(_context);

			const int pageSize = 10;
			var pageNumber = page.GetValueOrDefault(1);
			if (pageNumber < 1)
			{
				pageNumber = 1;
			}

			ViewData["CurrentSearch"] = searchString;
			ViewData["CurrentSort"] = sortBy;
			ViewData["CurrentFilter"] = currentFilter;

			var rentalsQuery = _context.Rentals
				.Include(r => r.Details)!
					.ThenInclude(d => d!.Vehicle)
				.AsQueryable();

			var today = DateOnly.FromDateTime(DateTime.Today);
			var thisMonthStart = new DateOnly(today.Year, today.Month, 1);
			var lastMonthStart = thisMonthStart.AddMonths(-1);

			var totalBookingsCount = await rentalsQuery.CountAsync();
			var pendingApprovalsCount = await rentalsQuery.CountAsync(r => r.RentalStatus == RentalStatus.Pending);

			var totalThisMonth = await rentalsQuery.CountAsync(r =>
				r.Details != null && r.Details.PickupDate >= thisMonthStart);
			var totalLastMonth = await rentalsQuery.CountAsync(r =>
				r.Details != null
				&& r.Details.PickupDate >= lastMonthStart
				&& r.Details.PickupDate < thisMonthStart);
			var pendingThisMonth = await rentalsQuery.CountAsync(r =>
				r.RentalStatus == RentalStatus.Pending
				&& r.Details != null
				&& r.Details.PickupDate >= thisMonthStart);
			var pendingLastMonth = await rentalsQuery.CountAsync(r =>
				r.RentalStatus == RentalStatus.Pending
				&& r.Details != null
				&& r.Details.PickupDate >= lastMonthStart
				&& r.Details.PickupDate < thisMonthStart);

			ViewData["TotalBookingsCount"] = totalBookingsCount;
			ViewData["PendingApprovalsCount"] = pendingApprovalsCount;
			ViewData["TotalBookingsChange"] = PctChange(totalThisMonth, totalLastMonth);
			ViewData["PendingApprovalsChange"] = PctChange(pendingThisMonth, pendingLastMonth);

			if (!string.IsNullOrEmpty(searchString))
			{
				var term = searchString.Trim();
				var idToken = term.StartsWith("BK-", StringComparison.OrdinalIgnoreCase)
					? term[3..]
					: term;
				var hasBookingId = int.TryParse(idToken, out var bookingId);

				rentalsQuery = rentalsQuery.Where(r =>
					r.CustomerName.Contains(term)
					|| r.ContactNumber.Contains(term)
					|| (r.Details != null && r.Details.Vehicle != null && (
						r.Details.Vehicle.Brand.Contains(term)
						|| r.Details.Vehicle.Model.Contains(term)
						|| r.Details.Vehicle.PlateNumber.Contains(term)))
					|| (hasBookingId && r.RentalId == bookingId));
			}

			if (!string.IsNullOrEmpty(currentFilter) && Enum.TryParse(currentFilter, true, out RentalStatus filterStatus))
			{
				rentalsQuery = rentalsQuery.Where(r => r.RentalStatus == filterStatus);
			}

			rentalsQuery = sortBy switch
			{
				"CustomerName" => rentalsQuery.OrderBy(r => r.CustomerName),
				"PickupDate" => rentalsQuery.OrderBy(r => r.Details!.PickupDate).ThenBy(r => r.Details!.PickupTime),
				"Status" => rentalsQuery.OrderBy(r => r.RentalStatus),
				"Vehicle" => rentalsQuery.OrderBy(r => r.Details!.Vehicle!.Brand).ThenBy(r => r.Details!.Vehicle!.Model),
				_ => rentalsQuery.OrderByDescending(r => r.RentalId)
			};

			var totalCount = await rentalsQuery.CountAsync();
			var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
			if (pageNumber > totalPages)
			{
				pageNumber = totalPages;
			}

			ViewData["PageIndex"] = pageNumber;
			ViewData["TotalPages"] = totalPages;
			ViewData["TotalCount"] = totalCount;
			ViewData["PageSize"] = pageSize;

			var rentals = await rentalsQuery
				.Skip((pageNumber - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();

			return View(rentals);
		}

		// GET: Rentals/Details/5
		public async Task<IActionResult> Details(int? id)
		{
			if (id == null)
			{
				return NotFound();
			}

			var rental = await _context.Rentals
				.Include(r => r.Details)!
					.ThenInclude(d => d!.Vehicle)
				.FirstOrDefaultAsync(m => m.RentalId == id);
			if (rental == null)
			{
				return NotFound();
			}

			var latestPayment = await _context.Payments
				.AsNoTracking()
				.Where(p => p.RentalId == rental.RentalId)
				.OrderByDescending(p => p.PaymentDate)
				.FirstOrDefaultAsync();

			var transit = await _context.Transits
				.AsNoTracking()
				.Include(t => t.Driver)
				.FirstOrDefaultAsync(t => t.RentalID == rental.RentalId);

			ViewData["LatestPayment"] = latestPayment;
			ViewData["Transit"] = transit;
			return View(rental);
		}

		// POST: Rentals/Approve/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Approve(int id)
		{
			var rental = await _context.Rentals
				.Include(r => r.Details)!
					.ThenInclude(d => d!.Vehicle)
				.FirstOrDefaultAsync(r => r.RentalId == id);
			if (rental == null)
			{
				return NotFound();
			}

			if (rental.RentalStatus == RentalStatus.Expired)
			{
				TempData["SuccessMessage"] = "This reservation already expired and cannot be approved.";
				return RedirectToAction(nameof(Details), new { id });
			}

			var hasPayment = await _context.Payments.AnyAsync(p => p.RentalId == id);
			if (rental.RentalOption == RentalOption.Reserve && !hasPayment)
			{
				TempData["SuccessMessage"] = "This reserve still has no payment. Ask the customer to pay before approving.";
				return RedirectToAction(nameof(Details), new { id });
			}

			rental.RentalStatus = RentalStatus.Approved;
			_logs.Record(
				SystemLogAction.Approved,
				SystemLogCategory.Booking,
				$"Approved booking BK-{rental.RentalId:D5} for {rental.CustomerName}.",
				"Rental",
				rental.RentalId);
			await _context.SaveChangesAsync();
			await EnsureTransitForRentalAsync(id);
			await _bookingEmailService.SendRentalConfirmedAsync(rental, rental.Details);

			var transit = await _context.Transits
				.AsNoTracking()
				.FirstOrDefaultAsync(t => t.RentalID == id);

			TempData["SuccessMessage"] = "Booking confirmed. Confirmation email with receipt PDF was sent to the customer (if email is on file).";
			if (transit != null)
			{
				return RedirectToAction("Details", "Transits", new { transitid = transit.TransitID });
			}

			return RedirectToAction(nameof(Details), new { id });
		}

		// POST: Rentals/Reject/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Reject(int id)
		{
			var rental = await _context.Rentals.FindAsync(id);
			if (rental == null)
			{
				return NotFound();
			}

			rental.RentalStatus = RentalStatus.Cancelled;
			rental.CancelledAt = DateTime.Now;
			rental.CancellationFee = 0m;

			var transit = await _context.Transits.FirstOrDefaultAsync(t => t.RentalID == id);
			if (transit != null && transit.TripStatus is not TripStatus.Completed)
			{
				transit.TripStatus = TripStatus.Cancelled;
			}

			_logs.Record(
				SystemLogAction.Rejected,
				SystemLogCategory.Booking,
				$"Rejected booking BK-{rental.RentalId:D5} for {rental.CustomerName}.",
				"Rental",
				rental.RentalId);
			await _context.SaveChangesAsync();
			TempData["SuccessMessage"] = "Booking rejected and marked as cancelled.";
			return RedirectToAction(nameof(Details), new { id });
		}

		private async Task EnsureTransitForRentalAsync(int rentalId)
		{
			if (await _context.Transits.AnyAsync(t => t.RentalID == rentalId))
			{
				return;
			}

			var details = await _context.RentalDetails
				.AsNoTracking()
				.FirstOrDefaultAsync(d => d.RentalID == rentalId);
			if (details == null)
			{
				return;
			}

			_context.Transits.Add(new Transit
			{
				RentalID = rentalId,
				VehicleID = details.VehicleId,
				TripStatus = TripStatus.Scheduled
			});
			await _context.SaveChangesAsync();
		}

		// GET: Rentals/Create
		public async Task<IActionResult> Create(int? vehicleId)
		{
			await PopulateVehicleListAsync(vehicleId);
			var model = new RentalInputModel
			{
				PickupTime = new TimeOnly(9, 0),
				ReturnTime = new TimeOnly(17, 0),
				PassengerCount = 1,
				Discount = Discount.No,
				RentalStatus = RentalStatus.Approved,
				RentalOption = RentalOption.Book,
				PaymentDate = DateTime.Now,
				PaymentType = "Full Payment"
			};
			if (vehicleId.HasValue && await _context.Vehicles.AnyAsync(v => v.VehicleId == vehicleId.Value))
			{
				model.VehicleId = vehicleId.Value;
			}

			var selectedVehicle = model.VehicleId > 0
				? await _context.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.VehicleId == model.VehicleId)
				: null;
			ViewData["TotalFare"] = selectedVehicle?.BasePrice ?? 0m;
			ViewData["DepositPercent"] = 5m;
			ViewData["ShowPayLaterOption"] = true;
			ViewData["PaymentTypeFromStep"] = 2;
			ViewData["PaymentTypeBackStep"] = 1;
			ViewData["InitialUnavailableDates"] = await GetUnavailableDatesAsync(model.VehicleId);
			return View(model);
		}

		// POST: Rentals/Create
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create([Bind("RentalId,RentalDetailsID,VehicleId,CustomerName,ContactNumber,PickupLocation,DropoffLocation,PickupDate,ReturnDate,PickupTime,ReturnTime,PassengerCount,Notes,Discount,DiscountImagePath,DiscountImageFile,RentalStatus,RentalOption,PaymentType,PaymentMethod,TotalAmount,AmountPaid,AccountName,TransactionReference,PaymentDate,ReceiptImageFile,PaymentNotes")] RentalInputModel model)
		{
			await ValidateRentalInputAsync(model, excludeRentalId: null);

			if (model.RentalOption != RentalOption.Book && model.RentalOption != RentalOption.Reserve)
			{
				model.RentalOption = RentalOption.Book;
			}

			var isPayLater = model.RentalOption == RentalOption.Reserve;
			if (isPayLater)
			{
				model.PaymentType = null;
				model.PaymentMethod = null;
				model.AmountPaid = 0;
				model.AccountName = null;
				model.TransactionReference = null;
				model.ReceiptImageFile = null;
				model.PaymentNotes = null;
				ModelState.Remove(nameof(model.PaymentType));
				ModelState.Remove(nameof(model.PaymentMethod));
				ModelState.Remove(nameof(model.AmountPaid));
				ModelState.Remove(nameof(model.AccountName));
				ModelState.Remove(nameof(model.TransactionReference));
				ModelState.Remove(nameof(model.ReceiptImageFile));
			}
			else
			{
				ValidatePaymentInput(model);
			}

			if (ModelState.IsValid)
			{
				await SaveDiscountImageAsync(model);

				var rental = new Rental();
				var details = new RentalDetails();

				if (isPayLater)
				{
					// Walk-in pay-later hold — pending until payment is completed.
					model.RentalStatus = RentalStatus.Pending;
					model.ApplyTo(rental, details);
					rental.RentalOption = RentalOption.Reserve;
					rental.PaymentDueAt = DateTime.Now.AddDays(RentalRules.ReservePaymentWindowDays);
				}
				else
				{
					// Walk-in with payment — approved immediately.
					model.RentalStatus = RentalStatus.Approved;
					model.ApplyTo(rental, details);
					rental.RentalOption = RentalOption.Book;
					rental.PaymentDueAt = null;
				}

				rental.CustomerId = await ResolveCustomerIdByContactAsync(model.ContactNumber);

				var vehicle = await _context.Vehicles.AsNoTracking()
					.FirstOrDefaultAsync(v => v.VehicleId == details.VehicleId);
				if (vehicle != null)
				{
					RentalFareCalculator.ApplyTo(rental, vehicle, details);
				}

				_context.Rentals.Add(rental);
				await _context.SaveChangesAsync();

				details.RentalID = rental.RentalId;
				_context.RentalDetails.Add(details);
				_logs.Record(
					SystemLogAction.Created,
					SystemLogCategory.Booking,
					$"Created booking BK-{rental.RentalId:D5} for {rental.CustomerName}.",
					"Rental",
					rental.RentalId);

				if (!isPayLater)
				{
					await SavePaymentForRentalAsync(model, rental, details);
					await _context.SaveChangesAsync();
					await EnsureTransitForRentalAsync(rental.RentalId);
					TempData["SuccessMessage"] = "Rental and payment created successfully.";
				}
				else
				{
					await _context.SaveChangesAsync();
					TempData["SuccessMessage"] = $"Pay-later hold created. Payment due by {rental.PaymentDueAt:MMM d, yyyy h:mm tt}.";
				}

				return RedirectToAction(nameof(CreateComplete), new { id = rental.RentalId });
			}

			await PopulateVehicleListAsync(model.VehicleId);
			ViewData["TotalFare"] = model.TotalAmount > 0 ? model.TotalAmount : 0m;
			ViewData["DepositPercent"] = 5m;
			ViewData["ShowPayLaterOption"] = true;
			ViewData["PaymentTypeFromStep"] = 2;
			ViewData["PaymentTypeBackStep"] = 1;
			ViewData["InitialUnavailableDates"] = await GetUnavailableDatesAsync(model.VehicleId);
			ViewData["WizardStartStep"] = ResolveCreateWizardStep(model);
			return View(model);
		}

		// GET: Rentals/CreateComplete/5
		public async Task<IActionResult> CreateComplete(int? id)
		{
			if (id == null)
			{
				return NotFound();
			}

			var rental = await _context.Rentals
				.AsNoTracking()
				.FirstOrDefaultAsync(r => r.RentalId == id);
			if (rental == null)
			{
				return NotFound();
			}

			ViewData["Title"] = "Add New Rental";
			ViewData["ActivePage"] = "Rentals";
			ViewData["RentalId"] = rental.RentalId;
			ViewData["IsPayLater"] = rental.RentalOption == RentalOption.Reserve;
			return View();
		}

		// GET: Rentals/Edit/5
		public async Task<IActionResult> Edit(int? id)
		{
			if (id == null)
			{
				return NotFound();
			}

			var rental = await _context.Rentals
				.Include(r => r.Details)!
					.ThenInclude(d => d!.Vehicle)
				.FirstOrDefaultAsync(r => r.RentalId == id);
			if (rental == null)
			{
				return NotFound();
			}

			var payment = await _context.Payments
				.AsNoTracking()
				.Where(p => p.RentalId == id)
				.OrderByDescending(p => p.PaymentId)
				.FirstOrDefaultAsync();

			var model = RentalInputModel.FromEntities(rental, rental.Details, payment);
			await PopulateVehicleListAsync(model.VehicleId);
			ViewData["TotalFare"] = model.TotalAmount > 0
				? model.TotalAmount
				: (rental.Details?.Vehicle?.BasePrice ?? 0m);
			ViewData["DepositPercent"] = 5m;
			ViewData["ShowPayLaterOption"] = true;
			ViewData["HidePaymentReminders"] = true;
			ViewData["ExistingReceiptPath"] = payment?.ReceiptImagePath;
			return View(model);
		}

		// POST: Rentals/Edit/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(int id, [Bind("RentalId,RentalDetailsID,VehicleId,CustomerName,ContactNumber,PickupLocation,DropoffLocation,PickupDate,ReturnDate,PickupTime,ReturnTime,PassengerCount,Notes,Discount,DiscountImagePath,DiscountImageFile,RentalStatus,RentalOption,PaymentId,PaymentType,PaymentMethod,TotalAmount,AmountPaid,AccountName,TransactionReference,PaymentDate,ReceiptImageFile,PaymentNotes")] RentalInputModel model)
		{
			if (id != model.RentalId)
			{
				return NotFound();
			}

			await ValidateRentalInputAsync(model, excludeRentalId: id);
			var existingReceipt = model.PaymentId > 0
				? await _context.Payments.AsNoTracking()
					.Where(p => p.PaymentId == model.PaymentId)
					.Select(p => p.ReceiptImagePath)
					.FirstOrDefaultAsync()
				: null;
			ValidatePaymentInput(model, requireReceiptIfCashless: string.IsNullOrWhiteSpace(existingReceipt));

			if (ModelState.IsValid)
			{
				try
				{
					var rental = await _context.Rentals
						.Include(r => r.Details)
						.FirstOrDefaultAsync(r => r.RentalId == id);
					if (rental == null)
					{
						return NotFound();
					}

					var details = rental.Details;
					if (details == null)
					{
						details = new RentalDetails { RentalID = rental.RentalId };
						_context.RentalDetails.Add(details);
					}

					await SaveDiscountImageAsync(model);
					model.ApplyTo(rental, details);
					rental.CustomerId = await ResolveCustomerIdByContactAsync(model.ContactNumber);

					var vehicle = await _context.Vehicles.AsNoTracking()
						.FirstOrDefaultAsync(v => v.VehicleId == details.VehicleId);
					if (vehicle != null)
					{
						RentalFareCalculator.ApplyTo(rental, vehicle, details);
					}

					await SavePaymentForRentalAsync(model, rental, details);
					_logs.Record(
						SystemLogAction.Updated,
						SystemLogCategory.Booking,
						$"Updated booking BK-{rental.RentalId:D5} for {rental.CustomerName}.",
						"Rental",
						rental.RentalId);
					await _context.SaveChangesAsync();

					if (rental.RentalStatus == RentalStatus.Approved)
					{
						await EnsureTransitForRentalAsync(rental.RentalId);
					}
				}
				catch (DbUpdateConcurrencyException)
				{
					if (!RentalExists(model.RentalId))
					{
						return NotFound();
					}
					throw;
				}
				return RedirectToAction(nameof(Details), new { id });
			}

			await PopulateVehicleListAsync(model.VehicleId);
			ViewData["TotalFare"] = model.TotalAmount;
			ViewData["DepositPercent"] = 5m;
			ViewData["ShowPayLaterOption"] = true;
			ViewData["HidePaymentReminders"] = true;
			ViewData["ExistingReceiptPath"] = existingReceipt;
			return View(model);
		}

		// GET: Rentals/Delete/5
		public async Task<IActionResult> Delete(int? id)
		{
			if (id == null)
			{
				return NotFound();
			}

			var rental = await _context.Rentals
				.Include(r => r.Details)!
					.ThenInclude(d => d!.Vehicle)
				.FirstOrDefaultAsync(m => m.RentalId == id);
			if (rental == null)
			{
				return NotFound();
			}

			return View(rental);
		}

		// POST: Rentals/Delete/5
		[HttpPost, ActionName("Delete")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteConfirmed(int id)
		{
			var rental = await _context.Rentals
				.Include(r => r.Details)
				.FirstOrDefaultAsync(r => r.RentalId == id);
			if (rental != null)
			{
				_logs.Record(
					SystemLogAction.Deleted,
					SystemLogCategory.Booking,
					$"Deleted booking BK-{rental.RentalId:D5} for {rental.CustomerName}.",
					"Rental",
					rental.RentalId);
				var payments = await _context.Payments.Where(p => p.RentalId == id).ToListAsync();
				if (payments.Count > 0)
				{
					_context.Payments.RemoveRange(payments);
				}

				var transit = await _context.Transits.FirstOrDefaultAsync(t => t.RentalID == id);
				if (transit != null)
				{
					_context.Transits.Remove(transit);
				}

				_context.Rentals.Remove(rental);
			}

			await _context.SaveChangesAsync();
			return RedirectToAction(nameof(Index));
		}

		// GET: Rentals/Availability?vehicleId=1
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

			var ranges = await (
				from details in _context.RentalDetails.AsNoTracking()
				join rental in _context.Rentals.AsNoTracking()
					on details.RentalID equals rental.RentalId
				where details.VehicleId == vehicleId
					&& rental.RentalStatus != RentalStatus.Cancelled
					&& rental.RentalStatus != RentalStatus.Expired
				select new { details.PickupDate, details.ReturnDate }
			).ToListAsync();

			var unavailableDates = new List<string>();
			foreach (var range in ranges)
			{
				for (var day = range.PickupDate; day <= range.ReturnDate; day = day.AddDays(1))
				{
					unavailableDates.Add(day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
				}
			}

			return unavailableDates.Distinct().OrderBy(d => d).ToList();
		}

		private int ResolveCreateWizardStep(RentalInputModel model)
		{
			static bool HasError(Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary state, params string[] keys)
			{
				foreach (var key in keys)
				{
					if (state.TryGetValue(key, out var entry) && entry.Errors.Count > 0)
					{
						return true;
					}
				}
				return false;
			}

			if (HasError(ModelState,
				nameof(RentalInputModel.VehicleId),
				nameof(RentalInputModel.CustomerName),
				nameof(RentalInputModel.ContactNumber),
				nameof(RentalInputModel.PickupLocation),
				nameof(RentalInputModel.DropoffLocation),
				nameof(RentalInputModel.PickupDate),
				nameof(RentalInputModel.ReturnDate),
				nameof(RentalInputModel.PickupTime),
				nameof(RentalInputModel.ReturnTime),
				nameof(RentalInputModel.PassengerCount),
				nameof(RentalInputModel.Discount),
				nameof(RentalInputModel.DiscountImageFile),
				nameof(RentalInputModel.Notes)))
			{
				return 1;
			}

			if (HasError(ModelState, nameof(RentalInputModel.PaymentType), nameof(RentalInputModel.RentalOption)))
			{
				return 2;
			}

			if (model.RentalOption != RentalOption.Reserve
				&& HasError(ModelState,
					nameof(RentalInputModel.PaymentMethod),
					nameof(RentalInputModel.AmountPaid),
					nameof(RentalInputModel.AccountName),
					nameof(RentalInputModel.TransactionReference),
					nameof(RentalInputModel.PaymentDate),
					nameof(RentalInputModel.ReceiptImageFile),
					nameof(RentalInputModel.PaymentNotes),
					nameof(RentalInputModel.TotalAmount)))
			{
				return 3;
			}

			// Stay on summary so the user can review and edit without retyping step 1.
			return 4;
		}

		private async Task ValidateRentalInputAsync(RentalInputModel model, int? excludeRentalId)
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
					from details in _context.RentalDetails
					join rental in _context.Rentals
						on details.RentalID equals rental.RentalId
					where details.VehicleId == model.VehicleId
						&& rental.RentalStatus != RentalStatus.Cancelled
						&& rental.RentalStatus != RentalStatus.Expired
						&& details.PickupDate <= model.ReturnDate
						&& details.ReturnDate >= model.PickupDate
					select details;

				if (excludeRentalId.HasValue)
				{
					conflictQuery = conflictQuery.Where(d => d.RentalID != excludeRentalId.Value);
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

		private void ValidatePaymentInput(RentalInputModel model, bool requireReceiptIfCashless = true)
		{
			if (string.IsNullOrWhiteSpace(model.PaymentType))
			{
				ModelState.AddModelError(nameof(model.PaymentType), "Please select a payment type.");
			}

			if (string.IsNullOrWhiteSpace(model.PaymentMethod))
			{
				ModelState.AddModelError(nameof(model.PaymentMethod), "Please select a payment method.");
			}

			if (model.AmountPaid <= 0)
			{
				ModelState.AddModelError(nameof(model.AmountPaid), "Amount paid must be greater than zero.");
			}

			var isCashless = !string.Equals(model.PaymentMethod, "Walk-in", StringComparison.OrdinalIgnoreCase);
			if (!isCashless)
			{
				return;
			}

			if (string.IsNullOrWhiteSpace(model.AccountName))
			{
				ModelState.AddModelError(nameof(model.AccountName), "Account name is required for cashless payment.");
			}

			if (string.IsNullOrWhiteSpace(model.TransactionReference))
			{
				ModelState.AddModelError(nameof(model.TransactionReference), "Transaction reference is required for cashless payment.");
			}

			if (requireReceiptIfCashless && model.ReceiptImageFile == null)
			{
				ModelState.AddModelError(nameof(model.ReceiptImageFile), "Please upload your payment receipt.");
			}
		}

		private async Task SavePaymentForRentalAsync(
			RentalInputModel model,
			Rental rental,
			RentalDetails details)
		{
			if (rental.TotalAmount <= 0)
			{
				var vehicle = await _context.Vehicles.AsNoTracking()
					.FirstOrDefaultAsync(v => v.VehicleId == details.VehicleId);
				if (vehicle != null)
				{
					RentalFareCalculator.ApplyTo(rental, vehicle, details);
				}
			}

			string? receiptPath = null;
			if (model.ReceiptImageFile != null)
			{
				receiptPath = await ImageStorage.SaveAsync(
					_webHostEnvironment,
					model.ReceiptImageFile,
					ImageStorage.PaymentReceiptsFolder);
			}

			var payment = model.PaymentId > 0
				? await _context.Payments.FirstOrDefaultAsync(p => p.PaymentId == model.PaymentId && p.RentalId == rental.RentalId)
				: await _context.Payments
					.Where(p => p.RentalId == rental.RentalId)
					.OrderByDescending(p => p.PaymentId)
					.FirstOrDefaultAsync();

			if (payment == null)
			{
				payment = new Payment { RentalId = rental.RentalId };
				_context.Payments.Add(payment);
			}

			payment.PaymentType = model.PaymentType?.Trim() ?? string.Empty;
			payment.PaymentMethod = model.PaymentMethod?.Trim() ?? string.Empty;
			payment.TotalAmount = rental.TotalAmount > 0 ? rental.TotalAmount : model.TotalAmount;
			payment.AmountPaid = model.AmountPaid;
			payment.AccountName = model.AccountName;
			payment.TransactionReference = model.TransactionReference;
			payment.PaymentDate = model.PaymentDate == default ? DateTime.Now : model.PaymentDate;
			payment.PaymentNotes = model.PaymentNotes;
			if (receiptPath != null)
			{
				payment.ReceiptImagePath = receiptPath;
			}
		}

		private async Task SaveDiscountImageAsync(RentalInputModel model)
		{
			if (model.DiscountImageFile != null)
			{
				model.DiscountImagePath = await ImageStorage.SaveAsync(
					_webHostEnvironment,
					model.DiscountImageFile,
					ImageStorage.RentalsFolder);
			}

			if (model.Discount == Discount.No)
			{
				model.DiscountImagePath = null;
			}
		}

		private async Task<int?> ResolveCustomerIdByContactAsync(string? contactNumber)
		{
			var variants = PhoneNumber.Variants(contactNumber);
			if (variants.Count == 0)
			{
				return null;
			}

			var candidates = await _context.CustomerProfiles
				.AsNoTracking()
				.Where(c => variants.Contains(c.ContactNumber))
				.Select(c => new { c.CustomerId, c.ContactNumber })
				.ToListAsync();

			if (candidates.Count == 0)
			{
				// Fallback: normalized compare when stored formats differ.
				var normalized = PhoneNumber.Normalize(contactNumber);
				candidates = await _context.CustomerProfiles
					.AsNoTracking()
					.Select(c => new { c.CustomerId, c.ContactNumber })
					.ToListAsync();
				candidates = candidates
					.Where(c => PhoneNumber.Normalize(c.ContactNumber) == normalized)
					.ToList();
			}

			return candidates.OrderBy(c => c.CustomerId).Select(c => (int?)c.CustomerId).FirstOrDefault();
		}

		private bool RentalExists(int id)
		{
			return _context.Rentals.Any(e => e.RentalId == id);
		}

		private static decimal PctChange(decimal current, decimal previous)
			=> Math.Round((current - previous) * 0.1m, 1);

		private async Task PopulateVehicleListAsync(int? selectedVehicleId = null)
		{
			var vehicles = await _context.Vehicles
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
