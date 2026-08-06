using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Models;
using EasyRent_Checking.Data;
using EasyRent_Checking.Services;

public class PaymentsController : Controller
{
	private readonly EasyRent_CheckingContext _context;
	private readonly IWebHostEnvironment _webHostEnvironment;

	public PaymentsController(EasyRent_CheckingContext context, IWebHostEnvironment webHostEnvironment)
	{
		_context = context;
		_webHostEnvironment = webHostEnvironment;
	}

	// GET: PAYMENTS
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

		var paymentsQuery = _context.Payment
			.AsNoTracking()
			.Include(p => p.Reservation)
			.AsQueryable();

		ViewData["TotalBookingsCount"] = await paymentsQuery.CountAsync();
		ViewData["PendingPaymentCount"] = await paymentsQuery.CountAsync(p => p.PaymentStatus == PaymentStatus.Pending);

		if (!string.IsNullOrEmpty(searchString))
		{
			var term = searchString.Trim();
			var idToken = term.StartsWith("BK-", StringComparison.OrdinalIgnoreCase)
				? term[3..]
				: term;
			var hasBookingId = int.TryParse(idToken, out var bookingId);

			paymentsQuery = paymentsQuery.Where(p =>
				(p.Reservation != null && (
					p.Reservation.CustomerName.Contains(term)
					|| p.Reservation.ContactNumber.Contains(term)))
				|| p.PaymentMethod.Contains(term)
				|| p.PaymentType.Contains(term)
				|| (p.TransactionReference != null && p.TransactionReference.Contains(term))
				|| (hasBookingId && p.ReservationId == bookingId)
				|| p.PaymentId == bookingId);
		}

		if (!string.IsNullOrEmpty(currentFilter) && Enum.TryParse(currentFilter, true, out PaymentStatus filterStatus))
		{
			paymentsQuery = paymentsQuery.Where(p => p.PaymentStatus == filterStatus);
		}

		paymentsQuery = sortBy switch
		{
			"CustomerName" => paymentsQuery.OrderBy(p => p.Reservation!.CustomerName),
			"AmountPaid" => paymentsQuery.OrderByDescending(p => p.AmountPaid),
			"Balance" => paymentsQuery.OrderByDescending(p => p.TotalAmount - p.AmountPaid),
			"PaymentType" => paymentsQuery.OrderBy(p => p.PaymentType),
			"PaymentStatus" => paymentsQuery.OrderBy(p => p.PaymentStatus),
			"PaymentDate" => paymentsQuery.OrderByDescending(p => p.PaymentDate),
			_ => paymentsQuery.OrderByDescending(p => p.PaymentId)
		};

		var totalCount = await paymentsQuery.CountAsync();
		var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
		if (pageNumber > totalPages)
		{
			pageNumber = totalPages;
		}

		ViewData["PageIndex"] = pageNumber;
		ViewData["TotalPages"] = totalPages;
		ViewData["TotalCount"] = totalCount;
		ViewData["PageSize"] = pageSize;

