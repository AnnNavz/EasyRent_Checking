using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using EasyRent_Checking.Models.Validation;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace EasyRent_Checking.Models
{
	public enum ReservationStatus
	{
		[Display(Name = "Pending Verification")]
		Pending,
		Confirmed,
		[Display(Name = "In Progress")]
		InProgress,
		Completed,
		Cancelled,
		Rejected
	}

	public enum PaymentChannel
	{
		GCash,
		[Display(Name = "BDO Bank Transfer / QR")]
		BDO,
		[Display(Name = "Walk-in (Pay at Office)")]
		WalkIn
	}

	public class Reservation
	{
		[Key]
		[Display(Name = "Reservation ID")]
		public int ReservationID { get; set; }

		[Required(ErrorMessage = "A vehicle must be selected.")]
		[Range(1, int.MaxValue, ErrorMessage = "A vehicle must be selected.")]
		[Display(Name = "Vehicle")]
		public int VehicleId { get; set; }

		[ForeignKey(nameof(VehicleId))]
		[ValidateNever]
		[JsonIgnore]
		public Vehicle? Vehicle { get; set; }

		[Required(ErrorMessage = "Customer name is required.")]
		[StringLength(100, ErrorMessage = "Customer name cannot exceed 100 characters.")]
		[Display(Name = "Customer Name")]
		public string CustomerName { get; set; } = string.Empty;

		[Required(ErrorMessage = "Contact number is required.")]
		[RegularExpression(@"^09\d{9}$", ErrorMessage = "Contact number must be an 11-digit Philippine mobile number (e.g. 09123456789).")]
		[StringLength(11, ErrorMessage = "Contact number cannot exceed 11 characters.")]
		[Display(Name = "Contact Number")]
		public string ContactInfo { get; set; } = string.Empty;

		[Required(ErrorMessage = "Pickup location is required.")]
		[StringLength(255, ErrorMessage = "Pickup location cannot exceed 255 characters.")]
		[Display(Name = "Pickup Location")]
		public string PickupLoc { get; set; } = string.Empty;

		[Required(ErrorMessage = "Drop-off location is required.")]
		[StringLength(255, ErrorMessage = "Drop-off location cannot exceed 255 characters.")]
		[Display(Name = "Drop-off Location")]
		public string DropoffLoc { get; set; } = string.Empty;

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
		[Display(Name = "Passenger Count")]
		[PassengerCountWithinVehicleCapacity]
		public int PassengerCount { get; set; }

		[StringLength(1000, ErrorMessage = "Special notes cannot exceed 1000 characters.")]
		[Display(Name = "Special Notes")]
		public string? SpNotes { get; set; }

		[Display(Name = "Senior Citizen / PWD Discount")]
		[Range(0, 1, ErrorMessage = "Discount must be 0 (No) or 1 (Yes).")]
		public int Discount { get; set; }

		[Required]
		[Display(Name = "Status")]
		public ReservationStatus Status { get; set; } = ReservationStatus.Pending;

		[Display(Name = "Created At (UTC)")]
		public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

		/// <summary>
		/// Soft-lock expiry while the customer completes payment.
		/// Null after payment proof is submitted (dates stay blocked until admin acts).
		/// </summary>
		[Display(Name = "Locked Until (UTC)")]
		public DateTime? LockedUntilUtc { get; set; }

		/// <summary>
		/// Admin wizard draft — not listed until Confirm Booking on step 4.
		/// </summary>
		public bool IsDraft { get; set; }

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

		[StringLength(255)]
		[Display(Name = "Payment Screenshot / Receipt")]
		public string? PaymentProofPath { get; set; }

		[StringLength(1000)]
		[Display(Name = "Additional Notes")]
		public string? PaymentNotes { get; set; }

		[StringLength(255)]
		[Display(Name = "Discount Valid ID Picture")]
		public string? ImagePath { get; set; }

		[NotMapped]
		[JsonIgnore]
		[Display(Name = "Upload Discount Valid ID Picture")]
		public IFormFile? ImageFile { get; set; }

		[NotMapped]
		[JsonIgnore]
		[Display(Name = "Upload Payment Screenshot / Receipt")]
		public IFormFile? PaymentProofFile { get; set; }
	}
}
