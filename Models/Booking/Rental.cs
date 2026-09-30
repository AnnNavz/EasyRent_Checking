using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyRent_Checking.Models
{
	public class Rental
	{
		[Key]
		public int RentalId { get; set; }

		/// <summary>Linked registered customer when known. Null for admin walk-in bookings.</summary>
		[Display(Name = "Customer")]
		public int? CustomerId { get; set; }

		[ForeignKey(nameof(CustomerId))]
		public CustomerProfile? Customer { get; set; }

		[Required(ErrorMessage = "Customer name is required.")]
		[StringLength(100, ErrorMessage = "Customer name cannot exceed 100 characters.")]
		[Display(Name = "Customer Name")]
		public string CustomerName { get; set; } = string.Empty;

		[Required(ErrorMessage = "Contact number is required.")]
		[DataType(DataType.PhoneNumber)]
		[StringLength(20, ErrorMessage = "Contact number cannot exceed 20 characters.")]
		[RegularExpression(FieldRules.PhMobile, ErrorMessage = FieldRules.PhMobileMessage)]
		[Display(Name = "Contact Number")]
		public string ContactNumber { get; set; } = string.Empty;

		[StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
		[Display(Name = "Notes")]
		public string? Notes { get; set; }

		[Display(Name = "Status")]
		public RentalStatus RentalStatus { get; set; } = RentalStatus.Pending;

		[Display(Name = "Rental Option")]
		public RentalOption RentalOption { get; set; } = RentalOption.Book;

		/// <summary>For Reserve: deadline to submit payment. Null for Book.</summary>
		[Display(Name = "Payment Due")]
		[DataType(DataType.DateTime)]
		public DateTime? PaymentDueAt { get; set; }

		/// <summary>When the customer (or staff) cancelled this rental.</summary>
		[Display(Name = "Cancelled At")]
		[DataType(DataType.DateTime)]
		public DateTime? CancelledAt { get; set; }

		/// <summary>Fee charged on cancel. ₱2,000 when trip was InTransit; otherwise 0.</summary>
		[Display(Name = "Cancellation Fee")]
		[Column(TypeName = "decimal(18,2)")]
		public decimal? CancellationFee { get; set; }

		/// <summary>Locked-in rental total (base + succeeding fee − discount) at booking time. Source of truth for payments.</summary>
		[Required]
		[Column(TypeName = "decimal(18,2)")]
		[Range(0, 999999999.99, ErrorMessage = "Total amount cannot be negative.")]
		[Display(Name = "Total Amount")]
		public decimal TotalAmount { get; set; }

		/// <summary>
		/// Total succeeding fee for this rental: (hours beyond the 8-hour base package) × succeeding rate.
		/// Example: 11 hours total → 3 succeeding hours × ₱400 = ₱1,200 stored here (not the ₱400 rate).
		/// </summary>
		[Required]
		[Column(TypeName = "decimal(18,2)")]
		[Range(0, 999999999.99, ErrorMessage = "Succeeding fee total cannot be negative.")]
		[Display(Name = "Total Succeeding Fee")]
		public decimal SucceedingFeeTotal { get; set; }

		public RentalDetails? Details { get; set; }

		public ICollection<RentalVehicle> RentalVehicles { get; set; } = new List<RentalVehicle>();

		[NotMapped]
		public bool IsUnpaidReserve =>
			RentalOption == RentalOption.Reserve
			&& RentalStatus == RentalStatus.Pending
			&& PaymentDueAt != null;
	}
}
