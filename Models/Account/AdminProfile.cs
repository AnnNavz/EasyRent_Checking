using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyRent_Checking.Models
{
	public class AdminProfile
	{
		[Key]
		[Display(Name = "Admin")]
		public int AdminId { get; set; }

		[ForeignKey(nameof(AdminId))]
		public User? User { get; set; }

		[Required(ErrorMessage = "Full name is required.")]
		[StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters.")]
		[Display(Name = "Full Name")]
		public string FullName { get; set; } = string.Empty;
	}
}
