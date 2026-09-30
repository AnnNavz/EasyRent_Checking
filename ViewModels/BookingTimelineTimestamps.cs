namespace EasyRent_Checking.ViewModels
{
	public class BookingTimelineTimestamps
	{
		public DateTime? SubmittedAt { get; init; }

		public DateTime? ConfirmedAt { get; init; }

		public DateTime? PreparedAt { get; init; }

		public DateTime? DispatchedAt { get; init; }

		public DateTime? CompletedAt { get; init; }
	}
}
