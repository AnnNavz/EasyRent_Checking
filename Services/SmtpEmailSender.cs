using System.Net;
using System.Net.Mail;
using System.Net.Mime;

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

		public async Task SendAsync(
			string toEmail,
			string subject,
			string htmlBody,
			IEnumerable<EmailAttachment>? attachments = null)
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

			var streams = new List<MemoryStream>();
			try
			{
				if (attachments != null)
				{
					foreach (var attachment in attachments)
					{
						if (attachment.Content.Length == 0)
						{
							continue;
						}

						var stream = new MemoryStream(attachment.Content);
						streams.Add(stream);
						message.Attachments.Add(new Attachment(
							stream,
							attachment.FileName,
							attachment.ContentType ?? MediaTypeNames.Application.Pdf));
					}
				}

				await client.SendMailAsync(message);
				_logger.LogInformation("SMTP accepted email to {ToEmail} from {FromEmail}", toEmail, fromEmail);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to send email to {Email}", toEmail);
				throw;
			}
			finally
			{
				foreach (var stream in streams)
				{
					await stream.DisposeAsync();
				}
			}
		}
	}
}
