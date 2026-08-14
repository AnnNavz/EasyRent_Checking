using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyRent_Checking.Models
{
	public enum UserRole
	{
		Admin,
		Customer,
		Staff
	}

	public class User
	{
		[Key]
		public int UserId { get; set; }

		[Required(ErrorMessage = "Email address is required.")]
		[EmailAddress(ErrorMessage = "Please enter a valid email address.")]
		[StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
		[Display(Name = "Email Address")]
		public string Email { get; set; } = string.Empty;

		[Required]
		[StringLength(100)]
		[Display(Name = "Password Hash")]
		public string PasswordHash { get; set; } = string.Empty;

		[NotMapped]
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

		[Required(ErrorMessage = "Role is required.")]
		[Display(Name = "Role")]
		public UserRole Role { get; set; } = UserRole.Customer;

		[Required]
		[DataType(DataType.DateTime)]
		[Display(Name = "Created At")]
		public DateTime CreatedAt { get; set; } = DateTime.Now;

		[Display(Name = "Email Confirmed")]
		public bool EmailConfirmed { get; set; }

		[StringLength(128)]
		public string? EmailVerificationToken { get; set; }

		public DateTime? EmailVerificationTokenExpires { get; set; }

		[Display(Name = "Failed Login Attempts")]
		public int AccessFailedCount { get; set; }

		[Display(Name = "Lockout End")]
		public DateTime? LockoutEndUtc { get; set; }

		public CustomerProfile? CustomerProfile { get; set; }

		public AdminProfile? AdminProfile { get; set; }

		/// <summary>
		/// Hashes the plain <see cref="Password"/> into <see cref="PasswordHash"/>.
		/// Call after model validation succeeds.
		/// </summary>
		public void SetPassword(string plainPassword)
		{
			PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword);
		}

		public bool VerifyPassword(string plainPassword)
		{
			if (string.IsNullOrEmpty(plainPassword) || string.IsNullOrEmpty(PasswordHash))
			{
				return false;
			}

			return BCrypt.Net.BCrypt.Verify(plainPassword, PasswordHash);
		}

		public bool IsLockedOut(DateTime utcNow)
		{
			return LockoutEndUtc.HasValue && LockoutEndUtc.Value > utcNow;
		}

		public int RemainingLockoutMinutes(DateTime utcNow)
		{
			if (!IsLockedOut(utcNow))
			{
				return 0;
			}

			var remaining = LockoutEndUtc!.Value - utcNow;
			return Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
		}

		public void ResetLockout()
		{
			AccessFailedCount = 0;
			LockoutEndUtc = null;
		}

		/// <summary>
		/// Records a failed password attempt. Returns true when this attempt triggers a lockout.
		/// </summary>
		public bool RegisterFailedAttempt(DateTime utcNow)
		{
			if (LockoutEndUtc.HasValue && LockoutEndUtc.Value <= utcNow)
			{
				ResetLockout();
			}

			AccessFailedCount++;
			if (AccessFailedCount < LoginLockoutRules.MaxFailedAttempts)
			{
				return false;
			}

			LockoutEndUtc = utcNow.AddMinutes(LoginLockoutRules.LockoutMinutes);
			return true;
		}
	}

	public static class LoginLockoutRules
	{
		public const int MaxFailedAttempts = 5;
		public const int LockoutMinutes = 15;
	}
}
