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
    public class DriversController : Controller
    {
        private readonly EasyRent_CheckingContext _context;
		private readonly IWebHostEnvironment _webHostEnvironment;
		public DriversController(EasyRent_CheckingContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
			_webHostEnvironment = webHostEnvironment;
		}

		// GET: Drivers
		public async Task<IActionResult> Index(string searchString, string sortBy, string currentFilter, int? page)
		{
			const int pageSize = 10;
			var pageNumber = page.GetValueOrDefault(1);
			if (pageNumber < 1)
			{
				pageNumber = 1;
			}
			// Keep parameters saved in ViewData so the active markup view can retain state tracking
			ViewData["CurrentSearch"] = searchString;
			ViewData["CurrentSort"] = sortBy;
			ViewData["CurrentFilter"] = currentFilter;

			// 1. Base query to work with
			var driversQuery = from d in _context.Driver select d;

			// 2. Real-time KPI Metric Card Calculation
			var systemDate = DateOnly.FromDateTime(DateTime.Now);
			ViewData["TotalDriversCount"] = await driversQuery.CountAsync();
			ViewData["ActiveDriversCount"] = await driversQuery.CountAsync(d => d.ExpiryDate >= systemDate);

			// 3. Search Bar Functional Handler
			if (!string.IsNullOrEmpty(searchString))
			{
				driversQuery = driversQuery.Where(d => d.Name.Contains(searchString)
													|| d.LicenseNo.Contains(searchString)
													|| d.Address.Contains(searchString));
			}

			// 4. Dropdown Filter Flags Handler
			if (!string.IsNullOrEmpty(currentFilter))
			{
				if (currentFilter == "ActiveOnly")
				{
					driversQuery = driversQuery.Where(d => d.ExpiryDate >= systemDate);
				}
				else if (currentFilter == "Expired")
				{
					driversQuery = driversQuery.Where(d => d.ExpiryDate < systemDate);
				}
			}

			// 5. "Sorting by" Rules Engine
			driversQuery = sortBy switch
			{
				"Name" => driversQuery.OrderBy(d => d.Name),
				"ExpiryDate" => driversQuery.OrderBy(d => d.ExpiryDate),
				_ => driversQuery.OrderByDescending(d => d.DriverId) // Default sorting by newest
			};

			var totalCount = await driversQuery.CountAsync();
			var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
			if (pageNumber > totalPages)
			{
				pageNumber = totalPages;
			}

			ViewData["PageIndex"] = pageNumber;
			ViewData["TotalPages"] = totalPages;
			ViewData["TotalCount"] = totalCount;
			ViewData["PageSize"] = pageSize;

			var drivers = await driversQuery
				.Skip((pageNumber - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();

			return View(drivers);
		}

		// GET: Drivers/Details/5
		public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var driver = await _context.Driver
                .FirstOrDefaultAsync(m => m.DriverId == id);
            if (driver == null)
            {
                return NotFound();
            }

            return View(driver);
        }

        // GET: Drivers/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Drivers/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("DriverId,Name,Address,ContactNo,LicenseNo,ExpiryDate,ImagePath,ImageFile")] Driver driver)
        {
            if (ModelState.IsValid)
            {
				if (driver.ImageFile != null)
				{
					// Folder path
					string folder = Path.Combine(_webHostEnvironment.WebRootPath, "images");

					// Create unique filename
					string fileName = Guid.NewGuid().ToString() + "_" + driver.ImageFile.FileName;

					// Full save path
					string filePath = Path.Combine(folder, fileName);

					// Save image
					using (var stream = new FileStream(filePath, FileMode.Create))
					{
						await driver.ImageFile.CopyToAsync(stream);
					}

					// Save filename to database
					driver.ImagePath = fileName;
				}

				_context.Add(driver);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(CreateSuccess), new { id = driver.DriverId });
            }
            return View(driver);
        }

        // GET: Drivers/CreateSuccess/5
        public async Task<IActionResult> CreateSuccess(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var driver = await _context.Driver.FindAsync(id);
            if (driver == null)
            {
                return NotFound();
            }

            var model = new CreateSuccessViewModel
            {
                PageTitle = "Add New Driver",
                ActivePage = "Drivers",
                Heading = "Driver Profile Created",
                MessageHtml = $"<strong>{driver.Name}</strong> has been successfully added to the system and is ready for assignment.",
                PrimaryActionText = "View Driver's Profile",
                PrimaryActionUrl = Url.Action(nameof(Details), new { id = driver.DriverId }) ?? "",
                SecondaryActionText = "Add Another Driver",
                SecondaryActionUrl = Url.Action(nameof(Create)) ?? ""
            };

            return View("CreateSuccess", model);
        }

        // GET: Drivers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var driver = await _context.Driver.FindAsync(id);
            if (driver == null)
            {
                return NotFound();
            }
            return View(driver);
        }

        // POST: Drivers/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("DriverId,Name,Address,ContactNo,LicenseNo,ExpiryDate,ImagePath,ImageFile")] Driver driver)
        {
			if (id != driver.DriverId)
			{
				return NotFound();
			}

			if (ModelState.IsValid)
			{
				try
				{
					if (driver.ImageFile != null)
					{
						// Folder path
						string folder = Path.Combine(_webHostEnvironment.WebRootPath, "images");

						// 2. FIXED: Safely ensure the directory exists before trying to save files into it
						if (!Directory.Exists(folder))
						{
							Directory.CreateDirectory(folder);
						}

						// Create unique filename
						string fileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(driver.ImageFile.FileName);

						// Full save path
						string filePath = Path.Combine(folder, fileName);

						// Save image
						using (var stream = new FileStream(filePath, FileMode.Create))
						{
							await driver.ImageFile.CopyToAsync(stream);
						}

						// Save filename to database
						driver.ImagePath = fileName;
					}

					_context.Update(driver);
					await _context.SaveChangesAsync();
				}
				catch (DbUpdateConcurrencyException)
				{
					if (!DriverExists(driver.DriverId))
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
			return View(driver);
		}

        // GET: Drivers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var driver = await _context.Driver
                .FirstOrDefaultAsync(m => m.DriverId == id);
            if (driver == null)
            {
                return NotFound();
            }

            return View(driver);
        }

        // POST: Drivers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var driver = await _context.Driver.FindAsync(id);
            if (driver != null)
            {
                _context.Driver.Remove(driver);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool DriverExists(int id)
        {
            return _context.Driver.Any(e => e.DriverId == id);
        }
    }
}
