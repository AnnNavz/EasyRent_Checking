using System.Linq;
using EasyRent_Checking.Models;

namespace EasyRent_Checking.Services
{
	public static class PhoneNumber
	{
		/// <summary>Normalize PH mobiles to 11-digit 09xxxxxxxxx form.</summary>
		public static string Normalize(string? phone)
			=> FieldRules.NormalizePhMobile(phone);

		/// <summary>Common stored variants for the same PH mobile (exact + normalized + +63…).</summary>
		public static IReadOnlyList<string> Variants(string? phone)
		{
			var variants = new HashSet<string>(StringComparer.Ordinal);
			if (!string.IsNullOrWhiteSpace(phone))
			{
				variants.Add(phone.Trim());
			}

			var normalized = Normalize(phone);
			if (!string.IsNullOrEmpty(normalized))
			{
				variants.Add(normalized);
				if (normalized.StartsWith('0') && normalized.Length == 11)
				{
					variants.Add("+63" + normalized[1..]);
					variants.Add("63" + normalized[1..]);
				}
			}

			return variants.ToList();
		}

		public static bool Matches(string? left, string? right) =>
			!string.IsNullOrEmpty(Normalize(left))
			&& Normalize(left) == Normalize(right);
	}
}
