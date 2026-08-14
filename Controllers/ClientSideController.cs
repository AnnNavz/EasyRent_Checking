using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Data;
using EasyRent_Checking.Models;
using EasyRent_Checking.ViewModels;
using EasyRent_Checking.Services;

namespace EasyRent_Checking.Controllers
{
	public class ClientSideController : Controller
	{
		private readonly EasyRent_CheckingContext _context;
		private readonly IWebHostEnvironment _webHostEnvironment;

		public ClientSideController(EasyRent_CheckingContext context, IWebHostEnvironment webHostEnvironment)
		{
			_context = context;
			_webHostEnvironment = webHostEnvironment;
		}

		// GET: ClientSide/Homepage
		public IActionResult Homepage()
		{
			ViewData["ActiveNav"] = "Home";
			return View("Home");
		}

		// GET: ClientSide/MyBookings
		[Authorize]
		public async Task<IActionResult> MyBookings(string? filter)
		{
			await RentalExpiry.ExpireOverdueReservesAsync(_context);

			var profile = await GetLoggedInCustomerProfileAsync();
			if (profile == null)
			{
				return RedirectToAction("Login", "Account");
			}

			var today = DateOnly.FromDateTime(DateTime.Today);
			var currentFilter = string.IsNullOrWhiteSpace(filter) ? "all" : filter.Trim().ToLowerInvariant();

			var rentals = await _context.Rentals
				.AsNoTracking()
				.Include(r => r.Details)!
					.ThenInclude(d => d!.Vehicle)
				.Where(r => r.CustomerId == profile.CustomerId)
				.OrderByDescending(r => r.RentalId)
				.ToListAsync();

			var rentalIds = rentals.Select(r => r.RentalId).ToList();
			var payments = await _context.Payments
				.AsNoTracking()
				.Where(p => rentalIds.Contains(p.RentalId))
				.OrderByDescending(p => p.PaymentId)
				.ToListAsync();
			var paymentByRental = payments
				.GroupBy(p => p.RentalId)
				.ToDictionary(g => g.Key, g => g.First());

			var transits = await _context.Transits
				.AsNoTracking()
				.Include(t => t.Driver)
				.Include(t => t.Feedback)
				.Where(t => rentalIds.Contains(t.RentalID))
				.ToListAsync();
			var transitByRental = transits.ToDictionary(t => t.RentalID);

			var items = rentals
				.Where(r => r.Details != null)
				.Select(r =>
				{
					var details = r.Details!;
					paymentByRental.TryGetValue(r.RentalId, out var payment);
					transitByRental.TryGetValue(r.RentalId, out var transit);
					var vehicle = details.Vehicle;
					var isPast = details.ReturnDate < today
						|| r.RentalStatus is RentalStatus.Cancelled or RentalStatus.Expired;
					var tripCompleted = transit?.TripStatus == TripStatus.Completed;
					var hasFeedback = transit?.Feedback != null;

					return new MyBookingListItem
					{
						RentalId = r.RentalId,
						BookingLabel = $"BK-{r.RentalId:D6}",
						RentalStatus = r.RentalStatus,
						RentalOption = r.RentalOption,
						VehicleTitle = vehicle != null ? $"{vehicle.Model} {vehicle.Brand}".Trim() : "Vehicle",
						VehicleImagePath = vehicle?.ImagePath,
						PickupDate = details.PickupDate,
						PickupTime = details.PickupTime,
						ReturnTime = details.ReturnTime,
						AmountPaid = payment?.AmountPaid ?? 0m,
						PaymentMethod = payment?.PaymentMethod,
						DriverName = transit?.Driver?.Name,
						IsPast = isPast,
						TripStatus = transit?.TripStatus,
						CanRate = tripCompleted && !hasFeedback,
						HasFeedback = hasFeedback
					};
				})
				.ToList();

			items = currentFilter switch
			{
				"upcoming" => items.Where(i => !i.IsPast).ToList(),
				"completed" => items.Where(i => i.IsPast).ToList(),
				_ => items
			};

			ViewData["ActiveNav"] = "";
			ViewData["CurrentFilter"] = currentFilter is "upcoming" or "completed" ? currentFilter : "all";
			return View(items);
		}

