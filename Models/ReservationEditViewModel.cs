using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace EasyRent_Checking.Models
{
	public class ReservationEditViewModel
	{
		[ValidateNever]
		public Vehicle Vehicle { get; set; } = null!;

		[ValidateNever]
		public ReservationPricingSummary Pricing { get; set; } = null!;

		public Reservation Reservation { get; set; } = new Reservation();

		[Required(ErrorMessage = "Please select a payment channel.")]
		[Display(Name = "Payment Channel")]
		public PaymentChannel? PaymentChannel { get; set; }

		[StringLength(100)]
		[Display(Name = "Account Name (Sender)")]
		public string? PayerAccountName { get; set; }

		[StringLength(100)]
		[Display(Name = "Reference / Transaction Number")]
		public string? PaymentReference { get; set; }

		[Display(Name = "Amount Sent (₱)")]
		[Column(TypeName = "decimal(18,2)")]
		public decimal? AmountSent { get; set; }

		[Display(Name = "Date & Time of Payment")]
		[DataType(DataType.DateTime)]
		public DateTime? PaymentDateTime { get; set; }

		[Display(Name = "Upload Payment Screenshot / Receipt")]
		public IFormFile? PaymentProofFile { get; set; }

		public string? PaymentProofPath { get; set; }

		[StringLength(1000)]
		[Display(Name = "Additional Notes (Optional)")]
		public string? PaymentNotes { get; set; }
	}
}
