using System.ComponentModel.DataAnnotations;

namespace EasyRent_Checking.ViewModels;

public class RejectCustomerRefundViewModel
{
	public int RentalId { get; set; }

	[Required(ErrorMessage = "Please select a rejection reason.")]
	[Display(Name = "Rejection reason")]
	public string Reason { get; set; } = string.Empty;

	[StringLength(500, ErrorMessage = "Details cannot exceed 500 characters.")]
	[Display(Name = "Additional details")]
	public string? OtherDetails { get; set; }

	public string CustomerReason { get; set; } = string.Empty;

	public decimal RefundRequestedAmount { get; set; }

	public static readonly string[] ReasonOptions =
	[
		"Suspicious or fraudulent transaction",
		"Unverifiable payment",
		"Policy violation",
		"Duplicate refund request",
		"Other"
	];

	public string BuildStoredReason()
	{
		if (string.Equals(Reason, "Other", StringComparison.OrdinalIgnoreCase))
		{
			return OtherDetails?.Trim() ?? string.Empty;
		}

		if (string.IsNullOrWhiteSpace(OtherDetails))
		{
			return Reason.Trim();
		}

		return $"{Reason.Trim()} — {OtherDetails.Trim()}";
	}
}