		// GET: ClientSide/MyBookingDetails/5
		[Authorize]
		public async Task<IActionResult> MyBookingDetails(int? id)
		{
			await RentalExpiry.ExpireOverdueReservesAsync(_context);

			if (id == null)
			{
				return NotFound();
			}

			var profile = await GetLoggedInCustomerProfileAsync();
			if (profile == null)
			{
				return RedirectToAction("Login", "Account");
			}

			var rental = await _context.Rentals
				.AsNoTracking()
				.Include(r => r.Details)!
					.ThenInclude(d => d!.Vehicle)
				.FirstOrDefaultAsync(r => r.RentalId == id
					&& r.CustomerId == profile.CustomerId);

			if (rental == null)
			{
				return NotFound();
			}

			var payment = await _context.Payments
				.AsNoTracking()
				.Where(p => p.RentalId == rental.RentalId)
				.OrderByDescending(p => p.PaymentId)
				.FirstOrDefaultAsync();

			var transit = await _context.Transits
				.AsNoTracking()
				.Include(t => t.Driver)
				.Include(t => t.Feedback)
				.FirstOrDefaultAsync(t => t.RentalID == rental.RentalId);

			ViewData["LatestPayment"] = payment;
			ViewData["Transit"] = transit;
			ViewData["Feedback"] = transit?.Feedback;
			ViewData["CanRate"] = transit?.TripStatus == TripStatus.Completed && transit.Feedback == null;
			ViewData["ActiveNav"] = "";
			return View(rental);
		}

		// GET: ClientSide/RateTrip/5
		[Authorize]
		public async Task<IActionResult> RateTrip(int? id)
		{
			var access = await GetCompletedTripForRatingAsync(id);
			if (access.Redirect != null)
			{
				return access.Redirect;
			}

			var transit = access.Transit!;
			var rental = access.Rental!;
			var vehicle = rental.Details?.Vehicle;
			var existing = await _context.Feedbacks
				.AsNoTracking()
				.FirstOrDefaultAsync(f => f.TransitID == transit.TransitID);

			if (existing != null)
			{
				TempData["RateTripInfo"] = "You already submitted a review for this trip.";
				return RedirectToAction(nameof(MyBookingDetails), new { id = rental.RentalId });
			}

			var model = new FeedbackInputModel
			{
				RentalId = rental.RentalId,
				BookingLabel = $"BK-{rental.RentalId:D6}",
				VehicleTitle = vehicle != null ? $"{vehicle.Model} {vehicle.Brand}".Trim() : "Vehicle",
				VehicleImagePath = vehicle?.ImagePath,
				DriverName = transit.Driver?.Name
			};

			ViewData["ActiveNav"] = "";
			return View(model);
		}

		// POST: ClientSide/RateTrip/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		[Authorize]
		public async Task<IActionResult> RateTrip(int id, FeedbackInputModel model)
		{
			model.RentalId = id;

			var access = await GetCompletedTripForRatingAsync(id);
			if (access.Redirect != null)
			{
				return access.Redirect;
			}

			var transit = access.Transit!;
			var rental = access.Rental!;
			var profile = access.Profile!;
			var vehicle = rental.Details?.Vehicle;

			model.BookingLabel = $"BK-{rental.RentalId:D6}";
			model.VehicleTitle = vehicle != null ? $"{vehicle.Model} {vehicle.Brand}".Trim() : "Vehicle";
			model.VehicleImagePath = vehicle?.ImagePath;
			model.DriverName = transit.Driver?.Name;

			var alreadyRated = await _context.Feedbacks.AnyAsync(f => f.TransitID == transit.TransitID);
			if (alreadyRated)
			{
				TempData["RateTripInfo"] = "You already submitted a review for this trip.";
				return RedirectToAction(nameof(MyBookingDetails), new { id });
			}

			if (!ModelState.IsValid)
			{
				ViewData["ActiveNav"] = "";
				return View(model);
			}

			_context.Feedbacks.Add(new Feedback
			{
				TransitID = transit.TransitID,
				CustomerId = profile.CustomerId,
				Rating = model.Rating,
				Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim(),
				CreatedAt = DateTime.Now
			});
			await _context.SaveChangesAsync();

			TempData["RateTripSuccess"] = "Thanks for your feedback!";
			return RedirectToAction(nameof(MyBookingDetails), new { id });
		}

