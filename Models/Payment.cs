using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyRent_Checking.Models
{
	public enum PaymentStatus
	{
		Pending,
		Approved,
		Rejected
	}

	public class Payment
	{
		[Key]
		public int PaymentId { get; set; }

		[Required(ErrorMessage = "Reservation is required.")]
		[Display(Name = "Reservation")]
		[ForeignKey(nameof(Reservation))]
		public int ReservationId { get; set; }

		public Reservation? Reservation { get; set; }

		[Required(ErrorMessage = "Payment method is required.")]
		[StringLength(30)]
		[Display(Name = "Payment Method")]
		public string PaymentMethod { get; set; } = string.Empty;

		[Required(ErrorMessage = "Payment type is required.")]
		[StringLength(30)]
		[Display(Name = "Payment Type")]
		public string PaymentType { get; set; } = string.Empty;

		[Required(ErrorMessage = "Total amount is required.")]
		[Column(TypeName = "decimal(18,2)")]
		[Range(0, 999999999.99)]
		[Display(Name = "Total Amount")]
		public decimal TotalAmount { get; set; }

		[Required(ErrorMessage = "Amount paid is required.")]
		[Column(TypeName = "decimal(18,2)")]
		[Range(0, 999999999.99)]
		[Display(Name = "Amount Paid")]
		public decimal AmountPaid { get; set; }

		[NotMapped]
		[Display(Name = "Change")]
		public decimal ChangeAmount => AmountPaid > TotalAmount ? AmountPaid - TotalAmount : 0;

		[StringLength(100, ErrorMessage = "Account name cannot exceed 100 characters.")]
		[Display(Name = "Account Name")]
		public string? AccountName { get; set; }

		[StringLength(100)]
		[Display(Name = "Transaction Reference")]
		public string? TransactionReference { get; set; }

		[Required(ErrorMessage = "Payment date is required.")]
		[DataType(DataType.DateTime)]
		[Display(Name = "Payment Date")]
		public DateTime PaymentDate { get; set; } = DateTime.Now;

		[StringLength(255)]
		[Display(Name = "Receipt Picture Path")]
		public string? ReceiptImagePath { get; set; }

		[NotMapped]
		[Display(Name = "Upload Receipt Picture")]
		[DataType(DataType.Upload)]
		public IFormFile? ReceiptImageFile { get; set; }

		[Required]
		[Display(Name = "Payment Status")]
		public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

		[StringLength(1000)]
		[Display(Name = "Payment Notes")]
		[DataType(DataType.MultilineText)]
		public string? PaymentNotes { get; set; }
	}
}
