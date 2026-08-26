using System.ComponentModel.DataAnnotations;

namespace EasyRent_Checking.ViewModels
{
	public class ForgotPasswordInputModel
	{
		[Required(ErrorMessage = "Email address is required.")]
		[EmailAddress(ErrorMessage = "Please enter a valid email address.")]
		[StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
		[Display(Name = "Email Address")]
		public string Email { get; set; } = string.Empty;
	}
}
