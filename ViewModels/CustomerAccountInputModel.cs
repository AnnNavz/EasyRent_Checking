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
		[StringLength(20, ErrorMessage = "Contact number cannot exceed 20 characters.")]
		[RegularExpression(FieldRules.PhMobile, ErrorMessage = FieldRules.PhMobileMessage)]
		[Display(Name = "Contact Number")]
		public string ContactNumber { get; set; } = string.Empty;

		[Required(ErrorMessage = "Email address is required.")]
		[EmailAddress(ErrorMessage = "Please enter a valid email address.")]
		[StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
		[Display(Name = "Email Address")]
		public string Email { get; set; } = string.Empty;

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

		[Required(ErrorMessage = "Valid ID type is required.")]
		[StringLength(30, ErrorMessage = "ID type cannot exceed 30 characters.")]
		[EnumDataType(typeof(ValidIDtype), ErrorMessage = "Please select a valid ID type.")]
		[Display(Name = "Choose what type of valid IDs")]
		public string ValidIDtype { get; set; } = string.Empty;

		[StringLength(255)]
		[Display(Name = "Front of Valid ID")]
		public string? FrontValidIDImagePath { get; set; }

		[StringLength(255)]
		[Display(Name = "Back of Valid ID")]
		public string? BackValidIDImagePath { get; set; }

		[NotMapped]
		[Display(Name = "Upload Front of Valid ID")]
		public IFormFile? FrontValidIDImageFile { get; set; }

		[NotMapped]
		[Display(Name = "Upload Back of Valid ID")]
		public IFormFile? BackValidIDImageFile { get; set; }

		[Required(ErrorMessage = "Status is required.")]
		[Display(Name = "Status")]
		public Status Status { get; set; } = Status.Pending;

		[Display(Name = "Email sign-in code")]
		public bool LoginMfaEnabled { get; set; }

		public static CustomerAccountInputModel FromEntities(User user, CustomerProfile profile)
		{
			return new CustomerAccountInputModel
			{
				CustomerId = profile.CustomerId,
				FullName = profile.FullName,
				ContactNumber = profile.ContactNumber,
				Email = user.Email,
				ValidIDtype = profile.ValidIDtype,
				FrontValidIDImagePath = profile.FrontValidIDImagePath,
				BackValidIDImagePath = profile.BackValidIDImagePath,
				Status = profile.Status,
				LoginMfaEnabled = user.LoginMfaEnabled
			};
		}
	}
}
