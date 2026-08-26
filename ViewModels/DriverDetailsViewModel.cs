using EasyRent_Checking.Models;

namespace EasyRent_Checking.ViewModels
{
	public class DriverActivityLogItem
	{
		public DateTime OccurredAt { get; set; }
		public string Title { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public TripStatus TripStatus { get; set; }
	}

	public class DriverReviewItem
	{
		public string CustomerName { get; set; } = "Customer";
		public string? Comment { get; set; }
		public DateTime CreatedAt { get; set; }
		public int Professionalism { get; set; }
		public int Driving { get; set; }
		public int Courtesy { get; set; }

		public int OverallStars
			=> (int)Math.Round((Professionalism + Driving + Courtesy) / 3.0, MidpointRounding.AwayFromZero);
	}

	public class DriverDocumentItem
	{
		public string DisplayName { get; set; } = string.Empty;
		public string FileName { get; set; } = string.Empty;
		public string? ImagePath { get; set; }
		public string TypeLabel { get; set; } = "Driver's License";
		public DateTime UploadedAt { get; set; }
		public bool IsVerified { get; set; }
	}

	public class DriverDetailsViewModel
	{
		public Driver Driver { get; set; } = null!;
		public IReadOnlyList<DriverActivityLogItem> ActivityHistory { get; set; } = Array.Empty<DriverActivityLogItem>();
		public IReadOnlyList<DriverReviewItem> Reviews { get; set; } = Array.Empty<DriverReviewItem>();
		public IReadOnlyList<DriverDocumentItem> Documents { get; set; } = Array.Empty<DriverDocumentItem>();
		public int ReviewCount { get; set; }
		public double OverallTen { get; set; }
		public double ProfessionalismTen { get; set; }
		public double DrivingTen { get; set; }
		public double CourtesyTen { get; set; }
	}
}
