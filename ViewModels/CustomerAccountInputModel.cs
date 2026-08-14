using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EasyRent_Checking.Models;

namespace EasyRent_Checking.ViewModels
{
	public class CustomerAccountInputModel
	{
		public int CustomerId { get; set; }

		[Required(ErrorMessage = "Full name is required.")]
		[StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters.")]
		[Display(Name = "Full Name")]
		public string FullName { get; set; } = string.Empty;

		[Required(ErrorMessage = "Contact number is required.")]
		[DataType(DataType.PhoneNumber)]
		[RegularExpression(@"^(09|\+639)\d{9}$", ErrorMessage = "Please enter a valid Philippine mobile number (e.g., 09123456789 or +639123456789).")]
		[Display(Name = "Contact Number")]
		public string ContactNumber { get; set; } = string.Empty;

		[Required(ErrorMessage = "Email address is required.")]
		[EmailAddress(ErrorMessage = "Please enter a valid email address.")]
		[StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
		[Display(Name = "Email Address")]
		public string Email { get; set; } = string.Empty;

		[StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 100 characters long.")]
		[DataType(DataType.Password)]
		[Display(Name = "Password")]
		public string? Password { get; set; }

		[NotMapped]
		[DataType(DataType.Password)]
		[Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
		[Display(Name = "Confirm Password")]
		public string? ConfirmPassword { get; set; }

		[Required(ErrorMessage = "Valid ID is required.")]
		[Display(Name = "Choose what type of valid IDs")]
		public string ValidIDtype { get; set; } = string.Empty;

		[StringLength(255)]
		[Display(Name = "Valid ID Picture")]
		public string? ValidIDImagePath { get; set; }

		[NotMapped]
		[Display(Name = "Upload Valid ID Picture")]
		public IFormFile? ValidIDImageFile { get; set; }

		[Required(ErrorMessage = "Status is required.")]
		[Display(Name = "Status")]
		public Status Status { get; set; } = Status.Pending;

		public static CustomerAccountInputModel FromEntities(User user, CustomerProfile profile)
		{
			return new CustomerAccountInputModel
			{
				CustomerId = profile.CustomerId,
				FullName = profile.FullName,
				ContactNumber = profile.ContactNumber,
				Email = user.Email,
				ValidIDtype = profile.ValidIDtype,
				ValidIDImagePath = profile.ValidIDImagePath,
				Status = profile.Status
			};
		}
	}
}
