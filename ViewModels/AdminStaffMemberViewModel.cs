using EasyRent_Checking.Models;

namespace EasyRent_Checking.ViewModels
{
	/// <summary>Shared details payload for admin or staff member pages.</summary>
	public class AdminStaffMemberViewModel
	{
		public int Id { get; set; }
		public string FullName { get; set; } = string.Empty;
		public string ContactNumber { get; set; } = string.Empty;
		public string Address { get; set; } = string.Empty;
		public DateTime CreatedAt { get; set; }
		public string? ProfileImagePath { get; set; }
		public bool IsSelfDeactivated { get; set; }
		public bool IsStaff { get; set; }
		public User? User { get; set; }

		public static AdminStaffMemberViewModel FromUser(User user)
			=> new()
			{
				Id = user.UserId,
				FullName = user.FullName,
				ContactNumber = user.ContactNumber,
				Address = user.Address ?? string.Empty,
				CreatedAt = user.CreatedAt,
				ProfileImagePath = user.ProfileImagePath,
				IsSelfDeactivated = user.IsSelfDeactivated,
				IsStaff = user.Role == UserRole.Staff,
				User = user
			};
	}
}
