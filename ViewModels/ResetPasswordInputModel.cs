using System.ComponentModel.DataAnnotations;
using EasyRent_Checking.Models;

namespace EasyRent_Checking.ViewModels
{
	public class ResetPasswordInputModel
	{
		public int UserId { get; set; }

		[Required]
		public string Token { get; set; } = string.Empty;

		[Required(ErrorMessage = "Password is required.")]
		[StringLength(100, MinimumLength = 8, ErrorMessage = FieldRules.PasswordLengthMessage)]
		[RegularExpression(FieldRules.Password, ErrorMessage = FieldRules.PasswordMessage)]
		[DataType(DataType.Password)]
		[Display(Name = "New Password")]
		public string Password { get; set; } = string.Empty;

		[Required(ErrorMessage = "Please confirm your password.")]
		[DataType(DataType.Password)]
		[Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
		[Display(Name = "Confirm Password")]
		public string ConfirmPassword { get; set; } = string.Empty;
	}
}
