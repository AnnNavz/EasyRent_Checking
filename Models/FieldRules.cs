using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace EasyRent_Checking.Models
{
	public static class FieldRules
	{
		public const string NamePlaceholder = "ex. Juan Dela Cruz";

		public const string PhMobile = @"^09\d{9}$";
		public const string PhMobileMessage = "Phone number must be exactly 11 digits (e.g., 09123456789).";

		public const string Plate = @"^[A-Za-z0-9][A-Za-z0-9 \-]{1,19}$";
		public const string PlateMessage = "Enter a valid plate number using letters, numbers, spaces, or hyphens (e.g., GAP 4821).";

		public const string License = @"^[A-Za-z0-9\-]{5,30}$";
		public const string LicenseMessage = "License number must be 5–30 letters or numbers.";

		public const string Password = @"^(?=.*[0-9]).{8,100}$";
		public const string PasswordMessage = "Password must be 8–100 characters and include at least one number.";
		public const string PasswordLengthMessage = "Password must be between 8 and 100 characters long.";

		public static string NormalizePhMobile(string? value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return string.Empty;
			}

			var digits = new string(value.Where(char.IsDigit).ToArray());
			if (digits.StartsWith("63") && digits.Length == 12)
			{
				digits = "0" + digits[2..];
			}

			return digits;
		}

		public static bool IsPhMobile(string? value)
			=> Regex.IsMatch(NormalizePhMobile(value), PhMobile);

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
