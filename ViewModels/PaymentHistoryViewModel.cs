namespace EasyRent_Checking.ViewModels
{
	public class BookingPaymentSummary
	{
		public decimal TotalFare { get; set; }
		public decimal AmountPaid { get; set; }
		public decimal RemainingBalance => Math.Max(0, TotalFare - AmountPaid);
		public bool IsSettled => TotalFare > 0 && RemainingBalance <= 0;
		public IList<PaymentHistoryRow> Payments { get; set; } = new List<PaymentHistoryRow>();
	}

	public class ClientPaymentHistoryViewModel
	{
		public decimal TotalPaid { get; set; }
		public decimal OutstandingBalance { get; set; }
		public int PaymentCount { get; set; }
		public string Sort { get; set; } = "newest";
		public string Filter { get; set; } = "all";
		public int Page { get; set; } = 1;
		public int TotalPages { get; set; } = 1;
		public IList<PaymentHistoryRow> Payments { get; set; } = new List<PaymentHistoryRow>();
	}

	public class PaymentHistoryRow
	{
		public int PaymentId { get; set; }
		public int RentalId { get; set; }
		public string BookingLabel { get; set; } = string.Empty;
		public string VehicleTitle { get; set; } = string.Empty;
		public string VehicleTypeLabel { get; set; } = string.Empty;
		public int PassengersCount { get; set; }
		public string? VehicleImagePath { get; set; }
		public DateTime PaymentDate { get; set; }
		public string PaymentMethod { get; set; } = string.Empty;
		public string PaymentType { get; set; } = string.Empty;
		public decimal AmountPaid { get; set; }
		public string? TransactionReference { get; set; }
		public string? ReceiptImagePath { get; set; }
	}
}
