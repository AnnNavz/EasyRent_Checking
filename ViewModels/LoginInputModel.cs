using System.ComponentModel.DataAnnotations;

namespace EasyRent_Checking.ViewModels
{
	public class LoginInputModel
	{
		[Display(Name = "Email Address")]
		public string Email { get; set; } = string.Empty;

		[DataType(DataType.Password)]
		[Display(Name = "Password")]
		public string Password { get; set; } = string.Empty;
	}
}
