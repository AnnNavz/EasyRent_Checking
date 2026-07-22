using Microsoft.AspNetCore.Mvc;

namespace EasyRent_Checking.Controllers
{
	public class AccountController : Controller
	{
		
		public IActionResult Registration()
		{
			return View();
		}
	}
}
