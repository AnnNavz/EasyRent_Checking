using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Data;
using EasyRent_Checking.Models;
using Microsoft.AspNetCore.Hosting;

namespace EasyRent_Checking.Controllers
{
    public class VehiclesController : Controller
    {
        private readonly EasyRent_CheckingContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public VehiclesController(EasyRent_CheckingContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

		// GET: Vehicles
		public async Task<IActionResult> Index(string searchString, string sortBy, string currentFilter, int? page)
		{
			const int pageSize = 10;
			var pageNumber = page.GetValueOrDefault(1);
			if (pageNumber < 1)
			{
				pageNumber = 1;
			}
			// Store parameters in ViewData to maintain UI control visibility states
			ViewData["CurrentSearch"] = searchString;
			ViewData["CurrentSort"] = sortBy;
			ViewData["CurrentFilter"] = currentFilter;

			// 1. Core LINQ data reference query
			var vehiclesQuery = from v in _context.Vehicle select v;

			// 2. Real-time KPI Card Computations safely handling case matching
			ViewData["TotalVehiclesCount"] = await vehiclesQuery.CountAsync();

			// SAFE FIX: Look for your active enum regardless of whether it's named 'Active' or 'ACTIVE'
			if (Enum.TryParse("Active", true, out VehicleStatus activeEnumVal))
			{
				ViewData["ActiveVehiclesCount"] = await vehiclesQuery.CountAsync(v => v.Status == activeEnumVal);
			}
			else
			{
				ViewData["ActiveVehiclesCount"] = 0;
			}

			// 3. Handle Live Input Search Logic
			if (!string.IsNullOrEmpty(searchString))
			{
				vehiclesQuery = vehiclesQuery.Where(v => v.Model.Contains(searchString)
													  || v.Brand.Contains(searchString)
													  || v.PlateNumber.Contains(searchString));
			}

			// 4. Handle Status Filter Categorization 
			// SAFE FIX: Added 'true' parameter to make Enum.TryParse completely case-insensitive
			if (!string.IsNullOrEmpty(currentFilter) && Enum.TryParse(currentFilter, true, out VehicleStatus filterStatus))
			{
				vehiclesQuery = vehiclesQuery.Where(v => v.Status == filterStatus);
			}

			// 5. Handle Query Layer Column Sorting
			vehiclesQuery = sortBy switch
			{
				"Model" => vehiclesQuery.OrderBy(v => v.Model),
				"Brand" => vehiclesQuery.OrderBy(v => v.Brand),
				"PlateNumber" => vehiclesQuery.OrderBy(v => v.PlateNumber),
				_ => vehiclesQuery.OrderByDescending(v => v.VehicleId)
			};

			var totalCount = await vehiclesQuery.CountAsync();
			var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
			if (pageNumber > totalPages)
			{
				pageNumber = totalPages;
			}

			ViewData["PageIndex"] = pageNumber;
			ViewData["TotalPages"] = totalPages;
			ViewData["TotalCount"] = totalCount;
			ViewData["PageSize"] = pageSize;

			var vehicles = await vehiclesQuery
				.Skip((pageNumber - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();

			return View(vehicles);
		}
		// GET: Vehicles/Details/5
		public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var vehicle = await _context.Vehicle
                .FirstOrDefaultAsync(m => m.VehicleId == id);
            if (vehicle == null)
            {
                return NotFound();
            }

            return View(vehicle);
        }

        // GET: Vehicles/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Vehicles/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("VehicleId,Model,PlateNumber,Brand,Color,Type,Status,RegistrationDate,RegistrationExpiry,ImagePath,ImageFile")] Vehicle vehicle)
        {
            if (ModelState.IsValid)
            {
                if (vehicle.ImageFile != null)
                {
                    string folder = Path.Combine(_webHostEnvironment.WebRootPath, "images");
                    if (!Directory.Exists(folder))
                    {
                        Directory.CreateDirectory(folder);
                    }

                    string fileName = Guid.NewGuid().ToString() + "_" + vehicle.ImageFile.FileName;

                    // Full save path
                    string filePath = Path.Combine(folder, fileName);

                    // Save image
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await vehicle.ImageFile.CopyToAsync(stream);
                    }

                    // Save filename to database
                    vehicle.ImagePath = fileName;
                }

                _context.Add(vehicle);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(CreateSuccess), new { id = vehicle.VehicleId });
            }
            return View(vehicle);
        }

