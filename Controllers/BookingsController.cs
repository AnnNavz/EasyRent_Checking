using System.Text.Json;
using EasyRent_Checking.Data;
using EasyRent_Checking.Models;
using EasyRent_Checking.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EasyRent_Checking.Controllers
{
	public class BookingsController : Controller
	{
		private const string AdminPendingReservationKey = "AdminPendingReservation";

		private readonly EasyRent_CheckingContext _context;
		private readonly IWebHostEnvironment _webHostEnvironment;
		private readonly ReservationAvailabilityService _availability;

		public BookingsController(
			EasyRent_CheckingContext context,
			IWebHostEnvironment webHostEnvironment,
			ReservationAvailabilityService availability)
		{
			_context = context;
			_webHostEnvironment = webHostEnvironment;
			_availability = availability;
		}

		// GET: Bookings
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
			ViewData["ActivePage"] = "Bookings";
			ViewData["Title"] = "Booking Management";

			var bookingsQuery = _context.Reservation
				.Include(r => r.Vehicle)
				.Where(r => !r.IsDraft);

			ViewData["TotalBookingsCount"] = await bookingsQuery.CountAsync();
			ViewData["PendingApprovalsCount"] = await bookingsQuery.CountAsync(r => r.Status == ReservationStatus.Pending);
			ViewData["ActiveTripsCount"] = await bookingsQuery.CountAsync(r => r.Status == ReservationStatus.InProgress);

			if (!string.IsNullOrEmpty(searchString))
			{
				bookingsQuery = bookingsQuery.Where(r =>
					r.CustomerName.Contains(searchString) ||
					r.ContactInfo.Contains(searchString) ||
					(r.Vehicle != null && (
						r.Vehicle.Brand.Contains(searchString) ||
						r.Vehicle.Model.Contains(searchString) ||
						r.Vehicle.PlateNumber.Contains(searchString))));
			}

			if (!string.IsNullOrEmpty(currentFilter) && Enum.TryParse(currentFilter, true, out ReservationStatus filterStatus))
			{
				bookingsQuery = bookingsQuery.Where(r => r.Status == filterStatus);
			}

			bookingsQuery = sortBy switch
			{
				"Customer" => bookingsQuery.OrderBy(r => r.CustomerName),
				"Dates" => bookingsQuery.OrderBy(r => r.PickupDate).ThenBy(r => r.PickupTime),
				"Status" => bookingsQuery.OrderBy(r => r.Status),
				"Vehicle" => bookingsQuery.OrderBy(r => r.Vehicle!.Brand).ThenBy(r => r.Vehicle!.Model),
				_ => bookingsQuery.OrderByDescending(r => r.ReservationID)
			};

			var totalCount = await bookingsQuery.CountAsync();
			var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
			if (pageNumber > totalPages)
			{
				pageNumber = totalPages;
			}

			ViewData["PageIndex"] = pageNumber;
			ViewData["TotalPages"] = totalPages;
			ViewData["TotalCount"] = totalCount;
			ViewData["PageSize"] = pageSize;

			var bookings = await bookingsQuery
				.Skip((pageNumber - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();

			return View(bookings);
		}

		// GET: Bookings/Create
		public async Task<IActionResult> Create(int? vehicleId)
		{
			SetBookingWizardViewData(1);

			var pending = await GetActivePendingReservationAsync();
			if (pending != null && (vehicleId == null || pending.VehicleId == vehicleId))
			{
				var pendingVehicle = await _context.Vehicle.FirstOrDefaultAsync(v => v.VehicleId == pending.VehicleId);
				if (pendingVehicle == null)
				{
					ClearPendingReservation();
					return NotFound();
				}

				await PopulateVehiclesAsync(pendingVehicle.VehicleId);
				return View(new ReservationFormViewModel
				{
					Vehicle = pendingVehicle,
					Reservation = pending
				});
			}

			await PopulateVehiclesAsync(vehicleId);

			if (vehicleId == null)
			{
				return View(new ReservationFormViewModel
				{
					Reservation = new Reservation
					{
						PickupDate = DateOnly.FromDateTime(DateTime.Today),
						ReturnDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
						PickupTime = new TimeOnly(8, 0),
						ReturnTime = new TimeOnly(18, 0),
						PassengerCount = 1
					}
				});
			}

			var vehicle = await _context.Vehicle.FirstOrDefaultAsync(v => v.VehicleId == vehicleId);
			if (vehicle == null)
			{
				return NotFound();
			}

			return View(new ReservationFormViewModel
			{
				Vehicle = vehicle,
				Reservation = new Reservation
				{
					VehicleId = vehicle.VehicleId,
					PickupDate = DateOnly.FromDateTime(DateTime.Today),
					ReturnDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
					PickupTime = new TimeOnly(8, 0),
					ReturnTime = new TimeOnly(18, 0),
					PassengerCount = 1
				}
			});
		}

		// POST: Bookings/Create
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(ReservationFormViewModel model)
		{
			SetBookingWizardViewData(1);

			var vehicleId = model.Reservation.VehicleId;
			if (vehicleId <= 0 && int.TryParse(Request.Form["Reservation.VehicleId"], out var postedVehicleId))
			{
				vehicleId = postedVehicleId;
			}

			var vehicle = await _context.Vehicle.FirstOrDefaultAsync(v => v.VehicleId == vehicleId);
			await PopulateVehiclesAsync(vehicleId);

			if (vehicle == null)
			{
				ModelState.AddModelError("Reservation.VehicleId", "Please select a vehicle.");
				model.Vehicle = new Vehicle();
				return View(model);
			}

			model.Reservation.VehicleId = vehicle.VehicleId;
			model.Vehicle = vehicle;

			ModelState.Remove("Vehicle");
			ModelState.Remove("Reservation.Vehicle");
			ModelState.Remove("Reservation.VehicleId");

			if (model.Reservation.ReturnDate < model.Reservation.PickupDate)
			{
				ModelState.AddModelError("Reservation.ReturnDate", "Return date cannot be earlier than pickup date.");
			}

			if (model.Reservation.Discount == 1 && model.Reservation.ImageFile == null && string.IsNullOrEmpty(model.Reservation.ImagePath))
			{
				ModelState.AddModelError("Reservation.ImageFile", "Please upload a valid ID picture for the Senior / PWD discount.");
			}

			var sessionPending = await GetActivePendingReservationAsync();
			var excludeId = sessionPending?.ReservationID > 0 ? sessionPending.ReservationID : (int?)null;

			if (ModelState.IsValid)
			{
				var hasConflict = await _availability.HasConflictAsync(
					vehicle.VehicleId,
					model.Reservation.PickupDate,
					model.Reservation.ReturnDate,
					excludeId);

				if (hasConflict)
				{
					ModelState.AddModelError(string.Empty, "Those dates are unavailable for this vehicle. Please choose another range.");
				}
			}

			if (!ModelState.IsValid)
			{
				return View(model);
			}

			if (model.Reservation.ImageFile != null)
			{
				model.Reservation.ImagePath = await SaveUploadedFileAsync(model.Reservation.ImageFile);
			}

			var now = DateTime.UtcNow;
			Reservation entity;

			if (excludeId.HasValue)
			{
				entity = await _context.Reservation.FirstAsync(r => r.ReservationID == excludeId.Value);
				entity.VehicleId = vehicle.VehicleId;
				entity.CustomerName = model.Reservation.CustomerName;
				entity.ContactInfo = model.Reservation.ContactInfo;
				entity.PickupLoc = model.Reservation.PickupLoc;
				entity.DropoffLoc = model.Reservation.DropoffLoc;
				entity.PickupDate = model.Reservation.PickupDate;
				entity.ReturnDate = model.Reservation.ReturnDate;
				entity.PickupTime = model.Reservation.PickupTime;
				entity.ReturnTime = model.Reservation.ReturnTime;
				entity.PassengerCount = model.Reservation.PassengerCount;
				entity.SpNotes = model.Reservation.SpNotes;
				entity.Discount = model.Reservation.Discount;
				if (!string.IsNullOrEmpty(model.Reservation.ImagePath))
				{
					entity.ImagePath = model.Reservation.ImagePath;
				}
			}
			else
			{
				entity = new Reservation
				{
					VehicleId = vehicle.VehicleId,
					CustomerName = model.Reservation.CustomerName,
					ContactInfo = model.Reservation.ContactInfo,
					PickupLoc = model.Reservation.PickupLoc,
					DropoffLoc = model.Reservation.DropoffLoc,
					PickupDate = model.Reservation.PickupDate,
					ReturnDate = model.Reservation.ReturnDate,
					PickupTime = model.Reservation.PickupTime,
					ReturnTime = model.Reservation.ReturnTime,
					PassengerCount = model.Reservation.PassengerCount,
					SpNotes = model.Reservation.SpNotes,
					Discount = model.Reservation.Discount,
					ImagePath = model.Reservation.ImagePath,
					CreatedAtUtc = now
				};
				_context.Reservation.Add(entity);
			}

			entity.Status = ReservationStatus.Pending;
			entity.IsDraft = true;
			entity.LockedUntilUtc = now.Add(ReservationAvailabilityService.SoftLockDuration);
			entity.PaymentChannel = null;
			entity.PayerAccountName = null;
			entity.PaymentReference = null;
			entity.AmountSent = null;
			entity.PaymentDateTime = null;
			entity.PaymentProofPath = null;
			entity.PaymentNotes = null;

			await _context.SaveChangesAsync();
			SavePendingReservation(entity);

			return RedirectToAction(nameof(PaymentDetails));
		}

		// GET: Bookings/PaymentDetails
		public async Task<IActionResult> PaymentDetails()
		{
			var context = await LoadActiveBookingContextAsync();
			if (context == null)
			{
				return RedirectToAction(nameof(Create));
			}

			SetBookingWizardViewData(2);
			return View(BuildPaymentDetailsViewModel(context.Value.Reservation, context.Value.Vehicle));
		}

		// GET: Bookings/PaymentConfirmation
		public async Task<IActionResult> PaymentConfirmation()
		{
			var context = await LoadActiveBookingContextAsync();
			if (context == null)
			{
				return RedirectToAction(nameof(Create));
			}

			SetBookingWizardViewData(3);
			var model = BuildPaymentDetailsViewModel(context.Value.Reservation, context.Value.Vehicle);
			model.PaymentChannel ??= PaymentChannel.GCash;
			model.AmountSent ??= model.Pricing.Total;
			return View(model);
		}

		// POST: Bookings/PaymentConfirmation
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> PaymentConfirmation(ReservationPaymentDetailsViewModel model)
		{
			var context = await LoadActiveBookingContextAsync();
			if (context == null)
			{
				return RedirectToAction(nameof(Create));
			}

			var reservation = context.Value.Reservation;
			var vehicle = context.Value.Vehicle;
			var pricing = ReservationPricingCalculator.Calculate(vehicle, reservation);
			model.Reservation = reservation;
			model.Vehicle = vehicle;
			model.Pricing = pricing;

			SetBookingWizardViewData(3);

			ModelState.Remove("Reservation");
			ModelState.Remove("Vehicle");
			ModelState.Remove("Pricing");

			ValidatePaymentConfirmation(model, pricing);

			if (!ModelState.IsValid)
			{
				return View(model);
			}

			if (model.PaymentProofFile != null)
			{
				model.PaymentProofPath = await SaveUploadedFileAsync(model.PaymentProofFile);
			}

			var entity = await _context.Reservation.FirstAsync(r => r.ReservationID == reservation.ReservationID);
			entity.PaymentChannel = model.PaymentChannel;
			entity.PayerAccountName = model.PayerAccountName?.Trim();
			entity.PaymentReference = model.PaymentReference?.Trim();
			entity.AmountSent = model.AmountSent;
			entity.PaymentDateTime = model.PaymentDateTime;
			entity.PaymentProofPath = model.PaymentProofPath ?? entity.PaymentProofPath;
			entity.PaymentNotes = model.PaymentNotes?.Trim();
			entity.Status = ReservationStatus.Pending;
			entity.IsDraft = true;
			// Keep soft lock alive until admin confirms on step 4.
			entity.LockedUntilUtc = DateTime.UtcNow.Add(ReservationAvailabilityService.SoftLockDuration);

			await _context.SaveChangesAsync();
			SavePendingReservation(entity);

			return RedirectToAction(nameof(BookingSummary));
		}

		// GET: Bookings/BookingSummary
		public async Task<IActionResult> BookingSummary()
		{
			var reservation = await GetActivePendingReservationAsync(requirePaymentSubmitted: true);
			if (reservation == null)
			{
				var softLocked = await GetActivePendingReservationAsync();
				if (softLocked == null)
				{
					return RedirectToAction(nameof(Create));
				}

				return RedirectToAction(nameof(PaymentConfirmation));
			}

			var vehicle = await _context.Vehicle.FirstOrDefaultAsync(v => v.VehicleId == reservation.VehicleId);
			if (vehicle == null)
			{
				ClearPendingReservation();
				return NotFound();
			}

			SetBookingWizardViewData(4);

			// Refresh draft lock while reviewing the summary.
			var tracked = await _context.Reservation.FirstAsync(r => r.ReservationID == reservation.ReservationID);
			tracked.LockedUntilUtc = DateTime.UtcNow.Add(ReservationAvailabilityService.SoftLockDuration);
			await _context.SaveChangesAsync();
			SavePendingReservation(tracked);

			return View(BuildPaymentDetailsViewModel(tracked, vehicle));
		}

		// POST: Bookings/ConfirmBooking — admin final approval
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ConfirmBooking()
		{
			var reservation = await GetActivePendingReservationAsync(requirePaymentSubmitted: true);
			if (reservation == null)
			{
				return RedirectToAction(nameof(Create));
			}

			var entity = await _context.Reservation.FirstAsync(r => r.ReservationID == reservation.ReservationID);
			entity.Status = ReservationStatus.Confirmed;
			entity.IsDraft = false;
			entity.LockedUntilUtc = null;
			await _context.SaveChangesAsync();

			ClearPendingReservation();
			TempData["BookingSuccess"] = $"BK-{entity.ReservationID:D4} has been approved and submitted.";
			return RedirectToAction(nameof(Index));
		}

		// POST: Bookings/Finish — clear draft and return to index
		[HttpPost]
		[ValidateAntiForgeryToken]
		public IActionResult Finish()
		{
			ClearPendingReservation();
			return RedirectToAction(nameof(Index));
		}

		// GET: Bookings/Edit/5
		public async Task<IActionResult> Edit(int? id, string? returnUrl)
		{
			if (id == null)
			{
				return NotFound();
			}

			var reservation = await _context.Reservation
				.AsNoTracking()
				.FirstOrDefaultAsync(r => r.ReservationID == id && !r.IsDraft);

			if (reservation == null)
			{
				return NotFound();
			}

			var vehicle = await _context.Vehicle.FirstOrDefaultAsync(v => v.VehicleId == reservation.VehicleId);
			if (vehicle == null)
			{
				return NotFound();
			}

			if (Url.IsLocalUrl(returnUrl))
			{
				ViewData["ReturnUrl"] = returnUrl;
			}

			ViewData["ActivePage"] = "Bookings";
			ViewData["Title"] = "Edit Details";
			await PopulateVehiclesAsync(vehicle.VehicleId);
			return View(BuildEditViewModel(reservation, vehicle));
		}

		// POST: Bookings/Edit/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(int id, ReservationEditViewModel model, string? returnUrl)
		{
			ViewData["ActivePage"] = "Bookings";
			ViewData["Title"] = "Edit Details";
			if (Url.IsLocalUrl(returnUrl))
			{
				ViewData["ReturnUrl"] = returnUrl;
			}

			var entity = await _context.Reservation.FirstOrDefaultAsync(r => r.ReservationID == id && !r.IsDraft);
			if (entity == null)
			{
				return NotFound();
			}

			var vehicleId = model.Reservation?.VehicleId > 0
				? model.Reservation.VehicleId
				: entity.VehicleId;

			var vehicle = await _context.Vehicle.FirstOrDefaultAsync(v => v.VehicleId == vehicleId);
			await PopulateVehiclesAsync(vehicleId);

			if (vehicle == null)
			{
				ModelState.AddModelError("Reservation.VehicleId", "Please select a vehicle.");
				model.Vehicle = new Vehicle();
				model.Pricing = new ReservationPricingSummary();
				return View(model);
			}

			model.Reservation ??= new Reservation();
			model.Reservation.ReservationID = id;
			model.Reservation.VehicleId = vehicle.VehicleId;
			model.Vehicle = vehicle;

			ModelState.Remove("Vehicle");
			ModelState.Remove("Pricing");
			ModelState.Remove("Reservation.Vehicle");
			ModelState.Remove("Reservation.VehicleId");

			if (model.Reservation.ReturnDate < model.Reservation.PickupDate)
			{
				ModelState.AddModelError("Reservation.ReturnDate", "Return date cannot be earlier than pickup date.");
			}

			if (model.Reservation.Discount == 1
				&& model.Reservation.ImageFile == null
				&& string.IsNullOrEmpty(model.Reservation.ImagePath))
			{
				ModelState.AddModelError("Reservation.ImageFile", "Please upload a valid ID picture for the Senior / PWD discount.");
			}

			var pricing = ReservationPricingCalculator.Calculate(vehicle, model.Reservation);
			model.Pricing = pricing;
			ValidateEditPayment(model, pricing);

			if (ModelState.IsValid)
			{
				var hasConflict = await _availability.HasConflictAsync(
					vehicle.VehicleId,
					model.Reservation.PickupDate,
					model.Reservation.ReturnDate,
					id);

				if (hasConflict)
				{
					ModelState.AddModelError(string.Empty, "Those dates are unavailable for this vehicle. Please choose another range.");
				}
			}

			if (!ModelState.IsValid)
			{
				return View(model);
			}

			if (model.Reservation.ImageFile != null)
			{
				model.Reservation.ImagePath = await SaveUploadedFileAsync(model.Reservation.ImageFile);
			}

			if (model.PaymentProofFile != null)
			{
				model.PaymentProofPath = await SaveUploadedFileAsync(model.PaymentProofFile);
			}

			entity.VehicleId = vehicle.VehicleId;
			entity.CustomerName = model.Reservation.CustomerName;
			entity.ContactInfo = model.Reservation.ContactInfo;
			entity.PickupLoc = model.Reservation.PickupLoc;
			entity.DropoffLoc = model.Reservation.DropoffLoc;
			entity.PickupDate = model.Reservation.PickupDate;
			entity.ReturnDate = model.Reservation.ReturnDate;
			entity.PickupTime = model.Reservation.PickupTime;
			entity.ReturnTime = model.Reservation.ReturnTime;
			entity.PassengerCount = model.Reservation.PassengerCount;
			entity.SpNotes = model.Reservation.SpNotes;
			entity.Discount = model.Reservation.Discount;
			entity.ImagePath = model.Reservation.Discount == 1
				? (model.Reservation.ImagePath ?? entity.ImagePath)
				: null;

			entity.PaymentChannel = model.PaymentChannel;
			if (model.PaymentChannel == PaymentChannel.WalkIn)
			{
				entity.PayerAccountName = null;
				entity.PaymentReference = null;
				entity.AmountSent = null;
				entity.PaymentDateTime = null;
				entity.PaymentProofPath = null;
				entity.PaymentNotes = model.PaymentNotes?.Trim();
			}
			else
			{
				entity.PayerAccountName = model.PayerAccountName?.Trim();
				entity.PaymentReference = model.PaymentReference?.Trim();
				entity.AmountSent = model.AmountSent;
				entity.PaymentDateTime = model.PaymentDateTime;
				entity.PaymentProofPath = string.IsNullOrEmpty(model.PaymentProofPath)
					? null
					: model.PaymentProofPath;
				entity.PaymentNotes = model.PaymentNotes?.Trim();
			}

			await _context.SaveChangesAsync();
			TempData["BookingSuccess"] = $"BK-{entity.ReservationID:D4} has been updated.";
			return RedirectToAction(nameof(Details), new { id });
		}

		// GET: Bookings/Details/5
		public async Task<IActionResult> Details(int? id)
		{
			if (id == null)
			{
				return NotFound();
			}

			var reservation = await _context.Reservation
				.Include(r => r.Vehicle)
				.AsNoTracking()
				.FirstOrDefaultAsync(r => r.ReservationID == id && !r.IsDraft);

			if (reservation == null)
			{
				return NotFound();
			}

			ViewData["ActivePage"] = "Bookings";
			ViewData["Title"] = "View Details";
			return View(reservation);
		}

		// POST: Bookings/Approve/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Approve(int id)
		{
			var reservation = await _context.Reservation.FirstOrDefaultAsync(r => r.ReservationID == id && !r.IsDraft);
			if (reservation == null)
			{
				return NotFound();
			}

			if (reservation.Status == ReservationStatus.Pending)
			{
				reservation.Status = ReservationStatus.Confirmed;
				reservation.LockedUntilUtc = null;
				await _context.SaveChangesAsync();
				TempData["BookingSuccess"] = $"BK-{reservation.ReservationID:D4} has been approved.";
			}

			return RedirectToAction(nameof(Details), new { id });
		}

		// POST: Bookings/Reject/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Reject(int id)
		{
			var reservation = await _context.Reservation.FirstOrDefaultAsync(r => r.ReservationID == id && !r.IsDraft);
			if (reservation == null)
			{
				return NotFound();
			}

			if (reservation.Status == ReservationStatus.Pending)
			{
				reservation.Status = ReservationStatus.Rejected;
				reservation.LockedUntilUtc = null;
				await _context.SaveChangesAsync();
				TempData["BookingSuccess"] = $"BK-{reservation.ReservationID:D4} has been rejected.";
			}

			return RedirectToAction(nameof(Details), new { id });
		}

		// POST: Bookings/Delete
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Delete(int id)
		{
			var reservation = await _context.Reservation.FindAsync(id);
			if (reservation != null)
			{
				_context.Reservation.Remove(reservation);
				await _context.SaveChangesAsync();
			}

			return RedirectToAction(nameof(Index));
		}

		private void SetBookingWizardViewData(int step)
		{
			ViewData["ActivePage"] = "Bookings";
			ViewData["Title"] = "Make a Reservation";
			ViewData["ReservationStep"] = step;
		}

		private async Task<(Reservation Reservation, Vehicle Vehicle)?> LoadActiveBookingContextAsync()
		{
			var reservation = await GetActivePendingReservationAsync();
			if (reservation == null)
			{
				return null;
			}

			var vehicle = await _context.Vehicle.FirstOrDefaultAsync(v => v.VehicleId == reservation.VehicleId);
			if (vehicle == null)
			{
				ClearPendingReservation();
				return null;
			}

			return (reservation, vehicle);
		}

		private void ValidatePaymentConfirmation(ReservationPaymentDetailsViewModel model, ReservationPricingSummary pricing)
		{
			ValidateOnlinePaymentFields(
				model.PaymentChannel,
				model.PayerAccountName,
				model.PaymentReference,
				model.AmountSent,
				model.PaymentDateTime,
				model.PaymentProofFile,
				model.PaymentProofPath,
				pricing,
				nameof(model.PayerAccountName),
				nameof(model.PaymentReference),
				nameof(model.AmountSent),
				nameof(model.PaymentDateTime),
				nameof(model.PaymentProofFile));
		}

		private void ValidateEditPayment(ReservationEditViewModel model, ReservationPricingSummary pricing)
		{
			if (model.PaymentChannel == null)
			{
				ModelState.AddModelError(nameof(model.PaymentChannel), "Please select a payment channel.");
				return;
			}

			ValidateOnlinePaymentFields(
				model.PaymentChannel,
				model.PayerAccountName,
				model.PaymentReference,
				model.AmountSent,
				model.PaymentDateTime,
				model.PaymentProofFile,
				model.PaymentProofPath,
				pricing,
				nameof(model.PayerAccountName),
				nameof(model.PaymentReference),
				nameof(model.AmountSent),
				nameof(model.PaymentDateTime),
				nameof(model.PaymentProofFile));
		}

		private void ValidateOnlinePaymentFields(
			PaymentChannel? channel,
			string? payerAccountName,
			string? paymentReference,
			decimal? amountSent,
			DateTime? paymentDateTime,
			IFormFile? paymentProofFile,
			string? paymentProofPath,
			ReservationPricingSummary pricing,
			string payerAccountNameKey,
			string paymentReferenceKey,
			string amountSentKey,
			string paymentDateTimeKey,
			string paymentProofFileKey)
		{
			if (channel == null || channel == PaymentChannel.WalkIn)
			{
				return;
			}

			if (string.IsNullOrWhiteSpace(payerAccountName))
			{
				ModelState.AddModelError(payerAccountNameKey, "Account name is required.");
			}

			if (string.IsNullOrWhiteSpace(paymentReference))
			{
				ModelState.AddModelError(paymentReferenceKey, "Reference / transaction number is required.");
			}

			if (amountSent == null)
			{
				ModelState.AddModelError(amountSentKey, "Amount sent is required.");
			}
			else if (Math.Abs(amountSent.Value - pricing.Total) > 0.009m)
			{
				ModelState.AddModelError(amountSentKey, $"Amount sent must match the total: ₱{pricing.Total:N2}.");
			}

			if (paymentDateTime == null)
			{
				ModelState.AddModelError(paymentDateTimeKey, "Date and time of payment is required.");
			}

			if (paymentProofFile == null && string.IsNullOrEmpty(paymentProofPath))
			{
				ModelState.AddModelError(paymentProofFileKey, "Please upload a payment screenshot or receipt.");
			}
		}

		private static ReservationPaymentDetailsViewModel BuildPaymentDetailsViewModel(Reservation reservation, Vehicle vehicle)
		{
			return new ReservationPaymentDetailsViewModel
			{
				Reservation = reservation,
				Vehicle = vehicle,
				Pricing = ReservationPricingCalculator.Calculate(vehicle, reservation),
				PaymentChannel = reservation.PaymentChannel,
				PayerAccountName = reservation.PayerAccountName,
				PaymentReference = reservation.PaymentReference,
				AmountSent = reservation.AmountSent,
				PaymentDateTime = reservation.PaymentDateTime,
				PaymentProofPath = reservation.PaymentProofPath,
				PaymentNotes = reservation.PaymentNotes
			};
		}

		private static ReservationEditViewModel BuildEditViewModel(Reservation reservation, Vehicle vehicle)
		{
			var pricing = ReservationPricingCalculator.Calculate(vehicle, reservation);
			return new ReservationEditViewModel
			{
				Reservation = reservation,
				Vehicle = vehicle,
				Pricing = pricing,
				PaymentChannel = reservation.PaymentChannel ?? PaymentChannel.GCash,
				PayerAccountName = reservation.PayerAccountName,
				PaymentReference = reservation.PaymentReference,
				AmountSent = reservation.AmountSent ?? pricing.Total,
				PaymentDateTime = reservation.PaymentDateTime,
				PaymentProofPath = reservation.PaymentProofPath,
				PaymentNotes = reservation.PaymentNotes
			};
		}

		private async Task<Reservation?> GetActivePendingReservationAsync(bool requirePaymentSubmitted = false)
		{
			var draft = GetPendingReservationFromSession();
			if (draft == null || draft.ReservationID <= 0)
			{
				ClearPendingReservation();
				return null;
			}

			var entity = await _context.Reservation.AsNoTracking()
				.FirstOrDefaultAsync(r => r.ReservationID == draft.ReservationID);

			if (entity == null || !entity.IsDraft || entity.Status != ReservationStatus.Pending)
			{
				ClearPendingReservation();
				return null;
			}

			var now = DateTime.UtcNow;
			var softLockActive = entity.LockedUntilUtc != null && entity.LockedUntilUtc > now;
			var paymentSubmitted = entity.PaymentChannel != null;

			if (requirePaymentSubmitted)
			{
				if (!paymentSubmitted || !softLockActive)
				{
					return null;
				}

				SavePendingReservation(entity);
				return entity;
			}

			if (!softLockActive)
			{
				ClearPendingReservation();
				return null;
			}

			SavePendingReservation(entity);
			return entity;
		}

		private void SavePendingReservation(Reservation reservation)
		{
			reservation.Vehicle = null;
			reservation.ImageFile = null;
			reservation.PaymentProofFile = null;
			HttpContext.Session.SetString(AdminPendingReservationKey, JsonSerializer.Serialize(reservation));
		}

		private Reservation? GetPendingReservationFromSession()
		{
			var json = HttpContext.Session.GetString(AdminPendingReservationKey);
			if (string.IsNullOrEmpty(json))
			{
				return null;
			}

			return JsonSerializer.Deserialize<Reservation>(json);
		}

		private void ClearPendingReservation()
		{
			HttpContext.Session.Remove(AdminPendingReservationKey);
		}

		private async Task PopulateVehiclesAsync(int? selectedVehicleId)
		{
			var vehicles = await _context.Vehicle
				.OrderBy(v => v.Brand)
				.ThenBy(v => v.Model)
				.ToListAsync();

			ViewBag.Vehicles = new SelectList(
				vehicles.Select(v => new
				{
					v.VehicleId,
					Label = $"{v.Brand} {v.Model} ({v.PlateNumber})"
				}),
				"VehicleId",
				"Label",
				selectedVehicleId);
		}

		private async Task<string> SaveUploadedFileAsync(IFormFile file)
		{
			string folder = Path.Combine(_webHostEnvironment.WebRootPath, "images");
			if (!Directory.Exists(folder))
			{
				Directory.CreateDirectory(folder);
			}

			string fileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
			string filePath = Path.Combine(folder, fileName);

			using (var stream = new FileStream(filePath, FileMode.Create))
			{
				await file.CopyToAsync(stream);
			}

			return fileName;
		}
	}
}
