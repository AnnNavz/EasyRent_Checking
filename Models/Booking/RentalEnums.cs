namespace EasyRent_Checking.Models
{
	public enum Discount
	{
		Yes,
		No
	}

	public enum RentalStatus
	{
		Pending,
		Approved,
		Cancelled,
		Expired,
		Refunded,
		RefundRejected
	}

	public enum RentalOption
	{
		Book,
		Reserve
	}
}
