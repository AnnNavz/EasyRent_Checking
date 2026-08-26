using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EasyRent_Checking.Services;

namespace EasyRent_Checking.Controllers
{
	[Authorize(Policy = "StaffArea")]
	[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
	public class NotificationsController : Controller
	{
		private readonly AdminNotificationService _notifications;

		public NotificationsController(AdminNotificationService notifications)
		{
			_notifications = notifications;
		}

		[HttpGet]
		public async Task<IActionResult> Feed()
		{
			var feed = await _notifications.GetFeedAsync();
			return Json(feed);
		}
	}
}
