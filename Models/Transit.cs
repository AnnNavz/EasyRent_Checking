using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyRent_Checking.Models
{
	public enum FuelLevel
	{
		Empty,
		Quarter,
		Half,
		ThreeQuarters,
		Full
	}

	public enum TripStatus
	{
		Scheduled,
		InTransit,
		Completed,
		Cancelled,
		Delayed
	}

	public class Transit
	{
		[Key]
		public int TransitID { get; set; }

		[Required(ErrorMessage = "Reservation is required.")]
		[Display(Name = "Reservation")]
		public int ReservationID { get; set; }

		[ForeignKey(nameof(ReservationID))]
		public Reservation? Reservation { get; set; }

		[Display(Name = "Driver")]
		public int? DriverID { get; set; }

		[ForeignKey(nameof(DriverID))]
		public Driver? Driver { get; set; }

		[Required(ErrorMessage = "Vehicle is required.")]
		[Display(Name = "Vehicle")]
		public int VehicleID { get; set; }

		[ForeignKey(nameof(VehicleID))]
		public Vehicle? Vehicle { get; set; }

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

		[NotMapped]
		[Display(Name = "Upload Pre-Trip Image")]
		[DataType(DataType.Upload)]
		public IFormFile? PreTripImageFile { get; set; }

		[StringLength(255)]
		[Display(Name = "Post-Trip Image")]
		public string? PostTripImagePath { get; set; }

		[NotMapped]
		[Display(Name = "Upload Post-Trip Image")]
		[DataType(DataType.Upload)]
		public IFormFile? PostTripImageFile { get; set; }

		[StringLength(1000, ErrorMessage = "Remarks cannot exceed 1000 characters.")]
		[DataType(DataType.MultilineText)]
		[Display(Name = "Remarks")]
		public string? Remarks { get; set; }

		[Required]
		[Display(Name = "Trip Status")]
		public TripStatus TripStatus { get; set; } = TripStatus.Scheduled;
	}
}
