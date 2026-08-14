using EasyRent_Checking.Data;
using EasyRent_Checking.Models;
using Microsoft.EntityFrameworkCore;

namespace EasyRent_Checking.Services
{
	public class BookingEmailService
	{
		private readonly EasyRent_CheckingContext _context;
		private readonly IEmailSender _emailSender;
		private readonly ILogger<BookingEmailService> _logger;

		public BookingEmailService(
			EasyRent_CheckingContext context,
			IEmailSender emailSender,
			ILogger<BookingEmailService> logger)
		{
			_context = context;
			_emailSender = emailSender;
			_logger = logger;
		}

		public async Task SendRentalConfirmedAsync(Rental rental, RentalDetails? details)
		{
			var email = await TryGetCustomerEmailAsync(rental.CustomerId);
			if (email == null)
			{
				_logger.LogInformation(
					"Skipping rental confirmation email for rental {RentalId}: no linked customer email.",
					rental.RentalId);
				return;
			}

			var bookingLabel = $"BK-{rental.RentalId:D6}";
			var tripLines = FormatTripLines(details);
			var body = $"""
				<p>Hi {rental.CustomerName},</p>
				<p>Your EasyRent booking <strong>{bookingLabel}</strong> has been confirmed.</p>
				{tripLines}
				<p>We look forward to serving you.</p>
				""";

			await TrySendAsync(email, $"Booking {bookingLabel} confirmed", body, rental.RentalId);
		}

		public async Task SendTripStartedAsync(Rental rental, RentalDetails? details)
		{
			var email = await TryGetCustomerEmailAsync(rental.CustomerId);
			if (email == null)
			{
				_logger.LogInformation(
					"Skipping trip-started email for rental {RentalId}: no linked customer email.",
					rental.RentalId);
				return;
			}

			var bookingLabel = $"BK-{rental.RentalId:D6}";
			var tripLines = FormatTripLines(details);
			var body = $"""
				<p>Hi {rental.CustomerName},</p>
				<p>Your EasyRent trip for booking <strong>{bookingLabel}</strong> has started.</p>
				{tripLines}
				<p>Safe travels!</p>
				""";

			await TrySendAsync(email, $"Your trip for {bookingLabel} has started", body, rental.RentalId);
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
				<p>Hi {customer.FullName},</p>
				<p>Good news — your EasyRent customer account has been approved.</p>
				{loginLine}
				<p>Welcome aboard!</p>
				""";

			await TrySendAsync(email, "Your EasyRent account has been approved", body, customer.CustomerId);
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

		private async Task TrySendAsync(string toEmail, string subject, string htmlBody, int relatedId)
		{
			try
			{
				await _emailSender.SendAsync(toEmail, subject, htmlBody);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to send notification email (id {RelatedId}) to {Email}", relatedId, toEmail);
			}
		}

		private static string FormatTripLines(RentalDetails? details)
		{
			if (details == null)
			{
				return string.Empty;
			}

			return $"""
				<ul>
					<li><strong>Pickup:</strong> {details.PickupLocation} — {details.PickupDate:MMM d, yyyy} {details.PickupTime:HH:mm}</li>
					<li><strong>Drop-off:</strong> {details.DropoffLocation} — {details.ReturnDate:MMM d, yyyy} {details.ReturnTime:HH:mm}</li>
				</ul>
				""";
		}
	}
}
