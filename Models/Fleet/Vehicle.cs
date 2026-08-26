using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyRent_Checking.Models
{
	public class Vehicle : IValidatableObject
	{
		[Key]
		public int VehicleId { get; set; }

		[Required(ErrorMessage = "Model is required.")]
		[StringLength(100, ErrorMessage = "Model cannot exceed 100 characters.")]
		[Display(Name = "Model")]
		public string Model { get; set; } = string.Empty;

		[Required(ErrorMessage = "Plate number is required.")]
		[StringLength(20, ErrorMessage = "Plate number cannot exceed 20 characters.")]
		[RegularExpression(FieldRules.Plate, ErrorMessage = FieldRules.PlateMessage)]
		[Display(Name = "Plate Number")]
		public string PlateNumber { get; set; } = string.Empty;

		[Required(ErrorMessage = "Brand is required.")]
		[StringLength(50, ErrorMessage = "Brand cannot exceed 50 characters.")]
		[Display(Name = "Brand")]
		public string Brand { get; set; } = string.Empty;

		[Required(ErrorMessage = "Color is required.")]
		[StringLength(30, ErrorMessage = "Color cannot exceed 30 characters.")]
		[Display(Name = "Color")]
		public string Color { get; set; } = string.Empty;

		[Required(ErrorMessage = "Please choose or add a vehicle type.")]
		[StringLength(30, ErrorMessage = "Type cannot exceed 30 characters.")]
		[Display(Name = "Type")]
		public string Type { get; set; } = VehicleTypes.Suv;

		[NotMapped]
		[StringLength(30, ErrorMessage = "Custom type cannot exceed 30 characters.")]
		[Display(Name = "Custom Type")]
		public string? CustomType { get; set; }

		[Required(ErrorMessage = "Status is required.")]
		[Display(Name = "Status")]
		public VehicleStatus Status { get; set; }

		[Display(Name = "Odometer (km)")]
		[Range(0, 9999999, ErrorMessage = "Odometer must be between 0 and 9,999,999 km.")]
		public int Odometer { get; set; }

		[Required(ErrorMessage = "Registration date is required.")]
		[DataType(DataType.Date)]
		[Display(Name = "Registration Date")]
		public DateTime RegistrationDate { get; set; }

		[Required(ErrorMessage = "Registration expiry date is required.")]
		[DataType(DataType.Date)]
		[Display(Name = "Registration Expiry")]
		public DateTime RegistrationExpiry { get; set; }

		[Required(ErrorMessage = "Base price is required.")]
		[Display(Name = "Base Price")]
		[Column(TypeName = "decimal(18,2)")]
		[Range(0.01, 999999.99, ErrorMessage = "Base price must be greater than zero.")]
		public decimal BasePrice { get; set; }

		[Required(ErrorMessage = "Succeeding fee is required.")]
		[Display(Name = "Succeeding Fee")]
		[Column(TypeName = "decimal(18,2)")]
		[Range(0.01, 999999.99, ErrorMessage = "Succeeding fee must be greater than zero.")]
		public decimal SucceedingFee { get; set; }

		[Required(ErrorMessage = "Passengers count is required.")]
		[Display(Name = "Passengers Count")]
		[Range(1, 50, ErrorMessage = "Passengers count must be between 1 and 50.")]
		public int PassengersCount { get; set; }

		[StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
		[DataType(DataType.MultilineText)]
		[Display(Name = "Description")]
		public string? Description { get; set; }

		[StringLength(255)]
		[Display(Name = "Vehicle Picture")]
		public string? ImagePath { get; set; }

		[NotMapped]
		[Display(Name = "Upload Vehicle Picture")]
		[DataType(DataType.Upload)]
		public IFormFile? ImageFile { get; set; }

		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
		{
			if (RegistrationExpiry <= RegistrationDate)
			{
				yield return new ValidationResult(
					"Registration expiry must be after the registration date.",
					new[] { nameof(RegistrationExpiry) });
			}
		}
	}
}
