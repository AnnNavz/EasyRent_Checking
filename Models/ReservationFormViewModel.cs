using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace EasyRent_Checking.Models
{
	public class ReservationFormViewModel
	{
		[ValidateNever]
		public Vehicle Vehicle { get; set; } = null!;

		public Reservation Reservation { get; set; } = new Reservation();
	}

}