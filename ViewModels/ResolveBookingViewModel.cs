using System.ComponentModel.DataAnnotations;
using EasyRent_Checking.Models;

namespace EasyRent_Checking.ViewModels
{
	public class ResolveBookingVehicleOption
	{
		public int VehicleId { get; set; }

		public string Title { get; set; } = string.Empty;

		public string TypeLabel { get; set; } = string.Empty;

		public int PassengersCount { get; set; }

		public string? ImagePath { get; set; }
	}

	public class ResolveBookingViewModel
	{
		public int RentalId { get; set; }

		public int? TransitId { get; set; }

		public string? ReturnUrl { get; set; }

		public string BookingLabel { get; set; } = string.Empty;

		public string CustomerName { get; set; } = string.Empty;

		public string CurrentVehicleLabel { get; set; } = string.Empty;

		public int CurrentVehicleId { get; set; }

		public decimal AmountPaid { get; set; }

		public DateOnly? PickupDate { get; set; }

		public TimeOnly PickupTime { get; set; }

		public string PickupLocation { get; set; } = string.Empty;

		public List<ResolveBookingVehicleOption> ReplacementVehicles { get; set; } = [];

		[Required(ErrorMessage = "Please enter a reason.")]
		[StringLength(200, ErrorMessage = "Reason cannot exceed 200 characters.")]
		[Display(Name = "Reason")]
		public string Reason { get; set; } = string.Empty;

		[Display(Name = "Replacement Vehicle")]
		public int? ReplacementVehicleId { get; set; }

		[Display(Name = "Refund Amount")]
		[Range(0, 999999999.99, ErrorMessage = "Refund amount cannot be negative.")]
		public decimal RefundAmount { get; set; }

		[Display(Name = "Refund Receipt")]
		public IFormFile? RefundReceiptImageFile { get; set; }

		[StringLength(500)]
		[Display(Name = "Notes")]
		public string? Notes { get; set; }
	}
}
