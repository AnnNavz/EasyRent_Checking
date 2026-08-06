using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyRent_Checking.Models
{
	public class ReservationInputModel
	{
		public int ReservationId { get; set; }
		public int ReservationDetailsID { get; set; }

		[Required(ErrorMessage = "Customer name is required.")]
		[StringLength(100, ErrorMessage = "Customer name cannot exceed 100 characters.")]
		[Display(Name = "Customer Name")]
		public string CustomerName { get; set; } = string.Empty;

		[Required(ErrorMessage = "Contact number is required.")]
		[DataType(DataType.PhoneNumber)]
		[RegularExpression(@"^(09|\+639)\d{9}$", ErrorMessage = "Please enter a valid mobile number.")]
		[Display(Name = "Contact Number")]
		public string ContactNumber { get; set; } = string.Empty;

		[StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
		[Display(Name = "Notes")]
		public string? Notes { get; set; }

		[Display(Name = "Status")]
		public ReservationStatus ReservationStatus { get; set; } = ReservationStatus.Pending;

		[Required(ErrorMessage = "Vehicle is required.")]
		[Display(Name = "Vehicle")]
		public int VehicleId { get; set; }

		[Required(ErrorMessage = "Pickup location is required.")]
		[StringLength(200)]
		[Display(Name = "Pickup Location")]
		public string PickupLocation { get; set; } = string.Empty;

		[Required(ErrorMessage = "Drop-off location is required.")]
		[StringLength(200)]
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
		[Range(1, 60)]
		[Display(Name = "Passenger Count")]
		public int PassengerCount { get; set; } = 1;

		[Display(Name = "Senior/PWD Discount (if applicable)")]
		public Discount Discount { get; set; } = Discount.No;

		[StringLength(255)]
		[Display(Name = "Discount Picture Path")]
		public string? DiscountImagePath { get; set; }

		[NotMapped]
		[Display(Name = "Upload Discount Picture")]
		[DataType(DataType.Upload)]
		public IFormFile? DiscountImageFile { get; set; }

		public static ReservationInputModel FromEntities(Reservation reservation, ReservationDetails? details = null)
		{
			details ??= reservation.Details;
			return new ReservationInputModel
			{
				ReservationId = reservation.ReservationId,
				ReservationDetailsID = details?.ReservationDetailsID ?? 0,
				CustomerName = reservation.CustomerName,
				ContactNumber = reservation.ContactNumber,
				Notes = reservation.Notes,
				ReservationStatus = reservation.ReservationStatus,
				VehicleId = details?.VehicleId ?? 0,
				PickupLocation = details?.PickupLocation ?? string.Empty,
				DropoffLocation = details?.DropoffLocation ?? string.Empty,
				PickupDate = details?.PickupDate ?? default,
				ReturnDate = details?.ReturnDate ?? default,
				PickupTime = details?.PickupTime ?? default,
				ReturnTime = details?.ReturnTime ?? default,
				PassengerCount = details?.PassengerCount ?? 1,
				Discount = details?.Discount ?? Discount.No,
				DiscountImagePath = details?.DiscountImagePath
			};
		}

		public void ApplyTo(Reservation reservation, ReservationDetails details)
		{
			reservation.CustomerName = CustomerName;
			reservation.ContactNumber = ContactNumber;
			reservation.Notes = Notes;
			reservation.ReservationStatus = ReservationStatus;

			details.VehicleId = VehicleId;
			details.PickupLocation = PickupLocation;
			details.DropoffLocation = DropoffLocation;
			details.PickupDate = PickupDate;
			details.ReturnDate = ReturnDate;
			details.PickupTime = PickupTime;
			details.ReturnTime = ReturnTime;
			details.PassengerCount = PassengerCount;
			details.Discount = Discount;
			details.DiscountImagePath = DiscountImagePath;
			details.DiscountImageFile = DiscountImageFile;
		}
	}
}
