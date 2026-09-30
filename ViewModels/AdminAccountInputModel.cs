using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EasyRent_Checking.Models;

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

		[Required(ErrorMessage = "Contact number is required.")]
		[DataType(DataType.PhoneNumber)]
		[StringLength(11, MinimumLength = 11, ErrorMessage = "Contact number must be exactly 11 digits.")]
		[RegularExpression(FieldRules.PhMobile, ErrorMessage = FieldRules.PhMobileMessage)]
		[Display(Name = "Contact Number")]
		public string ContactNumber { get; set; } = string.Empty;

		[Required(ErrorMessage = "Address is required.")]
		[StringLength(255, ErrorMessage = "Address cannot exceed 255 characters.")]
		[Display(Name = "Address")]
		public string Address { get; set; } = string.Empty;

		[StringLength(100, MinimumLength = 8, ErrorMessage = FieldRules.PasswordLengthMessage)]
		[RegularExpression(FieldRules.Password, ErrorMessage = FieldRules.PasswordMessage)]
		[DataType(DataType.Password)]
		[Display(Name = "Password")]
		public string? Password { get; set; }

		[NotMapped]
		[DataType(DataType.Password)]
		[Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
		[Display(Name = "Confirm Password")]
		public string? ConfirmPassword { get; set; }

		[Display(Name = "Role")]
		public UserRole Role { get; set; } = UserRole.Admin;

		public static AdminAccountInputModel FromUser(User user)
		{
			return new AdminAccountInputModel
			{
				AdminId = user.UserId,
				FullName = user.FullName,
				Email = user.Email,
				ContactNumber = user.ContactNumber,
				Address = user.Address ?? string.Empty,
				Role = user.Role == UserRole.Staff ? UserRole.Staff : UserRole.Admin
			};
		}
	}
}
