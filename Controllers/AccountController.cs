using Microsoft.AspNetCore.Mvc;
using EasyRent_Checking.Models;

namespace EasyRent_Checking.Controllers
{
	public class AccountController : Controller
	{
		[HttpGet]
		public IActionResult Registration()
		{
			return View(new Customer());
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public IActionResult Registration(Customer model)
		{
			if (string.IsNullOrWhiteSpace(model.ValidIDtype))
			{
				ModelState.AddModelError(nameof(model.ValidIDtype), "Please choose a valid ID type.");
			}
			else if (model.ValidIDImageFile == null)
			{
				ModelState.AddModelError(nameof(model.ValidIDImageFile), "Please upload a picture of your valid ID.");
			}

			// Auth persistence will be wired later; keep the designed form responsive for now.
			if (!ModelState.IsValid)
			{
				return View(model);
			}

			return RedirectToAction(nameof(Login));
		}

		[HttpGet]
		public IActionResult Login()
		{
			return View();
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public IActionResult Login(string email, string password, bool rememberMe)
		{
			_ = email;
			_ = password;
			_ = rememberMe;
			return View();
		}
	}
}
