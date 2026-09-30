using System.ComponentModel.DataAnnotations;

namespace EasyRent_Checking.ViewModels
{
	public class TransitFeedbackInputModel : IValidatableObject
	{
		public int TransitId { get; set; }

		public bool HasDriver { get; set; }

		[Required(ErrorMessage = "Please rate vehicle comfort.")]
		[Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars.")]
		[Display(Name = "Comfort")]
		public int VehicleComfort { get; set; }

		[Required(ErrorMessage = "Please rate vehicle performance.")]
		[Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars.")]
		[Display(Name = "Performance")]
		public int VehiclePerformance { get; set; }

		[Required(ErrorMessage = "Please rate vehicle safety.")]
		[Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars.")]
		[Display(Name = "Safety")]
		public int VehicleSafety { get; set; }

		[Display(Name = "Professionalism")]
		public int DriverProfessionalism { get; set; }

		[Display(Name = "Driving")]
		public int DriverDriving { get; set; }

		[Display(Name = "Courtesy")]
		public int DriverCourtesy { get; set; }

		[StringLength(1000, ErrorMessage = "Comment cannot exceed 1000 characters.")]
		[DataType(DataType.MultilineText)]
		[Display(Name = "Comment")]
		public string? Comment { get; set; }

		public string VehicleTitle { get; set; } = string.Empty;
		public string? VehicleTypeLabel { get; set; }
		public string? VehiclePlateNumber { get; set; }
		public string? VehicleImagePath { get; set; }
		public string? DriverName { get; set; }

		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
		{
			if (!HasDriver)
			{
				yield break;
			}

			if (DriverProfessionalism is < 1 or > 5)
			{
				yield return new ValidationResult(
					"Please rate the driver's professionalism.",
					new[] { nameof(DriverProfessionalism) });
			}

			if (DriverDriving is < 1 or > 5)
			{
				yield return new ValidationResult(
					"Please rate the driver's driving.",
					new[] { nameof(DriverDriving) });
			}

			if (DriverCourtesy is < 1 or > 5)
			{
				yield return new ValidationResult(
					"Please rate the driver's courtesy.",
					new[] { nameof(DriverCourtesy) });
			}
		}
	}
}
