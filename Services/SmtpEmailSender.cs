using System.Net;
using System.Net.Mail;

namespace EasyRent_Checking.Services
{
	public class SmtpEmailSender : IEmailSender
	{
		private readonly IConfiguration _config;
		private readonly ILogger<SmtpEmailSender> _logger;

		public SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger)
		{
			_config = config;
			_logger = logger;
		}

		public async Task SendAsync(string toEmail, string subject, string htmlBody)
		{
			var host = _config["Smtp:Host"]
				?? throw new InvalidOperationException("Smtp:Host is not configured.");
			var port = int.Parse(_config["Smtp:Port"] ?? "587");
			var enableSsl = bool.Parse(_config["Smtp:EnableSsl"] ?? "true");
			var username = _config["Smtp:Username"]
				?? throw new InvalidOperationException("Smtp:Username is not configured.");
			var password = _config["Smtp:Password"]
				?? throw new InvalidOperationException("Smtp:Password is not configured.");
			var fromEmail = _config["Smtp:FromEmail"] ?? username;
			var fromName = _config["Smtp:FromName"] ?? "EasyRent";

			using var client = new SmtpClient(host, port)
			{
				EnableSsl = enableSsl,
				Credentials = new NetworkCredential(username, password)
			};

			using var message = new MailMessage
			{
				From = new MailAddress(fromEmail, fromName),
				Subject = subject,
				Body = htmlBody,
				IsBodyHtml = true
			};
			message.To.Add(toEmail);

			try
			{
				await client.SendMailAsync(message);
				_logger.LogInformation("SMTP accepted email to {ToEmail} from {FromEmail}", toEmail, fromEmail);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to send email to {Email}", toEmail);
				throw;
			}
		}
	}
}
