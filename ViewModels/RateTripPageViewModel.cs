using System.ComponentModel.DataAnnotations;

namespace EasyRent_Checking.ViewModels
{
	public class RateTripPageViewModel
	{
		public int RentalId { get; set; }

		public string BookingLabel { get; set; } = string.Empty;

		public List<TransitFeedbackInputModel> Items { get; set; } = new();
	}
}
