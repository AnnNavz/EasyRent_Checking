using System.ComponentModel.DataAnnotations;

namespace EasyRent_Checking.ViewModels
{
	public class VerifyLoginCodeInputModel
	{
		[Required(ErrorMessage = "Enter the 6-digit code from your email.")]
		[RegularExpression(@"^\d{6}$", ErrorMessage = "Enter the 6-digit code from your email.")]
		[Display(Name = "Verification code")]
		public string Code { get; set; } = string.Empty;

		public string MaskedEmail { get; set; } = string.Empty;

		public int ResendWaitSeconds { get; set; }
	}
}
