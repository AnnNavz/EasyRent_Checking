using System.Globalization;

namespace EasyRent_Checking
{
	public static class AdminDates
	{
		public const string Pattern = "MM-dd-yyyy";
		public const string PatternWithTime = "MM-dd-yyyy h:mm tt";

		public static string Format(DateOnly value) => value.ToString(Pattern, CultureInfo.InvariantCulture);

		public static string Format(DateOnly? value) => value?.ToString(Pattern, CultureInfo.InvariantCulture) ?? "—";

		public static string Format(DateTime value, bool includeTime = false)
			=> value.ToString(includeTime ? PatternWithTime : Pattern, CultureInfo.InvariantCulture);

		public static string Format(DateTime? value, bool includeTime = false)
			=> value == null ? "—" : Format(value.Value, includeTime);

		public static string FormatRange(DateOnly start, DateOnly end)
			=> start == end ? Format(start) : $"{Format(start)} - {Format(end)}";
	}
}