		// GET: ClientSide/Browse
		public async Task<IActionResult> Browse(string? category, string? sortBy)
		{
			var vehiclesQuery = _context.Vehicles.AsQueryable();

			if (!string.IsNullOrEmpty(category) && !string.Equals(category, "All", StringComparison.OrdinalIgnoreCase))
			{
				if (Enum.TryParse(category, true, out VehicleType filterType))
				{
					vehiclesQuery = vehiclesQuery.Where(v => v.Type == filterType);
				}
			}

			vehiclesQuery = sortBy switch
			{
				"Model" => vehiclesQuery.OrderBy(v => v.Model),
				"Brand" => vehiclesQuery.OrderBy(v => v.Brand),
				_ => vehiclesQuery.OrderByDescending(v => v.VehicleId)
			};

			ViewData["CurrentCategory"] = string.IsNullOrEmpty(category) ? "All" : category;
			ViewData["CurrentSort"] = string.IsNullOrEmpty(sortBy) ? "Default" : sortBy;
			ViewData["ActiveNav"] = "Vehicles";

			return View("Vehicles", await vehiclesQuery.ToListAsync());
		}

		// GET: ClientSide/VehicleDetails/5
		public async Task<IActionResult> VehicleDetails(int? id)
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

			var reviews = await _context.Feedbacks
				.AsNoTracking()
				.Include(f => f.Customer)
				.Include(f => f.Transit)
				.Where(f => f.Transit != null && f.Transit.VehicleID == vehicle.VehicleId)
				.OrderByDescending(f => f.CreatedAt)
				.ToListAsync();

			var completedTrips = await _context.Transits
				.AsNoTracking()
				.CountAsync(t => t.VehicleID == vehicle.VehicleId && t.TripStatus == TripStatus.Completed);

			ViewData["VehicleReviews"] = reviews;
			ViewData["ReviewCount"] = reviews.Count;
			ViewData["AverageRating"] = reviews.Count == 0 ? 0d : reviews.Average(f => f.Rating);
			ViewData["CompletedTripsCount"] = completedTrips;
			ViewData["ActiveNav"] = "Vehicles";
			return View(vehicle);
		}

		// GET: ClientSide/Rental/5
		public async Task<IActionResult> Rental(int? id, string? option)
		{
			await RentalExpiry.ExpireOverdueReservesAsync(_context);

			if (id == null)
			{
				return NotFound();
			}

			var loginRedirect = RedirectGuestToBookingLogin(id);
			if (loginRedirect != null)
			{
				return loginRedirect;
			}

			var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.VehicleId == id);
			if (vehicle == null)
			{
				return NotFound();
			}

			if (vehicle.Status != VehicleStatus.Available)
			{
				TempData["RentalError"] = "This vehicle is not available to rent right now.";
				return RedirectToAction(nameof(VehicleDetails), new { id });
			}

			// Legacy links with ?option= still work; choice is primarily made in wizard step 2.
			var rentalOption = RentalOption.Book;
			if (Enum.TryParse(option, ignoreCase: true, out RentalOption parsed)
				&& (parsed == RentalOption.Book || parsed == RentalOption.Reserve))
			{
				rentalOption = parsed;
			}

			ViewData["ActiveNav"] = "Vehicles";
			ViewData["Vehicle"] = vehicle;
			ViewData["HideReserveButton"] = true;
			ViewData["LiveRateSummary"] = true;
			ViewData["TotalFare"] = vehicle.BasePrice;
			ViewData["DepositPercent"] = 5m;
			ViewData["IsReserveFlow"] = false;
			ViewData["PaymentDetailsFromStep"] = 3;
			ViewData["PaymentDetailsBackStep"] = 2;

