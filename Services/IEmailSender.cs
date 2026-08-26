namespace EasyRent_Checking.Services
{
	public sealed class EmailAttachment
	{
		public required string FileName { get; init; }
		public required byte[] Content { get; init; }
		public string ContentType { get; init; } = "application/pdf";
	}

	public interface IEmailSender
	{
		Task SendAsync(string toEmail, string subject, string htmlBody, IEnumerable<EmailAttachment>? attachments = null);
	}
}
