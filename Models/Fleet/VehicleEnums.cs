using System.ComponentModel.DataAnnotations;

namespace EasyRent_Checking.Models
{
	public static class VehicleTypes
	{
		public const string Suv = "SUV";
		public const string Van = "Van";
		public const string Sedan = "Sedan";
		public const string Luxury = "Luxury";
		public const string AddNewValue = "__add__";

		public static readonly string[] Presets = [Suv, Van, Sedan, Luxury];

		public static IList<string> Options(IEnumerable<string?> extra, string? current = null)
		{
			var extras = extra
				.Concat([current])
				.Where(t => !string.IsNullOrWhiteSpace(t) && t != AddNewValue)
				.Select(t => t!.Trim())
				.Where(t => !Presets.Contains(t, StringComparer.OrdinalIgnoreCase))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.OrderBy(t => t, StringComparer.OrdinalIgnoreCase);

			return Presets.Concat(extras).ToList();
		}

		public static IList<string> BrowseCategories(IEnumerable<string?> extra)
			=> new[] { "All" }.Concat(Options(extra)).ToList();

		public static string BrowseLabel(string category)
			=> category.ToUpperInvariant() switch
			{
				"ALL" => "All Cars",
				"SUV" => "SUVs",
				"SEDAN" => "Sedans",
				"VAN" => "VANs",
				"LUXURY" => "Luxury",
				_ => category
			};

		public static string Display(string? type)
			=> string.IsNullOrWhiteSpace(type) ? "—" : type.Trim();

		public static string DisplayUpper(string? type)
			=> Display(type).ToUpperInvariant();

		public static bool IsVan(string? type)
			=> string.Equals(type, Van, StringComparison.OrdinalIgnoreCase);

		public static string Normalize(string? type)
			=> string.IsNullOrWhiteSpace(type) ? string.Empty : type.Trim();
	}

	public enum VehicleStatus
	{
		Available,
		Rented,
		[Display(Name = "In Maintenance")]
		InMaintenance,
		Unavailable
	}
}
