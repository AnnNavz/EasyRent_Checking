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
		private readonly IEmailSender _emailSender;
		private const string ContactInboxEmail = "ccs.capstoneabadteam@gmail.com";

		public ClientSideController(
			EasyRent_CheckingContext context,
			IWebHostEnvironment webHostEnvironment,
			IEmailSender emailSender)
		{
			_context = context;
			_webHostEnvironment = webHostEnvironment;
			_emailSender = emailSender;
		}

		// GET: ClientSide/HowItWorks
		public IActionResult HowItWorks()
		{
			ViewData["ActiveNav"] = "HowItWorks";
			return View();
		}

		// GET: ClientSide/AboutUs
		public IActionResult AboutUs()
		{
			ViewData["ActiveNav"] = "AboutUs";
			return View();
		}

		// GET: ClientSide/PrivacyPolicy
		public IActionResult PrivacyPolicy()
		{
			ViewData["ActiveNav"] = "";
			ViewData["Title"] = "Privacy Policy";
			return View();
		}

		// GET: ClientSide/TermsConditions
		public IActionResult TermsConditions()
		{
			ViewData["ActiveNav"] = "";
			ViewData["Title"] = "Terms & Conditions";
			return View();
		}

		// GET: ClientSide/ContactUs
		public async Task<IActionResult> ContactUs()
		{
			ViewData["ActiveNav"] = "ContactUs";
			var model = new ContactUsInputModel();
			var profile = await GetLoggedInCustomerProfileAsync();
			if (profile != null)
			{
				model.FullName = profile.FullName;
				model.Email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
			}

			return View(model);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ContactUs(ContactUsInputModel model)
		{
			ViewData["ActiveNav"] = "ContactUs";
			if (!ModelState.IsValid)
			{
				return View(model);
			}

			var safeName = System.Net.WebUtility.HtmlEncode(model.FullName.Trim());
			var safeEmail = System.Net.WebUtility.HtmlEncode(model.Email.Trim());
			var safeMessage = System.Net.WebUtility.HtmlEncode(model.Message.Trim()).Replace("\n", "<br />");
			var body =
				$"<p><strong>Name:</strong> {safeName}</p>" +
				$"<p><strong>Email:</strong> {safeEmail}</p>" +
				$"<p><strong>Message:</strong></p><p>{safeMessage}</p>";

			try
			{
				await _emailSender.SendAsync(
					ContactInboxEmail,
					$"EasyRent inquiry from {model.FullName.Trim()}",
					body);
				TempData["SuccessMessage"] = "Your message has been sent. We’ll get back to you soon.";
				return RedirectToAction(nameof(ContactUs));
			}
			catch
			{
				ModelState.AddModelError(string.Empty, "We could not send your message right now. Please try again or use the contact details on this page.");
				return View(model);
			}
		}

		// GET: ClientSide/Homepage
		public async Task<IActionResult> Homepage()
		{
			ViewData["ActiveNav"] = "Home";

			var vehicles = await _context.Vehicles.AsNoTracking().ToListAsync();

			var ratingRows = await _context.Feedbacks
				.AsNoTracking()
				.Where(f => f.Transit != null)
				.Select(f => new
				{
					VehicleId = f.Transit!.VehicleID,
					Score = (f.VehicleComfort + f.VehiclePerformance + f.VehicleSafety) / 3.0
				})
				.ToListAsync();

			var ratingByVehicle = ratingRows
				.GroupBy(r => r.VehicleId)
				.ToDictionary(g => g.Key, g => g.Average(x => x.Score));

			var popularVehicles = vehicles
				.OrderByDescending(v => ratingByVehicle.GetValueOrDefault(v.VehicleId))
				.ThenByDescending(v => v.VehicleId)
				.Take(3)
				.Select(v => new HomeVehicleCard
				{
					VehicleId = v.VehicleId,
					Model = v.Model,
					TypeLabel = VehicleTypes.DisplayUpper(v.Type),
					PassengersCount = v.PassengersCount,
					BasePrice = v.BasePrice,
					ImagePath = v.ImagePath,
					Rating = ratingByVehicle.TryGetValue(v.VehicleId, out var score)
						? Math.Round(score, 1)
						: null
				})
				.ToList();

			var reviews = await _context.Feedbacks
				.AsNoTracking()
				.Include(f => f.Customer)
				.Where(f => f.Comment != null && f.Comment != "")
				.OrderByDescending(f => f.CreatedAt)
				.Take(3)
				.ToListAsync();

			var testimonials = reviews
				.Select(f => new HomeTestimonial
				{
					CustomerName = string.IsNullOrWhiteSpace(f.Customer?.FullName) ? "Customer" : f.Customer.FullName,
					Comment = f.Comment!.Trim(),
					Rating = Math.Round((f.VehicleComfort + f.VehiclePerformance + f.VehicleSafety) / 3.0, 1)
				})
				.ToList();

			var capacities = vehicles
				.Select(v => v.PassengersCount)
				.Distinct()
				.OrderBy(c => c)
				.ToList();

			if (capacities.Count == 0)
			{
				capacities = new List<int> { 4, 7, 8, 12 };
			}

			return View("Home", new HomeViewModel
			{
				CapacityOptions = capacities,
				PopularVehicles = popularVehicles,
				Testimonials = testimonials
			});
		}

		// GET: ClientSide/MyBookings
		[Authorize]
		public async Task<IActionResult> MyBookings(string? filter, string? sort)
		{
			await RentalExpiry.ExpireOverdueReservesAsync(_context);

			var profile = await GetLoggedInCustomerProfileAsync();
			if (profile == null)
			{
				return RedirectToAction("Login", "Account");
			}

			var today = DateOnly.FromDateTime(DateTime.Today);
			var currentFilter = string.IsNullOrWhiteSpace(filter) ? "all" : filter.Trim().ToLowerInvariant();
			var currentSort = string.IsNullOrWhiteSpace(sort) ? "newest" : sort.Trim().ToLowerInvariant();

			var rentals = await _context.Rentals
				.AsNoTracking()
				.Include(r => r.Details)!
					.ThenInclude(d => d!.Vehicle)
				.Include(r => r.RentalVehicles)
					.ThenInclude(rv => rv.Vehicle)
				.Where(r => r.CustomerId == profile.CustomerId)
				.OrderByDescending(r => r.RentalId)
				.ToListAsync();

			var rentalIds = rentals.Select(r => r.RentalId).ToList();
			var payments = await _context.Payments
				.AsNoTracking()
				.Where(p => rentalIds.Contains(p.RentalId))
				.OrderByDescending(p => p.PaymentId)
				.ToListAsync();
			var paymentsByRental = payments
				.GroupBy(p => p.RentalId)
				.ToDictionary(g => g.Key, g => g.ToList());

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
					paymentsByRental.TryGetValue(r.RentalId, out var rentalPayments);
					rentalPayments ??= new List<Payment>();
					var latestPayment = rentalPayments.FirstOrDefault();
					transitByRental.TryGetValue(r.RentalId, out var transit);
					var lineVehicles = (r.RentalVehicles ?? Enumerable.Empty<RentalVehicle>())
						.Where(rv => rv.Vehicle != null)
						.OrderBy(rv => rv.SortOrder)
						.Select(rv => rv.Vehicle!)
						.ToList();
					var vehicle = lineVehicles.FirstOrDefault() ?? details.Vehicle;
					var isPast = details.ReturnDate < today
						|| r.RentalStatus is RentalStatus.Cancelled or RentalStatus.Expired;
					var tripCompleted = transit?.TripStatus == TripStatus.Completed;
					var hasFeedback = transit?.Feedback != null;
					var ledger = SummarizePayments(rentalPayments, vehicle, details, r.TotalAmount);
					var vehicleTitle = lineVehicles.Count > 1
						? $"{lineVehicles.Count} vehicles"
						: (vehicle?.Model ?? "Vehicle");
					var vehicleTypeLabel = lineVehicles.Count > 1
						? string.Join(", ", lineVehicles.Select(v => $"{v.Brand} {v.Model}"))
						: VehicleTypes.DisplayUpper(vehicle?.Type);

					return new MyBookingListItem
					{
						RentalId = r.RentalId,
						BookingLabel = $"BK-{r.RentalId:D6}",
						RentalStatus = r.RentalStatus,
						RentalOption = r.RentalOption,
						VehicleTitle = vehicleTitle,
						VehicleTypeLabel = vehicleTypeLabel,
						PassengersCount = lineVehicles.Count > 0
							? lineVehicles.Max(v => v.PassengersCount)
							: (vehicle?.PassengersCount ?? 0),
						VehicleImagePath = vehicle?.ImagePath,
						PickupDate = details.PickupDate,
						PickupTime = details.PickupTime,
						ReturnTime = details.ReturnTime,
						TotalFare = ledger.TotalFare,
						AmountPaid = ledger.AmountPaid,
						RemainingBalance = r.RentalStatus is RentalStatus.Cancelled or RentalStatus.Expired
							? 0m
							: ledger.RemainingBalance,
						PaymentMethod = latestPayment?.PaymentMethod,
						DriverName = transit?.Driver?.Name,
						DriverInitials = GetInitials(transit?.Driver?.Name),
						IsPast = isPast,
						TripStatus = transit?.TripStatus,
						CanRate = tripCompleted && !hasFeedback,
						HasFeedback = hasFeedback,
						CanPay = r.RentalOption == RentalOption.Reserve
							&& r.RentalStatus == RentalStatus.Pending
							&& latestPayment == null
					};
				})
				.ToList();

			items = currentFilter switch
			{
				"upcoming" => items.Where(i => !i.IsPast).ToList(),
				"completed" => items.Where(i => i.IsPast).ToList(),
				_ => items
			};

			items = currentSort switch
			{
				"oldest" => items.OrderBy(i => i.RentalId).ToList(),
				"pickup" => items.OrderBy(i => i.PickupDate).ThenBy(i => i.PickupTime).ToList(),
				_ => items.OrderByDescending(i => i.RentalId).ToList()
			};

			ViewData["ActiveNav"] = "";
			ViewData["HubActive"] = "bookings";
			return View(new MyBookingsPageViewModel
			{
				FullName = profile.FullName,
				AvatarInitials = GetInitials(profile.FullName),
				ProfileImagePath = profile.ProfileImagePath,
				Filter = currentFilter is "upcoming" or "completed" ? currentFilter : "all",
				Sort = currentSort is "oldest" or "pickup" ? currentSort : "newest",
				Items = items
			});
		}

		// GET: ClientSide/PaymentHistory
		[Authorize]
		public async Task<IActionResult> PaymentHistory(string? sort, string? filter, int page = 1)
		{
			await RentalExpiry.ExpireOverdueReservesAsync(_context);

			var profile = await GetLoggedInCustomerProfileAsync();
			if (profile == null)
			{
				return RedirectToAction("Login", "Account");
			}

			var currentSort = sort is "oldest" or "amount" ? sort : "newest";
			var currentFilter = filter is "gcash" or "bdo" or "walkin" ? filter : "all";
			const int pageSize = 5;

			var rentals = await _context.Rentals
				.AsNoTracking()
				.Include(r => r.Details)!
					.ThenInclude(d => d!.Vehicle)
				.Where(r => r.CustomerId == profile.CustomerId)
				.ToListAsync();

			var rentalIds = rentals.Select(r => r.RentalId).ToList();
			var payments = await _context.Payments
				.AsNoTracking()
				.Where(p => rentalIds.Contains(p.RentalId))
				.OrderByDescending(p => p.PaymentDate)
				.ThenByDescending(p => p.PaymentId)
				.ToListAsync();

			var rentalById = rentals.ToDictionary(r => r.RentalId);
			var rows = new List<PaymentHistoryRow>();
			foreach (var payment in payments)
			{
				rentalById.TryGetValue(payment.RentalId, out var rental);
				var vehicle = rental?.Details?.Vehicle;
				rows.Add(ToPaymentHistoryRow(payment, rental, vehicle));
			}

			decimal outstanding = 0m;
			foreach (var rental in rentals)
			{
				if (rental.RentalStatus is RentalStatus.Cancelled or RentalStatus.Expired)
				{
					continue;
				}

				var rentalPayments = payments.Where(p => p.RentalId == rental.RentalId).ToList();
				var ledger = SummarizePayments(rentalPayments, rental.Details?.Vehicle, rental.Details, rental.TotalAmount);
				outstanding += ledger.RemainingBalance;
			}

			var filtered = currentFilter switch
			{
				"gcash" => rows.Where(r => r.PaymentMethod.Equals("GCash", StringComparison.OrdinalIgnoreCase)).ToList(),
				"bdo" => rows.Where(r => r.PaymentMethod.Equals("BDO", StringComparison.OrdinalIgnoreCase)).ToList(),
				"walkin" => rows.Where(r => r.PaymentMethod.Equals("Walk-in", StringComparison.OrdinalIgnoreCase)).ToList(),
				_ => rows
			};

			filtered = currentSort switch
			{
				"oldest" => filtered.OrderBy(r => r.PaymentDate).ThenBy(r => r.PaymentId).ToList(),
				"amount" => filtered.OrderByDescending(r => r.AmountPaid).ThenByDescending(r => r.PaymentDate).ToList(),
				_ => filtered.OrderByDescending(r => r.PaymentDate).ThenByDescending(r => r.PaymentId).ToList()
			};

			var totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)pageSize));
			if (page < 1)
			{
				page = 1;
			}
			else if (page > totalPages)
			{
				page = totalPages;
			}

			var paged = filtered
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.ToList();

			ViewData["ActiveNav"] = "";
			ViewData["HubActive"] = "payments";
			return View(new ClientPaymentHistoryViewModel
			{
				TotalPaid = payments.Sum(p => p.AmountPaid),
				OutstandingBalance = outstanding,
				PaymentCount = payments.Count,
				Sort = currentSort,
				Filter = currentFilter,
				Page = page,
				TotalPages = totalPages,
				Payments = paged
			});
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
				.Include(r => r.RentalVehicles)
					.ThenInclude(rv => rv.Vehicle)
				.FirstOrDefaultAsync(r => r.RentalId == id
					&& r.CustomerId == profile.CustomerId);

			if (rental == null)
			{
				return NotFound();
			}

			var payments = await _context.Payments
				.AsNoTracking()
				.Where(p => p.RentalId == rental.RentalId)
				.OrderByDescending(p => p.PaymentDate)
				.ThenByDescending(p => p.PaymentId)
				.ToListAsync();

			var transit = await _context.Transits
				.AsNoTracking()
				.Include(t => t.Driver)
				.Include(t => t.Feedback)
				.FirstOrDefaultAsync(t => t.RentalID == rental.RentalId);

			var primaryVehicle = rental.RentalVehicles?
				.OrderBy(rv => rv.SortOrder)
				.Select(rv => rv.Vehicle)
				.FirstOrDefault()
				?? rental.Details?.Vehicle;

			var paymentSummary = SummarizePayments(payments, primaryVehicle, rental.Details, rental.TotalAmount);
			paymentSummary.Payments = payments
				.Select(p => ToPaymentHistoryRow(p, rental, primaryVehicle))
				.ToList();

			ViewData["LatestPayment"] = payments.FirstOrDefault();
			ViewData["PaymentSummary"] = paymentSummary;
			ViewData["Transit"] = transit;
			ViewData["Feedback"] = transit?.Feedback;
			ViewData["CanRate"] = transit?.TripStatus == TripStatus.Completed && transit.Feedback == null;
			ViewData["CanCancel"] = CanCustomerCancel(rental, transit);
			ViewData["CancellationFee"] = GetCancellationFee(transit);
			ViewData["ActiveNav"] = "";
			ViewData["HubActive"] = "bookings";
			return View(rental);
		}

		// POST: ClientSide/CancelBooking/5
		[Authorize]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CancelBooking(int id)
		{
			var profile = await GetLoggedInCustomerProfileAsync();
			if (profile == null)
			{
				return RedirectToAction("Login", "Account");
			}

			var rental = await _context.Rentals
				.Include(r => r.Details)
				.FirstOrDefaultAsync(r => r.RentalId == id && r.CustomerId == profile.CustomerId);

			if (rental == null)
			{
				return NotFound();
			}

			var transit = await _context.Transits
				.FirstOrDefaultAsync(t => t.RentalID == rental.RentalId);

			if (!CanCustomerCancel(rental, transit))
			{
				TempData["CancelError"] = "This booking can no longer be cancelled.";
				return RedirectToAction(nameof(MyBookingDetails), new { id });
			}

			var fee = GetCancellationFee(transit);
			rental.RentalStatus = RentalStatus.Cancelled;
			rental.CancelledAt = DateTime.Now;
			rental.CancellationFee = fee;

			if (transit != null && transit.TripStatus is not TripStatus.Completed)
			{
				transit.TripStatus = TripStatus.Cancelled;
			}

			await _context.SaveChangesAsync();

			TempData["SuccessMessage"] = fee > 0
				? $"Booking cancelled. A ₱{fee:N2} cancellation fee applies because the vehicle was already out in transit. Please settle this fee with EasyRent."
				: "Booking cancelled at no charge.";

			return RedirectToAction(nameof(MyBookingDetails), new { id });
		}

		private static bool CanCustomerCancel(Rental rental, Transit? transit)
		{
			if (rental.RentalStatus is RentalStatus.Cancelled or RentalStatus.Expired)
			{
				return false;
			}

			if (transit?.TripStatus == TripStatus.Completed)
			{
				return false;
			}

			return rental.RentalStatus is RentalStatus.Pending or RentalStatus.Approved;
		}

		private static decimal GetCancellationFee(Transit? transit)
			=> transit?.TripStatus == TripStatus.InTransit
				? RentalRules.CancellationFeeWhenInTransit
				: 0m;

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
			var existing = await _context.Feedbacks
				.AsNoTracking()
				.FirstOrDefaultAsync(f => f.TransitID == transit.TransitID);

			if (existing != null)
			{
				TempData["RateTripInfo"] = "You already submitted a review for this trip.";
				return RedirectToAction(nameof(MyBookingDetails), new { id = rental.RentalId });
			}

			var model = new FeedbackInputModel { RentalId = rental.RentalId };
			PopulateRateTripModel(model, rental, transit);

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

			PopulateRateTripModel(model, rental, transit);
			ModelState.Clear();
			TryValidateModel(model);

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
				Rating = RatingScale.OverallStars(model.VehicleComfort, model.VehiclePerformance, model.VehicleSafety),
				VehicleComfort = model.VehicleComfort,
				VehiclePerformance = model.VehiclePerformance,
				VehicleSafety = model.VehicleSafety,
				DriverProfessionalism = model.HasDriver ? model.DriverProfessionalism : null,
				DriverDriving = model.HasDriver ? model.DriverDriving : null,
				DriverCourtesy = model.HasDriver ? model.DriverCourtesy : null,
				Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim(),
				CreatedAt = DateTime.Now
			});
			await _context.SaveChangesAsync();

			TempData["SuccessMessage"] = "Thanks for your feedback!";
			return RedirectToAction(nameof(MyBookingDetails), new { id });
		}

		// GET: ClientSide/Browse
		public async Task<IActionResult> Browse(string? category, string? sortBy, int? capacity, DateOnly? pickupDate, DateOnly? returnDate, string? q, int page = 1)
		{
			var vehiclesQuery = _context.Vehicles.AsQueryable();

			if (!string.IsNullOrEmpty(category) && !string.Equals(category, "All", StringComparison.OrdinalIgnoreCase))
			{
				vehiclesQuery = vehiclesQuery.Where(v => v.Type == category);
			}

			if (capacity.HasValue && capacity.Value > 0)
			{
				vehiclesQuery = vehiclesQuery.Where(v => v.PassengersCount >= capacity.Value);
			}

			if (!string.IsNullOrWhiteSpace(q))
			{
				var term = q.Trim();
				vehiclesQuery = vehiclesQuery.Where(v => v.Model.Contains(term) || v.Brand.Contains(term));
			}

			var startDate = pickupDate;
			var endDate = returnDate ?? pickupDate;
			if (startDate.HasValue && endDate.HasValue && endDate >= startDate)
			{
				var busyVehicleIds = await (
					from details in _context.RentalDetails
					join rental in _context.Rentals on details.RentalID equals rental.RentalId
					where rental.RentalStatus != RentalStatus.Cancelled
						&& rental.RentalStatus != RentalStatus.Expired
						&& details.PickupDate <= endDate.Value
						&& details.ReturnDate >= startDate.Value
					select details.VehicleId
				).Distinct().ToListAsync();

				if (busyVehicleIds.Count > 0)
				{
					vehiclesQuery = vehiclesQuery.Where(v => !busyVehicleIds.Contains(v.VehicleId));
				}
			}

			vehiclesQuery = sortBy switch
			{
				"Model" => vehiclesQuery.OrderBy(v => v.Model),
				"Brand" => vehiclesQuery.OrderBy(v => v.Brand),
				"Price" => vehiclesQuery.OrderBy(v => v.BasePrice),
				_ => vehiclesQuery.OrderByDescending(v => v.VehicleId)
			};

			const int pageSize = 6;
			var totalCount = await vehiclesQuery.CountAsync();
			var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
			page = Math.Clamp(page, 1, totalPages);

			var vehicles = await vehiclesQuery
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();

			var ratingRows = await _context.Feedbacks
				.AsNoTracking()
				.Where(f => f.Transit != null)
				.Select(f => new
				{
					VehicleId = f.Transit!.VehicleID,
					Score = (f.VehicleComfort + f.VehiclePerformance + f.VehicleSafety) / 3.0
				})
				.ToListAsync();

			var ratingByVehicle = ratingRows
				.GroupBy(r => r.VehicleId)
				.ToDictionary(g => g.Key, g => g.Average(x => x.Score));

			var cards = vehicles.Select(v => new HomeVehicleCard
			{
				VehicleId = v.VehicleId,
				Model = v.Model,
				TypeLabel = VehicleTypes.DisplayUpper(v.Type),
				PassengersCount = v.PassengersCount,
				BasePrice = v.BasePrice,
				SucceedingFee = v.SucceedingFee,
				ImagePath = v.ImagePath,
				Rating = ratingByVehicle.TryGetValue(v.VehicleId, out var score)
					? Math.Round(score, 1)
					: null
			}).ToList();

			var capacities = await _context.Vehicles
				.AsNoTracking()
				.Select(v => v.PassengersCount)
				.Distinct()
				.OrderBy(c => c)
				.ToListAsync();

			if (capacities.Count == 0)
			{
				capacities = new List<int> { 4, 7, 8, 12 };
			}

			var typeNames = await _context.Vehicles.AsNoTracking()
				.Select(v => v.Type)
				.Where(t => t != null && t != "")
				.Distinct()
				.ToListAsync();

			ViewData["ActiveNav"] = "Vehicles";

			return View("Vehicles", new BrowseVehiclesViewModel
			{
				Vehicles = cards,
				CapacityOptions = capacities,
				Categories = VehicleTypes.BrowseCategories(typeNames),
				CurrentCategory = string.IsNullOrEmpty(category) ? "All" : category,
				CurrentSort = string.IsNullOrEmpty(sortBy) ? "Default" : sortBy,
				Capacity = capacity,
				PickupDate = pickupDate,
				ReturnDate = returnDate,
				Query = string.IsNullOrWhiteSpace(q) ? null : q.Trim(),
				Page = page,
				TotalPages = totalPages,
				TotalCount = totalCount
			});
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

			var averageRating = reviews.Count == 0
				? 0d
				: reviews.Average(f => (f.VehicleComfort + f.VehiclePerformance + f.VehicleSafety) / 3.0);

			var driverReviews = reviews.Where(f => f.HasDriverRatings).ToList();

			ViewData["VehicleReviews"] = reviews;
			ViewData["ReviewCount"] = reviews.Count;
			ViewData["AverageRating"] = averageRating;
			ViewData["ProfessionalismTen"] = RatingScale.ToTen(driverReviews.Select(f => f.DriverProfessionalism!.Value));
			ViewData["DrivingTen"] = RatingScale.ToTen(driverReviews.Select(f => f.DriverDriving!.Value));
			ViewData["CourtesyTen"] = RatingScale.ToTen(driverReviews.Select(f => f.DriverCourtesy!.Value));
			ViewData["HasDriverRatings"] = driverReviews.Count > 0;
			ViewData["CompletedTripsCount"] = completedTrips;
			ViewData["ActiveNav"] = "Vehicles";

			var profile = await GetLoggedInCustomerProfileAsync();
			ViewData["IsFavorite"] = profile != null
				&& await _context.VehicleFavorites.AnyAsync(f =>
					f.CustomerId == profile.CustomerId && f.VehicleId == vehicle.VehicleId);

			return View(vehicle);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ToggleFavorite(int id)
		{
			var detailsUrl = Url.Action(nameof(VehicleDetails), new { id });
			var loginUrl = Url.Action("Login", "Account", new { returnUrl = detailsUrl });

			var profile = await GetLoggedInCustomerProfileAsync();
			if (profile == null)
			{
				return Json(new { ok = false, loginRequired = true, loginUrl });
			}

			var vehicleExists = await _context.Vehicles.AnyAsync(v => v.VehicleId == id);
			if (!vehicleExists)
			{
				return NotFound();
			}

			var existing = await _context.VehicleFavorites
				.FirstOrDefaultAsync(f => f.CustomerId == profile.CustomerId && f.VehicleId == id);

			var isFavorite = existing == null;
			if (existing == null)
			{
				_context.VehicleFavorites.Add(new VehicleFavorite
				{
					CustomerId = profile.CustomerId,
					VehicleId = id,
					CreatedAt = DateTime.Now
				});
			}
			else
			{
				_context.VehicleFavorites.Remove(existing);
			}

			await _context.SaveChangesAsync();
			return Json(new
			{
				ok = true,
				isFavorite,
				message = isFavorite
					? "Vehicle saved to your favorites."
					: "Vehicle removed from your favorites."
			});
		}

		[Authorize]
		public async Task<IActionResult> MyFavorites(string? sort, string? filter)
		{
			var profile = await GetLoggedInCustomerProfileAsync();
			if (profile == null)
			{
				return RedirectToAction("Login", "Account");
			}

			var currentSort = sort is "oldest" or "price-asc" or "price-desc" or "name" ? sort : "newest";
			var currentFilter = string.IsNullOrWhiteSpace(filter) ? "all" : filter.Trim().ToLowerInvariant();

			var favorites = await _context.VehicleFavorites
				.AsNoTracking()
				.Where(f => f.CustomerId == profile.CustomerId && f.Vehicle != null)
				.Select(f => new { f.CreatedAt, Vehicle = f.Vehicle! })
				.ToListAsync();

			var ratingRows = await _context.Feedbacks
				.AsNoTracking()
				.Where(f => f.Transit != null)
				.Select(f => new
				{
					VehicleId = f.Transit!.VehicleID,
					Score = (f.VehicleComfort + f.VehiclePerformance + f.VehicleSafety) / 3.0
				})
				.ToListAsync();

			var ratingByVehicle = ratingRows
				.GroupBy(r => r.VehicleId)
				.ToDictionary(g => g.Key, g => g.Average(x => x.Score));

			var cards = favorites.Select(f => new
			{
				f.CreatedAt,
				Card = new HomeVehicleCard
				{
					VehicleId = f.Vehicle.VehicleId,
					Model = f.Vehicle.Model,
					TypeLabel = VehicleTypes.DisplayUpper(f.Vehicle.Type),
					PassengersCount = f.Vehicle.PassengersCount,
					BasePrice = f.Vehicle.BasePrice,
					SucceedingFee = f.Vehicle.SucceedingFee,
					ImagePath = f.Vehicle.ImagePath,
					Rating = ratingByVehicle.TryGetValue(f.Vehicle.VehicleId, out var score)
						? Math.Round(score, 1)
						: null
				}
			}).ToList();

			var typeFilters = cards
				.Select(c => c.Card.TypeLabel)
				.Where(t => !string.IsNullOrWhiteSpace(t))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
				.ToList();

			if (currentFilter != "all"
				&& !typeFilters.Any(t => t.Equals(currentFilter, StringComparison.OrdinalIgnoreCase)))
			{
				currentFilter = "all";
			}

			var filtered = currentFilter == "all"
				? cards
				: cards.Where(c => c.Card.TypeLabel.Equals(currentFilter, StringComparison.OrdinalIgnoreCase)).ToList();

			filtered = currentSort switch
			{
				"oldest" => filtered.OrderBy(c => c.CreatedAt).ToList(),
				"price-asc" => filtered.OrderBy(c => c.Card.BasePrice).ThenBy(c => c.Card.Model).ToList(),
				"price-desc" => filtered.OrderByDescending(c => c.Card.BasePrice).ThenBy(c => c.Card.Model).ToList(),
				"name" => filtered.OrderBy(c => c.Card.Model).ToList(),
				_ => filtered.OrderByDescending(c => c.CreatedAt).ToList()
			};

			ViewData["HubActive"] = "favorites";
			return View(new SavedVehiclesPageViewModel
			{
				Items = filtered.Select(c => c.Card).ToList(),
				Sort = currentSort,
				Filter = currentFilter,
				TypeFilters = typeFilters
			});
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
			ViewData["AvailableVehicles"] = await GetAvailableVehiclesForBookingAsync(excludeVehicleId: null);
			ViewData["HideReserveButton"] = true;
			ViewData["LiveRateSummary"] = true;
			ViewData["TotalFare"] = vehicle.BasePrice;
			ViewData["DepositPercent"] = 5m;
			ViewData["IsReserveFlow"] = false;
			ViewData["PaymentDetailsFromStep"] = 3;
			ViewData["PaymentDetailsBackStep"] = 2;
			ViewData["InitialUnavailableDates"] = await GetUnavailableDatesAsync(vehicle.VehicleId);

			var model = new RentalInputModel
			{
				VehicleId = vehicle.VehicleId,
				VehicleIds = new List<int> { vehicle.VehicleId },
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

		// GET: ClientSide/Availability?vehicleId=1&vehicleIds=1&vehicleIds=2
		[Authorize]
		[HttpGet]
		public async Task<IActionResult> Availability(int? vehicleId, [FromQuery] List<int>? vehicleIds)
		{
			var ids = (vehicleIds ?? new List<int>())
				.Where(id => id > 0)
				.Distinct()
				.ToList();
			if (vehicleId is > 0 && !ids.Contains(vehicleId.Value))
			{
				ids.Insert(0, vehicleId.Value);
			}

			var unavailableDates = await GetUnavailableDatesForVehiclesAsync(ids);
			return Json(new { unavailableDates });
		}

		// POST: ClientSide/Rental/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Rental(
			int id,
			[Bind("VehicleId,VehicleIds,CustomerName,ContactNumber,PickupLocation,DropoffLocation,PickupDate,ReturnDate,PickupTime,ReturnTime,PassengerCount,Notes,Discount,DiscountImageFile,RentalOption,PaymentType,PaymentMethod,TotalAmount,AmountPaid,AccountName,TransactionReference,PaymentDate,ReceiptImageFile,PaymentNotes")] RentalInputModel model)
		{
			await RentalExpiry.ExpireOverdueReservesAsync(_context);

			var loginRedirect = RedirectGuestToBookingLogin(id);
			if (loginRedirect != null)
			{
				return loginRedirect;
			}

			var primaryVehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.VehicleId == id);
			if (primaryVehicle == null)
			{
				return NotFound();
			}

			// Route vehicle is always included as the first line.
			model.VehicleIds ??= new List<int>();
			if (!model.VehicleIds.Contains(id))
			{
				model.VehicleIds.Insert(0, id);
			}
			model.VehicleIds = model.VehicleIds.Where(v => v > 0).Distinct().ToList();
			if (!model.VehicleIds.Contains(id))
			{
				model.VehicleIds.Insert(0, id);
			}
			model.VehicleId = id;
			model.RentalStatus = RentalStatus.Pending;
			if (model.RentalOption != RentalOption.Book && model.RentalOption != RentalOption.Reserve)
			{
				model.RentalOption = RentalOption.Book;
			}

			var isReserve = model.RentalOption == RentalOption.Reserve;
			var vehicleIds = model.GetNormalizedVehicleIds().ToList();
			if (!vehicleIds.Contains(id))
			{
				vehicleIds.Insert(0, id);
			}

			var selectedVehicles = await _context.Vehicles
				.Where(v => vehicleIds.Contains(v.VehicleId))
				.ToListAsync();
			var orderedVehicles = vehicleIds
				.Select(vid => selectedVehicles.FirstOrDefault(v => v.VehicleId == vid))
				.Where(v => v != null)
				.Cast<Vehicle>()
				.ToList();

			if (orderedVehicles.Count == 0)
			{
				orderedVehicles.Add(primaryVehicle);
			}

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

			var maxCapacity = orderedVehicles.Max(v => v.PassengersCount);
			if (model.PassengerCount < 1 || model.PassengerCount > maxCapacity)
			{
				ModelState.AddModelError(nameof(model.PassengerCount), $"Passenger count must be between 1 and {maxCapacity}.");
			}

			if (orderedVehicles.Any(v => v.Status != VehicleStatus.Available))
			{
				ModelState.AddModelError(nameof(model.VehicleId), "One or more selected vehicles are not available to rent right now.");
			}

			if (model.PickupDate >= today && model.ReturnDate >= model.PickupDate)
			{
				var hasConflict = await HasVehicleScheduleConflictAsync(
					vehicleIds,
					model.PickupDate,
					model.ReturnDate,
					excludeRentalId: null);

				if (hasConflict)
				{
					ModelState.AddModelError(nameof(model.PickupDate), "Selected dates overlap an existing rental for one of the selected vehicles.");
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
				details.VehicleId = id;

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

				RentalFareCalculator.ApplyTo(rental, orderedVehicles, details);

				_context.Rentals.Add(rental);
				await _context.SaveChangesAsync();

				details.RentalID = rental.RentalId;
				_context.RentalDetails.Add(details);

				var lines = RentalFareCalculator.BuildRentalVehicles(orderedVehicles, details);
				foreach (var line in lines)
				{
					line.RentalId = rental.RentalId;
					_context.RentalVehicles.Add(line);
				}

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

					var payment = new Payment
					{
						RentalId = rental.RentalId,
						PaymentType = model.PaymentType!.Trim(),
						PaymentMethod = model.PaymentMethod!.Trim(),
						TotalAmount = rental.TotalAmount > 0 ? rental.TotalAmount : model.TotalAmount,
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
				? "Reservation saved. Complete payment before the due date."
				: "Booking request submitted successfully.";

			return RedirectToAction(nameof(BookingRequestSent), new { id = rental.RentalId });
			}

			ViewData["ActiveNav"] = "Vehicles";
			ViewData["Vehicle"] = primaryVehicle;
			ViewData["AvailableVehicles"] = await GetAvailableVehiclesForBookingAsync(excludeVehicleId: null);
			ViewData["HideReserveButton"] = true;
			ViewData["LiveRateSummary"] = true;
			ViewData["TotalFare"] = model.TotalAmount > 0 ? model.TotalAmount : primaryVehicle.BasePrice;
			ViewData["DepositPercent"] = 5m;
			ViewData["IsReserveFlow"] = isReserve;
			ViewData["InitialUnavailableDates"] = await GetUnavailableDatesForVehiclesAsync(vehicleIds);
			return View(model);
		}

		// GET: ClientSide/BookingRequestSent/5
		[Authorize]
		public async Task<IActionResult> BookingRequestSent(int? id)
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
				.Include(r => r.RentalVehicles)
					.ThenInclude(rv => rv.Vehicle)
				.FirstOrDefaultAsync(r => r.RentalId == id
					&& r.CustomerId == profile.CustomerId);

			if (rental == null)
			{
				return NotFound();
			}

			ViewData["ActiveNav"] = "Vehicles";
			return View(rental);
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
			if (rental.TotalAmount <= 0 && rental.Details != null)
			{
				RentalFareCalculator.ApplyTo(rental, vehicle!, rental.Details);
				await _context.SaveChangesAsync();
			}
			model.TotalAmount = rental.TotalAmount > 0
				? rental.TotalAmount
				: RentalFareCalculator.Calculate(vehicle!, rental.Details!).TotalAmount;
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
			ViewData["PaymentSummaryOnly"] = true;
			ViewData["SummaryPaymentEditStep"] = "2";
			ViewData["SummaryConfirmLabel"] = "Submit Payment";
			ViewData["SummaryBackStep"] = "2";
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

			// Payment-only post: rental trip/customer details already exist on the hold.
			// Clear [Required] noise from unbound RentalInputModel fields.
			ModelState.Clear();
			ValidateClientPayment(model);

			if (!ModelState.IsValid)
			{
				var reload = RentalInputModel.FromEntities(rental!, rental!.Details);
				reload.PaymentType = model.PaymentType;
				reload.PaymentMethod = model.PaymentMethod;
				reload.TotalAmount = rental!.TotalAmount > 0
					? rental.TotalAmount
					: (model.TotalAmount > 0 ? model.TotalAmount : RentalFareCalculator.Calculate(vehicle!, rental.Details!).TotalAmount);
				reload.AmountPaid = model.AmountPaid;
				reload.AccountName = model.AccountName;
				reload.TransactionReference = model.TransactionReference;
				reload.PaymentDate = model.PaymentDate == default ? DateTime.Now : model.PaymentDate;
				reload.PaymentNotes = model.PaymentNotes;
				reload.ReceiptImageFile = model.ReceiptImageFile;

				ViewData["ActiveNav"] = "";
				ViewData["Vehicle"] = vehicle;
				ViewData["Rental"] = rental;
				ViewData["TotalFare"] = reload.TotalAmount;
				ViewData["DepositPercent"] = 5m;
				ViewData["PaymentTypeFromStep"] = 1;
				ViewData["PaymentTypeBackUrl"] = Url.Action(nameof(MyBookingDetails), new { id });
				ViewData["PaymentDetailsFromStep"] = 2;
				ViewData["PaymentDetailsBackStep"] = 1;
				ViewData["PaymentSummaryOnly"] = true;
				ViewData["SummaryPaymentEditStep"] = "2";
				ViewData["SummaryConfirmLabel"] = "Submit Payment";
				ViewData["SummaryBackStep"] = "2";
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

			if (rental!.TotalAmount <= 0 && rental.Details != null)
			{
				RentalFareCalculator.ApplyTo(rental, vehicle!, rental.Details);
			}

			_context.Payments.Add(new Payment
			{
				RentalId = rental.RentalId,
				PaymentType = model.PaymentType!.Trim(),
				PaymentMethod = model.PaymentMethod!.Trim(),
				TotalAmount = rental.TotalAmount > 0 ? rental.TotalAmount : model.TotalAmount,
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

		private static BookingPaymentSummary SummarizePayments(
			IReadOnlyList<Payment> payments,
			Vehicle? vehicle,
			RentalDetails? details,
			decimal rentalTotalAmount = 0m)
		{
			var paid = payments.Sum(p => p.AmountPaid);
			var fare = rentalTotalAmount > 0
				? rentalTotalAmount
				: payments.FirstOrDefault(p => p.TotalAmount > 0)?.TotalAmount
					?? (vehicle != null && details != null
						? RentalFareCalculator.Calculate(vehicle, details).TotalAmount
						: 0m);

			return new BookingPaymentSummary
			{
				TotalFare = fare,
				AmountPaid = paid
			};
		}

		private static PaymentHistoryRow ToPaymentHistoryRow(Payment payment, Rental? rental, Vehicle? vehicle)
		{
			return new PaymentHistoryRow
			{
				PaymentId = payment.PaymentId,
				RentalId = payment.RentalId,
				BookingLabel = $"BK-{(rental?.RentalId ?? payment.RentalId):D6}",
				VehicleTitle = vehicle?.Model ?? "Vehicle",
				VehicleTypeLabel = VehicleTypes.DisplayUpper(vehicle?.Type),
				PassengersCount = vehicle?.PassengersCount ?? 0,
				VehicleImagePath = vehicle?.ImagePath,
				PaymentDate = payment.PaymentDate,
				PaymentMethod = payment.PaymentMethod,
				PaymentType = payment.PaymentType,
				AmountPaid = payment.AmountPaid,
				TransactionReference = payment.TransactionReference,
				ReceiptImagePath = payment.ReceiptImagePath
			};
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

		private async Task<List<string>> GetUnavailableDatesAsync(int vehicleId)
		{
			if (vehicleId <= 0)
			{
				return new List<string>();
			}

			var rangesFromLines = await (
				from rv in _context.RentalVehicles.AsNoTracking()
				join rental in _context.Rentals.AsNoTracking() on rv.RentalId equals rental.RentalId
				join details in _context.RentalDetails.AsNoTracking() on rental.RentalId equals details.RentalID
				where rv.VehicleId == vehicleId
					&& rental.RentalStatus != RentalStatus.Cancelled
					&& rental.RentalStatus != RentalStatus.Expired
				select new { details.PickupDate, details.ReturnDate }
			).ToListAsync();

			var rangesFromLegacy = await (
				from details in _context.RentalDetails.AsNoTracking()
				join rental in _context.Rentals.AsNoTracking()
					on details.RentalID equals rental.RentalId
				where details.VehicleId == vehicleId
					&& !_context.RentalVehicles.Any(rv => rv.RentalId == details.RentalID)
					&& rental.RentalStatus != RentalStatus.Cancelled
					&& rental.RentalStatus != RentalStatus.Expired
				select new { details.PickupDate, details.ReturnDate }
			).ToListAsync();

			var unavailableDates = new List<string>();
			foreach (var range in rangesFromLines.Concat(rangesFromLegacy))
			{
				for (var day = range.PickupDate; day <= range.ReturnDate; day = day.AddDays(1))
				{
					unavailableDates.Add(day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
				}
			}

			return unavailableDates.Distinct().OrderBy(d => d).ToList();
		}

		private async Task<List<string>> GetUnavailableDatesForVehiclesAsync(IReadOnlyList<int> vehicleIds)
		{
			var all = new HashSet<string>();
			foreach (var vehicleId in vehicleIds.Where(id => id > 0).Distinct())
			{
				foreach (var day in await GetUnavailableDatesAsync(vehicleId))
				{
					all.Add(day);
				}
			}

			return all.OrderBy(d => d).ToList();
		}

		private async Task<bool> HasVehicleScheduleConflictAsync(
			IReadOnlyList<int> vehicleIds,
			DateOnly pickupDate,
			DateOnly returnDate,
			int? excludeRentalId)
		{
			var conflictFromLines =
				from rv in _context.RentalVehicles
				join rental in _context.Rentals on rv.RentalId equals rental.RentalId
				join details in _context.RentalDetails on rental.RentalId equals details.RentalID
				where vehicleIds.Contains(rv.VehicleId)
					&& rental.RentalStatus != RentalStatus.Cancelled
					&& rental.RentalStatus != RentalStatus.Expired
					&& details.PickupDate <= returnDate
					&& details.ReturnDate >= pickupDate
				select rv.RentalId;

			var conflictFromLegacy =
				from details in _context.RentalDetails
				join rental in _context.Rentals on details.RentalID equals rental.RentalId
				where vehicleIds.Contains(details.VehicleId)
					&& !_context.RentalVehicles.Any(rv => rv.RentalId == details.RentalID)
					&& rental.RentalStatus != RentalStatus.Cancelled
					&& rental.RentalStatus != RentalStatus.Expired
					&& details.PickupDate <= returnDate
					&& details.ReturnDate >= pickupDate
				select details.RentalID;

			var query = conflictFromLines.Concat(conflictFromLegacy);
			if (excludeRentalId.HasValue)
			{
				query = query.Where(rentalId => rentalId != excludeRentalId.Value);
			}

			return await query.AnyAsync();
		}

		private async Task<List<Vehicle>> GetAvailableVehiclesForBookingAsync(int? excludeVehicleId)
		{
			var query = _context.Vehicles.AsNoTracking()
				.Where(v => v.Status == VehicleStatus.Available);
			if (excludeVehicleId.HasValue)
			{
				query = query.Where(v => v.VehicleId != excludeVehicleId.Value);
			}

			return await query
				.OrderBy(v => v.Brand)
				.ThenBy(v => v.Model)
				.ToListAsync();
		}

		private static void PopulateRateTripModel(FeedbackInputModel model, Rental rental, Transit transit)
		{
			var vehicle = rental.Details?.Vehicle;
			model.RentalId = rental.RentalId;
			model.BookingLabel = $"BK-{rental.RentalId:D6}";
			model.VehicleTitle = vehicle != null ? $"{vehicle.Model} {vehicle.Brand}".Trim() : "Vehicle";
			model.VehicleTypeLabel = vehicle != null ? VehicleTypes.DisplayUpper(vehicle.Type) : null;
			model.VehicleImagePath = vehicle?.ImagePath;
			model.HasDriver = transit.DriverID.HasValue;
			model.DriverName = transit.Driver?.Name;
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

		private static string GetInitials(string? name)
		{
			var parts = (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length == 0)
			{
				return "U";
			}

			if (parts.Length == 1)
			{
				return char.ToUpperInvariant(parts[0][0]).ToString();
			}

			return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
		}
	}
}
