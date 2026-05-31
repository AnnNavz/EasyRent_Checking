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
		public async Task<IActionResult> Index(string searchString, string sortBy, string currentFilter)
		{
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

			return View(await vehiclesQuery.ToListAsync());
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
                    // Folder path
                    string folder = Path.Combine(_webHostEnvironment.WebRootPath, "images");

                    // Create unique filename
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
                return RedirectToAction(nameof(Index));
            }
            return View(vehicle);
        }

        // GET: Vehicles/Edit/5
        public async Task<IActionResult> Edit(int? id)
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
            return View(vehicle);
        }

        // POST: Vehicles/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("VehicleId,Model,PlateNumber,Brand,Color,Type,Status,RegistrationDate,RegistrationExpiry,ImagePath,ImageFile")] Vehicle vehicle)
        {
            if (id != vehicle.VehicleId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                //try
                //{
                //    _context.Update(vehicle);
                //    await _context.SaveChangesAsync();
                //}
                //catch (DbUpdateConcurrencyException)
                //{
                //    if (!VehicleExists(vehicle.VehicleId))
                //    {
                //        return NotFound();
                //    }
                //    else
                //    {
                //        throw;
                //    }
                //}
                //return RedirectToAction(nameof(Index));

                try
                {
                    if (vehicle.ImageFile != null)
                    {
                        // Folder path
                        string folder = Path.Combine(_webHostEnvironment.WebRootPath, "images");

                        // 2. FIXED: Safely ensure the directory exists before trying to save files into it
                        if (!Directory.Exists(folder))
                        {
                            Directory.CreateDirectory(folder);
                        }

                        // Create unique filename
                        string fileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(vehicle.ImageFile.FileName);

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

                    _context.Update(vehicle);
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
                return RedirectToAction(nameof(Index));
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
