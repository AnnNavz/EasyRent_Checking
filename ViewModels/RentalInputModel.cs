using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EasyRent_Checking.Models;
using EasyRent_Checking.Services;

namespace EasyRent_Checking.ViewModels
{
	public class RentalInputModel : IValidatableObject
	{
		public int RentalId { get; set; }

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

		[Display(Name = "Payment Due")]
		[DataType(DataType.DateTime)]
		public DateTime? PaymentDueAt { get; set; }

		[Required(ErrorMessage = "At least one vehicle is required.")]
		[Display(Name = "Vehicle")]
		public int VehicleId { get; set; }

		/// <summary>
		/// All vehicles on this booking (includes <see cref="VehicleId"/> as first).
		/// Bound from hidden inputs on admin create/edit.
		/// </summary>
		[Display(Name = "Vehicles")]
		public List<int> VehicleIds { get; set; } = new();

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

		public int PaymentId { get; set; }

		[StringLength(30, ErrorMessage = "Payment type cannot exceed 30 characters.")]
		[Display(Name = "Payment Type")]
		public string? PaymentType { get; set; }

		[StringLength(30, ErrorMessage = "Payment method cannot exceed 30 characters.")]
		[Display(Name = "Payment Method")]
		public string? PaymentMethod { get; set; }

		[Range(0, 999999999.99, ErrorMessage = "Total amount cannot be negative.")]
		[Display(Name = "Total Amount")]
		public decimal TotalAmount { get; set; }

		[Range(0, 999999999.99, ErrorMessage = "Amount paid cannot be negative.")]
		[Display(Name = "Amount Paid")]
		public decimal AmountPaid { get; set; }

		[StringLength(100, ErrorMessage = "Account name cannot exceed 100 characters.")]
		[Display(Name = "Account Name")]
		public string? AccountName { get; set; }

		[StringLength(100, ErrorMessage = "Transaction reference cannot exceed 100 characters.")]
		[Display(Name = "Transaction Reference")]
		public string? TransactionReference { get; set; }

		[DataType(DataType.DateTime)]
		[Display(Name = "Payment Date")]
		public DateTime PaymentDate { get; set; } = DateTime.Now;

		[NotMapped]
		[Display(Name = "Upload Receipt Picture")]
		[DataType(DataType.Upload)]
		public IFormFile? ReceiptImageFile { get; set; }

		[StringLength(1000, ErrorMessage = "Payment notes cannot exceed 1000 characters.")]
		[Display(Name = "Payment Notes")]
		public string? PaymentNotes { get; set; }

		public static RentalInputModel FromEntities(
			Rental rental,
			Payment? payment = null,
			IEnumerable<RentalVehicle>? rentalVehicles = null)
		{
			var vehicleIds = (rentalVehicles ?? rental.RentalVehicles ?? Enumerable.Empty<RentalVehicle>())
				.OrderBy(v => v.SortOrder)
				.ThenBy(v => v.RentalVehicleId)
				.Select(v => v.VehicleId)
				.Distinct()
				.ToList();
			if (vehicleIds.Count == 0)
			{
				vehicleIds = RentalVehicleWorkflow.ResolveVehicleIds(rental, rentalVehicles);
			}

			return new RentalInputModel
			{
				RentalId = rental.RentalId,
				CustomerName = rental.CustomerName,
				ContactNumber = rental.ContactNumber,
				Notes = rental.Notes,
				RentalStatus = rental.RentalStatus,
				RentalOption = rental.RentalOption,
				PaymentDueAt = rental.PaymentDueAt,
				VehicleId = vehicleIds.FirstOrDefault() > 0
					? vehicleIds.First()
					: 0,
				VehicleIds = vehicleIds,
				PickupLocation = rental.PickupLocation,
				DropoffLocation = rental.DropoffLocation,
				PickupDate = rental.PickupDate,
				ReturnDate = rental.ReturnDate,
				PickupTime = rental.PickupTime,
				ReturnTime = rental.ReturnTime,
				PassengerCount = rental.PassengerCount,
				Discount = rental.Discount,
				DiscountImagePath = rental.DiscountImagePath,
				PaymentId = payment?.PaymentId ?? 0,
				PaymentType = payment?.PaymentType,
				PaymentMethod = payment?.PaymentMethod,
				TotalAmount = rental.TotalAmount > 0
					? rental.TotalAmount
					: (payment?.TotalAmount ?? 0m),
				AmountPaid = payment?.AmountPaid ?? 0m,
				AccountName = payment?.AccountName,
				TransactionReference = payment?.TransactionReference,
				PaymentDate = payment?.PaymentDate ?? DateTime.Now,
				PaymentNotes = payment?.PaymentNotes
			};
		}

		public IReadOnlyList<int> GetNormalizedVehicleIds()
		{
			var ids = (VehicleIds ?? new List<int>())
				.Where(id => id > 0)
				.Distinct()
				.ToList();
			if (ids.Count == 0 && VehicleId > 0)
			{
				ids.Add(VehicleId);
			}
			else if (VehicleId > 0 && !ids.Contains(VehicleId))
			{
				ids.Insert(0, VehicleId);
			}

			return ids;
		}

		public void ApplyTo(Rental rental)
		{
			rental.CustomerName = CustomerName;
			rental.ContactNumber = ContactNumber;
			rental.Notes = Notes;
			rental.RentalStatus = RentalStatus;
			rental.RentalOption = RentalOption;
			rental.PaymentDueAt = PaymentDueAt;

			var vehicleIds = GetNormalizedVehicleIds();
			VehicleId = vehicleIds.FirstOrDefault();
			VehicleIds = vehicleIds.ToList();

			rental.PickupLocation = PickupLocation;
			rental.DropoffLocation = DropoffLocation;
			rental.PickupDate = PickupDate;
			rental.ReturnDate = ReturnDate;
			rental.PickupTime = PickupTime;
			rental.ReturnTime = ReturnTime;
			rental.PassengerCount = PassengerCount;
			rental.Discount = Discount;
			rental.DiscountImagePath = DiscountImagePath;
			rental.DiscountImageFile = DiscountImageFile;
		}

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
