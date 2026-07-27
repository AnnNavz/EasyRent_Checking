namespace EasyRent_Checking.Models
{
	public class ReservationPricingSummary
	{
		public double TotalHours { get; set; }

		public string DurationLabel { get; set; } = string.Empty;

		public decimal BaseFee { get; set; }

		public int SucceedingHours { get; set; }

		public decimal SucceedingHourlyRate { get; set; }

		public decimal SucceedingFee { get; set; }

		public decimal Subtotal { get; set; }

		public bool HasDiscount { get; set; }

		public decimal DiscountAmount { get; set; }

		public decimal Total { get; set; }
	}

}
