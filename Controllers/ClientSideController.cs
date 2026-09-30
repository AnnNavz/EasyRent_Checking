using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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
		private readonly BookingEmailService _bookingEmailService;
		private readonly SystemLogService _logs;
		private const string ContactInboxEmail = "ccs.capstoneabadteam@gmail.com";

		public ClientSideController(
			EasyRent_CheckingContext context,
			IWebHostEnvironment webHostEnvironment,
			IEmailSender emailSender,
			BookingEmailService bookingEmailService,
			SystemLogService logs)
		{
			_context = context;
			_webHostEnvironment = webHostEnvironment;
			_emailSender = emailSender;
			_bookingEmailService = bookingEmailService;
			_logs = logs;
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
				model.FullName = profile.User?.FullName ?? string.Empty;
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
			// Public landing page should open as a guest. Admin/staff sessions stay on /Admin/*.
			if (User.Identity?.IsAuthenticated == true
				&& (User.IsInRole(nameof(UserRole.Admin)) || User.IsInRole(nameof(UserRole.Staff))))
			{
				await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
				return RedirectToAction(nameof(Homepage));
			}

			ViewData["ActiveNav"] = "Home";

			var vehicles = await _context.Vehicles.AsNoTracking().Where(v => v.IsActive).ToListAsync();

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
				.Include(f => f.Customer).ThenInclude(c => c!.User)
				.Where(f => f.Comment != null && f.Comment != ""
					&& (f.VehicleComfort + f.VehiclePerformance + f.VehicleSafety) / 3.0 >= 4.5)
				.OrderByDescending(f => f.CreatedAt)
				.Take(3)
				.ToListAsync();

			var testimonials = reviews
				.Select(f => new HomeTestimonial
				{
					CustomerName = string.IsNullOrWhiteSpace(f.Customer?.User?.FullName) ? "Customer" : f.Customer.User.FullName,
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

			return View("~/Views/ClientSide/Homepage.cshtml", new HomeViewModel
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
			var transitsByRental = transits
				.GroupBy(t => t.RentalID)
				.ToDictionary(g => g.Key, g => g.ToList());

			var pendingVehicleIds = rentals
				.Where(r => (r.RentalVehicles == null || r.RentalVehicles.Count == 0)
					&& !string.IsNullOrEmpty(r.PendingVehicleIdsJson))
				.SelectMany(r => RentalVehicleWorkflow.DeserializeVehicleIds(
					r.PendingVehicleIdsJson))
				.Distinct()
				.ToList();
			var pendingVehiclesById = pendingVehicleIds.Count > 0
				? await _context.Vehicles.AsNoTracking()
					.Where(v => pendingVehicleIds.Contains(v.VehicleId))
					.ToDictionaryAsync(v => v.VehicleId)
				: new Dictionary<int, Vehicle>();

			var items = rentals
				.Select(r =>
				{
					paymentsByRental.TryGetValue(r.RentalId, out var rentalPayments);
					rentalPayments ??= new List<Payment>();
					var latestPayment = rentalPayments.FirstOrDefault();
					transitsByRental.TryGetValue(r.RentalId, out var rentalTransits);
					rentalTransits ??= new List<Transit>();
					var (_, driverName, driverImagePath, tripStatus, canRate, hasFeedback) = SummarizeRentalTransits(rentalTransits);
					var lineVehicles = (r.RentalVehicles ?? Enumerable.Empty<RentalVehicle>())
						.Where(rv => rv.Vehicle != null)
						.OrderBy(rv => rv.SortOrder)
						.Select(rv => rv.Vehicle!)
						.ToList();
					if (lineVehicles.Count == 0 && !string.IsNullOrEmpty(r.PendingVehicleIdsJson))
					{
						lineVehicles = RentalVehicleWorkflow.DeserializeVehicleIds(r.PendingVehicleIdsJson)
							.Where(pendingVehiclesById.ContainsKey)
							.Select(id => pendingVehiclesById[id])
							.ToList();
					}
					var vehicle = lineVehicles.FirstOrDefault();
					var isPast = r.ReturnDate < today
						|| r.RentalStatus is RentalStatus.Cancelled
							or RentalStatus.Expired
							or RentalStatus.Refunded
							or RentalStatus.RefundRejected;
					var ledger = SummarizePayments(rentalPayments, vehicle, r, r.TotalAmount);
					var vehicleItems = (lineVehicles.Count > 0
							? lineVehicles
							: vehicle != null
								? new List<Vehicle> { vehicle }
								: new List<Vehicle>())
						.Select(v => new MyBookingVehicleItem
						{
							Brand = v.Brand,
							Model = v.Model,
							TypeLabel = VehicleTypes.DisplayUpper(v.Type),
							ImagePath = v.ImagePath,
							PassengersCount = v.PassengersCount
						})
						.ToList();
					var vehicleTitle = vehicleItems.Count > 1
						? $"{vehicleItems.Count} vehicles"
						: (vehicle?.Model ?? "Vehicle");
					var vehicleTypeLabel = vehicleItems.Count > 1
						? string.Join(", ", vehicleItems.Select(v => $"{v.Brand} {v.Model}"))
						: VehicleTypes.DisplayUpper(vehicle?.Type);

					var canPay = r.RentalOption == RentalOption.Reserve
							&& r.RentalStatus == RentalStatus.Pending
							&& latestPayment == null;
					var (progressLabel, progressTone) = BuildBookingProgressLabel(
						r.RentalStatus, tripStatus, driverName, canPay);

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
						Vehicles = vehicleItems,
						PickupDate = r.PickupDate,
						PickupTime = r.PickupTime,
						ReturnTime = r.ReturnTime,
						TotalFare = ledger.TotalFare,
						AmountPaid = ledger.AmountPaid,
						RemainingBalance = r.RentalStatus is RentalStatus.Cancelled
							or RentalStatus.Expired
							or RentalStatus.Refunded
							or RentalStatus.RefundRejected
							? 0m
							: ledger.RemainingBalance,
						PaymentMethod = latestPayment?.PaymentMethod,
						DriverName = driverName,
						DriverInitials = GetInitials(driverName),
						DriverImagePath = driverImagePath,
						IsPast = isPast,
						TripStatus = tripStatus,
						CanRate = canRate,
						HasFeedback = hasFeedback,
						CanPay = canPay,
						ProgressLabel = progressLabel,
						ProgressTone = progressTone,
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
				FullName = profile.User?.FullName ?? string.Empty,
				AvatarInitials = GetInitials(profile.User?.FullName ?? string.Empty),
				ProfileImagePath = profile.User?.ProfileImagePath,
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
				.Include(r => r.RentalVehicles)
					.ThenInclude(rv => rv.Vehicle)
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
				var vehicle = rental?.RentalVehicles?
					.OrderBy(rv => rv.SortOrder)
					.Select(rv => rv.Vehicle)
					.FirstOrDefault();
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
				var ledger = SummarizePayments(rentalPayments, rental.RentalVehicles?.OrderBy(rv => rv.SortOrder).Select(rv => rv.Vehicle).FirstOrDefault(), rental, rental.TotalAmount);
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

			var transits = await _context.Transits
				.AsNoTracking()
				.Include(t => t.Driver)
				.Include(t => t.Vehicle)
				.Include(t => t.Feedback)
				.Where(t => t.RentalID == rental.RentalId)
				.OrderBy(t => t.TransitID)
				.ToListAsync();

			var transit = transits.OrderByDescending(t => t.TransitID).FirstOrDefault();

			var primaryVehicle = rental.RentalVehicles?
				.OrderBy(rv => rv.SortOrder)
				.Select(rv => rv.Vehicle)
				.FirstOrDefault()
				?? rental.RentalVehicles?
					.OrderBy(rv => rv.SortOrder)
					.Select(rv => rv.Vehicle)
					.FirstOrDefault();
			var selectedVehicles = await RentalVehicleWorkflow.LoadSelectedVehiclesAsync(_context, rental);
			if (primaryVehicle == null && selectedVehicles.Count > 0)
			{
				primaryVehicle = selectedVehicles[0];
			}

			ViewData["SelectedVehicles"] = selectedVehicles;
			var paymentSummary = SummarizePayments(payments, primaryVehicle, rental, rental.TotalAmount);
			paymentSummary.Payments = payments
				.Select(p => ToPaymentHistoryRow(p, rental, primaryVehicle))
				.ToList();

			ViewData["LatestPayment"] = payments.FirstOrDefault();
			ViewData["PaymentSummary"] = paymentSummary;
			ViewData["Transit"] = transit;
			ViewData["Transits"] = transits;
			ViewData["CanRate"] = transits.Any(t => t.TripStatus == TripStatus.Completed && t.Feedback == null);

			var assignedDriverIds = transits
				.Where(t => t.DriverID != null)
				.Select(t => t.DriverID!.Value)
				.Distinct()
				.ToList();
			ViewData["DriverRatingsById"] = await GetDriverRatingsAsync(assignedDriverIds);

			ViewData["CanCancel"] = CanCustomerCancel(rental, transits);
			ViewData["CancellationFee"] = GetCancellationFee(transits);
			ViewData["AmountPaid"] = paymentSummary.AmountPaid;
			ViewData["IsInTransit"] = transits.Any(t => t.TripStatus == TripStatus.InTransit);
			ViewData["TimelineTimestamps"] = await BuildBookingTimelineTimestampsAsync(rental, payments, transits);
			ViewData["ActiveNav"] = "";
			ViewData["HubActive"] = "bookings";
			return View(rental);
		}

		// POST: ClientSide/CancelBooking/5
		[Authorize]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CancelBooking(
			int id,
			[Bind("RentalId,Reason,RequestRefund,PaymentMethod,AccountName,TransactionReference,ReceiptImageFile,PaymentNotes,CancellationFeeAmount")] CustomerCancelBookingViewModel model)
		{
			var profile = await GetLoggedInCustomerProfileAsync();
			if (profile == null)
			{
				return RedirectToAction("Login", "Account");
			}

			model.RentalId = id;

			var rental = await _context.Rentals
				.Include(r => r.RentalVehicles)
				.FirstOrDefaultAsync(r => r.RentalId == id && r.CustomerId == profile.CustomerId);

			if (rental == null)
			{
				return NotFound();
			}

			var transits = await _context.Transits
				.Where(t => t.RentalID == rental.RentalId)
				.ToListAsync();

			if (!CanCustomerCancel(rental, transits))
			{
				TempData["CancelError"] = "This booking can no longer be cancelled.";
				return RedirectToAction(nameof(MyBookingDetails), new { id });
			}

			var fee = GetCancellationFee(transits);
			var amountPaid = await RentalResolution.GetAmountPaidAsync(_context, rental.RentalId);
			var isInTransit = fee > 0;

			if (isInTransit)
			{
				ValidateCancellationFeePayment(model, ModelState);
			}

			if (model.RequestRefund && amountPaid <= 0)
			{
				ModelState.AddModelError(nameof(model.RequestRefund), "There is no payment to refund for this booking.");
			}

			if (!ModelState.IsValid)
			{
				TempData["CancelError"] = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage
					?? "Please complete all required cancellation fields.";
				return RedirectToAction(nameof(MyBookingDetails), new { id });
			}

			if (isInTransit)
			{
				string? receiptPath = null;
				if (model.ReceiptImageFile != null && model.ReceiptImageFile.Length > 0)
				{
					receiptPath = await ImageStorage.SaveAsync(
						_webHostEnvironment,
						model.ReceiptImageFile,
						ImageStorage.PaymentReceiptsFolder);
				}

				_context.Payments.Add(new Payment
				{
					RentalId = rental.RentalId,
					PaymentType = RentalResolution.CancellationFeePaymentType,
					PaymentMethod = model.PaymentMethod!.Trim(),
					TotalAmount = RentalRules.CancellationFeeWhenInTransit,
					AmountPaid = RentalRules.CancellationFeeWhenInTransit,
					AccountName = model.AccountName,
					TransactionReference = model.TransactionReference,
					PaymentDate = DateTime.Now,
					ReceiptImagePath = receiptPath,
					PaymentNotes = model.PaymentNotes
				});

				rental.CancellationFee = fee;
				rental.CancellationFeePaidAt = DateTime.Now;
			}

			rental.RentalStatus = RentalStatus.Cancelled;
			rental.CancelledAt = DateTime.Now;
			rental.CustomerCancellationReason = model.Reason.Trim();

			if (model.RequestRefund && amountPaid > 0)
			{
				rental.RefundRequestedAt = DateTime.Now;
				rental.RefundRequestedAmount = amountPaid;
			}

			foreach (var transit in transits.Where(t => t.TripStatus is not TripStatus.Completed))
			{
				transit.TripStatus = TripStatus.Cancelled;
			}

			await _context.SaveChangesAsync();

			if (model.RequestRefund && amountPaid > 0)
			{
				TempData["SuccessMessage"] = fee > 0
					? "Booking cancelled. Your cancellation fee payment was recorded and a refund request was sent to EasyRent."
					: "Booking cancelled. Your refund request was sent to EasyRent for review.";
			}
			else
			{
				TempData["SuccessMessage"] = fee > 0
					? $"Booking cancelled. Your ₱{fee:N2} cancellation fee payment was recorded."
					: "Booking cancelled at no charge.";
			}

			return RedirectToAction(nameof(MyBookingDetails), new { id });
		}

		private static void ValidateCancellationFeePayment(
			CustomerCancelBookingViewModel model,
			ModelStateDictionary modelState)
		{
			if (string.IsNullOrWhiteSpace(model.PaymentMethod))
			{
				modelState.AddModelError(nameof(model.PaymentMethod), "Please select a payment method for the cancellation fee.");
			}

			var isCashless = !string.Equals(model.PaymentMethod, "Walk-in", StringComparison.OrdinalIgnoreCase);
			if (!isCashless)
			{
				return;
			}

			if (string.IsNullOrWhiteSpace(model.AccountName))
			{
				modelState.AddModelError(nameof(model.AccountName), "Account name is required for cashless payment.");
			}

			if (string.IsNullOrWhiteSpace(model.TransactionReference))
			{
				modelState.AddModelError(nameof(model.TransactionReference), "Transaction reference is required for cashless payment.");
			}

			if (model.ReceiptImageFile == null || model.ReceiptImageFile.Length == 0)
			{
				modelState.AddModelError(nameof(model.ReceiptImageFile), "Please upload your payment receipt.");
			}
		}

		private static bool CanCustomerCancel(Rental rental, IEnumerable<Transit> transits)
		{
			if (rental.RentalStatus is RentalStatus.Cancelled or RentalStatus.Expired or RentalStatus.Refunded or RentalStatus.RefundRejected)
			{
				return false;
			}

			if (transits.Any(t => t.TripStatus == TripStatus.Completed))
			{
				return false;
			}

			return rental.RentalStatus is RentalStatus.Pending or RentalStatus.Approved;
		}

		private static decimal GetCancellationFee(IEnumerable<Transit> transits)
			=> transits.Any(t => t.TripStatus == TripStatus.InTransit)
				? RentalRules.CancellationFeeWhenInTransit
				: 0m;

		// GET: ClientSide/RateTrip/5
		[Authorize]
		public async Task<IActionResult> RateTrip(int? id)
		{
			var access = await GetRateableBookingAsync(id);
			if (access.Redirect != null)
			{
				return access.Redirect;
			}

			var model = BuildRateTripPageModel(access.Rental!, access.Transits);

			ViewData["ActiveNav"] = "";
			return View(model);
		}

		// POST: ClientSide/RateTrip/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		[Authorize]
		public async Task<IActionResult> RateTrip(int id, RateTripPageViewModel model)
		{
			model.RentalId = id;

			var access = await GetRateableBookingAsync(id);
			if (access.Redirect != null)
			{
				return access.Redirect;
			}

			var rental = access.Rental!;
			var transits = access.Transits;
			var profile = access.Profile!;

			MergeRateTripPageModel(model, rental, transits);
			ModelState.Clear();
			TryValidateModel(model);

			for (var i = 0; i < model.Items.Count; i++)
			{
				TryValidateModel(model.Items[i], $"{nameof(RateTripPageViewModel.Items)}[{i}]");
			}

			var rateableIds = transits.Select(t => t.TransitID).ToHashSet();
			var postedIds = model.Items.Select(i => i.TransitId).ToHashSet();
			if (!rateableIds.SetEquals(postedIds))
			{
				ModelState.AddModelError(string.Empty, "Please rate each vehicle on this booking.");
			}

			var alreadyRated = await _context.Feedbacks
				.AsNoTracking()
				.AnyAsync(f => rateableIds.Contains(f.TransitID));
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

			var now = DateTime.Now;

			foreach (var item in model.Items)
			{
				var comment = string.IsNullOrWhiteSpace(item.Comment) ? null : item.Comment.Trim();

				_context.Feedbacks.Add(new Feedback
				{
					TransitID = item.TransitId,
					CustomerId = profile.CustomerId,
					Rating = RatingScale.OverallStars(item.VehicleComfort, item.VehiclePerformance, item.VehicleSafety),
					VehicleComfort = item.VehicleComfort,
					VehiclePerformance = item.VehiclePerformance,
					VehicleSafety = item.VehicleSafety,
					DriverProfessionalism = item.HasDriver ? item.DriverProfessionalism : null,
					DriverDriving = item.HasDriver ? item.DriverDriving : null,
					DriverCourtesy = item.HasDriver ? item.DriverCourtesy : null,
					Comment = comment,
					CreatedAt = now
				});
			}

			await _context.SaveChangesAsync();

			TempData["SuccessMessage"] = model.Items.Count > 1
				? "Thanks for rating all vehicles on this booking!"
				: "Thanks for your feedback!";
			return RedirectToAction(nameof(MyBookingDetails), new { id });
		}

		// GET: ClientSide/Browse
		public async Task<IActionResult> Browse(string? category, string? sortBy, int? capacity, DateOnly? pickupDate, DateOnly? returnDate, string? q, int page = 1)
		{
			var vehiclesQuery = _context.Vehicles.AsQueryable().Where(v => v.IsActive);

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
					from rv in _context.RentalVehicles
					join rental in _context.Rentals on rv.RentalId equals rental.RentalId
					where rental.RentalStatus != RentalStatus.Cancelled
						&& rental.RentalStatus != RentalStatus.Expired
						&& rental.PickupDate <= endDate.Value
						&& rental.ReturnDate >= startDate.Value
					select rv.VehicleId
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
				.Include(f => f.Customer).ThenInclude(c => c!.User)
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
		// Displays the rental booking form for a specific vehicle
		public async Task<IActionResult> Rental(int? id, string? option)
		{
			// Step 1: Expire any overdue reserve bookings
			await RentalExpiry.ExpireOverdueReservesAsync(_context);

			// Step 2: Validate vehicle ID
			if (id == null)
			{
				return NotFound();
			}

			// Step 3: Redirect guests to login before booking
			var loginRedirect = RedirectGuestToBookingLogin(id);
			if (loginRedirect != null)
			{
				return loginRedirect;
			}

			// Step 4: Load the vehicle from database
			var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.VehicleId == id);
			if (vehicle == null)
			{
				return NotFound();
			}

			// Step 5: Check if vehicle is available for rent
			if (!vehicle.IsActive || vehicle.Status != VehicleStatus.Available)
			{
				TempData["RentalError"] = "This vehicle is not available to rent right now.";
				return RedirectToAction(nameof(VehicleDetails), new { id });
			}

			// Step 6: Parse rental option (Book or Reserve) from URL parameter
			// Legacy links with ?option= still work; choice is primarily made in wizard step 2.
			var rentalOption = RentalOption.Book;
			if (Enum.TryParse(option, ignoreCase: true, out RentalOption parsed)
				&& (parsed == RentalOption.Book || parsed == RentalOption.Reserve))
			{
				rentalOption = parsed;
			}

			// Step 7: Set up view data for the booking form
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

			// Step 8: Create booking model with default values
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

			// Step 9: Pre-fill customer details if user is logged in
			await ApplyLoggedInCustomerAsync(model);

			// Step 10: Display the booking form
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
		// Processes the rental booking form submission
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Rental(
			int id,
			[Bind("VehicleId,VehicleIds,CustomerName,ContactNumber,PickupLocation,DropoffLocation,PickupDate,ReturnDate,PickupTime,ReturnTime,PassengerCount,Notes,Discount,DiscountImageFile,RentalOption,PaymentType,PaymentMethod,TotalAmount,AmountPaid,AccountName,TransactionReference,PaymentDate,ReceiptImageFile,PaymentNotes")] RentalInputModel model)
		{
			// Step 1: Expire any overdue reserve bookings
			await RentalExpiry.ExpireOverdueReservesAsync(_context);

			// Step 2: Redirect guests to login before booking
			var loginRedirect = RedirectGuestToBookingLogin(id);
			if (loginRedirect != null)
			{
				return loginRedirect;
			}

			// Step 3: Load the primary vehicle from database
			var primaryVehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.VehicleId == id);
			if (primaryVehicle == null)
			{
				return NotFound();
			}

			// Step 4: Normalize vehicle IDs (ensure route vehicle is first, remove duplicates)
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

			// Step 5: Determine if this is a Reserve or Book flow
			var isReserve = model.RentalOption == RentalOption.Reserve;
			var vehicleIds = model.GetNormalizedVehicleIds().ToList();
			if (!vehicleIds.Contains(id))
			{
				vehicleIds.Insert(0, id);
			}

			// Step 6: Load selected vehicles from database in the correct order
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

			// Step 7: Apply logged-in customer details to model
			// Keep rental contact details tied to the signed-in customer account.
			await ApplyLoggedInCustomerAsync(model);

			// Step 8: Validate pickup date is not in the past
			var today = DateOnly.FromDateTime(DateTime.Today);
			if (model.PickupDate < today)
			{
				ModelState.AddModelError(nameof(model.PickupDate), "Please select a pick-up date on the calendar.");
			}

			// Step 9: Validate return date is not before pickup date
			if (model.ReturnDate < model.PickupDate)
			{
				ModelState.AddModelError(nameof(model.ReturnDate), "Return date cannot be earlier than pick-up date.");
			}

			// Step 10: Validate passenger count is within vehicle capacity
			var maxCapacity = orderedVehicles.Max(v => v.PassengersCount);
			if (model.PassengerCount < 1 || model.PassengerCount > maxCapacity)
			{
				ModelState.AddModelError(nameof(model.PassengerCount), $"Passenger count must be between 1 and {maxCapacity}.");
			}

			// Step 11: Validate all selected vehicles are available
			if (orderedVehicles.Any(v => !v.IsActive || v.Status != VehicleStatus.Available))
			{
				ModelState.AddModelError(nameof(model.VehicleId), "One or more selected vehicles are not available to rent right now.");
			}

			// Step 12: Check for schedule conflicts with existing rentals
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

			// Step 13: Validate discount ID upload if discount is requested
			if (model.Discount == Discount.Yes && model.DiscountImageFile == null)
			{
				ModelState.AddModelError(nameof(model.DiscountImageFile), "Please upload a Senior/PWD ID image.");
			}

			// Step 14: Validate payment for Book option, clear payment fields for Reserve option
			if (!isReserve)
			{
				var bookingTotal = PaymentAmountRules.CalculateBookingTotal(orderedVehicles, model);
				ValidateClientPayment(model, bookingTotal);
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

			// Step 15: If validation passes, create the booking
			if (ModelState.IsValid)
			{
				// Step 15a: Save discount ID image if uploaded
				if (model.DiscountImageFile != null)
				{
					model.DiscountImagePath = await ImageStorage.SaveAsync(
						_webHostEnvironment,
						model.DiscountImageFile,
						ImageStorage.RentalsFolder);
				}

				// Step 15b: Clear discount image path if no discount
				if (model.Discount == Discount.No)
				{
					model.DiscountImagePath = null;
				}

				// Step 15c: Create the rental from the submitted model
				var rental = new Rental();
				model.ApplyTo(rental);

				// Step 15d: Link rental to logged-in customer if applicable
				var loggedInProfile = await GetLoggedInCustomerProfileAsync();
				if (loggedInProfile != null)
				{
					rental.CustomerId = loggedInProfile.CustomerId;
				}

				// Step 15e: Set rental option and payment deadline
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

				// Step 15f: Save rental to database
				_context.Rentals.Add(rental);
				await _context.SaveChangesAsync();

				await RentalVehicleWorkflow.SyncPendingSelectionAsync(
					_context,
					rental,
					orderedVehicles,
					vehicleIds);

				if (isReserve)
				{
					await _context.SaveChangesAsync();
					TempData["SuccessMessage"] = "Reservation saved. Complete payment before the due date to confirm your booking.";
				}
				else
				{
					string? receiptPath = null;
					if (model.ReceiptImageFile != null)
					{
						receiptPath = await ImageStorage.SaveAsync(
							_webHostEnvironment,
							model.ReceiptImageFile,
							ImageStorage.PaymentReceiptsFolder);
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
					await _context.SaveChangesAsync();
					await RentalConfirmation.ConfirmPaidBookingAsync(
						_context,
						_logs,
						_bookingEmailService,
						rental);
					TempData["SuccessMessage"] = "Booking confirmed. A confirmation email with your receipt was sent if an email is on file.";
				}

				return RedirectToAction(nameof(BookingRequestSent), new { id = rental.RentalId });
			}

			// Step 16: If validation fails, redisplay form with errors
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
				.Include(r => r.RentalVehicles)
					.ThenInclude(rv => rv.Vehicle)
				.FirstOrDefaultAsync(r => r.RentalId == id
					&& r.CustomerId == profile.CustomerId);

			if (rental == null)
			{
				return NotFound();
			}

			ViewData["SelectedVehicles"] = await RentalVehicleWorkflow.LoadSelectedVehiclesAsync(_context, rental);
			ViewData["ActiveNav"] = "Vehicles";
			return View(rental);
		}

		// GET: ClientSide/PayRental/5
		// Displays payment form for a reserve booking
		[Authorize]
		public async Task<IActionResult> PayRental(int? id)
		{
			// Step 1: Expire any overdue reserve bookings
			await RentalExpiry.ExpireOverdueReservesAsync(_context);

			// Step 2: Load the reserve booking and validate it can be paid
			var (rental, vehicle, error) = await LoadPayableReserveAsync(id);
			if (error != null)
			{
				return error;
			}

			// Step 3: Create payment model from rental entities
			var model = RentalInputModel.FromEntities(rental!);
			
			// Step 4: Calculate fare if not already set
			if (rental.TotalAmount <= 0)
			{
				RentalFareCalculator.ApplyTo(rental, vehicle!);
				await _context.SaveChangesAsync();
			}
			
			// Step 5: Set total amount and default payment type
			model.TotalAmount = rental.TotalAmount > 0
				? rental.TotalAmount
				: RentalFareCalculator.Calculate(vehicle!, rental).TotalAmount;
			model.PaymentDate = DateTime.Now;
			model.PaymentType = "Full Payment";

			// Step 6: Load selected vehicles for display
			var selectedVehicles = await RentalVehicleWorkflow.LoadSelectedVehiclesAsync(_context, rental!);

			// Step 7: Set up view data for payment form
			ViewData["ActiveNav"] = "";
			ViewData["HubActive"] = "bookings";
			ViewData["Vehicle"] = vehicle;
			ViewData["Rental"] = rental;
			ViewData["SelectedVehicles"] = selectedVehicles;
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
			
			// Step 8: Display payment form
			return View(model);
		}

		// POST: ClientSide/PayRental/5
		// Processes payment submission for a reserve booking
		[HttpPost]
		[ValidateAntiForgeryToken]
		[Authorize]
		public async Task<IActionResult> PayRental(
			int id,
			[Bind("RentalId,PaymentType,PaymentMethod,TotalAmount,AmountPaid,AccountName,TransactionReference,PaymentDate,ReceiptImageFile,PaymentNotes")] RentalInputModel model)
		{
			// Step 1: Expire any overdue reserve bookings
			await RentalExpiry.ExpireOverdueReservesAsync(_context);

			// Step 2: Load the reserve booking and validate it can be paid
			var (rental, vehicle, error) = await LoadPayableReserveAsync(id);
			if (error != null)
			{
				return error;
			}

			// Step 3: Set rental ID on model
			model.RentalId = id;

			// Step 4: Clear model state and validate payment amount
			// Payment-only post: rental trip/customer details already exist on the hold.
			// Clear [Required] noise from unbound RentalInputModel fields.
			ModelState.Clear();
			var bookingTotal = rental!.TotalAmount > 0
				? rental.TotalAmount
				: (vehicle != null
					? RentalFareCalculator.Calculate(vehicle, rental).TotalAmount
					: model.TotalAmount);
			ValidateClientPayment(model, bookingTotal);

			// Step 5: If validation fails, redisplay form with errors
			if (!ModelState.IsValid)
			{
				var reload = RentalInputModel.FromEntities(rental!);
				reload.PaymentType = model.PaymentType;
				reload.PaymentMethod = model.PaymentMethod;
				reload.TotalAmount = rental!.TotalAmount > 0
					? rental.TotalAmount
					: (model.TotalAmount > 0 ? model.TotalAmount : RentalFareCalculator.Calculate(vehicle!, rental).TotalAmount);
				reload.AmountPaid = model.AmountPaid;
				reload.AccountName = model.AccountName;
				reload.TransactionReference = model.TransactionReference;
				reload.PaymentDate = model.PaymentDate == default ? DateTime.Now : model.PaymentDate;
				reload.PaymentNotes = model.PaymentNotes;
				reload.ReceiptImageFile = model.ReceiptImageFile;

				ViewData["ActiveNav"] = "";
				ViewData["HubActive"] = "bookings";
				ViewData["Vehicle"] = vehicle;
				ViewData["Rental"] = rental;
				ViewData["SelectedVehicles"] = await RentalVehicleWorkflow.LoadSelectedVehiclesAsync(_context, rental!);
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

			// Step 6: Save receipt image if uploaded
			string? receiptPath = null;
			if (model.ReceiptImageFile != null)
			{
				receiptPath = await ImageStorage.SaveAsync(
					_webHostEnvironment,
					model.ReceiptImageFile,
					ImageStorage.PaymentReceiptsFolder);
			}

			// Step 7: Calculate and apply fare if not already set
			if (rental!.TotalAmount <= 0)
			{
				RentalFareCalculator.ApplyTo(rental, vehicle!);
			}

			// Step 8: Create payment record
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

			rental.PaymentDueAt = null;
			await _context.SaveChangesAsync();
			await RentalConfirmation.ConfirmPaidBookingAsync(
				_context,
				_logs,
				_bookingEmailService,
				rental);

			TempData["SuccessMessage"] = "Payment received. Your booking is confirmed.";
			return RedirectToAction(nameof(MyBookingDetails), new { id = rental.RentalId });
		}

		private void ValidateClientPayment(RentalInputModel model, decimal bookingTotal)
		{
			if (string.IsNullOrWhiteSpace(model.PaymentType))
			{
				ModelState.AddModelError(nameof(model.PaymentType), "Please select a payment type.");
			}

			if (string.IsNullOrWhiteSpace(model.PaymentMethod))
			{
				ModelState.AddModelError(nameof(model.PaymentMethod), "Please select a payment method.");
			}

			var isWalkIn = string.Equals(model.PaymentMethod, "Walk-in", StringComparison.OrdinalIgnoreCase);
			if (isWalkIn)
			{
				model.AmountPaid = PaymentAmountRules.GetMinimumDue(model.PaymentType, bookingTotal);
				if (model.PaymentDate == default)
				{
					model.PaymentDate = DateTime.Now;
				}

				return;
			}

			var amountError = PaymentAmountRules.ValidateAmountPaid(model.AmountPaid, model.PaymentType, bookingTotal);
			if (amountError != null)
			{
				ModelState.AddModelError(nameof(model.AmountPaid), amountError);
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
				.Include(r => r.RentalVehicles)
					.ThenInclude(rv => rv.Vehicle)
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

			var vehicle = rental.RentalVehicles?
				.OrderBy(rv => rv.SortOrder)
				.Select(rv => rv.Vehicle)
				.FirstOrDefault();
			if (vehicle == null)
			{
				var selectedVehicles = await RentalVehicleWorkflow.LoadSelectedVehiclesAsync(_context, rental);
				vehicle = selectedVehicles.FirstOrDefault();
			}

			if (vehicle == null)
			{
				return (null, null, NotFound());
			}

			return (rental, vehicle, null);
		}

		private static BookingPaymentSummary SummarizePayments(
			IReadOnlyList<Payment> payments,
			Vehicle? vehicle,
			Rental? rental,
			decimal rentalTotalAmount = 0m)
		{
			var paid = payments.Sum(p => p.AmountPaid);
			var fare = rentalTotalAmount > 0
				? rentalTotalAmount
				: payments.FirstOrDefault(p => p.TotalAmount > 0)?.TotalAmount
					?? (vehicle != null && rental != null
						? RentalFareCalculator.Calculate(vehicle, rental).TotalAmount
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

			model.CustomerName = profile.User?.FullName ?? string.Empty;
			model.ContactNumber = profile.User?.ContactNumber ?? string.Empty;
			ViewData["CustomerFieldsLocked"] = true;
		}

		private async Task<List<string>> GetUnavailableDatesAsync(int vehicleId) =>
			await RentalVehicleWorkflow.GetUnavailableDatesAsync(_context, vehicleId);

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

		private Task<bool> HasVehicleScheduleConflictAsync(
			IReadOnlyList<int> vehicleIds,
			DateOnly pickupDate,
			DateOnly returnDate,
			int? excludeRentalId) =>
			RentalVehicleWorkflow.HasScheduleConflictAsync(
				_context,
				vehicleIds,
				pickupDate,
				returnDate,
				excludeRentalId);

		private async Task<List<Vehicle>> GetAvailableVehiclesForBookingAsync(int? excludeVehicleId)
		{
			var query = _context.Vehicles.AsNoTracking()
				.Where(v => v.IsActive && v.Status == VehicleStatus.Available);
			if (excludeVehicleId.HasValue)
			{
				query = query.Where(v => v.VehicleId != excludeVehicleId.Value);
			}

			return await query
				.OrderBy(v => v.Brand)
				.ThenBy(v => v.Model)
				.ToListAsync();
		}

		private static RateTripPageViewModel BuildRateTripPageModel(Rental rental, IReadOnlyList<Transit> transits)
		{
			return new RateTripPageViewModel
			{
				RentalId = rental.RentalId,
				BookingLabel = $"BK-{rental.RentalId:D6}",
				Items = transits.Select(MapTransitFeedbackItem).ToList()
			};
		}

		private static void MergeRateTripPageModel(RateTripPageViewModel model, Rental rental, IReadOnlyList<Transit> transits)
		{
			model.RentalId = rental.RentalId;
			model.BookingLabel = $"BK-{rental.RentalId:D6}";

			var transitById = transits.ToDictionary(t => t.TransitID);
			var merged = new List<TransitFeedbackInputModel>();

			foreach (var posted in model.Items)
			{
				if (!transitById.TryGetValue(posted.TransitId, out var transit))
				{
					continue;
				}

				var item = MapTransitFeedbackItem(transit);
				item.VehicleComfort = posted.VehicleComfort;
				item.VehiclePerformance = posted.VehiclePerformance;
				item.VehicleSafety = posted.VehicleSafety;
				item.DriverProfessionalism = posted.DriverProfessionalism;
				item.DriverDriving = posted.DriverDriving;
				item.DriverCourtesy = posted.DriverCourtesy;
				item.Comment = posted.Comment;
				merged.Add(item);
			}

			if (merged.Count == 0)
			{
				merged.AddRange(transits.Select(MapTransitFeedbackItem));
			}

			model.Items = merged
				.OrderBy(i => i.TransitId)
				.ToList();
		}

		private static TransitFeedbackInputModel MapTransitFeedbackItem(Transit transit)
		{
			var vehicle = transit.Vehicle;

			return new TransitFeedbackInputModel
			{
				TransitId = transit.TransitID,
				VehicleTitle = vehicle != null ? $"{vehicle.Model} {vehicle.Brand}".Trim() : "Vehicle",
				VehicleTypeLabel = vehicle != null ? VehicleTypes.DisplayUpper(vehicle.Type) : null,
				VehiclePlateNumber = vehicle?.PlateNumber,
				VehicleImagePath = vehicle?.ImagePath,
				HasDriver = transit.DriverID.HasValue,
				DriverName = transit.Driver?.Name
			};
		}

		private async Task<(Rental? Rental, IReadOnlyList<Transit> Transits, CustomerProfile? Profile, IActionResult? Redirect)> GetRateableBookingAsync(int? rentalId)
		{
			if (rentalId == null)
			{
				return (null, Array.Empty<Transit>(), null, NotFound());
			}

			var profile = await GetLoggedInCustomerProfileAsync();
			if (profile == null)
			{
				return (null, Array.Empty<Transit>(), null, RedirectToAction("Login", "Account"));
			}

			var rental = await _context.Rentals
				.AsNoTracking()
				.FirstOrDefaultAsync(r => r.RentalId == rentalId
					&& r.CustomerId == profile.CustomerId);

			if (rental == null)
			{
				return (null, Array.Empty<Transit>(), null, NotFound());
			}

			if (rental.RentalStatus is RentalStatus.Cancelled or RentalStatus.Expired)
			{
				TempData["RateTripError"] = "This booking cannot be rated.";
				return (null, Array.Empty<Transit>(), null, RedirectToAction(nameof(MyBookingDetails), new { id = rentalId }));
			}

			var transits = await _context.Transits
				.AsNoTracking()
				.Include(t => t.Driver)
				.Include(t => t.Vehicle)
				.Include(t => t.Feedback)
				.Where(t => t.RentalID == rental.RentalId
					&& t.TripStatus == TripStatus.Completed
					&& t.Feedback == null)
				.OrderBy(t => t.TransitID)
				.ToListAsync();

			if (transits.Count == 0)
			{
				TempData["RateTripError"] = "You can rate a trip only after it is completed.";
				return (null, Array.Empty<Transit>(), null, RedirectToAction(nameof(MyBookingDetails), new { id = rentalId }));
			}

			return (rental, transits, profile, null);
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
				.Include(c => c.User)
				.FirstOrDefaultAsync(c => c.CustomerId == userId);
		}

		private static (string Label, string Tone) BuildBookingProgressLabel(
			RentalStatus rentalStatus,
			TripStatus? tripStatus,
			string? driverName,
			bool canPay)
		{
			if (rentalStatus is RentalStatus.Cancelled or RentalStatus.RefundRejected)
			{
				return ("Cancelled", "danger");
			}

			if (rentalStatus == RentalStatus.Refunded)
			{
				return ("Refunded", "muted");
			}

			if (rentalStatus == RentalStatus.Expired)
			{
				return ("Expired", "muted");
			}

			if (canPay)
			{
				return ("Payment due", "warning");
			}

			if (rentalStatus == RentalStatus.Pending)
			{
				return ("Payment due", "warning");
			}

			if (tripStatus == TripStatus.InTransit)
			{
				return ("In transit", "active");
			}

			if (tripStatus == TripStatus.Completed)
			{
				return ("Completed", "success");
			}

			if (rentalStatus == RentalStatus.Approved && !string.IsNullOrWhiteSpace(driverName))
			{
				return ("Driver assigned", "success");
			}

			if (rentalStatus == RentalStatus.Approved)
			{
				return ("Preparing vehicle & driver", "active");
			}

			return ("Pending", "warning");
		}

		private async Task<IReadOnlyDictionary<int, (double RatingTen, int ReviewCount)>> GetDriverRatingsAsync(
			IEnumerable<int> driverIds)
		{
			var ids = driverIds.Distinct().ToList();
			if (ids.Count == 0)
			{
				return new Dictionary<int, (double RatingTen, int ReviewCount)>();
			}

			var reviews = await _context.Feedbacks
				.AsNoTracking()
				.Where(f => f.Transit != null
					&& f.Transit.DriverID != null
					&& ids.Contains(f.Transit.DriverID.Value)
					&& f.DriverProfessionalism != null
					&& f.DriverDriving != null
					&& f.DriverCourtesy != null)
				.Select(f => new
				{
					DriverId = f.Transit!.DriverID!.Value,
					f.DriverProfessionalism,
					f.DriverDriving,
					f.DriverCourtesy
				})
				.ToListAsync();

			return reviews
				.GroupBy(r => r.DriverId)
				.ToDictionary(
					g => g.Key,
					g =>
					{
						var stars = g.Select(r => RatingScale.OverallStars(
							r.DriverProfessionalism!.Value,
							r.DriverDriving!.Value,
							r.DriverCourtesy!.Value));
						return (RatingScale.ToTen(stars), g.Count());
					});
		}

		private static (Transit? Primary, string? DriverName, string? DriverImagePath, TripStatus? TripStatus, bool CanRate, bool HasFeedback) SummarizeRentalTransits(
			IReadOnlyList<Transit> rentalTransits)
		{
			if (rentalTransits.Count == 0)
			{
				return (null, null, null, null, false, false);
			}

			var primary = rentalTransits.OrderByDescending(t => t.TransitID).First();
			var tripStatus = rentalTransits.Any(t => t.TripStatus == TripStatus.InTransit)
				? TripStatus.InTransit
				: rentalTransits.Count > 0 && rentalTransits.All(t => t.TripStatus == TripStatus.Completed)
					? TripStatus.Completed
					: primary.TripStatus;
			var driverNames = rentalTransits
				.Where(t => !string.IsNullOrWhiteSpace(t.Driver?.Name))
				.Select(t => t.Driver!.Name!.Trim())
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

			string? driverName = driverNames.Count switch
			{
				0 => null,
				1 => driverNames[0],
				_ => "Multiple drivers"
			};

			var primaryDriver = rentalTransits.FirstOrDefault(t => t.Driver != null)?.Driver;
			string? driverImagePath = primaryDriver?.ImagePath;

			var canRate = rentalTransits.Any(t => t.TripStatus == TripStatus.Completed && t.Feedback == null);
			var hasFeedback = rentalTransits.Any(t => t.Feedback != null);

			return (primary, driverName, driverImagePath, tripStatus, canRate, hasFeedback);
		}

		private async Task<BookingTimelineTimestamps> BuildBookingTimelineTimestampsAsync(
			Rental rental,
			IReadOnlyList<Payment> payments,
			IReadOnlyList<Transit> transits)
		{
			var transitIds = transits.Select(t => t.TransitID).ToList();

			var logs = await _context.SystemLogs
				.AsNoTracking()
				.Where(l =>
					(l.EntityType == "Rental" && l.EntityId == rental.RentalId)
					|| (l.EntityType == "Transit"
						&& l.EntityId != null
						&& transitIds.Contains(l.EntityId.Value)))
				.ToListAsync();

			var firstPayment = payments
				.OrderBy(p => p.PaymentDate)
				.ThenBy(p => p.PaymentId)
				.FirstOrDefault();

			DateTime? confirmedAt = logs
				.Where(l => l.EntityType == "Rental" && l.Action == SystemLogAction.Approved)
				.OrderBy(l => l.CreatedAt)
				.Select(l => (DateTime?)l.CreatedAt)
				.FirstOrDefault();

			if (confirmedAt == null && rental.RentalStatus == RentalStatus.Approved)
			{
				confirmedAt = firstPayment?.PaymentDate;
			}

			DateTime? preparedAt = logs
				.Where(l => l.EntityType == "Transit"
					&& l.Action == SystemLogAction.Updated
					&& l.Summary.Contains("vehicle preparation", StringComparison.OrdinalIgnoreCase))
				.OrderByDescending(l => l.CreatedAt)
				.Select(l => (DateTime?)l.CreatedAt)
				.FirstOrDefault();

			if (preparedAt == null
				&& transits.Count > 0
				&& transits.All(TransitPreparation.IsPrepared))
			{
				var departure = transits
					.Where(t => t.DepartureTime.HasValue)
					.Select(t => t.DepartureTime!.Value)
					.DefaultIfEmpty(rental.PickupTime)
					.Max();
				preparedAt = rental.PickupDate.ToDateTime(departure);
			}

			DateTime? dispatchedAt = logs
				.Where(l => l.EntityType == "Transit" && l.Action == SystemLogAction.Started)
				.OrderBy(l => l.CreatedAt)
				.Select(l => (DateTime?)l.CreatedAt)
				.FirstOrDefault();

			if (dispatchedAt == null
				&& transits.Any(t => t.TripStatus is TripStatus.InTransit or TripStatus.Completed))
			{
				dispatchedAt = rental.PickupDate.ToDateTime(rental.PickupTime);
			}

			DateTime? completedAt = logs
				.Where(l => l.EntityType == "Transit" && l.Action == SystemLogAction.Completed)
				.OrderByDescending(l => l.CreatedAt)
				.Select(l => (DateTime?)l.CreatedAt)
				.FirstOrDefault();

			if (completedAt == null
				&& transits.Count > 0
				&& transits.All(t => t.TripStatus == TripStatus.Completed))
			{
				var returnTime = transits
					.Where(t => t.ReturnTime.HasValue)
					.Select(t => t.ReturnTime!.Value)
					.DefaultIfEmpty(rental.ReturnTime)
					.Max();
				completedAt = rental.ReturnDate.ToDateTime(returnTime);
			}

			return new BookingTimelineTimestamps
			{
				SubmittedAt = firstPayment?.PaymentDate,
				ConfirmedAt = confirmedAt,
				PreparedAt = preparedAt,
				DispatchedAt = dispatchedAt,
				CompletedAt = completedAt
			};
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
