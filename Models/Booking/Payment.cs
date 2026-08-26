using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyRent_Checking.Models
{
	public class Payment
	{
		[Key]
		public int PaymentId { get; set; }

		[Required(ErrorMessage = "Rental is required.")]
		[Display(Name = "Rental")]
		[ForeignKey(nameof(Rental))]
		public int RentalId { get; set; }

		public Rental? Rental { get; set; }

		[Required(ErrorMessage = "Payment method is required.")]
		[StringLength(30, ErrorMessage = "Payment method cannot exceed 30 characters.")]
		[Display(Name = "Payment Method")]
		public string PaymentMethod { get; set; } = string.Empty;

		[Required(ErrorMessage = "Payment type is required.")]
		[StringLength(30, ErrorMessage = "Payment type cannot exceed 30 characters.")]
		[Display(Name = "Payment Type")]
		public string PaymentType { get; set; } = string.Empty;

		[Required(ErrorMessage = "Total amount is required.")]
		[Column(TypeName = "decimal(18,2)")]
		[Range(0, 999999999.99, ErrorMessage = "Total amount cannot be negative.")]
		[Display(Name = "Total Amount")]
		public decimal TotalAmount { get; set; }

		[Required(ErrorMessage = "Amount paid is required.")]
		[Column(TypeName = "decimal(18,2)")]
		[Range(0, 999999999.99, ErrorMessage = "Amount paid cannot be negative.")]
		[Display(Name = "Amount Paid")]
		public decimal AmountPaid { get; set; }

		[NotMapped]
		[Display(Name = "Change")]
		public decimal ChangeAmount => AmountPaid > TotalAmount ? AmountPaid - TotalAmount : 0;

		[StringLength(100, ErrorMessage = "Account name cannot exceed 100 characters.")]
		[Display(Name = "Account Name")]
		public string? AccountName { get; set; }

		[StringLength(100, ErrorMessage = "Transaction reference cannot exceed 100 characters.")]
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

		[StringLength(1000, ErrorMessage = "Payment notes cannot exceed 1000 characters.")]
		[Display(Name = "Payment Notes")]
		[DataType(DataType.MultilineText)]
		public string? PaymentNotes { get; set; }
	}
}
