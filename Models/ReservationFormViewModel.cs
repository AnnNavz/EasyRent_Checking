using System.ComponentModel.DataAnnotations;

namespace EasyRent_Checking.Models
{
	public class ReservationFormViewModel
	{
		public Vehicle Vehicle { get; set; } = null!;

		public Reservation Reservation { get; set; } = new Reservation();
	}

}