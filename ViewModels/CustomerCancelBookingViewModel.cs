using System.ComponentModel.DataAnnotations;
using EasyRent_Checking.Models;

namespace EasyRent_Checking.ViewModels;

public class CustomerCancelBookingViewModel
{
	public int RentalId { get; set; }

	[Required(ErrorMessage = "Please tell us why you are cancelling.")]
	[StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
	[Display(Name = "Cancellation reason")]
	public string Reason { get; set; } = string.Empty;

	[Display(Name = "Request refund of amount paid")]
	public bool RequestRefund { get; set; }

	[Display(Name = "Payment method")]
	public string? PaymentMethod { get; set; }

	[Display(Name = "Account name")]
	public string? AccountName { get; set; }

	[Display(Name = "Transaction reference")]
	public string? TransactionReference { get; set; }

	[Display(Name = "Payment receipt")]
	public IFormFile? ReceiptImageFile { get; set; }

	[StringLength(500)]
	public string? PaymentNotes { get; set; }

	public decimal CancellationFeeAmount { get; set; } = RentalRules.CancellationFeeWhenInTransit;
}
