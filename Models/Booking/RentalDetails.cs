using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyRent_Checking.Models
{
	public class RentalDetails : IValidatableObject
	{
		[Key]
		public int RentalDetailsID { get; set; }

		[Required(ErrorMessage = "Rental is required.")]
		[Display(Name = "Rental")]
		public int RentalID { get; set; }

		[ForeignKey(nameof(RentalID))]
		public Rental? Rental { get; set; }

		[Required(ErrorMessage = "Vehicle is required.")]
		[Display(Name = "Vehicle")]
		public int VehicleId { get; set; }

		[ForeignKey(nameof(VehicleId))]
		public Vehicle? Vehicle { get; set; }

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
