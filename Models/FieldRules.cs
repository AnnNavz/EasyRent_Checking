using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace EasyRent_Checking.Models
{
	public static class FieldRules
	{
		public const string PhMobile = @"^(09|\+639)\d{9}$";
		public const string PhMobileMessage = "Please enter a valid Philippine mobile number (e.g., 09123456789 or +639123456789).";

		public const string Plate = @"^[A-Za-z0-9][A-Za-z0-9 \-]{1,19}$";
		public const string PlateMessage = "Enter a valid plate number using letters, numbers, spaces, or hyphens (e.g., GAP 4821).";

		public const string License = @"^[A-Za-z0-9\-]{5,30}$";
		public const string LicenseMessage = "License number must be 5–30 letters or numbers.";

		public const string Password = @"^(?=.*[0-9]).{8,100}$";
		public const string PasswordMessage = "Password must be 8–100 characters and include at least one number.";
		public const string PasswordLengthMessage = "Password must be between 8 and 100 characters long.";

		public static bool IsPhMobile(string? value)
			=> !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value, PhMobile);

		public static IEnumerable<ValidationResult> ValidateReturnAfterPickup(
			DateOnly pickupDate,
			TimeOnly pickupTime,
			DateOnly returnDate,
			TimeOnly returnTime,
			string returnDateField,
			string returnTimeField)
		{
			if (pickupDate == default || returnDate == default)
			{
				yield break;
			}

			var pickup = pickupDate.ToDateTime(pickupTime);
			var returned = returnDate.ToDateTime(returnTime);
			if (returned <= pickup)
			{
				yield return new ValidationResult(
					"Return date and time must be after pickup.",
					new[] { returnDateField, returnTimeField });
			}
		}
	}
}
