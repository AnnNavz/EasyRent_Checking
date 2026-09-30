using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Data;

namespace EasyRent_Checking.Controllers
{
	public class DiagnosticsController : Controller
	{
		private readonly EasyRent_CheckingContext _context;
		private readonly IConfiguration _configuration;
		private readonly IWebHostEnvironment _environment;

		public DiagnosticsController(
			EasyRent_CheckingContext context,
			IConfiguration configuration,
			IWebHostEnvironment environment)
		{
			_context = context;
			_configuration = configuration;
			_environment = environment;
		}

		[HttpGet]
		public async Task<IActionResult> DbCheck()
		{
			var connectionString = _configuration.GetConnectionString("EasyRent_CheckingContext");
			if (string.IsNullOrWhiteSpace(connectionString))
			{
				return Content("FAIL: Connection string 'EasyRent_CheckingContext' is missing.");
			}

			try
			{
				var canConnect = await _context.Database.CanConnectAsync();
				if (!canConnect)
				{
					return Content("FAIL: Database.CanConnectAsync returned false.");
				}

				var vehicleCount = await _context.Vehicles.CountAsync();
				var migrationCount = await _context.Database
					.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM [__EFMigrationsHistory]")
					.FirstAsync();

				return Content(
					"OK: Database connection works.\r\n" +
					$"Environment: {_environment.EnvironmentName}\r\n" +
					$"Vehicles in database: {vehicleCount}\r\n" +
					$"Applied migrations: {migrationCount}");
			}
			catch (Exception ex)
			{
				return Content("FAIL: " + ex.Message + "\r\n\r\n" + ex.StackTrace);
			}
		}

	}
}
