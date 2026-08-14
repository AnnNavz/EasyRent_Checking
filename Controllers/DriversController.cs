using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Data;
using EasyRent_Checking.Models;
using EasyRent_Checking.ViewModels;
using EasyRent_Checking.Services;
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
			var driversQuery = from d in _context.Drivers select d;

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

            var driver = await _context.Drivers
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
        public async Task<IActionResult> Create([Bind("DriverId,Name,Address,ContactNo,LicenseNo,ExpiryDate,ImagePath,ImageFile,FrontLicenseImagePath,FrontLicenseImageFile,BackLicenseImagePath,BackLicenseImageFile")] Driver driver)
        {
            if (ModelState.IsValid)
            {
				if (driver.ImageFile != null)
				{
					driver.ImagePath = await ImageStorage.SaveAsync(_webHostEnvironment, driver.ImageFile, ImageStorage.DriversFolder);
				}

				if (driver.FrontLicenseImageFile != null)
				{
					driver.FrontLicenseImagePath = await ImageStorage.SaveAsync(_webHostEnvironment, driver.FrontLicenseImageFile, ImageStorage.DriversFolder);
				}

				if (driver.BackLicenseImageFile != null)
				{
					driver.BackLicenseImagePath = await ImageStorage.SaveAsync(_webHostEnvironment, driver.BackLicenseImageFile, ImageStorage.DriversFolder);
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

            var driver = await _context.Drivers.FindAsync(id);
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
                PrimaryActionText = "View Drivers",
                PrimaryActionUrl = Url.Action(nameof(Index)) ?? "",
                SecondaryActionText = "Add Another Driver",
                SecondaryActionUrl = Url.Action(nameof(Create)) ?? ""
            };

            return View("CreateSuccess", model);
        }

        // GET: Drivers/Edit/5
        public async Task<IActionResult> Edit(int? id, string? returnUrl)
        {
            if (id == null)
            {
                return NotFound();
            }

            var driver = await _context.Drivers.FindAsync(id);
            if (driver == null)
            {
                return NotFound();
            }

            if (Url.IsLocalUrl(returnUrl))
            {
                ViewData["ReturnUrl"] = returnUrl;
            }

            return View(driver);
        }

        // POST: Drivers/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("DriverId,Name,Address,ContactNo,LicenseNo,ExpiryDate,ImagePath,ImageFile,FrontLicenseImagePath,FrontLicenseImageFile,BackLicenseImagePath,BackLicenseImageFile")] Driver driver, string? returnUrl)
        {
			if (id != driver.DriverId)
			{
				return NotFound();
			}

			var existingDriver = await _context.Drivers.FindAsync(driver.DriverId);
			if (existingDriver == null)
			{
				return NotFound();
			}

			if (ModelState.IsValid)
			{
				try
				{
					existingDriver.Name = driver.Name;
					existingDriver.Address = driver.Address;
					existingDriver.ContactNo = driver.ContactNo;
					existingDriver.LicenseNo = driver.LicenseNo;
					existingDriver.ExpiryDate = driver.ExpiryDate;

					if (driver.ImageFile != null)
					{
						existingDriver.ImagePath = await ImageStorage.SaveAsync(_webHostEnvironment, driver.ImageFile, ImageStorage.DriversFolder);
					}
					else if (string.IsNullOrEmpty(driver.ImagePath))
					{
						existingDriver.ImagePath = null;
					}

					if (driver.FrontLicenseImageFile != null)
					{
						existingDriver.FrontLicenseImagePath = await ImageStorage.SaveAsync(_webHostEnvironment, driver.FrontLicenseImageFile, ImageStorage.DriversFolder);
					}
					else if (string.IsNullOrEmpty(driver.FrontLicenseImagePath))
					{
						existingDriver.FrontLicenseImagePath = null;
					}

					if (driver.BackLicenseImageFile != null)
					{
						existingDriver.BackLicenseImagePath = await ImageStorage.SaveAsync(_webHostEnvironment, driver.BackLicenseImageFile, ImageStorage.DriversFolder);
					}
					else if (string.IsNullOrEmpty(driver.BackLicenseImagePath))
					{
						existingDriver.BackLicenseImagePath = null;
					}

					_context.Update(existingDriver);
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
				return RedirectToAction(nameof(Details), new { id = existingDriver.DriverId });
			}

			if (Url.IsLocalUrl(returnUrl))
			{
				ViewData["ReturnUrl"] = returnUrl;
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

            var driver = await _context.Drivers
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
            var driver = await _context.Drivers.FindAsync(id);
            if (driver != null)
            {
                _context.Drivers.Remove(driver);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool DriverExists(int id)
        {
            return _context.Drivers.Any(e => e.DriverId == id);
        }
    }
}
