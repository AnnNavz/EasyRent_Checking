using EasyRent_Checking.Models;
using EasyRent_Checking.ViewModels;

namespace EasyRent_Checking.Services
{
	public static class PaymentAmountRules
	{
		public const decimal PartialDepositPercent = 5m;
		public const string PartialPaymentType = "Partial Payment";
		public const string FullPaymentType = "Full Payment";

		public static bool IsPartialPayment(string? paymentType)
			=> string.Equals(paymentType, PartialPaymentType, StringComparison.OrdinalIgnoreCase);

		public static decimal GetMinimumDue(string? paymentType, decimal totalAmount)
		{
			if (totalAmount <= 0)
			{
				return 0m;
			}

			return IsPartialPayment(paymentType)
				? Math.Round(totalAmount * (PartialDepositPercent / 100m), 2)
				: totalAmount;
		}

		public static decimal CalculateBookingTotal(IReadOnlyList<Vehicle> vehicles, RentalInputModel model)
		{
			if (vehicles == null || vehicles.Count == 0)
			{
				return 0m;
			}

			var rental = new Rental
			{
				PickupDate = model.PickupDate,
				ReturnDate = model.ReturnDate,
				PickupTime = model.PickupTime,
				ReturnTime = model.ReturnTime,
				Discount = model.Discount
			};

			return RentalFareCalculator.Aggregate(
				RentalFareCalculator.CalculateLines(vehicles, rental),
				model.Discount == Discount.Yes).TotalAmount;
		}

		public static string? ValidateAmountPaid(decimal amountPaid, string? paymentType, decimal totalAmount)
		{
			if (amountPaid <= 0)
			{
				return "Amount paid must be greater than zero.";
			}

			var minimum = GetMinimumDue(paymentType, totalAmount);
			if (minimum <= 0 || amountPaid >= minimum)
			{
				return null;
			}

			return IsPartialPayment(paymentType)
				? $"Amount paid must be at least the partial deposit of ₱{minimum:N2}."
				: $"Amount paid must be at least ₱{minimum:N2}.";
		}
	}
}
