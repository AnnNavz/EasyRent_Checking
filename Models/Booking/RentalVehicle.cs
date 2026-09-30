using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyRent_Checking.Models
{
	/// <summary>
	/// One vehicle line on a rental (order line). Shared trip dates/locations live on <see cref="Rental"/>.
	/// Rows are created when admin approves the booking, not when the customer submits the request.
	/// </summary>
	public class RentalVehicle
	{
		[Key]
		public int RentalVehicleId { get; set; }

		[Required]
		[Display(Name = "Rental")]
		public int RentalId { get; set; }

		[ForeignKey(nameof(RentalId))]
		public Rental? Rental { get; set; }

		[Required(ErrorMessage = "Vehicle is required.")]
		[Display(Name = "Vehicle")]
		public int VehicleId { get; set; }

		[ForeignKey(nameof(VehicleId))]
		public Vehicle? Vehicle { get; set; }

		[Required]
		[Column(TypeName = "decimal(18,2)")]
		[Range(0, 999999999.99)]
		[Display(Name = "Line Base Amount")]
		public decimal LineBaseAmount { get; set; }

		[Required]
		[Column(TypeName = "decimal(18,2)")]
		[Range(0, 999999999.99)]
		[Display(Name = "Line Succeeding Fee")]
		public decimal LineSucceedingFeeTotal { get; set; }

		[Required]
		[Column(TypeName = "decimal(18,2)")]
		[Range(0, 999999999.99)]
		[Display(Name = "Line Total")]
		public decimal LineTotalAmount { get; set; }

		[Display(Name = "Sort Order")]
		public int SortOrder { get; set; }
	}
}
