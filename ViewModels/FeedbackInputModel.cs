using System.ComponentModel.DataAnnotations;

namespace EasyRent_Checking.ViewModels
{
	public class FeedbackInputModel
	{
		public int RentalId { get; set; }

		[Required(ErrorMessage = "Please select a star rating.")]
		[Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars.")]
		[Display(Name = "Rating")]
		public int Rating { get; set; }

		[StringLength(1000, ErrorMessage = "Comment cannot exceed 1000 characters.")]
		[DataType(DataType.MultilineText)]
		[Display(Name = "Comment")]
		public string? Comment { get; set; }

		public string BookingLabel { get; set; } = string.Empty;
		public string VehicleTitle { get; set; } = string.Empty;
		public string? VehicleImagePath { get; set; }
		public string? DriverName { get; set; }
	}
}
