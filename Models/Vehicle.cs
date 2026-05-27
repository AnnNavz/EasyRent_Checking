using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyRent_Checking.Models
{
	public enum VehicleType
	{
		Sedan,
		SUV,
		Van
	}

	public enum VehicleStatus
	{
		Available,
		Rented,
		[Display(Name = "In Maintenance")]
		InMaintenance,
		Unavailable
	}
	public class Vehicle
	{
		[Key]
		public int VehicleId { get; set; }

		[Required, StringLength(100)]
		public string Model { get; set; }

		[Required, StringLength(20)]
		[Display(Name = "Plate Number")]
		public string PlateNumber { get; set; }

		[Required, StringLength(50)]
		public string Brand { get; set; }

		[Required, StringLength(30)]
		public string Color { get; set; }

		[Required]
		public VehicleType Type { get; set; }

		[Required]
		public VehicleStatus Status { get; set; }

		[Required(ErrorMessage = "Registration date is required.")]
		[DataType(DataType.Date)]
		[Display(Name = "Registration Date")]
		public DateTime RegistrationDate { get; set; }

		[Required(ErrorMessage = "Registration expiry date is required.")]
		[DataType(DataType.Date)]
		[Display(Name = "Registration Expiry")]
		public DateTime RegistrationExpiry { get; set; }

		[StringLength(255)]
		[Display(Name = "Vehicle Picture")]
		public string? ImagePath { get; set; }

		[NotMapped]
		[Display(Name = "Upload Vehicle Picture")]
		public IFormFile? ImageFile { get; set; }
	}
}
