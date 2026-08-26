using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyRent_Checking.Models
{
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
		[StringLength(20, ErrorMessage = "Contact number cannot exceed 20 characters.")]
		[RegularExpression(FieldRules.PhMobile, ErrorMessage = FieldRules.PhMobileMessage)]
		[Display(Name = "Contact Number")]
		public string ContactNumber { get; set; } = string.Empty;

		[Required(ErrorMessage = "Valid ID type is required.")]
		[StringLength(30, ErrorMessage = "ID type cannot exceed 30 characters.")]
		[EnumDataType(typeof(ValidIDtype), ErrorMessage = "Please select a valid ID type.")]
		[Display(Name = "Choose what type of valid IDs")]
		public string ValidIDtype { get; set; } = string.Empty;

		[StringLength(255)]
		[Display(Name = "Front of Valid ID")]
		public string? FrontValidIDImagePath { get; set; }

		[StringLength(255)]
		[Display(Name = "Back of Valid ID")]
		public string? BackValidIDImagePath { get; set; }

		[Required(ErrorMessage = "Status is required.")]
		[Display(Name = "Status")]
		public Status Status { get; set; } = Status.Pending;

		[Display(Name = "Self Deactivated")]
		public bool IsSelfDeactivated { get; set; }
	}
}
