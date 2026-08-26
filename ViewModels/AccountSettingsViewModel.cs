using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EasyRent_Checking.Models;

namespace EasyRent_Checking.ViewModels
{
	public class AccountSettingsViewModel
	{
		public string Tab { get; set; } = "profile";
		public string? EditField { get; set; }

		public string FullName { get; set; } = string.Empty;
		public string Email { get; set; } = string.Empty;
		public string ContactNumber { get; set; } = string.Empty;
		public string ValidIDtype { get; set; } = string.Empty;
		public string ValidIdTypeLabel { get; set; } = string.Empty;
		public bool IsIdentityVerified { get; set; }
		public bool LoginMfaEnabled { get; set; }
		public string AvatarInitials { get; set; } = "U";
		public DateTime MemberSince { get; set; }
		public int TotalRentals { get; set; }
		public int CompletedTrips { get; set; }
		public decimal OutstandingBalance { get; set; }
		public string? FrontValidIDImagePath { get; set; }
		public string? BackValidIDImagePath { get; set; }

		public ChangePasswordInputModel Password { get; set; } = new();
	}

	public class ChangePasswordInputModel
	{
		[Required(ErrorMessage = "Enter your current password.")]
		[DataType(DataType.Password)]
		[Display(Name = "Current Password")]
		public string CurrentPassword { get; set; } = string.Empty;

		[Required(ErrorMessage = "New password is required.")]
		[StringLength(100, MinimumLength = 8, ErrorMessage = FieldRules.PasswordLengthMessage)]
		[RegularExpression(FieldRules.Password, ErrorMessage = FieldRules.PasswordMessage)]
		[DataType(DataType.Password)]
		[Display(Name = "New Password")]
		public string NewPassword { get; set; } = string.Empty;

		[NotMapped]
		[Required(ErrorMessage = "Please confirm your new password.")]
		[DataType(DataType.Password)]
		[Compare(nameof(NewPassword), ErrorMessage = "The password and confirmation password do not match.")]
		[Display(Name = "Confirm New Password")]
		public string ConfirmPassword { get; set; } = string.Empty;
	}
}
