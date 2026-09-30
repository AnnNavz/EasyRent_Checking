using EasyRent_Checking.Data;
using EasyRent_Checking.Models;
using Microsoft.EntityFrameworkCore;

namespace EasyRent_Checking.Services
{
	public class BookingEmailService
	{
		private readonly EasyRent_CheckingContext _context;
		private readonly IEmailSender _emailSender;
		private readonly ReceiptPdfService _receiptPdfService;
		private readonly ILogger<BookingEmailService> _logger;

		public BookingEmailService(
			EasyRent_CheckingContext context,
			IEmailSender emailSender,
			ReceiptPdfService receiptPdfService,
			ILogger<BookingEmailService> logger)
		{
			_context = context;
			_emailSender = emailSender;
			_receiptPdfService = receiptPdfService;
			_logger = logger;
		}

		public async Task SendRentalConfirmedAsync(Rental rental)
		{
			var email = await TryGetCustomerEmailAsync(rental.CustomerId);
			if (email == null)
			{
				_logger.LogInformation(
					"Skipping rental confirmation email for rental {RentalId}: no linked customer email.",
					rental.RentalId);
				return;
			}

			var rentalVehicles = await _context.RentalVehicles
				.AsNoTracking()
				.Include(rv => rv.Vehicle)
				.Where(rv => rv.RentalId == rental.RentalId)
				.OrderBy(rv => rv.SortOrder)
				.ThenBy(rv => rv.RentalVehicleId)
				.ToListAsync();

			var payment = await _context.Payments
				.AsNoTracking()
				.Where(p => p.RentalId == rental.RentalId)
				.OrderByDescending(p => p.PaymentId)
				.FirstOrDefaultAsync();

			var bookingLabel = $"BK-{rental.RentalId:D5}";
			var tripLines = FormatTripLines(rental, rentalVehicles);
			var paymentLines = FormatPaymentLines(payment);
			var body = $"""
				<p>Hi {rental.CustomerName},</p>
				<p>Your EasyRent booking <strong>{bookingLabel}</strong> has been confirmed.</p>
				{tripLines}
				{paymentLines}
				<p>Your official payment receipt is attached as a PDF.</p>
				<p>We look forward to serving you.</p>
				""";

			EmailAttachment[]? attachments = null;
			try
			{
				var pdfBytes = _receiptPdfService.Generate(rental, payment, rentalVehicles);
				attachments =
				[
					new EmailAttachment
					{
						FileName = $"EasyRent-Receipt-{bookingLabel}.pdf",
						Content = pdfBytes,
						ContentType = "application/pdf"
					}
				];
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to generate receipt PDF for rental {RentalId}", rental.RentalId);
			}

			await TrySendAsync(email, $"Booking {bookingLabel} confirmed — receipt attached", body, rental.RentalId, attachments);
		}

		public async Task SendTripStartedAsync(Rental rental)
		{
			var email = await TryGetCustomerEmailAsync(rental.CustomerId);
			if (email == null)
			{
				_logger.LogInformation(
					"Skipping trip-started email for rental {RentalId}: no linked customer email.",
					rental.RentalId);
				return;
			}

			var rentalVehicles = await _context.RentalVehicles
				.AsNoTracking()
				.Include(rv => rv.Vehicle)
				.Where(rv => rv.RentalId == rental.RentalId)
				.OrderBy(rv => rv.SortOrder)
				.ThenBy(rv => rv.RentalVehicleId)
				.ToListAsync();

			var bookingLabel = $"BK-{rental.RentalId:D5}";
			var tripLines = FormatTripLines(rental, rentalVehicles);
			var body = $"""
				<p>Hi {rental.CustomerName},</p>
				<p>Your EasyRent trip for booking <strong>{bookingLabel}</strong> has started.</p>
				{tripLines}
				<p>Safe travels!</p>
				""";

			await TrySendAsync(email, $"Your trip for {bookingLabel} has started", body, rental.RentalId);
		}

