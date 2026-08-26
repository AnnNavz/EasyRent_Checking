using System.ComponentModel.DataAnnotations;

namespace EasyRent_Checking.ViewModels
{
	public class ContactUsInputModel
	{
		[Required(ErrorMessage = "Full name is required.")]
		[StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters.")]
		[Display(Name = "Full Name")]
		public string FullName { get; set; } = string.Empty;

		[Required(ErrorMessage = "Email address is required.")]
		[EmailAddress(ErrorMessage = "Please enter a valid email address.")]
		[StringLength(150)]
		[Display(Name = "Email Address")]
		public string Email { get; set; } = string.Empty;

		[Required(ErrorMessage = "Message is required.")]
		[StringLength(2000, ErrorMessage = "Message cannot exceed 2000 characters.")]
		[Display(Name = "Message")]
		public string Message { get; set; } = string.Empty;
	}
}
