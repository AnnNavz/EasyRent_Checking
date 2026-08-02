using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyRent_Checking.Models
{
	public enum ValidIDtype
	{
		NationalID,
		Passport,
		UMID,
		DriverLicense,
		VotersID
	}
	public class Customer
	{

		[Key]
		public int CustomerId { get; set; }

		[Required(ErrorMessage = "Full name is required.")]
		[StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters.")]
		[Display(Name = "Full Name")]
		public string FullName { get; set; }

		[Required(ErrorMessage = "Contact number is required.")]
		[DataType(DataType.PhoneNumber)]
		[RegularExpression(@"^(09|\+639)\d{9}$", ErrorMessage = "Please enter a valid Philippine mobile number (e.g., 09123456789 or +639123456789).")]
		[Display(Name = "Contact Number")]
		public string ContactNumber { get; set; }

		[Required(ErrorMessage = "Email address is required.")]
		[EmailAddress(ErrorMessage = "Please enter a valid email address.")]
		[StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
		[Display(Name = "Email Address")]
		public string Email { get; set; }

		[Required(ErrorMessage = "Password is required.")]
		[StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 100 characters long.")]
		[DataType(DataType.Password)]
		public string Password { get; set; }

		[NotMapped]
		[Required(ErrorMessage = "Please confirm your password.")]
		[DataType(DataType.Password)]
		[Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
		[Display(Name = "Confirm Password")]
		public string ConfirmPassword { get; set; }

		[Required(ErrorMessage = "Valid ID is required.")]
		[Display(Name = "Choose what type of valid IDs")]
		public string ValidIDtype { get; set; }

		[StringLength(255)]
		[Display(Name = "Valid ID Picture")]
		public string? ValidIDImagePath { get; set; }

		[NotMapped]
		[Display(Name = "Upload Valid ID Picture")]
		public IFormFile? ValidIDImageFile { get; set; } 
	}
}
