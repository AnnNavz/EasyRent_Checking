namespace EasyRent_Checking.Services
{
	public interface IEmailSender
	{
		Task SendAsync(string toEmail, string subject, string htmlBody);
	}
}
