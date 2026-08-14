using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyRent_Checking.ViewModels
{
	public class AdminAccountInputModel
	{
		public int AdminId { get; set; }

		[Required(ErrorMessage = "Full name is required.")]
		[StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters.")]
		[Display(Name = "Full Name")]
		public string FullName { get; set; } = string.Empty;

		[Required(ErrorMessage = "Email address is required.")]
		[EmailAddress(ErrorMessage = "Please enter a valid email address.")]
		[StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
		[Display(Name = "Email Address")]
		public string Email { get; set; } = string.Empty;

		[Required(ErrorMessage = "Password is required.")]
		[StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 100 characters long.")]
		[DataType(DataType.Password)]
		[Display(Name = "Password")]
		public string Password { get; set; } = string.Empty;

		[NotMapped]
		[Required(ErrorMessage = "Please confirm your password.")]
		[DataType(DataType.Password)]
		[Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
		[Display(Name = "Confirm Password")]
		public string ConfirmPassword { get; set; } = string.Empty;
	}
}
