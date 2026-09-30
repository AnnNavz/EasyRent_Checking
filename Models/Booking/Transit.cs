using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyRent_Checking.Models
{
	public class Transit : IValidatableObject
	{
		[Key]
		public int TransitID { get; set; }

		[Required(ErrorMessage = "Rental is required.")]
		[Display(Name = "Rental")]
		public int RentalID { get; set; }

		[ForeignKey(nameof(RentalID))]
		public Rental? Rental { get; set; }

		[Display(Name = "Driver")]
		public int? DriverID { get; set; }

		[ForeignKey(nameof(DriverID))]
		public Driver? Driver { get; set; }

		[Required(ErrorMessage = "Vehicle is required.")]
		[Display(Name = "Vehicle")]
		public int VehicleID { get; set; }

		[ForeignKey(nameof(VehicleID))]
		public Vehicle? Vehicle { get; set; }

		/// <summary>Optional link to the rental vehicle line (multi-vehicle bookings).</summary>
		[Display(Name = "Rental Vehicle")]
		public int? RentalVehicleId { get; set; }

		[ForeignKey(nameof(RentalVehicleId))]
		public RentalVehicle? RentalVehicle { get; set; }

		[DataType(DataType.Time)]
		[Display(Name = "Departure Time")]
		public TimeOnly? DepartureTime { get; set; }

		[DataType(DataType.Time)]
		[Display(Name = "Return Time")]
		public TimeOnly? ReturnTime { get; set; }

		[Display(Name = "Fuel Level (Start)")]
		public FuelLevel? FuelLevelStart { get; set; }

		[Display(Name = "Fuel Level (End)")]
		public FuelLevel? FuelLevelEnd { get; set; }

		[Display(Name = "Odometer (Start)")]
		[Range(0, 9999999, ErrorMessage = "Odometer must be between 0 and 9,999,999 km.")]
		public int? OdometerStart { get; set; }

		[Display(Name = "Odometer (End)")]
		[Range(0, 9999999, ErrorMessage = "Odometer must be between 0 and 9,999,999 km.")]
		public int? OdometerEnd { get; set; }

		[StringLength(500, ErrorMessage = "Vehicle condition (start) cannot exceed 500 characters.")]
		[DataType(DataType.MultilineText)]
		[Display(Name = "Vehicle Condition (Start)")]
		public string? VehicleConditionStart { get; set; }

		[StringLength(500, ErrorMessage = "Vehicle condition (end) cannot exceed 500 characters.")]
		[DataType(DataType.MultilineText)]
		[Display(Name = "Vehicle Condition (End)")]
		public string? VehicleConditionEnd { get; set; }

		[StringLength(255)]
		[Display(Name = "Pre-Trip Image")]
		public string? PreTripImagePath { get; set; }

		[StringLength(2000)]
		public string? PreTripImagePathsJson { get; set; }

		[NotMapped]
		[Display(Name = "Upload Pre-Trip Image")]
		[DataType(DataType.Upload)]
		public IFormFile? PreTripImageFile { get; set; }

		[StringLength(255)]
		[Display(Name = "Post-Trip Image")]
		public string? PostTripImagePath { get; set; }

		[StringLength(2000)]
		public string? PostTripImagePathsJson { get; set; }

		[NotMapped]
		[Display(Name = "Upload Post-Trip Image")]
		[DataType(DataType.Upload)]
		public IFormFile? PostTripImageFile { get; set; }

		[StringLength(1000, ErrorMessage = "Remarks cannot exceed 1000 characters.")]
		[DataType(DataType.MultilineText)]
		[Display(Name = "Remarks")]
		public string? Remarks { get; set; }

		[Required(ErrorMessage = "Trip status is required.")]
		[Display(Name = "Trip Status")]
		public TripStatus TripStatus { get; set; } = TripStatus.Scheduled;

		public Feedback? Feedback { get; set; }

		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
		{
			if (OdometerStart.HasValue && OdometerEnd.HasValue && OdometerEnd.Value < OdometerStart.Value)
			{
				yield return new ValidationResult(
					"Ending odometer cannot be lower than the starting reading.",
					new[] { nameof(OdometerEnd) });
			}
		}
	}
}
