using Microsoft.AspNetCore.Mvc;

namespace EasyRent_Checking.Controllers
{
	public class AdminController : Controller
	{
		public IActionResult Dashboard()
		{
			return View();
		}
	}
}