        // GET: Vehicles/CreateSuccess/5
        public async Task<IActionResult> CreateSuccess(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var vehicle = await _context.Vehicle.FindAsync(id);
            if (vehicle == null)
            {
                return NotFound();
            }

            var vehicleLabel = $"{vehicle.Brand} {vehicle.Model}".Trim();
            var model = new CreateSuccessViewModel
            {
                PageTitle = "Add New Vehicle",
                ActivePage = "Vehicles",
                Heading = "New Registered Vehicle",
                MessageHtml = $"<strong>{vehicleLabel}</strong> has been successfully registered and added to the fleet inventory.",
                PrimaryActionText = "View Vehicles",
                PrimaryActionUrl = Url.Action(nameof(Index)) ?? "",
                SecondaryActionText = "Add Another Vehicle",
                SecondaryActionUrl = Url.Action(nameof(Create)) ?? ""
            };

            return View("CreateSuccess", model);
        }

        // GET: Vehicles/Edit/5
        public async Task<IActionResult> Edit(int? id, string? returnUrl)
        {
            if (id == null)
            {
                return NotFound();
            }

            var vehicle = await _context.Vehicle.FindAsync(id);
            if (vehicle == null)
            {
                return NotFound();
            }

            if (Url.IsLocalUrl(returnUrl))
            {
                ViewData["ReturnUrl"] = returnUrl;
            }

            return View(vehicle);
        }

        // POST: Vehicles/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("VehicleId,Model,PlateNumber,Brand,Color,Type,Status,RegistrationDate,RegistrationExpiry,ImagePath,ImageFile")] Vehicle vehicle, string? returnUrl)
        {
            if (id != vehicle.VehicleId)
            {
                return NotFound();
            }

            var existingVehicle = await _context.Vehicle.FindAsync(vehicle.VehicleId);
            if (existingVehicle == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    existingVehicle.Model = vehicle.Model;
                    existingVehicle.PlateNumber = vehicle.PlateNumber;
                    existingVehicle.Brand = vehicle.Brand;
                    existingVehicle.Color = vehicle.Color;
                    existingVehicle.Type = vehicle.Type;
                    existingVehicle.Status = vehicle.Status;
                    existingVehicle.RegistrationDate = vehicle.RegistrationDate;
                    existingVehicle.RegistrationExpiry = vehicle.RegistrationExpiry;

                    string folder = Path.Combine(_webHostEnvironment.WebRootPath, "images");
                    if (!Directory.Exists(folder))
                    {
                        Directory.CreateDirectory(folder);
                    }

                    if (vehicle.ImageFile != null)
                    {
                        string fileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(vehicle.ImageFile.FileName);
                        string filePath = Path.Combine(folder, fileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await vehicle.ImageFile.CopyToAsync(stream);
                        }

                        existingVehicle.ImagePath = fileName;
                    }
                    else if (string.IsNullOrEmpty(vehicle.ImagePath))
                    {
                        existingVehicle.ImagePath = null;
                    }

                    _context.Update(existingVehicle);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!VehicleExists(vehicle.VehicleId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Details), new { id = existingVehicle.VehicleId });
            }

            if (Url.IsLocalUrl(returnUrl))
            {
                ViewData["ReturnUrl"] = returnUrl;
            }

            return View(vehicle);
        }

        // GET: Vehicles/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var vehicle = await _context.Vehicle
                .FirstOrDefaultAsync(m => m.VehicleId == id);
            if (vehicle == null)
            {
                return NotFound();
            }

            return View(vehicle);
        }

        // POST: Vehicles/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var vehicle = await _context.Vehicle.FindAsync(id);
            if (vehicle != null)
            {
                _context.Vehicle.Remove(vehicle);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool VehicleExists(int id)
        {
            return _context.Vehicle.Any(e => e.VehicleId == id);
        }

		// GET: Vehicles/ClientView
		public async Task<IActionResult> Homepage()
		{
			var allVehicles = await _context.Vehicle.ToListAsync();

			return View("~/Views/ClientSide/Homepage.cshtml", allVehicles);
		}
	}
}
