using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using EasyRent_Checking.ViewModels;

namespace EasyRent_Checking.Controllers
{
	public class HomeController : Controller
	{
		[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
		public IActionResult Error()
		{
			var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
			var error = exceptionFeature?.Error;

			return View(new ErrorViewModel
			{
				RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
				ErrorMessage = error?.Message
			});
		}
	}
}
