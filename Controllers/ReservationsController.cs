using EasyRent_Checking.Data;
using EasyRent_Checking.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyRent_Checking.Controllers
{
	public class ReservationsController : Controller
	{
		private readonly EasyRent_CheckingContext _context;
		private readonly IWebHostEnvironment _webHostEnvironment;

		public ReservationsController(EasyRent_CheckingContext context, IWebHostEnvironment webHostEnvironment)
		{
			_context = context;
			_webHostEnvironment = webHostEnvironment;
		}

		// GET: Reservations/Create?vehicleId=5
		public async Task<IActionResult> Create(int? vehicleId)
		{
			if (vehicleId == null)
			{
				return NotFound();
			}

			var vehicle = await _context.Vehicle.FirstOrDefaultAsync(v => v.VehicleId == vehicleId);
			if (vehicle == null)
			{
				return NotFound();
			}

			ViewData["ActiveNav"] = "Vehicles";

			var model = new ReservationFormViewModel
			{
				Vehicle = vehicle,
				Reservation = new Reservation
				{
					VehicleId = vehicle.VehicleId,
					PickupDate = DateOnly.FromDateTime(DateTime.Today),
					ReturnDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
					PickupTime = new TimeOnly(8, 0),
					ReturnTime = new TimeOnly(18, 0)
				}
			};

			return View("~/Views/ClientSide/Reservation.cshtml", model);
		}

		// POST: Reservations/Create
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(ReservationFormViewModel model)
		{
			var vehicle = await _context.Vehicle.FirstOrDefaultAsync(v => v.VehicleId == model.Reservation.VehicleId);
			if (vehicle == null)
			{
				return NotFound();
			}

			model.Vehicle = vehicle;
			ViewData["ActiveNav"] = "Vehicles";

			if (model.Reservation.ReturnDate < model.Reservation.PickupDate)
			{
				ModelState.AddModelError("Reservation.ReturnDate", "Return date cannot be earlier than pickup date.");
			}

			if (model.Reservation.Discount == 1 && model.Reservation.ImageFile == null && string.IsNullOrEmpty(model.Reservation.ImagePath))
			{
				ModelState.AddModelError("Reservation.ImageFile", "Please upload a valid ID picture for the Senior / PWD discount.");
			}

			if (!ModelState.IsValid)
			{
				return View("~/Views/ClientSide/Reservation.cshtml", model);
			}

			if (model.Reservation.ImageFile != null)
			{
				string folder = Path.Combine(_webHostEnvironment.WebRootPath, "images");
				if (!Directory.Exists(folder))
				{
					Directory.CreateDirectory(folder);
				}

				string fileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(model.Reservation.ImageFile.FileName);
				string filePath = Path.Combine(folder, fileName);

				using (var stream = new FileStream(filePath, FileMode.Create))
				{
					await model.Reservation.ImageFile.CopyToAsync(stream);
				}

				model.Reservation.ImagePath = fileName;
			}

			_context.Add(model.Reservation);
			await _context.SaveChangesAsync();

			TempData["ReservationSuccess"] = true;
			return RedirectToAction(nameof(Create), new { vehicleId = vehicle.VehicleId });
		}
	}
}
