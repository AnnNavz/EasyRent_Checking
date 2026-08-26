namespace EasyRent_Checking.Models
{
	public static class LoginMfaRules
	{
		public const int CodeLength = 6;
		public const int CodeLifetimeMinutes = 10;
		public const int MaxFailedAttempts = 5;
		public const int ResendCooldownSeconds = 60;
	}
}
