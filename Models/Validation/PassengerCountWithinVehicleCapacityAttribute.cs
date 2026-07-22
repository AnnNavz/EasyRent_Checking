using System.ComponentModel.DataAnnotations;
using EasyRent_Checking.Data;
using Microsoft.Extensions.DependencyInjection;

namespace EasyRent_Checking.Models.Validation
{
	[AttributeUsage(AttributeTargets.Property)]
	public class PassengerCountWithinVehicleCapacityAttribute : ValidationAttribute
	{
		protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
		{
			if (validationContext.ObjectInstance is not Reservation reservation)
			{
				return ValidationResult.Success;
			}

			if (value is not int passengerCount)
			{
				return new ValidationResult("Passenger count is required.");
			}

			if (passengerCount < 1)
			{
				return new ValidationResult("Passenger count must be at least 1.");
			}

			if (reservation.VehicleId <= 0)
			{
				return new ValidationResult("A vehicle must be selected before validating passenger count.");
			}

			var db = validationContext.GetService<EasyRent_CheckingContext>();
			if (db == null)
			{
				return ValidationResult.Success;
			}

			var vehicleCapacity = db.Vehicle
				.Where(v => v.VehicleId == reservation.VehicleId)
				.Select(v => v.PassengersCount)
				.FirstOrDefault();

			if (vehicleCapacity <= 0)
			{
				return new ValidationResult("The selected vehicle could not be found.");
			}

			if (passengerCount > vehicleCapacity)
			{
				return new ValidationResult($"Passenger count cannot exceed {vehicleCapacity} for the selected vehicle.");
			}

			return ValidationResult.Success;
		}
	}
}