		public async Task SendVehicleReplacedAsync(
			Rental rental,
			Vehicle previousVehicle,
			Vehicle newVehicle,
			string reason)
		{
			var email = await TryGetCustomerEmailAsync(rental.CustomerId);
			if (email == null)
			{
				_logger.LogInformation(
					"Skipping vehicle-replaced email for rental {RentalId}: no linked customer email.",
					rental.RentalId);
				return;
			}

			var bookingLabel = $"BK-{rental.RentalId:D5}";
			var previousLabel = FormatVehicleLabel(previousVehicle);
			var newLabel = FormatVehicleLabel(newVehicle);
			var tripLines = FormatTripLines(rental, []);
			var body = $"""
				<p>Hi {rental.CustomerName},</p>
				<p>We need to update your EasyRent booking <strong>{bookingLabel}</strong>.</p>
				<p>The originally assigned vehicle (<strong>{previousLabel}</strong>) is unavailable due to {reason}.</p>
				<p>Your booking has been moved to <strong>{newLabel}</strong> at the same rate and schedule.</p>
				{tripLines}
				<p>We apologize for the inconvenience and look forward to serving you.</p>
				""";

			await TrySendAsync(email, $"Vehicle change for booking {bookingLabel}", body, rental.RentalId);
		}

		public async Task SendRefundIssuedAsync(
			Rental rental,
			decimal refundAmount,
			string reason,
			string? receiptImagePath,
			IWebHostEnvironment webHostEnvironment)
		{
			var email = await TryGetCustomerEmailAsync(rental.CustomerId);
			if (email == null)
			{
				_logger.LogInformation(
					"Skipping refund email for rental {RentalId}: no linked customer email.",
					rental.RentalId);
				return;
			}

			var bookingLabel = $"BK-{rental.RentalId:D5}";
			var tripLines = FormatTripLines(rental, []);
			var refundLine = refundAmount > 0
				? $"<p>A refund of <strong>₱{refundAmount:N2}</strong> has been issued because {reason}.</p>"
				: "<p>Your booking was cancelled at no charge.</p>";
			var body = $"""
				<p>Hi {rental.CustomerName},</p>
				<p>Your EasyRent booking <strong>{bookingLabel}</strong> has been cancelled by our team.</p>
				{refundLine}
				{tripLines}
				<p>Your refund receipt is attached when applicable.</p>
				<p>We apologize for the inconvenience.</p>
				""";

			EmailAttachment[]? attachments = null;
			if (!string.IsNullOrWhiteSpace(receiptImagePath))
			{
				try
				{
					var physicalPath = Path.Combine(webHostEnvironment.WebRootPath, "images", receiptImagePath);
					if (System.IO.File.Exists(physicalPath))
					{
						var bytes = await System.IO.File.ReadAllBytesAsync(physicalPath);
						var extension = Path.GetExtension(physicalPath).ToLowerInvariant();
						var contentType = extension switch
						{
							".png" => "image/png",
							".webp" => "image/webp",
							".gif" => "image/gif",
							_ => "image/jpeg"
						};
						attachments =
						[
							new EmailAttachment
							{
								FileName = $"EasyRent-Refund-{bookingLabel}{extension}",
								Content = bytes,
								ContentType = contentType
							}
						];
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Failed to attach refund receipt for rental {RentalId}", rental.RentalId);
				}
			}

			await TrySendAsync(
				email,
				$"Booking {bookingLabel} cancelled — refund issued",
				body,
				rental.RentalId,
				attachments);
		}

		public async Task SendRefundRejectedAsync(
			Rental rental,
			string rejectionReason)
		{
			var email = await TryGetCustomerEmailAsync(rental.CustomerId);
			if (email == null)
			{
				_logger.LogInformation(
					"Skipping refund rejection email for rental {RentalId}: no linked customer email.",
					rental.RentalId);
				return;
			}

			var bookingLabel = $"BK-{rental.RentalId:D5}";
			var tripLines = FormatTripLines(rental, []);
			var body = $"""
				<p>Hi {rental.CustomerName},</p>
				<p>We reviewed your refund request for EasyRent booking <strong>{bookingLabel}</strong>.</p>
				<p>Unfortunately, your refund request could not be approved.</p>
				<p><strong>Reason:</strong> {rejectionReason}</p>
				{tripLines}
				<p>If you have questions, please contact EasyRent support.</p>
				""";

			await TrySendAsync(
				email,
				$"Refund request declined for booking {bookingLabel}",
				body,
				rental.RentalId);
		}

		private async Task<string?> TryGetCustomerEmailAsync(int? customerId)
		{
			if (customerId == null)
			{
				return null;
			}

			return await _context.Users
				.AsNoTracking()
				.Where(u => u.UserId == customerId.Value)
				.Select(u => u.Email)
				.FirstOrDefaultAsync();
		}

		public async Task SendAccountApprovedAsync(CustomerProfile customer, string? loginUrl = null)
		{
			var email = customer.User?.Email
				?? await TryGetCustomerEmailAsync(customer.CustomerId);

			if (string.IsNullOrWhiteSpace(email))
			{
				_logger.LogInformation(
					"Skipping account approval email for customer {CustomerId}: no email on file.",
					customer.CustomerId);
				return;
			}

			var loginLine = string.IsNullOrWhiteSpace(loginUrl)
				? "<p>You can now sign in to EasyRent and start booking.</p>"
				: $"""<p><a href="{loginUrl}">Sign in to EasyRent</a> and start booking.</p>""";

			var body = $"""
				<p>Hi {customer.User?.FullName ?? "there"},</p>
				<p>Good news — your EasyRent customer account has been approved.</p>
				{loginLine}
				<p>Welcome aboard!</p>
				""";

			await TrySendAsync(email, "Your EasyRent account has been approved", body, customer.CustomerId);
		}

		public async Task SendAccountDeactivatedAsync(CustomerProfile customer)
		{
			var email = customer.User?.Email
				?? await TryGetCustomerEmailAsync(customer.CustomerId);

			if (string.IsNullOrWhiteSpace(email))
			{
				_logger.LogInformation(
					"Skipping account deactivation email for customer {CustomerId}: no email on file.",
					customer.CustomerId);
				return;
			}

			var body = $"""
				<p>Hi {customer.User?.FullName ?? "there"},</p>
				<p>Your EasyRent customer account has been deactivated by our staff. You will not be able to sign in until the account is reactivated.</p>
				<p>If you believe this was a mistake, please contact EasyRent so we can review your account.</p>
				""";

			await TrySendAsync(email, "Your EasyRent account has been deactivated", body, customer.CustomerId);
		}

		private async Task TrySendAsync(
			string toEmail,
			string subject,
			string htmlBody,
			int relatedId,
			IEnumerable<EmailAttachment>? attachments = null)
		{
			try
			{
				await _emailSender.SendAsync(toEmail, subject, htmlBody, attachments);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to send notification email (id {RelatedId}) to {Email}", relatedId, toEmail);
			}
		}

		private static string FormatTripLines(Rental rental, IReadOnlyList<RentalVehicle> rentalVehicles)
		{
			var vehicleLines = rentalVehicles.Count > 0
				? string.Join("\n", rentalVehicles.Select((rv, index) =>
				{
					var vehicle = rv.Vehicle;
					var title = vehicle == null
						? $"Vehicle #{rv.VehicleId}"
						: $"{vehicle.Brand} {vehicle.Model} ({vehicle.PlateNumber})".Trim();
					return $"\t<li><strong>Vehicle {index + 1}:</strong> {title}</li>";
				}))
				: string.Empty;

			var vehiclesBlock = string.IsNullOrEmpty(vehicleLines)
				? string.Empty
				: $"""
					<ul>
					{vehicleLines}
					</ul>
					""";

			return $"""
				{vehiclesBlock}
				<ul>
					<li><strong>Pickup:</strong> {rental.PickupLocation} — {rental.PickupDate:MMM d, yyyy} {rental.PickupTime:h:mm tt}</li>
					<li><strong>Drop-off:</strong> {rental.DropoffLocation} — {rental.ReturnDate:MMM d, yyyy} {rental.ReturnTime:h:mm tt}</li>
				</ul>
				""";
		}

		private static string FormatPaymentLines(Payment? payment)
		{
			if (payment == null)
			{
				return string.Empty;
			}

			return $"""
				<ul>
					<li><strong>Payment type:</strong> {payment.PaymentType}</li>
					<li><strong>Method:</strong> {payment.PaymentMethod}</li>
					<li><strong>Amount paid:</strong> ₱{payment.AmountPaid:N2}</li>
				</ul>
				""";
		}

		private static string FormatVehicleLabel(Vehicle vehicle)
		{
			var name = $"{vehicle.Brand} {vehicle.Model}".Trim();
			return string.IsNullOrEmpty(name)
				? vehicle.PlateNumber
				: $"{name} ({vehicle.PlateNumber})";
		}
	}
}
