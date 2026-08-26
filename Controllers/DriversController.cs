using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
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
    [Authorize(Policy = "StaffArea")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class DriversController : Controller
    {
        private readonly EasyRent_CheckingContext _context;
		private readonly IWebHostEnvironment _webHostEnvironment;
		private readonly SystemLogService _logs;

		public DriversController(EasyRent_CheckingContext context, IWebHostEnvironment webHostEnvironment, SystemLogService logs)
        {
            _context = context;
			_webHostEnvironment = webHostEnvironment;
			_logs = logs;
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
			var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

			var totalDriversCount = await driversQuery.CountAsync();
			var activeDriversCount = await driversQuery.CountAsync(d => d.IsActive && d.ExpiryDate >= systemDate);
			var totalDriversAtMonthStart = await driversQuery.CountAsync(d => d.CreatedAt < monthStart);
			var activeDriversAtMonthStart = await driversQuery.CountAsync(d =>
				d.CreatedAt < monthStart
				&& d.IsActive
				&& d.ExpiryDate >= DateOnly.FromDateTime(monthStart));

			ViewData["TotalDriversCount"] = totalDriversCount;
			ViewData["ActiveDriversCount"] = activeDriversCount;
			ViewData["TotalDriversChange"] = PctChange(totalDriversCount, totalDriversAtMonthStart);
			ViewData["ActiveDriversChange"] = PctChange(activeDriversCount, activeDriversAtMonthStart);

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
					driversQuery = driversQuery.Where(d => d.IsActive && d.ExpiryDate >= systemDate);
				}
				else if (currentFilter == "Expired")
				{
					driversQuery = driversQuery.Where(d => d.ExpiryDate < systemDate);
				}
				else if (currentFilter == "Deactivated")
				{
					driversQuery = driversQuery.Where(d => !d.IsActive);
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

            var transits = await _context.Transits
                .AsNoTracking()
                .Include(t => t.Rental)
                .Where(t => t.DriverID == id)
                .OrderByDescending(t => t.TransitID)
                .Take(20)
                .ToListAsync();

            var rentalIds = transits.Select(t => t.RentalID).Distinct().ToList();
            var rentalDetailsByRentalId = await _context.RentalDetails
                .AsNoTracking()
                .Where(d => rentalIds.Contains(d.RentalID))
                .ToDictionaryAsync(d => d.RentalID);

            var activityHistory = transits
                .Select(t =>
                {
                    rentalDetailsByRentalId.TryGetValue(t.RentalID, out var details);
                    return MapTransitToActivity(t, details, t.Rental);
                })
                .ToList();

            var driverReviews = await _context.Feedbacks
                .AsNoTracking()
                .Include(f => f.Customer)
                .Where(f => f.Transit != null
                    && f.Transit.DriverID == id
                    && f.DriverProfessionalism != null
                    && f.DriverDriving != null
                    && f.DriverCourtesy != null)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            var reviewItems = driverReviews
                .Select(f => new DriverReviewItem
                {
                    CustomerName = f.Customer?.FullName ?? "Customer",
                    Comment = f.Comment,
                    CreatedAt = f.CreatedAt,
                    Professionalism = f.DriverProfessionalism!.Value,
                    Driving = f.DriverDriving!.Value,
                    Courtesy = f.DriverCourtesy!.Value
                })
                .ToList();

            var documents = BuildDriverDocuments(driver);

            var model = new DriverDetailsViewModel
            {
                Driver = driver,
                ActivityHistory = activityHistory,
                Reviews = reviewItems,
                Documents = documents,
                ReviewCount = reviewItems.Count,
                OverallTen = RatingScale.ToTen(reviewItems.Select(r => r.OverallStars)),
                ProfessionalismTen = RatingScale.ToTen(reviewItems.Select(r => r.Professionalism)),
                DrivingTen = RatingScale.ToTen(reviewItems.Select(r => r.Driving)),
                CourtesyTen = RatingScale.ToTen(reviewItems.Select(r => r.Courtesy))
            };

            return View(model);
        }

        // POST: Drivers/ToggleActive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var driver = await _context.Drivers.FindAsync(id);
            if (driver == null)
            {
                return NotFound();
            }

            driver.IsActive = !driver.IsActive;
            _logs.Record(
                driver.IsActive ? SystemLogAction.Reactivated : SystemLogAction.Deactivated,
                SystemLogCategory.Driver,
                driver.IsActive
                    ? $"Reactivated driver {driver.Name}."
                    : $"Deactivated driver {driver.Name}.",
                "Driver",
                driver.DriverId);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = driver.IsActive
                ? $"{driver.Name} has been reactivated."
                : $"{driver.Name} has been deactivated.";

            return RedirectToAction(nameof(Details), new { id });
        }

        private static DriverActivityLogItem MapTransitToActivity(Transit transit, RentalDetails? details, Rental? rental)
        {
            var bookingRef = $"#FL-{transit.RentalID}";

            var title = transit.TripStatus switch
            {
                TripStatus.Completed => $"Rent Trip Successful : {bookingRef}",
                TripStatus.InTransit => $"Trip In Progress : {bookingRef}",
                TripStatus.Cancelled => $"Trip Cancelled : {bookingRef}",
                TripStatus.Delayed => $"Trip Delayed : {bookingRef}",
                _ => $"Trip Scheduled : {bookingRef}"
            };

            string description;
            if (details != null)
            {
                var dayCount = details.ReturnDate.DayNumber - details.PickupDate.DayNumber + 1;

                description = transit.TripStatus switch
                {
                    TripStatus.Completed when dayCount > 1 =>
                        $"Successful trip to {details.PickupLocation} for the first day and {details.DropoffLocation} for the second day of the reservation.",
                    TripStatus.Completed when !string.IsNullOrWhiteSpace(rental?.Notes) =>
                        $"Successful trip to {details.DropoffLocation} for {rental.Notes.Trim().TrimEnd('.')} for one day.",
                    TripStatus.Completed =>
                        $"Successful trip to {details.DropoffLocation} for one day.",
                    _ => $"Trip from {details.PickupLocation} to {details.DropoffLocation}."
                };
            }
            else
            {
                description = transit.TripStatus == TripStatus.Completed
                    ? "Rental trip completed."
                    : "Assigned rental trip.";
            }

            return new DriverActivityLogItem
            {
                OccurredAt = ResolveActivityDate(transit, details),
                Title = title,
                Description = description,
                TripStatus = transit.TripStatus
            };
        }

        private static IReadOnlyList<DriverDocumentItem> BuildDriverDocuments(Driver driver)
        {
            var uploadedAt = driver.CreatedAt.ToLocalTime();
            var documents = new List<DriverDocumentItem>();

            if (!string.IsNullOrEmpty(driver.FrontLicenseImagePath))
            {
                documents.Add(new DriverDocumentItem
                {
                    DisplayName = "Front License",
                    FileName = GetStoredFileName(driver.FrontLicenseImagePath),
                    ImagePath = driver.FrontLicenseImagePath,
                    UploadedAt = uploadedAt,
                    IsVerified = true
                });
            }

            if (!string.IsNullOrEmpty(driver.BackLicenseImagePath))
            {
                documents.Add(new DriverDocumentItem
                {
                    DisplayName = "Back License",
                    FileName = GetStoredFileName(driver.BackLicenseImagePath),
                    ImagePath = driver.BackLicenseImagePath,
                    UploadedAt = uploadedAt,
                    IsVerified = true
                });
            }

            return documents;
        }

        private static string GetStoredFileName(string path)
        {
            var separatorIndex = path.IndexOf('_');
            return separatorIndex >= 0 && separatorIndex < path.Length - 1
                ? path[(separatorIndex + 1)..]
                : path;
        }

        private static DateTime ResolveActivityDate(Transit transit, RentalDetails? details)
        {
            if (details == null)
            {
                return DateTime.Now;
            }

            return transit.TripStatus switch
            {
                TripStatus.Completed => details.ReturnDate.ToDateTime(details.ReturnTime),
                TripStatus.InTransit => details.PickupDate.ToDateTime(details.PickupTime),
                _ => details.PickupDate.ToDateTime(details.PickupTime)
            };
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
				_logs.Record(
					SystemLogAction.Created,
					SystemLogCategory.Driver,
					$"Added driver {driver.Name}.",
					"Driver",
					driver.DriverId);
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
                PrimaryActionText = "View Driver's Profile",
                PrimaryActionUrl = Url.Action(nameof(Details), new { id = driver.DriverId }) ?? "",
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
					_logs.Record(
						SystemLogAction.Updated,
						SystemLogCategory.Driver,
						$"Updated driver {existingDriver.Name}.",
						"Driver",
						existingDriver.DriverId);
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
				_logs.Record(
					SystemLogAction.Deleted,
					SystemLogCategory.Driver,
					$"Deleted driver {driver.Name}.",
					"Driver",
					driver.DriverId);
                _context.Drivers.Remove(driver);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool DriverExists(int id)
        {
            return _context.Drivers.Any(e => e.DriverId == id);
        }

        private static decimal PctChange(decimal current, decimal previous)
            => Math.Round((current - previous) * 0.1m, 1);
    }
}
