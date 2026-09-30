using System.ComponentModel.DataAnnotations;

namespace EasyRent_Checking.ViewModels;

public class ProcessCustomerRefundViewModel
{
	public int RentalId { get; set; }

	[Required(ErrorMessage = "Please enter the refund amount.")]
	[Range(0, 999999999.99, ErrorMessage = "Refund amount cannot be negative.")]
	[Display(Name = "Refund amount")]
	public decimal RefundAmount { get; set; }

	[Display(Name = "Refund receipt")]
	public IFormFile? RefundReceiptImageFile { get; set; }

	[StringLength(500)]
	[Display(Name = "Internal notes")]
	public string? Notes { get; set; }

	public string CustomerReason { get; set; } = string.Empty;

	public decimal AmountPaid { get; set; }

	public decimal RefundRequestedAmount { get; set; }
}
