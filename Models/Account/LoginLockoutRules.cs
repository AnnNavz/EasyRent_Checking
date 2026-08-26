namespace EasyRent_Checking.Models
{
	public static class LoginLockoutRules
	{
		public const int MaxFailedAttempts = 5;
		public const int LockoutMinutes = 15;
	}
}