		var payments = await paymentsQuery
			.Skip((pageNumber - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync();

		return View(payments);
	}

	// GET: PAYMENTS/Details/5
	public async Task<IActionResult> Details(int? paymentid)
	{
		if (paymentid == null)
		{
			return NotFound();
		}

		var payment = await _context.Payment
			.AsNoTracking()
			.Include(p => p.Reservation)!
				.ThenInclude(r => r!.Details)!
					.ThenInclude(d => d!.Vehicle)
			.FirstOrDefaultAsync(m => m.PaymentId == paymentid);
		if (payment == null)
		{
			return NotFound();
		}

		PopulatePaymentFareBreakdown(payment.Reservation);
		return View(payment);
	}

	// POST: PAYMENTS/Approve/5
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Approve(int paymentid)
	{
		var payment = await _context.Payment.FindAsync(paymentid);
		if (payment == null)
		{
			return NotFound();
		}

		payment.PaymentStatus = PaymentStatus.Approved;
		await _context.SaveChangesAsync();
		await EnsureTransitForReservationAsync(payment.ReservationId);
		TempData["SuccessMessage"] = "Payment approved. Booking is now in Transit.";
		return RedirectToAction(nameof(Details), new { paymentid });
	}

	// POST: PAYMENTS/Reject/5
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Reject(int paymentid)
	{
		var payment = await _context.Payment.FindAsync(paymentid);
		if (payment == null)
		{
			return NotFound();
		}

		payment.PaymentStatus = PaymentStatus.Rejected;
		await _context.SaveChangesAsync();
		TempData["SuccessMessage"] = "Payment rejected.";
		return RedirectToAction(nameof(Details), new { paymentid });
	}

	// GET: PAYMENTS/Create
	public async Task<IActionResult> Create(int? reservationId)
	{
		var payment = new Payment
		{
			ReservationId = reservationId ?? 0,
			PaymentDate = DateTime.Now,
			PaymentStatus = PaymentStatus.Approved,
			PaymentMethod = string.Empty,
			PaymentType = "Full Payment"
		};

		await PopulatePaymentCreateContextAsync(payment);
		return View(payment);
	}

	// POST: PAYMENTS/Create
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Create([Bind("PaymentId,ReservationId,PaymentMethod,PaymentType,TotalAmount,AmountPaid,AccountName,TransactionReference,PaymentDate,ReceiptImagePath,ReceiptImageFile,PaymentStatus,PaymentNotes")] Payment payment)
	{
		if (payment.ReservationId <= 0)
		{
			ModelState.AddModelError(nameof(payment.ReservationId), "Reservation is required.");
		}

		if (string.IsNullOrWhiteSpace(payment.PaymentType))
		{
			ModelState.AddModelError(nameof(payment.PaymentType), "Please select a payment type.");
		}

		if (string.IsNullOrWhiteSpace(payment.PaymentMethod))
		{
			ModelState.AddModelError(nameof(payment.PaymentMethod), "Please select a payment method.");
		}

		if (ModelState.IsValid)
		{
			// Admin-created payments are approved immediately.
			payment.PaymentStatus = PaymentStatus.Approved;
			payment.PaymentDate = payment.PaymentDate == default ? DateTime.Now : payment.PaymentDate;
			await SaveReceiptImageAsync(payment);
			_context.Add(payment);
			await _context.SaveChangesAsync();
			await EnsureTransitForReservationAsync(payment.ReservationId);
			return RedirectToAction(nameof(CreateSuccess), new { paymentid = payment.PaymentId });
		}

		await PopulatePaymentCreateContextAsync(payment);
		return View(payment);
	}

	// GET: PAYMENTS/CreateSuccess/5
	public async Task<IActionResult> CreateSuccess(int? paymentid)
	{
		if (paymentid == null)
		{
			return NotFound();
		}

		var payment = await _context.Payment
			.AsNoTracking()
			.Include(p => p.Reservation)
			.FirstOrDefaultAsync(p => p.PaymentId == paymentid);
		if (payment == null)
		{
			return NotFound();
		}

		var bookingLabel = payment.ReservationId > 0
			? $"BK-{payment.ReservationId:D5}"
			: "this booking";
		var amountLabel = payment.AmountPaid.ToString("N2");

		var transit = await _context.Transit
			.AsNoTracking()
			.FirstOrDefaultAsync(t => t.ReservationID == payment.ReservationId);

		var model = new CreateSuccessViewModel
		{
			PageTitle = "Create Payment",
			ActivePage = "Payments",
			Heading = "Payment Successfully Created",
			MessageHtml = $"Payment of <strong>₱{amountLabel}</strong> for <strong>{bookingLabel}</strong> has been recorded and approved.",
			PrimaryActionText = transit != null ? "Open Transit" : "View Payments",
			PrimaryActionUrl = transit != null
				? (Url.Action("Details", "Transits", new { transitid = transit.TransitID }) ?? "")
				: (Url.Action(nameof(Index)) ?? ""),
			SecondaryActionText = "See Payment Details",
			SecondaryActionUrl = Url.Action(nameof(Details), new { paymentid = payment.PaymentId }) ?? "",
			ShowSecondaryPlusIcon = false
		};

		return View("CreateSuccess", model);
	}

	private async Task PopulatePaymentCreateContextAsync(Payment payment)
	{
		const decimal depositPercent = 5m;
		decimal totalFare = payment.TotalAmount;

		if (payment.ReservationId > 0)
		{
			var reservation = await _context.Reservation
				.AsNoTracking()
				.Include(r => r.Details)!
					.ThenInclude(d => d!.Vehicle)
				.FirstOrDefaultAsync(r => r.ReservationId == payment.ReservationId);

			if (reservation?.Details?.Vehicle != null)
			{
				totalFare = CalculateReservationFare(reservation);
				ViewData["ReservationLabel"] = $"BK-{reservation.ReservationId:D5} · {reservation.CustomerName}";
			}
			else if (reservation != null)
			{
				ViewData["ReservationLabel"] = $"BK-{reservation.ReservationId:D5} · {reservation.CustomerName}";
			}
		}

		if (totalFare <= 0)
		{
			totalFare = payment.TotalAmount > 0 ? payment.TotalAmount : 0m;
		}

		payment.TotalAmount = totalFare;
		ViewData["TotalFare"] = totalFare;
		ViewData["DepositPercent"] = depositPercent;
	}

	private void PopulatePaymentFareBreakdown(Reservation? reservation)
	{
		const int baseHours = 8;
		var details = reservation?.Details;
		var vehicle = details?.Vehicle;

		decimal basePrice = 0m;
		var succeedingHours = 0;
		decimal succeedingFee = 0m;
		decimal succeedingAmount = 0m;
		decimal discountAmount = 0m;
		decimal total = 0m;

		if (details != null && vehicle != null)
		{
			basePrice = vehicle.BasePrice;
			succeedingFee = vehicle.SucceedingFee;
			var start = details.PickupDate.ToDateTime(details.PickupTime);
			var end = details.ReturnDate.ToDateTime(details.ReturnTime);
			var hours = end > start ? (int)Math.Round((end - start).TotalHours) : 0;
			succeedingHours = Math.Max(0, hours - baseHours);
			succeedingAmount = succeedingHours * succeedingFee;
			var subtotal = basePrice + succeedingAmount;
			discountAmount = details.Discount == Discount.Yes ? Math.Round(subtotal * 0.10m, 2) : 0m;
			total = Math.Round(subtotal - discountAmount, 2);
		}

		ViewData["BasePrice"] = basePrice;
		ViewData["SucceedingHours"] = succeedingHours;
		ViewData["SucceedingFee"] = succeedingFee;
		ViewData["SucceedingAmount"] = succeedingAmount;
		ViewData["HasSeniorDiscount"] = details?.Discount == Discount.Yes;
		ViewData["DiscountAmount"] = discountAmount;
		ViewData["FareTotal"] = total;
		ViewData["DiscountImagePath"] = details?.DiscountImagePath;
	}

	private async Task SaveReceiptImageAsync(Payment payment)
	{
		if (payment.ReceiptImageFile == null)
		{
			return;
		}

		payment.ReceiptImagePath = await ImageStorage.SaveAsync(
			_webHostEnvironment,
			payment.ReceiptImageFile,
			ImageStorage.PaymentReceiptsFolder);
	}

	private async Task EnsureTransitForReservationAsync(int reservationId)
	{
		if (reservationId <= 0)
		{
			return;
		}

		var alreadyExists = await _context.Transit.AnyAsync(t => t.ReservationID == reservationId);
		if (alreadyExists)
		{
			return;
		}

		var details = await _context.ReservationDetails
			.AsNoTracking()
			.FirstOrDefaultAsync(d => d.ReservationID == reservationId);
		if (details == null)
		{
			return;
		}

		_context.Transit.Add(new Transit
		{
			ReservationID = reservationId,
			VehicleID = details.VehicleId,
			TripStatus = TripStatus.Scheduled
		});
		await _context.SaveChangesAsync();
	}

	private static decimal CalculateReservationFare(Reservation reservation)
	{
		var details = reservation.Details;
		var vehicle = details?.Vehicle;
		if (details == null || vehicle == null)
		{
			return 0m;
		}

		const int baseHours = 8;
		var start = details.PickupDate.ToDateTime(details.PickupTime);
		var end = details.ReturnDate.ToDateTime(details.ReturnTime);
		var hours = end > start ? (int)Math.Round((end - start).TotalHours) : 0;
		var succeedingHours = Math.Max(0, hours - baseHours);
		var subtotal = vehicle.BasePrice + (succeedingHours * vehicle.SucceedingFee);
		var discount = details.Discount == Discount.Yes ? subtotal * 0.10m : 0m;
		return Math.Round(subtotal - discount, 2);
	}

	// GET: PAYMENTS/Edit/5
	public async Task<IActionResult> Edit(int? paymentid)
	{
		if (paymentid == null)
		{
			return NotFound();
		}

		var payment = await _context.Payment.FindAsync(paymentid);
		if (payment == null)
		{
			return NotFound();
		}
		return View(payment);
	}

	// POST: PAYMENTS/Edit/5
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Edit(int? paymentid, [Bind("PaymentId,ReservationId,PaymentMethod,PaymentType,TotalAmount,AmountPaid,AccountName,TransactionReference,PaymentDate,ReceiptImagePath,ReceiptImageFile,PaymentStatus,PaymentNotes")] Payment payment)
	{
		if (paymentid != payment.PaymentId)
		{
			return NotFound();
		}

		if (ModelState.IsValid)
		{
			try
			{
				var existing = await _context.Payment.AsNoTracking()
					.FirstOrDefaultAsync(p => p.PaymentId == payment.PaymentId);
				if (existing != null && payment.ReceiptImageFile == null)
				{
					payment.ReceiptImagePath = existing.ReceiptImagePath;
				}

				await SaveReceiptImageAsync(payment);
				_context.Update(payment);
				await _context.SaveChangesAsync();

				if (payment.PaymentStatus == PaymentStatus.Approved)
				{
					await EnsureTransitForReservationAsync(payment.ReservationId);
				}
			}
			catch (DbUpdateConcurrencyException)
			{
				if (!PaymentExists(payment.PaymentId))
				{
					return NotFound();
				}
				else
				{
					throw;
				}
			}
			return RedirectToAction(nameof(Index));
		}
		return View(payment);
	}

	// GET: PAYMENTS/Delete/5
	public async Task<IActionResult> Delete(int? paymentid)
	{
		if (paymentid == null)
		{
			return NotFound();
		}

		var payment = await _context.Payment
			.FirstOrDefaultAsync(m => m.PaymentId == paymentid);
		if (payment == null)
		{
			return NotFound();
		}

		return View(payment);
	}

	// POST: PAYMENTS/Delete/5
	[HttpPost, ActionName("Delete")]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> DeleteConfirmed(int? paymentid)
	{
		var payment = await _context.Payment.FindAsync(paymentid);
		if (payment != null)
		{
			_context.Payment.Remove(payment);
		}

		await _context.SaveChangesAsync();
		return RedirectToAction(nameof(Index));
	}

	private bool PaymentExists(int? paymentid)
	{
		return _context.Payment.Any(e => e.PaymentId == paymentid);
	}
}
