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

		[Required(ErrorMessage = "Valid ID type is required.")]
		[StringLength(30, ErrorMessage = "ID type cannot exceed 30 characters.")]
		[EnumDataType(typeof(ValidIDtype), ErrorMessage = "Please select a valid ID type.")]
		[Display(Name = "Type of valid IDs")]
		public string ValidIDtype { get; set; } = string.Empty;

		[StringLength(255)]
		[Display(Name = "Front of Valid ID")]
		public string? FrontValidIDImagePath { get; set; }

		[StringLength(255)]
		[Display(Name = "Back of Valid ID")]
		public string? BackValidIDImagePath { get; set; }
	}
}
