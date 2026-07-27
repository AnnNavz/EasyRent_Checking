using System.Text.Json;
using EasyRent_Checking.Data;
using EasyRent_Checking.Models;
using EasyRent_Checking.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyRent_Checking.Controllers
{
	public class ReservationsController : Controller
	{
		private const string PendingReservationKey = "PendingReservation";

		private readonly EasyRent_CheckingContext _context;
		private readonly IWebHostEnvironment _webHostEnvironment;
		private readonly ReservationAvailabilityService _availability;

		public ReservationsController(
			EasyRent_CheckingContext context,
			IWebHostEnvironment webHostEnvironment,
			ReservationAvailabilityService availability)
		{
			_context = context;
			_webHostEnvironment = webHostEnvironment;
			_availability = availability;
		}

		// GET: Reservations/Create?vehicleId=5
		public async Task<IActionResult> Create(int? vehicleId)
		{
			ViewData["ActiveNav"] = "Vehicles";
			ViewData["ReservationStep"] = 1;

			var pending = await GetActivePendingReservationAsync();
			if (pending != null && (vehicleId == null || pending.VehicleId == vehicleId))
			{
				var pendingVehicle = await _context.Vehicle.FirstOrDefaultAsync(v => v.VehicleId == pending.VehicleId);
				if (pendingVehicle == null)
				{
					ClearPendingReservation();
					return NotFound();
				}

				return View("~/Views/ClientSide/Reservation.cshtml", new ReservationFormViewModel
				{
					Vehicle = pendingVehicle,
					Reservation = pending
				});
			}

			if (vehicleId == null)
			{
				return NotFound();
			}

			var vehicle = await _context.Vehicle.FirstOrDefaultAsync(v => v.VehicleId == vehicleId);
			if (vehicle == null)
			{
				return NotFound();
			}

			var model = new ReservationFormViewModel
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
			};

			return View("~/Views/ClientSide/Reservation.cshtml", model);
		}

		// POST: Reservations/Create
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(ReservationFormViewModel model)
		{
			var vehicleId = model.Reservation.VehicleId;
			if (vehicleId <= 0 && int.TryParse(Request.Form["Reservation.VehicleId"], out var postedVehicleId))
			{
				vehicleId = postedVehicleId;
			}

			var vehicle = await _context.Vehicle.FirstOrDefaultAsync(v => v.VehicleId == vehicleId);
			if (vehicle == null)
			{
				return NotFound();
			}

			model.Reservation.VehicleId = vehicle.VehicleId;
			model.Vehicle = vehicle;
			ViewData["ActiveNav"] = "Vehicles";
			ViewData["ReservationStep"] = 1;

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
				return View("~/Views/ClientSide/Reservation.cshtml", model);
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

			// Soft lock: Pending + 30-minute hold on the selected dates.
			entity.Status = ReservationStatus.Pending;
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

		// GET: Reservations/Availability?vehicleId=5&excludeReservationId=1
		[HttpGet]
		public async Task<IActionResult> Availability(int vehicleId, int? excludeReservationId = null)
		{
			var ranges = await _availability.GetBlockedRangesAsync(vehicleId, excludeReservationId);
			return Json(ranges.Select(r => new
			{
				pickupDate = r.PickupDate.ToString("yyyy-MM-dd"),
				returnDate = r.ReturnDate.ToString("yyyy-MM-dd")
			}));
		}

		// GET: Reservations/PaymentDetails
		public async Task<IActionResult> PaymentDetails()
		{
			var reservation = await GetActivePendingReservationAsync();
			if (reservation == null)
			{
				return RedirectToAction(nameof(Create));
			}

			var vehicle = await _context.Vehicle.FirstOrDefaultAsync(v => v.VehicleId == reservation.VehicleId);
			if (vehicle == null)
			{
				ClearPendingReservation();
				return NotFound();
			}

			ViewData["ActiveNav"] = "Vehicles";
			ViewData["ReservationStep"] = 2;

			var model = BuildPaymentDetailsViewModel(reservation, vehicle);
			return View("~/Views/ClientSide/PaymentDetails.cshtml", model);
		}

		// GET: Reservations/PaymentConfirmation
		public async Task<IActionResult> PaymentConfirmation()
		{
			var reservation = await GetActivePendingReservationAsync();
			if (reservation == null)
			{
				return RedirectToAction(nameof(Create));
			}

			var vehicle = await _context.Vehicle.FirstOrDefaultAsync(v => v.VehicleId == reservation.VehicleId);
			if (vehicle == null)
			{
				ClearPendingReservation();
				return NotFound();
			}

			ViewData["ActiveNav"] = "Vehicles";
			ViewData["ReservationStep"] = 3;

			var model = BuildPaymentDetailsViewModel(reservation, vehicle);
			model.PaymentChannel ??= PaymentChannel.GCash;
			model.AmountSent ??= model.Pricing.Total;
			return View("~/Views/ClientSide/PaymentConfirmation.cshtml", model);
		}

		// POST: Reservations/PaymentConfirmation
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> PaymentConfirmation(ReservationPaymentDetailsViewModel model)
		{
			var reservation = await GetActivePendingReservationAsync();
			if (reservation == null)
			{
				return RedirectToAction(nameof(Create));
			}

			var vehicle = await _context.Vehicle.FirstOrDefaultAsync(v => v.VehicleId == reservation.VehicleId);
			if (vehicle == null)
			{
				ClearPendingReservation();
				return NotFound();
			}

			var pricing = ReservationPricingCalculator.Calculate(vehicle, reservation);
			model.Reservation = reservation;
			model.Vehicle = vehicle;
			model.Pricing = pricing;

			ViewData["ActiveNav"] = "Vehicles";
			ViewData["ReservationStep"] = 3;

			ModelState.Remove("Reservation");
			ModelState.Remove("Vehicle");
			ModelState.Remove("Pricing");

			ValidatePaymentConfirmation(model, pricing);

			if (!ModelState.IsValid)
			{
				return View("~/Views/ClientSide/PaymentConfirmation.cshtml", model);
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
			// Payment submitted: keep dates blocked for admin review (no soft-lock expiry).
			entity.LockedUntilUtc = null;

			await _context.SaveChangesAsync();
			SavePendingReservation(entity);

			return RedirectToAction(nameof(BookingSummary));
		}

		// GET: Reservations/BookingSummary
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

			ViewData["ActiveNav"] = "Vehicles";
			ViewData["ReservationStep"] = 4;

			var model = BuildPaymentDetailsViewModel(reservation, vehicle);
			return View("~/Views/ClientSide/BookingSummary.cshtml", model);
		}

		// POST: Reservations/ConfirmBooking
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ConfirmBooking()
		{
			var reservation = await GetActivePendingReservationAsync(requirePaymentSubmitted: true);
			if (reservation == null)
			{
				return RedirectToAction(nameof(Create));
			}

			ClearPendingReservation();
			TempData["BookingSubmitted"] = true;
			TempData["BookingReference"] = reservation.ReservationID;

			return RedirectToAction("Browse", "Vehicles");
		}

		private void ValidatePaymentConfirmation(ReservationPaymentDetailsViewModel model, ReservationPricingSummary pricing)
		{
			if (model.PaymentChannel == null)
			{
				return;
			}

			if (model.PaymentChannel == PaymentChannel.WalkIn)
			{
				return;
			}

			if (string.IsNullOrWhiteSpace(model.PayerAccountName))
			{
				ModelState.AddModelError(nameof(model.PayerAccountName), "Account name is required.");
			}

			if (string.IsNullOrWhiteSpace(model.PaymentReference))
			{
				ModelState.AddModelError(nameof(model.PaymentReference), "Reference / transaction number is required.");
			}

			if (model.AmountSent == null)
			{
				ModelState.AddModelError(nameof(model.AmountSent), "Amount sent is required.");
			}
			else if (Math.Abs(model.AmountSent.Value - pricing.Total) > 0.009m)
			{
				ModelState.AddModelError(nameof(model.AmountSent), $"Amount sent must match the total: ₱{pricing.Total:N2}.");
			}

			if (model.PaymentDateTime == null)
			{
				ModelState.AddModelError(nameof(model.PaymentDateTime), "Date and time of payment is required.");
			}

			if (model.PaymentProofFile == null && string.IsNullOrEmpty(model.PaymentProofPath))
			{
				ModelState.AddModelError(nameof(model.PaymentProofFile), "Please upload a payment screenshot or receipt.");
			}
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

		/// <summary>
		/// Loads the session draft and confirms the DB row is still an active hold
		/// (soft-locked Pending, or payment-submitted Pending awaiting admin).
		/// </summary>
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

			if (entity == null || entity.Status != ReservationStatus.Pending)
			{
				ClearPendingReservation();
				return null;
			}

			var now = DateTime.UtcNow;
			var softLockActive = entity.LockedUntilUtc != null && entity.LockedUntilUtc > now;
			var awaitingReview = entity.LockedUntilUtc == null && entity.PaymentChannel != null;

			if (!softLockActive && !awaitingReview)
			{
				ClearPendingReservation();
				return null;
			}

			if (requirePaymentSubmitted && !awaitingReview)
			{
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
			HttpContext.Session.SetString(PendingReservationKey, JsonSerializer.Serialize(reservation));
		}

		private Reservation? GetPendingReservationFromSession()
		{
			var json = HttpContext.Session.GetString(PendingReservationKey);
			if (string.IsNullOrEmpty(json))
			{
				return null;
			}

			return JsonSerializer.Deserialize<Reservation>(json);
		}

		private void ClearPendingReservation()
		{
			HttpContext.Session.Remove(PendingReservationKey);
		}
	}
}
