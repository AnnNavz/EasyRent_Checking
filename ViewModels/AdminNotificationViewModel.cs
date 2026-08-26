namespace EasyRent_Checking.ViewModels
{
	public class AdminNotificationFeed
	{
		public IList<AdminNotificationItem> Critical { get; set; } = new List<AdminNotificationItem>();
		public IList<AdminNotificationItem> Items { get; set; } = new List<AdminNotificationItem>();
	}

	public class AdminNotificationItem
	{
		public string Id { get; set; } = "";
		public string Category { get; set; } = "system";
		public string Tone { get; set; } = "system";
		public string Icon { get; set; } = "bi-bell";
		public string Title { get; set; } = "";
		public string Detail { get; set; } = "";
		public string TimeAgo { get; set; } = "";
		public string Url { get; set; } = "";
	}
}
