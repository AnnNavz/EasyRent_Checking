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
				.Include(r => r.Customer).ThenInclude(c => c!.User)
				.Include(r => r.RentalVehicles)
					.ThenInclude(rv => rv.Vehicle)
				.AsQueryable();

			var today = DateOnly.FromDateTime(DateTime.Today);
			var thisMonthStart = new DateOnly(today.Year, today.Month, 1);
			var lastMonthStart = thisMonthStart.AddMonths(-1);

			var totalBookingsCount = await rentalsQuery.CountAsync();
			var pendingApprovalsCount = await rentalsQuery.CountAsync(r => r.RentalStatus == RentalStatus.Pending);
			var refundableCount = await rentalsQuery.CountAsync(r =>
				r.RefundRequestedAt != null
				&& r.RefundedAt == null
				&& r.RefundRejectedAt == null
				&& r.RentalStatus == RentalStatus.Cancelled);

			var totalThisMonth = await rentalsQuery.CountAsync(r => r.PickupDate >= thisMonthStart);
			var totalLastMonth = await rentalsQuery.CountAsync(r =>
				r.PickupDate >= lastMonthStart
				&& r.PickupDate < thisMonthStart);
			var pendingThisMonth = await rentalsQuery.CountAsync(r =>
				r.RentalStatus == RentalStatus.Pending
				&& r.PickupDate >= thisMonthStart);
			var pendingLastMonth = await rentalsQuery.CountAsync(r =>
				r.RentalStatus == RentalStatus.Pending
				&& r.PickupDate >= lastMonthStart
				&& r.PickupDate < thisMonthStart);

			ViewData["TotalBookingsCount"] = totalBookingsCount;
			ViewData["PendingApprovalsCount"] = pendingApprovalsCount;
			ViewData["RefundableCount"] = refundableCount;
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
					|| r.RentalVehicles.Any(rv => rv.Vehicle != null && (
						rv.Vehicle.Brand.Contains(term)
						|| rv.Vehicle.Model.Contains(term)
						|| rv.Vehicle.PlateNumber.Contains(term)))
					|| (hasBookingId && r.RentalId == bookingId));
			}

			if (string.Equals(currentFilter, "Refundable", StringComparison.OrdinalIgnoreCase))
			{
				rentalsQuery = rentalsQuery.Where(r =>
					r.RefundRequestedAt != null
					&& r.RefundedAt == null
					&& r.RefundRejectedAt == null
					&& r.RentalStatus == RentalStatus.Cancelled);
			}
			else if (!string.IsNullOrEmpty(currentFilter) && Enum.TryParse(currentFilter, true, out RentalStatus filterStatus))
			{
				rentalsQuery = rentalsQuery.Where(r => r.RentalStatus == filterStatus);
			}

			rentalsQuery = sortBy switch
			{
				"CustomerName" => rentalsQuery.OrderBy(r => r.CustomerName),
				"PickupDate" => rentalsQuery.OrderBy(r => r.PickupDate).ThenBy(r => r.PickupTime),
				"Status" => rentalsQuery.OrderBy(r => r.RentalStatus),
				"Vehicle" => rentalsQuery
					.OrderBy(r => r.RentalVehicles.Select(rv => rv.Vehicle!.Brand).FirstOrDefault())
					.ThenBy(r => r.RentalVehicles.Select(rv => rv.Vehicle!.Model).FirstOrDefault()),
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
				.Include(r => r.Customer).ThenInclude(c => c!.User)
				.Include(r => r.RentalVehicles)
					.ThenInclude(rv => rv.Vehicle)
				.FirstOrDefaultAsync(m => m.RentalId == id);
			if (rental == null)
			{
				return NotFound();
			}

			var latestPayment = await RentalResolution.GetLatestRentalPaymentAsync(_context, rental.RentalId);
			var cancellationFeePayment = await RentalResolution.GetCancellationFeePaymentAsync(_context, rental.RentalId);

			var transits = await _context.Transits
				.AsNoTracking()
				.Include(t => t.Driver)
				.Include(t => t.Vehicle)
				.Where(t => t.RentalID == rental.RentalId)
				.OrderBy(t => t.TransitID)
				.ToListAsync();

			ViewData["LatestPayment"] = latestPayment;
			ViewData["CancellationFeePayment"] = cancellationFeePayment;
			ViewData["Transit"] = transits.FirstOrDefault();
			ViewData["Transits"] = transits;
			ViewData["SelectedVehicles"] = await RentalVehicleWorkflow.LoadSelectedVehiclesAsync(_context, rental);
			ViewData["AmountPaid"] = await RentalResolution.GetAmountPaidAsync(_context, rental.RentalId);
			ViewData["HasPendingRefundRequest"] = RentalResolution.HasPendingRefundRequest(rental);
			ViewData["ProcessRefundModel"] = RentalResolution.HasPendingRefundRequest(rental)
				? new ProcessCustomerRefundViewModel
				{
					RentalId = rental.RentalId,
					RefundAmount = rental.RefundRequestedAmount ?? await RentalResolution.GetAmountPaidAsync(_context, rental.RentalId),
					CustomerReason = rental.CustomerCancellationReason ?? "",
					AmountPaid = await RentalResolution.GetAmountPaidAsync(_context, rental.RentalId),
					RefundRequestedAmount = rental.RefundRequestedAmount ?? 0m
				}
				: null;
			ViewData["RejectRefundModel"] = RentalResolution.HasPendingRefundRequest(rental)
				? new RejectCustomerRefundViewModel
				{
					RentalId = rental.RentalId,
					CustomerReason = rental.CustomerCancellationReason ?? "",
					RefundRequestedAmount = rental.RefundRequestedAmount ?? 0m
				}
				: null;
			return View(rental);
		}

		// POST: Rentals/Approve/5
		// Approves a pending booking and sends confirmation email
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Approve(int id)
		{
			// Step 1: Load rental with details and vehicles
			var rental = await _context.Rentals
				.Include(r => r.RentalVehicles)
				.FirstOrDefaultAsync(r => r.RentalId == id);
			if (rental == null)
			{
				return NotFound();
			}

			// Step 2: Check for pending refund requests
			if (RentalResolution.HasPendingRefundRequest(rental))
			{
				TempData["SuccessMessage"] = "This booking has a pending refund request. Process the refund before confirming.";
				return RedirectToAction(nameof(Details), new { id });
			}

			if (rental.RentalStatus == RentalStatus.Expired)
			{
				TempData["SuccessMessage"] = "This reservation already expired and cannot be confirmed.";
				return RedirectToAction(nameof(Details), new { id });
			}

			var hasPayment = await _context.Payments.AnyAsync(p => p.RentalId == id);
			if (rental.RentalOption == RentalOption.Reserve && !hasPayment)
			{
				TempData["SuccessMessage"] = "This hold still has no payment. The booking confirms automatically when payment is recorded.";
				return RedirectToAction(nameof(Details), new { id });
			}

			await RentalConfirmation.ConfirmPaidBookingAsync(
				_context,
				_logs,
				_bookingEmailService,
				rental);

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
		// Rejects a pending booking and marks it as cancelled
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Reject(int id)
		{
			// Step 1: Load the rental
			var rental = await _context.Rentals.FindAsync(id);
			if (rental == null)
			{
				return NotFound();
			}

			// Step 2: Check for pending refund requests
			if (RentalResolution.HasPendingRefundRequest(rental))
			{
				TempData["SuccessMessage"] = "This booking has a pending refund request. Process the refund before rejecting.";
				return RedirectToAction(nameof(Details), new { id });
			}

			// Step 3: Update rental status to Cancelled with no cancellation fee
			rental.RentalStatus = RentalStatus.Cancelled;
			rental.CancelledAt = DateTime.Now;
			rental.CancellationFee = 0m;

			// Step 4: Cancel associated transit if not already completed
			var transit = await _context.Transits.FirstOrDefaultAsync(t => t.RentalID == id);
			if (transit != null && transit.TripStatus is not TripStatus.Completed)
			{
				transit.TripStatus = TripStatus.Cancelled;
			}

			// Step 5: Log the rejection action
			_logs.Record(
				SystemLogAction.Rejected,
				SystemLogCategory.Booking,
				$"Rejected booking BK-{rental.RentalId:D5} for {rental.CustomerName}.",
				"Rental",
				rental.RentalId);
			await _context.SaveChangesAsync();
			
			// Step 6: Show success message and redirect
			TempData["SuccessMessage"] = "Booking rejected and marked as cancelled.";
			return RedirectToAction(nameof(Details), new { id });
		}

		// GET: Rentals/Resolve/5
		public async Task<IActionResult> Resolve(int id, int? transitid, string? returnUrl)
		{
			var rental = await LoadRentalForResolutionAsync(id);
			if (rental == null)
			{
				return NotFound();
			}

			var transits = await _context.Transits
				.AsNoTracking()
				.Include(t => t.Vehicle)
				.Where(t => t.RentalID == id)
				.OrderBy(t => t.TransitID)
				.ToListAsync();

			if (!RentalResolution.CanResolve(rental, transits))
			{
				TempData["ErrorMessage"] = "This booking can no longer be resolved (trip already started or booking not approved).";
				return RedirectToAction(nameof(Details), new { id });
			}

			var transit = transitid.HasValue
				? transits.FirstOrDefault(t => t.TransitID == transitid.Value)
				: transits.FirstOrDefault(t => t.TripStatus is TripStatus.Scheduled or TripStatus.Delayed);

			var currentVehicleId = transit?.VehicleID
				?? RentalVehicleWorkflow.GetPrimaryVehicleId(rental);
			var currentVehicle = transit?.Vehicle
				?? await _context.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.VehicleId == currentVehicleId);
			if (currentVehicle == null)
			{
				TempData["ErrorMessage"] = "Assigned vehicle could not be found.";
				return RedirectToAction(nameof(Details), new { id });
			}

			var amountPaid = await RentalResolution.GetAmountPaidAsync(_context, id);
			var model = new ResolveBookingViewModel
			{
				RentalId = id,
				TransitId = transit?.TransitID,
				ReturnUrl = returnUrl,
				BookingLabel = $"BK-{id:D5}",
				CustomerName = rental.CustomerName,
				CurrentVehicleId = currentVehicleId,
				CurrentVehicleLabel = $"{currentVehicle.Brand} {currentVehicle.Model} ({currentVehicle.PlateNumber})".Trim(),
				AmountPaid = amountPaid,
				RefundAmount = amountPaid,
				PickupDate = rental.PickupDate,
				PickupTime = rental.PickupTime,
				PickupLocation = rental.PickupLocation,
				ReplacementVehicles = await RentalResolution.GetReplacementCandidatesAsync(
					_context,
					rental,
					currentVehicleId)
			};

			ViewData["Title"] = "Resolve Booking";
			ViewData["ActivePage"] = "Rentals";
			ViewData["ResolveActiveTab"] = model.ReplacementVehicles.Count > 0 ? "replace" : "refund";
			return View(model);
		}

		// POST: Rentals/ReplaceVehicle/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ReplaceVehicle(
			int id,
			[Bind("RentalId,TransitId,ReturnUrl,Reason,ReplacementVehicleId,Notes")] ResolveBookingViewModel model)
		{
			if (id != model.RentalId)
			{
				return NotFound();
			}

			ModelState.Remove(nameof(model.RefundReceiptImageFile));
			ModelState.Remove(nameof(model.RefundAmount));

			if (model.ReplacementVehicleId is not > 0)
			{
				ModelState.AddModelError(nameof(model.ReplacementVehicleId), "Please select a replacement vehicle.");
			}

			var rentalPreview = await LoadRentalForResolutionAsync(id);
			var transitPreview = model.TransitId.HasValue
				? await _context.Transits.AsNoTracking().FirstOrDefaultAsync(t => t.TransitID == model.TransitId.Value)
				: null;
			model.CurrentVehicleId = transitPreview?.VehicleID
				?? (rentalPreview != null ? RentalVehicleWorkflow.GetPrimaryVehicleId(rentalPreview) : 0);

			if (!ModelState.IsValid)
			{
				return await ReloadResolveViewAsync(model, "replace");
			}

			var rental = await LoadRentalForResolutionAsync(id);
			if (rental == null)
			{
				return NotFound();
			}

			var transits = await _context.Transits
				.Where(t => t.RentalID == id)
				.ToListAsync();

			if (!RentalResolution.CanResolve(rental, transits))
			{
				TempData["ErrorMessage"] = "This booking can no longer be resolved.";
				return RedirectToAction(nameof(Details), new { id });
			}

			var transit = model.TransitId.HasValue
				? transits.FirstOrDefault(t => t.TransitID == model.TransitId.Value)
				: transits.FirstOrDefault(t => t.TripStatus is TripStatus.Scheduled or TripStatus.Delayed);

			var currentVehicleId = transit?.VehicleID
				?? RentalVehicleWorkflow.GetPrimaryVehicleId(rental);
			var previousVehicle = await _context.Vehicles.AsNoTracking()
				.FirstOrDefaultAsync(v => v.VehicleId == currentVehicleId);
			if (previousVehicle == null)
			{
				TempData["ErrorMessage"] = "Current vehicle could not be found.";
				return RedirectToAction(nameof(Resolve), new { id, transitid = model.TransitId, returnUrl = model.ReturnUrl });
			}

			var result = await RentalResolution.ReplaceVehicleAsync(
				_context,
				rental,
				transit,
				currentVehicleId,
				model.ReplacementVehicleId!.Value,
				model.Reason);

			if (!result.Success || result.NewVehicle == null)
			{
				model.CurrentVehicleId = currentVehicleId;
				ModelState.AddModelError(string.Empty, result.Error ?? "Could not replace the vehicle.");
				return await ReloadResolveViewAsync(model, "replace");
			}

			await TransitIssueSync.ResolveForRentalAsync(
				_context,
				id,
				model.TransitId,
				"ReplaceVehicle",
				model.Reason);
			await VehicleStatusSync.ApplyAsync(_context, currentVehicleId);
			await VehicleStatusSync.ApplyAsync(_context, model.ReplacementVehicleId!.Value);

			_logs.Record(
				SystemLogAction.Updated,
				SystemLogCategory.Booking,
				$"Replaced vehicle on BK-{rental.RentalId:D5} ({model.Reason}).",
				"Rental",
				rental.RentalId);
			await _context.SaveChangesAsync();

			await _bookingEmailService.SendVehicleReplacedAsync(
				rental,
				previousVehicle,
				result.NewVehicle,
				model.Reason);

			TempData["SuccessMessage"] = "Vehicle replaced. The customer was emailed about the change.";
			return RedirectAfterResolve(id, model.TransitId, model.ReturnUrl);
		}

		// POST: Rentals/CancelWithRefund/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CancelWithRefund(
			int id,
			[Bind("RentalId,TransitId,ReturnUrl,Reason,RefundAmount,RefundReceiptImageFile,Notes")] ResolveBookingViewModel model)
		{
			if (id != model.RentalId)
			{
				return NotFound();
			}

			ModelState.Remove(nameof(model.ReplacementVehicleId));

			var rental = await LoadRentalForResolutionAsync(id);
			if (rental == null)
			{
				return NotFound();
			}

			var transits = await _context.Transits
				.AsNoTracking()
				.Where(t => t.RentalID == id)
				.ToListAsync();

			if (!RentalResolution.CanResolve(rental, transits))
			{
				TempData["ErrorMessage"] = "This booking can no longer be resolved.";
				return RedirectToAction(nameof(Details), new { id });
			}

			var amountPaid = await RentalResolution.GetAmountPaidAsync(_context, id);
			if (model.RefundAmount < 0 || model.RefundAmount > amountPaid)
			{
				ModelState.AddModelError(
					nameof(model.RefundAmount),
					amountPaid > 0
						? $"Refund cannot exceed the amount paid (₱{amountPaid:N2})."
						: "Refund amount must be zero when nothing was paid.");
			}

			if (model.RefundAmount > 0 && model.RefundReceiptImageFile == null)
			{
				ModelState.AddModelError(
					nameof(model.RefundReceiptImageFile),
					"Please upload the refund receipt.");
			}

			if (!ModelState.IsValid)
			{
				model.AmountPaid = amountPaid;
				var transitPreview = model.TransitId.HasValue
					? await _context.Transits.AsNoTracking().FirstOrDefaultAsync(t => t.TransitID == model.TransitId.Value)
					: null;
				model.CurrentVehicleId = transitPreview?.VehicleID
					?? RentalVehicleWorkflow.GetPrimaryVehicleId(rental);
				return await ReloadResolveViewAsync(model, "refund");
			}

			string? receiptPath = null;
			if (model.RefundReceiptImageFile != null && model.RefundReceiptImageFile.Length > 0)
			{
				receiptPath = await ImageStorage.SaveAsync(
					_webHostEnvironment,
					model.RefundReceiptImageFile,
					ImageStorage.RefundReceiptsFolder);
			}

			await RentalResolution.CancelWithRefundAsync(
				_context,
				rental,
				model.RefundAmount,
				model.Reason,
				receiptPath,
				model.Notes);

			await TransitIssueSync.ResolveForRentalAsync(
				_context,
				id,
				model.TransitId,
				"RefundCustomer",
				model.Reason);

			_logs.Record(
				SystemLogAction.Cancelled,
				SystemLogCategory.Booking,
				$"Cancelled BK-{rental.RentalId:D5} with refund ₱{model.RefundAmount:N2} ({model.Reason}).",
				"Rental",
				rental.RentalId);
			await _context.SaveChangesAsync();

			await _bookingEmailService.SendRefundIssuedAsync(
				rental,
				model.RefundAmount,
				model.Reason,
				receiptPath,
				_webHostEnvironment);

			TempData["SuccessMessage"] = model.RefundAmount > 0
				? $"Booking cancelled. Refund of ₱{model.RefundAmount:N2} recorded and emailed to the customer."
				: "Booking cancelled at no charge.";
			return RedirectAfterResolve(id, model.TransitId, model.ReturnUrl);
		}

		// POST: Rentals/ProcessCustomerRefund/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ProcessCustomerRefund(
			int id,
			[Bind("RentalId,RefundAmount,RefundReceiptImageFile,Notes")] ProcessCustomerRefundViewModel model)
		{
			if (id != model.RentalId)
			{
				return NotFound();
			}

			var rental = await _context.Rentals
				.Include(r => r.RentalVehicles)
				.FirstOrDefaultAsync(r => r.RentalId == id);
			if (rental == null)
			{
				return NotFound();
			}

			if (!RentalResolution.HasPendingRefundRequest(rental))
			{
				TempData["ErrorMessage"] = "This booking does not have a pending refund request.";
				return RedirectToAction(nameof(Details), new { id });
			}

			var amountPaid = await RentalResolution.GetAmountPaidAsync(_context, id);
			var maxRefund = rental.RefundRequestedAmount ?? amountPaid;
			if (model.RefundAmount < 0 || model.RefundAmount > maxRefund)
			{
				ModelState.AddModelError(
					nameof(model.RefundAmount),
					maxRefund > 0
						? $"Refund cannot exceed the requested amount (₱{maxRefund:N2})."
						: "Refund amount must be zero when nothing was paid.");
			}

			if (model.RefundAmount > 0 && model.RefundReceiptImageFile == null)
			{
				ModelState.AddModelError(
					nameof(model.RefundReceiptImageFile),
					"Please upload the refund receipt.");
			}

			if (!ModelState.IsValid)
			{
				TempData["ErrorMessage"] = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage
					?? "Please complete all refund fields.";
				return RedirectToAction(nameof(Details), new { id });
			}

			string? receiptPath = null;
			if (model.RefundReceiptImageFile != null && model.RefundReceiptImageFile.Length > 0)
			{
				receiptPath = await ImageStorage.SaveAsync(
					_webHostEnvironment,
					model.RefundReceiptImageFile,
					ImageStorage.RefundReceiptsFolder);
			}

			try
			{
				await RentalResolution.ProcessCustomerRefundAsync(
					_context,
					rental,
					model.RefundAmount,
					receiptPath,
					model.Notes);
			}
			catch (InvalidOperationException ex)
			{
				TempData["ErrorMessage"] = ex.Message;
				return RedirectToAction(nameof(Details), new { id });
			}

			var reason = rental.CustomerCancellationReason ?? "the customer cancelled the booking";
			_logs.Record(
				SystemLogAction.Cancelled,
				SystemLogCategory.Booking,
				$"Processed customer refund for BK-{rental.RentalId:D5}: ₱{model.RefundAmount:N2}.",
				"Rental",
				rental.RentalId);

			await _bookingEmailService.SendRefundIssuedAsync(
				rental,
				model.RefundAmount,
				reason,
				receiptPath,
				_webHostEnvironment);

			TempData["SuccessMessage"] = model.RefundAmount > 0
				? $"Refund approved. ₱{model.RefundAmount:N2} was recorded and emailed to the customer."
				: "Refund request approved with no payment returned.";
			return RedirectToAction(nameof(Details), new { id });
		}

		// POST: Rentals/RejectCustomerRefund/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> RejectCustomerRefund(
			int id,
			[Bind("RentalId,Reason,OtherDetails")] RejectCustomerRefundViewModel model)
		{
			if (id != model.RentalId)
			{
				return NotFound();
			}

			if (string.Equals(model.Reason, "Other", StringComparison.OrdinalIgnoreCase)
				&& string.IsNullOrWhiteSpace(model.OtherDetails))
			{
				ModelState.AddModelError(nameof(model.OtherDetails), "Please provide details for the rejection reason.");
			}

			if (!RejectCustomerRefundViewModel.ReasonOptions.Contains(model.Reason))
			{
				ModelState.AddModelError(nameof(model.Reason), "Please select a valid rejection reason.");
			}

			var rental = await _context.Rentals
				.Include(r => r.RentalVehicles)
				.FirstOrDefaultAsync(r => r.RentalId == id);
			if (rental == null)
			{
				return NotFound();
			}

			if (!RentalResolution.HasPendingRefundRequest(rental))
			{
				TempData["ErrorMessage"] = "This refund request is no longer pending review.";
				return RedirectToAction(nameof(Details), new { id });
			}

			if (!ModelState.IsValid)
			{
				TempData["ErrorMessage"] = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage
					?? "Please complete all rejection fields.";
				return RedirectToAction(nameof(Details), new { id });
			}

			var storedReason = model.BuildStoredReason();
			if (string.IsNullOrWhiteSpace(storedReason))
			{
				TempData["ErrorMessage"] = "A rejection reason is required.";
				return RedirectToAction(nameof(Details), new { id });
			}

			try
			{
				await RentalResolution.RejectCustomerRefundAsync(_context, rental, storedReason);
			}
			catch (InvalidOperationException ex)
			{
				TempData["ErrorMessage"] = ex.Message;
				return RedirectToAction(nameof(Details), new { id });
			}

			_logs.Record(
				SystemLogAction.Rejected,
				SystemLogCategory.Booking,
				$"Rejected customer refund for BK-{rental.RentalId:D5}: {storedReason}.",
				"Rental",
				rental.RentalId);

			await _bookingEmailService.SendRefundRejectedAsync(rental, storedReason);

			TempData["SuccessMessage"] = "Refund request rejected. The customer was notified by email.";
			return RedirectToAction(nameof(Details), new { id });
		}

		private async Task<Rental?> LoadRentalForResolutionAsync(int rentalId)
			=> await _context.Rentals
				.Include(r => r.RentalVehicles)
				.FirstOrDefaultAsync(r => r.RentalId == rentalId);

		private async Task<IActionResult> ReloadResolveViewAsync(ResolveBookingViewModel model, string activeTab = "replace")
		{
			var rental = await LoadRentalForResolutionAsync(model.RentalId);
			if (rental == null)
			{
				return NotFound();
			}

			var currentVehicle = await _context.Vehicles.AsNoTracking()
				.FirstOrDefaultAsync(v => v.VehicleId == model.CurrentVehicleId);

			model.BookingLabel = $"BK-{model.RentalId:D5}";
			model.CustomerName = rental.CustomerName;
			model.AmountPaid = await RentalResolution.GetAmountPaidAsync(_context, model.RentalId);
			model.CurrentVehicleLabel = currentVehicle != null
				? $"{currentVehicle.Brand} {currentVehicle.Model} ({currentVehicle.PlateNumber})".Trim()
				: model.CurrentVehicleLabel;
			model.PickupDate = rental.PickupDate;
			model.PickupTime = rental.PickupTime;
			model.PickupLocation = rental.PickupLocation;
			model.ReplacementVehicles = await RentalResolution.GetReplacementCandidatesAsync(
				_context,
				rental,
				model.CurrentVehicleId);

			ViewData["Title"] = "Resolve Booking";
			ViewData["ActivePage"] = "Rentals";
			ViewData["ResolveActiveTab"] = activeTab;
			return View("Resolve", model);
		}

		private IActionResult RedirectAfterResolve(int rentalId, int? transitId, string? returnUrl)
		{
			if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
			{
				return Redirect(returnUrl);
			}

			if (transitId.HasValue)
			{
				return RedirectToAction("Details", "Transits", new { transitid = transitId.Value });
			}

			return RedirectToAction(nameof(Details), new { id = rentalId });
		}

		private async Task EnsureTransitForRentalAsync(int rentalId)
			=> await RentalConfirmation.EnsureTransitsAsync(_context, rentalId);

		private async Task SyncVehicleSelectionAsync(
			Rental rental,
			IReadOnlyList<int> vehicleIds,
			bool materializeLines)
		{
			var vehicles = await _context.Vehicles.AsNoTracking()
				.Where(v => vehicleIds.Contains(v.VehicleId))
				.ToListAsync();

			var ordered = vehicleIds
				.Select(id => vehicles.FirstOrDefault(v => v.VehicleId == id))
				.Where(v => v != null)
				.Cast<Vehicle>()
				.ToList();

			if (ordered.Count == 0)
			{
				return;
			}

			if (materializeLines)
			{
				await RentalVehicleWorkflow.MaterializeAsync(_context, rental, vehicleIds);
			}
			else
			{
				await RentalVehicleWorkflow.SyncPendingSelectionAsync(
					_context,
					rental,
					ordered,
					vehicleIds);
			}
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
				model.VehicleIds = new List<int> { vehicleId.Value };
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
		public async Task<IActionResult> Create([Bind("RentalId,VehicleId,VehicleIds,CustomerName,ContactNumber,PickupLocation,DropoffLocation,PickupDate,ReturnDate,PickupTime,ReturnTime,PassengerCount,Notes,Discount,DiscountImagePath,DiscountImageFile,RentalStatus,RentalOption,PaymentType,PaymentMethod,TotalAmount,AmountPaid,AccountName,TransactionReference,PaymentDate,ReceiptImageFile,PaymentNotes")] RentalInputModel model)
		{
			NormalizeVehicleIds(model);
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
				var bookingTotal = await ResolveBookingTotalAsync(model);
				ValidatePaymentInput(model, bookingTotal);
			}

			if (ModelState.IsValid)
			{
				await SaveDiscountImageAsync(model);

				var rental = new Rental();

				if (isPayLater)
				{
					// Walk-in pay-later hold — pending until payment is completed.
					model.RentalStatus = RentalStatus.Pending;
					model.ApplyTo(rental);
					rental.RentalOption = RentalOption.Reserve;
					rental.PaymentDueAt = DateTime.Now.AddDays(RentalRules.ReservePaymentWindowDays);
				}
				else
				{
					// Walk-in with payment — approved immediately.
					model.RentalStatus = RentalStatus.Approved;
					model.ApplyTo(rental);
					rental.RentalOption = RentalOption.Book;
					rental.PaymentDueAt = null;
				}

				rental.CustomerId = await ResolveCustomerIdByContactAsync(model.ContactNumber);

				_context.Rentals.Add(rental);
				await _context.SaveChangesAsync();

				await SyncVehicleSelectionAsync(
					rental,
					model.GetNormalizedVehicleIds(),
					materializeLines: !isPayLater);
				_logs.Record(
					SystemLogAction.Created,
					SystemLogCategory.Booking,
					$"Created booking BK-{rental.RentalId:D5} for {rental.CustomerName} ({model.GetNormalizedVehicleIds().Count} vehicle(s)).",
					"Rental",
					rental.RentalId);

				if (!isPayLater)
				{
					await SavePaymentForRentalAsync(model, rental);
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
			ViewData["InitialUnavailableDates"] = await GetUnavailableDatesForVehiclesAsync(model.GetNormalizedVehicleIds());
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
				.Include(r => r.RentalVehicles)
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

			var model = RentalInputModel.FromEntities(rental, payment, rental.RentalVehicles);
			await PopulateVehicleListAsync(model.VehicleId);
			ViewData["TotalFare"] = model.TotalAmount > 0
				? model.TotalAmount
				: (rental.RentalVehicles.OrderBy(rv => rv.SortOrder).Select(rv => rv.Vehicle?.BasePrice).FirstOrDefault() ?? 0m);
			ViewData["DepositPercent"] = 5m;
			ViewData["ShowPayLaterOption"] = true;
			ViewData["HidePaymentReminders"] = true;
			ViewData["ExistingReceiptPath"] = payment?.ReceiptImagePath;
			return View(model);
		}

		// POST: Rentals/Edit/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(int id, [Bind("RentalId,VehicleId,VehicleIds,CustomerName,ContactNumber,PickupLocation,DropoffLocation,PickupDate,ReturnDate,PickupTime,ReturnTime,PassengerCount,Notes,Discount,DiscountImagePath,DiscountImageFile,RentalStatus,RentalOption,PaymentId,PaymentType,PaymentMethod,TotalAmount,AmountPaid,AccountName,TransactionReference,PaymentDate,ReceiptImageFile,PaymentNotes")] RentalInputModel model)
		{
			if (id != model.RentalId)
			{
				return NotFound();
			}

			NormalizeVehicleIds(model);
			await ValidateRentalInputAsync(model, excludeRentalId: id);
			var existingReceipt = model.PaymentId > 0
				? await _context.Payments.AsNoTracking()
					.Where(p => p.PaymentId == model.PaymentId)
					.Select(p => p.ReceiptImagePath)
					.FirstOrDefaultAsync()
				: null;
			ValidatePaymentInput(model, await ResolveBookingTotalAsync(model), requireReceiptIfCashless: string.IsNullOrWhiteSpace(existingReceipt));

			if (ModelState.IsValid)
			{
				try
				{
					var rental = await _context.Rentals
						.Include(r => r.RentalVehicles)
						.FirstOrDefaultAsync(r => r.RentalId == id);
					if (rental == null)
					{
						return NotFound();
					}

					await SaveDiscountImageAsync(model);
					model.ApplyTo(rental);
					rental.CustomerId = await ResolveCustomerIdByContactAsync(model.ContactNumber);
					await SyncVehicleSelectionAsync(
						rental,
						model.GetNormalizedVehicleIds(),
						materializeLines: rental.RentalStatus == RentalStatus.Approved);

					await SavePaymentForRentalAsync(model, rental);
					_logs.Record(
						SystemLogAction.Updated,
						SystemLogCategory.Booking,
						$"Updated booking BK-{rental.RentalId:D5} for {rental.CustomerName} ({model.GetNormalizedVehicleIds().Count} vehicle(s)).",
						"Rental",
						rental.RentalId);
					await _context.SaveChangesAsync();

					var hasPayment = await _context.Payments.AnyAsync(p =>
						p.RentalId == rental.RentalId && p.AmountPaid > 0);
					if (rental.RentalStatus == RentalStatus.Pending && hasPayment)
					{
						await RentalConfirmation.ConfirmPaidBookingAsync(
							_context,
							_logs,
							_bookingEmailService,
							rental);
					}
					else if (rental.RentalStatus == RentalStatus.Approved)
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
				.Include(r => r.RentalVehicles)
					.ThenInclude(rv => rv.Vehicle)
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
				.Include(r => r.RentalVehicles)
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

		// GET: Rentals/Availability?vehicleId=1&vehicleIds=1&vehicleIds=2
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

		private Task<List<string>> GetUnavailableDatesAsync(int vehicleId) =>
			RentalVehicleWorkflow.GetUnavailableDatesAsync(_context, vehicleId);

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
			NormalizeVehicleIds(model);
			var today = DateOnly.FromDateTime(DateTime.Today);
			if (model.PickupDate < today)
			{
				ModelState.AddModelError(nameof(model.PickupDate), "Please select a pick-up date on the calendar.");
			}

			if (model.ReturnDate < model.PickupDate)
			{
				ModelState.AddModelError(nameof(model.ReturnDate), "Return date cannot be earlier than pick-up date.");
			}

			var vehicleIds = model.GetNormalizedVehicleIds();
			if (vehicleIds.Count == 0)
			{
				ModelState.AddModelError(nameof(model.VehicleId), "Please select at least one vehicle.");
			}

			if (vehicleIds.Count > 0
				&& model.PickupDate >= today
				&& model.ReturnDate >= model.PickupDate)
			{
				var hasConflict = await RentalVehicleWorkflow.HasScheduleConflictAsync(
					_context,
					vehicleIds,
					model.PickupDate,
					model.ReturnDate,
					excludeRentalId);

				if (hasConflict)
				{
					ModelState.AddModelError(nameof(model.PickupDate), "Selected dates overlap an existing rental for one of the selected vehicles.");
				}
			}

			if (model.Discount == Discount.Yes && model.DiscountImageFile == null && string.IsNullOrEmpty(model.DiscountImagePath))
			{
				ModelState.AddModelError(nameof(model.DiscountImageFile), "Please upload a Senior/PWD ID image.");
			}
		}

		private static void NormalizeVehicleIds(RentalInputModel model)
		{
			var ids = model.GetNormalizedVehicleIds().ToList();
			model.VehicleIds = ids;
			model.VehicleId = ids.FirstOrDefault();
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

		private async Task<decimal> ResolveBookingTotalAsync(RentalInputModel model)
		{
			var vehicleIds = model.GetNormalizedVehicleIds();
			if (vehicleIds.Count == 0)
			{
				return model.TotalAmount;
			}

			var vehicles = await _context.Vehicles
				.Where(v => vehicleIds.Contains(v.VehicleId))
				.ToListAsync();
			var orderedVehicles = vehicleIds
				.Select(id => vehicles.FirstOrDefault(v => v.VehicleId == id))
				.Where(v => v != null)
				.Cast<Vehicle>()
				.ToList();

			return orderedVehicles.Count == 0
				? model.TotalAmount
				: PaymentAmountRules.CalculateBookingTotal(orderedVehicles, model);
		}

		private void ValidatePaymentInput(RentalInputModel model, decimal bookingTotal, bool requireReceiptIfCashless = true)
		{
			if (string.IsNullOrWhiteSpace(model.PaymentType))
			{
				ModelState.AddModelError(nameof(model.PaymentType), "Please select a payment type.");
			}

			if (string.IsNullOrWhiteSpace(model.PaymentMethod))
			{
				ModelState.AddModelError(nameof(model.PaymentMethod), "Please select a payment method.");
			}

			var amountError = PaymentAmountRules.ValidateAmountPaid(model.AmountPaid, model.PaymentType, bookingTotal);
			if (amountError != null)
			{
				ModelState.AddModelError(nameof(model.AmountPaid), amountError);
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
			Rental rental)
		{
			if (rental.TotalAmount <= 0)
			{
				var vehicleIds = await _context.RentalVehicles.AsNoTracking()
					.Where(rv => rv.RentalId == rental.RentalId)
					.OrderBy(rv => rv.SortOrder)
					.Select(rv => rv.VehicleId)
					.ToListAsync();

				var vehicles = await _context.Vehicles.AsNoTracking()
					.Where(v => vehicleIds.Contains(v.VehicleId))
					.ToListAsync();
				var ordered = vehicleIds
					.Select(id => vehicles.FirstOrDefault(v => v.VehicleId == id))
					.Where(v => v != null)
					.Cast<Vehicle>()
					.ToList();
				if (ordered.Count > 0)
				{
					RentalFareCalculator.ApplyTo(rental, ordered);
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
				.Where(c => variants.Contains(c.User!.ContactNumber))
				.Select(c => new { c.CustomerId, c.User!.ContactNumber })
				.ToListAsync();

			if (candidates.Count == 0)
			{
				// Fallback: normalized compare when stored formats differ.
				var normalized = PhoneNumber.Normalize(contactNumber);
				candidates = await _context.CustomerProfiles
					.AsNoTracking()
					.Select(c => new { c.CustomerId, c.User!.ContactNumber })
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
				.Where(v => v.IsActive || (selectedVehicleId.HasValue && v.VehicleId == selectedVehicleId.Value))
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
