using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyRent_Checking.Models
{
	public class Driver
	{
		[Key]
		public int DriverId { get; set; }

		[Required(ErrorMessage = "Driver name is required.")]
		[StringLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
		[Display(Name = "Full Name")]
		public string Name { get; set; } = string.Empty;

		[Required(ErrorMessage = "Address is required.")]
		[StringLength(255, ErrorMessage = "Address cannot exceed 255 characters.")]
		public string Address { get; set; } = string.Empty;

		[Required(ErrorMessage = "Contact number is required.")]
		[Phone(ErrorMessage = "Invalid phone number format.")]
		[StringLength(11, ErrorMessage = "Contact number cannot exceed 11 characters.")]
		[Display(Name = "Contact Number")]
		public string ContactNo { get; set; } = string.Empty;

		[Required(ErrorMessage = "License number is required.")]
		[StringLength(30, ErrorMessage = "License number cannot exceed 11 characters.")]
		[Display(Name = "License Number")]
		public string LicenseNo { get; set; } = string.Empty;

		[Required(ErrorMessage = "License expiry date is required.")]
		[DataType(DataType.Date)]
		[Display(Name = "License Expiry Date")]
		public DateOnly ExpiryDate { get; set; }

		[StringLength(255)]
		[Display(Name = "Driver Picture")]
		public string? ImagePath { get; set; }

		[NotMapped]
		[Display(Name = "Upload Driver Picture")]
		public IFormFile? ImageFile { get; set; }

		[StringLength(255)]
		[Display(Name = "Front License Picture")]
		public string? FrontLicenseImagePath { get; set; }

		[NotMapped]
		[Display(Name = "Upload Front License Picture")]
		public IFormFile? FrontLicenseImageFile { get; set; }

		[StringLength(255)]
		[Display(Name = "Back License Picture")]
		public string? BackLicenseImagePath { get; set; }

		[NotMapped]
		[Display(Name = "Upload Back License Picture")]
		public IFormFile? BackLicenseImageFile { get; set; }
	}
}
