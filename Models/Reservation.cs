using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EasyRent_Checking.Models.Validation;

namespace EasyRent_Checking.Models
{
	public class Reservation
	{
		[Key]
		[Display(Name = "Reservation ID")]
		public int ReservationID { get; set; }

		[Required(ErrorMessage = "A vehicle must be selected.")]
		[Display(Name = "Vehicle")]
		public int VehicleId { get; set; }

		[ForeignKey(nameof(VehicleId))]
		public Vehicle? Vehicle { get; set; }

		[Required(ErrorMessage = "Customer name is required.")]
		[StringLength(100, ErrorMessage = "Customer name cannot exceed 100 characters.")]
		[Display(Name = "Customer Name")]
		public string CustomerName { get; set; } = string.Empty;

		[Required(ErrorMessage = "Contact information is required.")]
		[StringLength(100, ErrorMessage = "Contact information cannot exceed 100 characters.")]
		[Display(Name = "Contact Information")]
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

		[StringLength(255)]
		[Display(Name = "Discount Valid ID Picture")]
		public string? ImagePath { get; set; }

		[NotMapped]
		[Display(Name = "Upload Discount Valid ID Picture")]
		public IFormFile? ImageFile { get; set; }
	}
}
