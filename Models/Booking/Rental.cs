using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyRent_Checking.Models
{
	public class Rental : IValidatableObject
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
		[StringLength(11, MinimumLength = 11, ErrorMessage = "Contact number must be exactly 11 digits.")]
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

		/// <summary>Why staff cancelled this booking (company-initiated resolution).</summary>
		[StringLength(500)]
		[Display(Name = "Cancellation Reason")]
		public string? CompanyCancellationReason { get; set; }

		/// <summary>Reason provided by the customer when cancelling.</summary>
		[StringLength(500)]
		[Display(Name = "Customer Cancellation Reason")]
		public string? CustomerCancellationReason { get; set; }

		[Display(Name = "Refund Requested At")]
		[DataType(DataType.DateTime)]
		public DateTime? RefundRequestedAt { get; set; }

		[Column(TypeName = "decimal(18,2)")]
		[Display(Name = "Refund Requested Amount")]
		public decimal? RefundRequestedAmount { get; set; }

		[Display(Name = "Cancellation Fee Paid At")]
		[DataType(DataType.DateTime)]
		public DateTime? CancellationFeePaidAt { get; set; }

		/// <summary>Amount refunded to the customer when the booking was cancelled by staff.</summary>
		[Column(TypeName = "decimal(18,2)")]
		[Display(Name = "Refund Amount")]
		public decimal? RefundAmount { get; set; }

		[Display(Name = "Refunded At")]
		[DataType(DataType.DateTime)]
		public DateTime? RefundedAt { get; set; }

		[Display(Name = "Refund Rejected At")]
		[DataType(DataType.DateTime)]
		public DateTime? RefundRejectedAt { get; set; }

		[StringLength(500)]
		[Display(Name = "Refund Rejection Reason")]
		public string? RefundRejectionReason { get; set; }

		[StringLength(255)]
		[Display(Name = "Refund Receipt")]
		public string? RefundReceiptImagePath { get; set; }

		[NotMapped]
		[Display(Name = "Upload Refund Receipt")]
		[DataType(DataType.Upload)]
		public IFormFile? RefundReceiptImageFile { get; set; }

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

		[Required(ErrorMessage = "Pickup location is required.")]
		[StringLength(200, ErrorMessage = "Pickup location cannot exceed 200 characters.")]
		[Display(Name = "Pickup Location")]
		public string PickupLocation { get; set; } = string.Empty;

		[Required(ErrorMessage = "Drop-off location is required.")]
		[StringLength(200, ErrorMessage = "Drop-off location cannot exceed 200 characters.")]
		[Display(Name = "Drop-off Location")]
		public string DropoffLocation { get; set; } = string.Empty;

		[Required(ErrorMessage = "Pickup date is required.")]
		[DataType(DataType.Date)]
		[Display(Name = "Pickup Date")]
		public DateOnly PickupDate { get; set; }

		[Required(ErrorMessage = "Return date is required.")]
		[DataType(DataType.Date)]
		[Display(Name = "Return Date")]
		public DateOnly ReturnDate { get; set; }

		[Required(ErrorMessage = "Pickup time is required.")]
		[DataType(DataType.Time)]
		[Display(Name = "Pickup Time")]
		public TimeOnly PickupTime { get; set; }

		[Required(ErrorMessage = "Return time is required.")]
		[DataType(DataType.Time)]
		[Display(Name = "Return Time")]
		public TimeOnly ReturnTime { get; set; }

		[Required(ErrorMessage = "Passenger count is required.")]
		[Range(1, 60, ErrorMessage = "Passenger count must be between 1 and 60.")]
		[Display(Name = "Passenger Count")]
		public int PassengerCount { get; set; }

		[Display(Name = "Senior/PWD Discount (if applicable)")]
		public Discount Discount { get; set; } = Discount.No;

		[StringLength(255)]
		[Display(Name = "Discount Picture Path")]
		public string? DiscountImagePath { get; set; }

		[NotMapped]
		[Display(Name = "Upload Discount Picture")]
		[DataType(DataType.Upload)]
		public IFormFile? DiscountImageFile { get; set; }

		/// <summary>
		/// Vehicle lines materialized on admin approve. Until then, selected ids live in <see cref="PendingVehicleIdsJson"/>.
		/// </summary>
		public ICollection<RentalVehicle> RentalVehicles { get; set; } = new List<RentalVehicle>();

		/// <summary>JSON array of vehicle ids chosen at booking submit while status is still pending.</summary>
		[StringLength(500)]
		public string? PendingVehicleIdsJson { get; set; }

		[NotMapped]
		public bool IsUnpaidReserve =>
			RentalOption == RentalOption.Reserve
			&& RentalStatus == RentalStatus.Pending
			&& PaymentDueAt != null;

		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
			=> FieldRules.ValidateReturnAfterPickup(
				PickupDate,
				PickupTime,
				ReturnDate,
				ReturnTime,
				nameof(ReturnDate),
				nameof(ReturnTime));
	}
}
