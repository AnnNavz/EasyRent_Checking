namespace EasyRent_Checking.Services
{
	public static class PasswordHasher
	{
		public static string Hash(string plainPassword)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(plainPassword);
			return BCrypt.Net.BCrypt.HashPassword(plainPassword);
		}

		public static bool Verify(string plainPassword, string passwordHash)
		{
			if (string.IsNullOrEmpty(plainPassword) || string.IsNullOrEmpty(passwordHash))
			{
				return false;
			}

			return BCrypt.Net.BCrypt.Verify(plainPassword, passwordHash);
		}

		public static bool IsHashed(string? value)
			=> !string.IsNullOrEmpty(value)
			   && (value.StartsWith("$2a$", StringComparison.Ordinal)
				   || value.StartsWith("$2b$", StringComparison.Ordinal)
				   || value.StartsWith("$2y$", StringComparison.Ordinal));
	}
}
