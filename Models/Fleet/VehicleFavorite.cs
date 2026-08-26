using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyRent_Checking.Models
{
	public class VehicleFavorite
	{
		[Key]
		public int FavoriteId { get; set; }

		[Required]
		[Display(Name = "Customer")]
		public int CustomerId { get; set; }

		[ForeignKey(nameof(CustomerId))]
		public CustomerProfile? Customer { get; set; }

		[Required]
		[Display(Name = "Vehicle")]
		public int VehicleId { get; set; }

		[ForeignKey(nameof(VehicleId))]
		public Vehicle? Vehicle { get; set; }

		[Display(Name = "Saved At")]
		public DateTime CreatedAt { get; set; } = DateTime.Now;
	}
}
