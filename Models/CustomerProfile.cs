using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyRent_Checking.Models
{
	public enum ValidIDtype
	{
		NationalID,
		Passport,
		UMID,
		DriverLicense,
		VotersID
	}

	public enum Status
	{
		Active,
		Inactive,
		Pending
	}

	public class CustomerProfile
	{
		[Key]
		[Display(Name = "Customer")]
		public int CustomerId { get; set; }

		[ForeignKey(nameof(CustomerId))]
		public User? User { get; set; }

		[Required(ErrorMessage = "Full name is required.")]
		[StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters.")]
		[Display(Name = "Full Name")]
		public string FullName { get; set; } = string.Empty;

		[Required(ErrorMessage = "Contact number is required.")]
		[DataType(DataType.PhoneNumber)]
		[RegularExpression(@"^(09|\+639)\d{9}$", ErrorMessage = "Please enter a valid Philippine mobile number (e.g., 09123456789 or +639123456789).")]
		[Display(Name = "Contact Number")]
		public string ContactNumber { get; set; } = string.Empty;

		[Required(ErrorMessage = "Valid ID is required.")]
		[Display(Name = "Choose what type of valid IDs")]
		public string ValidIDtype { get; set; } = string.Empty;

		[StringLength(255)]
		[Display(Name = "Valid ID Picture")]
		public string? ValidIDImagePath { get; set; }

		[NotMapped]
		[Display(Name = "Upload Valid ID Picture")]
		public IFormFile? ValidIDImageFile { get; set; }

		[Required(ErrorMessage = "Status is required.")]
		[Display(Name = "Status")]
		public Status Status { get; set; } = Status.Pending;
	}
}