			var model = new RentalInputModel
			{
				VehicleId = vehicle.VehicleId,
				PickupTime = new TimeOnly(9, 0),
				ReturnTime = new TimeOnly(17, 0),
				PassengerCount = 1,
				Discount = Discount.No,
				RentalStatus = RentalStatus.Pending,
				RentalOption = rentalOption,
				TotalAmount = vehicle.BasePrice,
				PaymentDate = DateTime.Now
			};

			await ApplyLoggedInCustomerAsync(model);

			return View(model);
		}

		// POST: ClientSide/Rental/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Rental(
			int id,
			[Bind("VehicleId,CustomerName,ContactNumber,PickupLocation,DropoffLocation,PickupDate,ReturnDate,PickupTime,ReturnTime,PassengerCount,Notes,Discount,DiscountImageFile,RentalOption,PaymentType,PaymentMethod,TotalAmount,AmountPaid,AccountName,TransactionReference,PaymentDate,ReceiptImageFile,PaymentNotes")] RentalInputModel model)
		{
			await RentalExpiry.ExpireOverdueReservesAsync(_context);

			var loginRedirect = RedirectGuestToBookingLogin(id);
			if (loginRedirect != null)
			{
				return loginRedirect;
			}

			var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.VehicleId == id);
			if (vehicle == null)
			{
				return NotFound();
			}

			model.VehicleId = id;
			model.RentalStatus = RentalStatus.Pending;
			if (model.RentalOption != RentalOption.Book && model.RentalOption != RentalOption.Reserve)
			{
				model.RentalOption = RentalOption.Book;
			}

			var isReserve = model.RentalOption == RentalOption.Reserve;

			// Keep rental contact details tied to the signed-in customer account.
			await ApplyLoggedInCustomerAsync(model);

			var today = DateOnly.FromDateTime(DateTime.Today);
			if (model.PickupDate < today)
			{
				ModelState.AddModelError(nameof(model.PickupDate), "Please select a pick-up date on the calendar.");
			}

			if (model.ReturnDate < model.PickupDate)
			{
				ModelState.AddModelError(nameof(model.ReturnDate), "Return date cannot be earlier than pick-up date.");
			}

			if (model.PassengerCount < 1 || model.PassengerCount > vehicle.PassengersCount)
			{
				ModelState.AddModelError(nameof(model.PassengerCount), $"Passenger count must be between 1 and {vehicle.PassengersCount}.");
			}

			if (model.PickupDate >= today && model.ReturnDate >= model.PickupDate)
			{
				var hasConflict = await _context.RentalDetails.AnyAsync(d =>
					d.VehicleId == id
					&& d.Rental != null
					&& d.Rental.RentalStatus != RentalStatus.Cancelled
					&& d.Rental.RentalStatus != RentalStatus.Expired
					&& d.PickupDate <= model.ReturnDate
					&& d.ReturnDate >= model.PickupDate);

				if (hasConflict)
				{
					ModelState.AddModelError(nameof(model.PickupDate), "Selected dates overlap an existing rental for this vehicle.");
				}
			}

			if (model.Discount == Discount.Yes && model.DiscountImageFile == null)
			{
				ModelState.AddModelError(nameof(model.DiscountImageFile), "Please upload a Senior/PWD ID image.");
			}

			if (!isReserve)
			{
				ValidateClientPayment(model);
			}
			else
			{
				// Clear payment fields so ModelState doesn't keep stale client values.
				model.PaymentType = null;
				model.PaymentMethod = null;
				model.AmountPaid = 0;
				model.AccountName = null;
				model.TransactionReference = null;
				model.ReceiptImageFile = null;
				model.PaymentNotes = null;
			}

			if (ModelState.IsValid)
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

				var rental = new Rental();
				var details = new RentalDetails();
				model.ApplyTo(rental, details);

				var loggedInProfile = await GetLoggedInCustomerProfileAsync();
				if (loggedInProfile != null)
				{
					rental.CustomerId = loggedInProfile.CustomerId;
				}

				if (isReserve)
				{
					rental.PaymentDueAt = DateTime.Now.AddDays(RentalRules.ReservePaymentWindowDays);
					rental.RentalOption = RentalOption.Reserve;
				}
				else
				{
					rental.PaymentDueAt = null;
					rental.RentalOption = RentalOption.Book;
				}

				_context.Rentals.Add(rental);
				await _context.SaveChangesAsync();

				details.RentalID = rental.RentalId;
				_context.RentalDetails.Add(details);

				if (!isReserve)
				{
					string? receiptPath = null;
					if (model.ReceiptImageFile != null)
					{
						receiptPath = await ImageStorage.SaveAsync(
							_webHostEnvironment,
							model.ReceiptImageFile,
							ImageStorage.PaymentReceiptsFolder);
					}

					var computedTotal = CalculateClientFare(vehicle, details);
					var payment = new Payment
					{
						RentalId = rental.RentalId,
						PaymentType = model.PaymentType!.Trim(),
						PaymentMethod = model.PaymentMethod!.Trim(),
						TotalAmount = computedTotal > 0 ? computedTotal : model.TotalAmount,
						AmountPaid = model.AmountPaid,
						AccountName = model.AccountName,
						TransactionReference = model.TransactionReference,
						PaymentDate = model.PaymentDate == default ? DateTime.Now : model.PaymentDate,
						ReceiptImagePath = receiptPath,
						PaymentNotes = model.PaymentNotes
					};
					_context.Payments.Add(payment);
				}

				await _context.SaveChangesAsync();

				TempData["SuccessMessage"] = isReserve
					? $"Your rental is held. Please complete payment by {rental.PaymentDueAt:MMM d, yyyy h:mm tt} or it will expire."
					: "Your booking and payment were submitted. Please wait for admin confirmation.";
				return RedirectToAction(nameof(MyBookingDetails), new { id = rental.RentalId });
			}

			ViewData["ActiveNav"] = "Vehicles";
			ViewData["Vehicle"] = vehicle;
			ViewData["HideReserveButton"] = true;
			ViewData["LiveRateSummary"] = true;
			ViewData["TotalFare"] = model.TotalAmount > 0 ? model.TotalAmount : vehicle.BasePrice;
			ViewData["DepositPercent"] = 5m;
			ViewData["IsReserveFlow"] = isReserve;
			return View(model);
		}

		// GET: ClientSide/PayRental/5
		[Authorize]
		public async Task<IActionResult> PayRental(int? id)
		{
			await RentalExpiry.ExpireOverdueReservesAsync(_context);

			var (rental, vehicle, error) = await LoadPayableReserveAsync(id);
			if (error != null)
			{
				return error;
			}

			var model = RentalInputModel.FromEntities(rental!, rental!.Details);
			model.TotalAmount = CalculateClientFare(vehicle!, rental.Details!);
			model.PaymentDate = DateTime.Now;
			model.PaymentType = "Full Payment";

			ViewData["ActiveNav"] = "";
			ViewData["Vehicle"] = vehicle;
			ViewData["Rental"] = rental;
			ViewData["TotalFare"] = model.TotalAmount;
			ViewData["DepositPercent"] = 5m;
			ViewData["PaymentTypeFromStep"] = 1;
			ViewData["PaymentTypeBackUrl"] = Url.Action(nameof(MyBookingDetails), new { id = rental.RentalId });
			ViewData["PaymentDetailsFromStep"] = 2;
			ViewData["PaymentDetailsBackStep"] = 1;
			return View(model);
		}

		// POST: ClientSide/PayRental/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		[Authorize]
		public async Task<IActionResult> PayRental(
			int id,
			[Bind("RentalId,PaymentType,PaymentMethod,TotalAmount,AmountPaid,AccountName,TransactionReference,PaymentDate,ReceiptImageFile,PaymentNotes")] RentalInputModel model)
		{
			await RentalExpiry.ExpireOverdueReservesAsync(_context);

			var (rental, vehicle, error) = await LoadPayableReserveAsync(id);
			if (error != null)
			{
				return error;
			}

			model.RentalId = id;
			ValidateClientPayment(model);

			if (!ModelState.IsValid)
			{
				var reload = RentalInputModel.FromEntities(rental!, rental!.Details);
				reload.PaymentType = model.PaymentType;
				reload.PaymentMethod = model.PaymentMethod;
				reload.TotalAmount = model.TotalAmount > 0 ? model.TotalAmount : CalculateClientFare(vehicle!, rental.Details!);
				reload.AmountPaid = model.AmountPaid;
				reload.AccountName = model.AccountName;
				reload.TransactionReference = model.TransactionReference;
				reload.PaymentDate = model.PaymentDate == default ? DateTime.Now : model.PaymentDate;
				reload.PaymentNotes = model.PaymentNotes;

				ViewData["ActiveNav"] = "";
				ViewData["Vehicle"] = vehicle;
				ViewData["Rental"] = rental;
				ViewData["TotalFare"] = reload.TotalAmount;
				ViewData["DepositPercent"] = 5m;
				ViewData["PaymentTypeFromStep"] = 1;
				ViewData["PaymentTypeBackUrl"] = Url.Action(nameof(MyBookingDetails), new { id });
				ViewData["PaymentDetailsFromStep"] = 2;
				ViewData["PaymentDetailsBackStep"] = 1;
				return View(reload);
			}

			string? receiptPath = null;
			if (model.ReceiptImageFile != null)
			{
				receiptPath = await ImageStorage.SaveAsync(
					_webHostEnvironment,
					model.ReceiptImageFile,
					ImageStorage.PaymentReceiptsFolder);
			}

			var computedTotal = CalculateClientFare(vehicle!, rental!.Details!);
			_context.Payments.Add(new Payment
			{
				RentalId = rental.RentalId,
				PaymentType = model.PaymentType!.Trim(),
				PaymentMethod = model.PaymentMethod!.Trim(),
				TotalAmount = computedTotal > 0 ? computedTotal : model.TotalAmount,
				AmountPaid = model.AmountPaid,
				AccountName = model.AccountName,
				TransactionReference = model.TransactionReference,
				PaymentDate = model.PaymentDate == default ? DateTime.Now : model.PaymentDate,
				ReceiptImagePath = receiptPath,
				PaymentNotes = model.PaymentNotes
			});

			// Payment submitted — clear hold deadline; stay Pending for admin review.
			rental.PaymentDueAt = null;
			await _context.SaveChangesAsync();

			TempData["SuccessMessage"] = "Payment submitted. Please wait for admin confirmation.";
			return RedirectToAction(nameof(MyBookingDetails), new { id = rental.RentalId });
		}

		private void ValidateClientPayment(RentalInputModel model)
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

			if (model.ReceiptImageFile == null)
			{
				ModelState.AddModelError(nameof(model.ReceiptImageFile), "Please upload your payment receipt.");
			}
		}

		private async Task<(Rental? rental, Vehicle? vehicle, IActionResult? error)> LoadPayableReserveAsync(int? id)
		{
			if (id == null)
			{
				return (null, null, NotFound());
			}

			var profile = await GetLoggedInCustomerProfileAsync();
			if (profile == null)
			{
				return (null, null, RedirectToAction("Login", "Account"));
			}

			var rental = await _context.Rentals
				.Include(r => r.Details)!
					.ThenInclude(d => d!.Vehicle)
				.FirstOrDefaultAsync(r => r.RentalId == id
					&& r.CustomerId == profile.CustomerId);

			if (rental == null)
			{
				return (null, null, NotFound());
			}

			if (rental.RentalStatus == RentalStatus.Expired)
			{
				TempData["RentalError"] = "This rental has expired because payment was not completed in time.";
				return (null, null, RedirectToAction(nameof(MyBookingDetails), new { id }));
			}

			if (rental.RentalOption != RentalOption.Reserve || rental.RentalStatus != RentalStatus.Pending)
			{
				TempData["RentalError"] = "This booking is not awaiting payment.";
				return (null, null, RedirectToAction(nameof(MyBookingDetails), new { id }));
			}

			var hasPayment = await _context.Payments.AnyAsync(p => p.RentalId == rental.RentalId);
			if (hasPayment)
			{
				TempData["RentalError"] = "Payment was already submitted for this rental.";
				return (null, null, RedirectToAction(nameof(MyBookingDetails), new { id }));
			}

			var vehicle = rental.Details?.Vehicle;
			if (vehicle == null)
			{
				return (null, null, NotFound());
			}

			return (rental, vehicle, null);
		}

		private static decimal CalculateClientFare(Vehicle vehicle, RentalDetails details)
		{
			const int baseHours = 8;
			var start = details.PickupDate.ToDateTime(details.PickupTime);
			var end = details.ReturnDate.ToDateTime(details.ReturnTime);
			var hours = end > start ? (int)Math.Round((end - start).TotalHours) : 0;
			var succeedingHours = Math.Max(0, hours - baseHours);
			var subtotal = vehicle.BasePrice + (succeedingHours * vehicle.SucceedingFee);
			var discount = details.Discount == Discount.Yes ? subtotal * 0.10m : 0m;
			return Math.Round(subtotal - discount, 2);
		}

		private IActionResult? RedirectGuestToBookingLogin(int? vehicleId)
		{
			if (User.Identity?.IsAuthenticated == true)
			{
				return null;
			}

			var returnUrl = Url.Action(nameof(Rental), new { id = vehicleId });
			return RedirectToAction("Login", "Account", new { returnUrl });
		}

		private async Task ApplyLoggedInCustomerAsync(RentalInputModel model)
		{
			var profile = await GetLoggedInCustomerProfileAsync();
			if (profile == null)
			{
				ViewData["CustomerFieldsLocked"] = false;
				return;
			}

			model.CustomerName = profile.FullName;
			model.ContactNumber = profile.ContactNumber;
			ViewData["CustomerFieldsLocked"] = true;
		}

		private async Task<(Rental? Rental, Transit? Transit, CustomerProfile? Profile, IActionResult? Redirect)> GetCompletedTripForRatingAsync(int? rentalId)
		{
			if (rentalId == null)
			{
				return (null, null, null, NotFound());
			}

			var profile = await GetLoggedInCustomerProfileAsync();
			if (profile == null)
			{
				return (null, null, null, RedirectToAction("Login", "Account"));
			}

			var rental = await _context.Rentals
				.AsNoTracking()
				.Include(r => r.Details)!
					.ThenInclude(d => d!.Vehicle)
				.FirstOrDefaultAsync(r => r.RentalId == rentalId
					&& r.CustomerId == profile.CustomerId);

			if (rental == null)
			{
				return (null, null, null, NotFound());
			}

			if (rental.RentalStatus is RentalStatus.Cancelled or RentalStatus.Expired)
			{
				TempData["RateTripError"] = "This booking cannot be rated.";
				return (null, null, null, RedirectToAction(nameof(MyBookingDetails), new { id = rentalId }));
			}

			var transit = await _context.Transits
				.AsNoTracking()
				.Include(t => t.Driver)
				.FirstOrDefaultAsync(t => t.RentalID == rental.RentalId);

			if (transit == null || transit.TripStatus != TripStatus.Completed)
			{
				TempData["RateTripError"] = "You can rate a trip only after it is completed.";
				return (null, null, null, RedirectToAction(nameof(MyBookingDetails), new { id = rentalId }));
			}

			return (rental, transit, profile, null);
		}

		private async Task<CustomerProfile?> GetLoggedInCustomerProfileAsync()
		{
			if (User.Identity?.IsAuthenticated != true)
			{
				return null;
			}

			var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (!int.TryParse(userIdValue, out var userId))
			{
				return null;
			}

			return await _context.CustomerProfiles
				.AsNoTracking()
				.FirstOrDefaultAsync(c => c.CustomerId == userId);
		}
	}
}
