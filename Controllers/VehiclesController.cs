using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Data;
using EasyRent_Checking.Models;
using EasyRent_Checking.ViewModels;
using EasyRent_Checking.Services;

namespace EasyRent_Checking.Controllers
{
    [Authorize(Policy = "StaffArea")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class VehiclesController : Controller
    {
        private readonly EasyRent_CheckingContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly SystemLogService _logs;

        public VehiclesController(EasyRent_CheckingContext context, IWebHostEnvironment webHostEnvironment, SystemLogService logs)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _logs = logs;
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
			var vehiclesQuery = from v in _context.Vehicles select v;

			// 2. Real-time KPI Card Computations
			var today = DateTime.Today;
			var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
			var totalVehiclesCount = await vehiclesQuery.CountAsync();
			var activeVehiclesCount = await vehiclesQuery.CountAsync(v => v.IsActive && v.Status == VehicleStatus.Available);
			var expiredRegistrationsCount = await vehiclesQuery.CountAsync(v => v.RegistrationExpiry.Date < today);
			var totalVehiclesAtMonthStart = await vehiclesQuery.CountAsync(v => v.RegistrationDate < monthStart);
			var activeVehiclesAtMonthStart = await vehiclesQuery.CountAsync(v =>
				v.RegistrationDate < monthStart && v.IsActive && v.Status == VehicleStatus.Available);
			var expiredRegistrationsAtMonthStart = await vehiclesQuery.CountAsync(v =>
				v.RegistrationExpiry.Date < monthStart);

			ViewData["TotalVehiclesCount"] = totalVehiclesCount;
			ViewData["ActiveVehiclesCount"] = activeVehiclesCount;
			ViewData["ExpiredRegistrationsCount"] = expiredRegistrationsCount;
			ViewData["TotalVehiclesChange"] = PctChange(totalVehiclesCount, totalVehiclesAtMonthStart);
			ViewData["ActiveVehiclesChange"] = PctChange(activeVehiclesCount, activeVehiclesAtMonthStart);
			ViewData["ExpiredRegistrationsChange"] = PctChange(expiredRegistrationsCount, expiredRegistrationsAtMonthStart);

			// 3. Handle Live Input Search Logic
			if (!string.IsNullOrEmpty(searchString))
			{
				vehiclesQuery = vehiclesQuery.Where(v => v.Model.Contains(searchString)
													  || v.Brand.Contains(searchString)
													  || v.PlateNumber.Contains(searchString));
			}

			// 4. Handle Status Filter Categorization
			if (!string.IsNullOrEmpty(currentFilter))
			{
				if (currentFilter == "Active")
				{
					vehiclesQuery = vehiclesQuery.Where(v => v.IsActive && v.Status == VehicleStatus.Available);
				}
				else if (currentFilter == "Maintenance")
				{
					vehiclesQuery = vehiclesQuery.Where(v => v.Status == VehicleStatus.InMaintenance);
				}
				else if (currentFilter == "Deactivated")
				{
					vehiclesQuery = vehiclesQuery.Where(v => !v.IsActive);
				}
				else if (currentFilter == "ExpiredRegistration")
				{
					vehiclesQuery = vehiclesQuery.Where(v => v.RegistrationExpiry.Date < today);
				}
				else if (Enum.TryParse(currentFilter, true, out VehicleStatus filterStatus))
				{
					vehiclesQuery = vehiclesQuery.Where(v => v.Status == filterStatus);
				}
			}

			// 5. Handle Query Layer Column Sorting
			vehiclesQuery = sortBy switch
			{
				"Model" => vehiclesQuery.OrderBy(v => v.Model),
				"Brand" => vehiclesQuery.OrderBy(v => v.Brand),
				"PlateNumber" => vehiclesQuery.OrderBy(v => v.PlateNumber),
				"RegistrationExpiry" => vehiclesQuery.OrderBy(v => v.RegistrationExpiry),
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

            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(m => m.VehicleId == id);
            if (vehicle == null)
            {
                return NotFound();
            }

			var plans = await _context.MaintenancePlans
				.AsNoTracking()
				.Include(p => p.Logs)
				.Where(p => p.VehicleId == vehicle.VehicleId)
				.ToListAsync();

			var maintenanceItems = plans
				.Select(p =>
				{
					var openLog = p.Logs.FirstOrDefault(l => l.Status == MaintenanceStatus.InProgress);
					return new MaintenanceDueItem
					{
						MaintenancePlanId = p.MaintenancePlanId,
						VehicleId = p.VehicleId,
						VehicleName = $"{vehicle.Brand} {vehicle.Model}".Trim(),
						PlateNumber = vehicle.PlateNumber,
						Type = p.Type,
						Trigger = p.Trigger,
						ScheduleLabel = PmsRules.FormatSchedule(p),
						DueKind = PmsRules.Classify(p, vehicle, openLog != null),
						NextDueDate = p.NextDueDate,
						NextDueOdometer = p.NextDueOdometer,
						CurrentOdometer = vehicle.Odometer,
						OpenLogId = openLog?.MaintenanceLogId,
						DueLabel = PmsRules.FormatDue(p),
						LastCompletedDate = p.LastCompletedDate,
						LastOdometer = p.LastOdometer,
						VehicleStatus = vehicle.Status
					};
				})
				.OrderBy(i => i.DueKind)
				.ThenBy(i => i.Type)
				.ToList();

			var bookingRows = await (
				from t in _context.Transits.AsNoTracking()
				join r in _context.Rentals.AsNoTracking() on t.RentalID equals r.RentalId
				where t.VehicleID == vehicle.VehicleId
				orderby t.TransitID descending
				select new { t, r }
			).Take(50).ToListAsync();

			var bookingHistory = bookingRows
				.Select(row => MapBookingHistory(row.t, row.r))
				.ToList();

			return View(new VehicleDetailsViewModel
			{
				Vehicle = vehicle,
				MaintenanceItems = maintenanceItems,
				BookingHistory = bookingHistory
			});
        }

		// POST: Vehicles/ToggleActive/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ToggleActive(int id)
		{
			var vehicle = await _context.Vehicles.FindAsync(id);
			if (vehicle == null)
			{
				return NotFound();
			}

			vehicle.IsActive = !vehicle.IsActive;
			var vehicleLabel = $"{vehicle.Brand} {vehicle.Model}".Trim();
			_logs.Record(
				vehicle.IsActive ? SystemLogAction.Reactivated : SystemLogAction.Deactivated,
				SystemLogCategory.Vehicle,
				vehicle.IsActive
					? $"Reactivated vehicle {vehicleLabel} ({vehicle.PlateNumber})."
					: $"Deactivated vehicle {vehicleLabel} ({vehicle.PlateNumber}).",
				"Vehicle",
				vehicle.VehicleId);
			await _context.SaveChangesAsync();

			TempData["SuccessMessage"] = vehicle.IsActive
				? $"{vehicleLabel} has been reactivated."
				: $"{vehicleLabel} has been deactivated.";

			return RedirectToAction(nameof(Details), new { id });
		}

		private static VehicleBookingHistoryItem MapBookingHistory(Transit transit, Rental rental)
		{
			var start = rental.PickupDate.ToDateTime(rental.PickupTime);
			var end = rental.ReturnDate.ToDateTime(rental.ReturnTime);
			if (end < start)
			{
				end = start;
			}

			var totalHours = Math.Max(1, (int)Math.Round((end - start).TotalHours));
			var status = transit.TripStatus switch
			{
				TripStatus.Completed => "Completed",
				TripStatus.InTransit => "In Transit",
				TripStatus.Cancelled => "Cancelled",
				TripStatus.Delayed => "Delayed",
				_ => "Scheduled"
			};

			return new VehicleBookingHistoryItem
			{
				RentalId = rental.RentalId,
				BookingLabel = $"BK-{rental.RentalId:D5}",
				CustomerName = rental.CustomerName,
				TripDates = FormatTripDates(rental.PickupDate, rental.ReturnDate),
				Duration = totalHours == 1 ? "1 hour" : $"{totalHours} hours",
				SortDate = start,
				Status = status,
				StatusClass = transit.TripStatus switch
				{
					TripStatus.Completed => "is-completed",
					TripStatus.InTransit => "is-progress",
					TripStatus.Cancelled => "is-cancelled",
					TripStatus.Delayed => "is-due-soon",
					_ => "is-upcoming"
				}
			};
		}

		private static string FormatTripDates(DateOnly start, DateOnly end)
		{
			if (start == end)
			{
				return start.ToString("MMM. d, yyyy");
			}

			if (start.Year == end.Year && start.Month == end.Month)
			{
				return $"{start:MMM. d} - {end:d yyyy}";
			}

			if (start.Year == end.Year)
			{
				return $"{start:MMM. d} - {end:MMM. d yyyy}";
			}

			return $"{start:MMM. d, yyyy} - {end:MMM. d, yyyy}";
		}

        // GET: Vehicles/Create
        public async Task<IActionResult> Create()
        {
            await PopulateVehicleTypeOptionsAsync();
            return View(new Vehicle { Status = VehicleStatus.Available, IsActive = true, Type = VehicleTypes.Suv, PassengersCount = 4 });
        }

        // POST: Vehicles/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("VehicleId,Model,PlateNumber,Brand,Color,Type,CustomType,Status,IsActive,Odometer,RegistrationDate,RegistrationExpiry,BasePrice,SucceedingFee,PassengersCount,Description,ImagePath,ImageFile")] Vehicle vehicle)
        {
            ApplyVehicleType(vehicle);
            vehicle.IsActive = true;
            if (ModelState.IsValid)
            {
                if (vehicle.ImageFile != null)
                {
                    vehicle.ImagePath = await ImageStorage.SaveAsync(_webHostEnvironment, vehicle.ImageFile, ImageStorage.VehiclesFolder);
                }

                _context.Add(vehicle);
                await _context.SaveChangesAsync();
                _logs.Record(
                    SystemLogAction.Created,
                    SystemLogCategory.Vehicle,
                    $"Added vehicle {vehicle.Brand} {vehicle.Model} ({vehicle.PlateNumber}).",
                    "Vehicle",
                    vehicle.VehicleId);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(CreateSuccess), new { id = vehicle.VehicleId });
            }
            await PopulateVehicleTypeOptionsAsync(vehicle.Type);
            return View(vehicle);
        }

        // GET: Vehicles/CreateSuccess/5
        public async Task<IActionResult> CreateSuccess(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var vehicle = await _context.Vehicles.FindAsync(id);
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

            var vehicle = await _context.Vehicles.FindAsync(id);
            if (vehicle == null)
            {
                return NotFound();
            }

            if (Url.IsLocalUrl(returnUrl))
            {
                ViewData["ReturnUrl"] = returnUrl;
            }

            await PopulateVehicleTypeOptionsAsync(vehicle.Type);
            return View(vehicle);
        }

        // POST: Vehicles/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("VehicleId,Model,PlateNumber,Brand,Color,Type,CustomType,Status,Odometer,RegistrationDate,RegistrationExpiry,BasePrice,SucceedingFee,PassengersCount,Description,ImagePath,ImageFile")] Vehicle vehicle, string? returnUrl)
        {
            if (id != vehicle.VehicleId)
            {
                return NotFound();
            }

            var existingVehicle = await _context.Vehicles.FindAsync(vehicle.VehicleId);
            if (existingVehicle == null)
            {
                return NotFound();
            }

            ApplyVehicleType(vehicle);
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
                    existingVehicle.Odometer = vehicle.Odometer;
                    existingVehicle.RegistrationDate = vehicle.RegistrationDate;
                    existingVehicle.RegistrationExpiry = vehicle.RegistrationExpiry;
                    existingVehicle.BasePrice = vehicle.BasePrice;
                    existingVehicle.SucceedingFee = vehicle.SucceedingFee;
                    existingVehicle.PassengersCount = vehicle.PassengersCount;
                    existingVehicle.Description = vehicle.Description;

                    if (vehicle.ImageFile != null)
                    {
                        existingVehicle.ImagePath = await ImageStorage.SaveAsync(_webHostEnvironment, vehicle.ImageFile, ImageStorage.VehiclesFolder);
                    }
                    else if (string.IsNullOrEmpty(vehicle.ImagePath))
                    {
                        existingVehicle.ImagePath = null;
                    }

                    _context.Update(existingVehicle);
                    _logs.Record(
                        SystemLogAction.Updated,
                        SystemLogCategory.Vehicle,
                        $"Updated vehicle {existingVehicle.Brand} {existingVehicle.Model} ({existingVehicle.PlateNumber}).",
                        "Vehicle",
                        existingVehicle.VehicleId);
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

            await PopulateVehicleTypeOptionsAsync(vehicle.Type);
            return View(vehicle);
        }

        // GET: Vehicles/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var vehicle = await _context.Vehicles
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
            var vehicle = await _context.Vehicles.FindAsync(id);
            if (vehicle != null)
            {
                _logs.Record(
                    SystemLogAction.Deleted,
                    SystemLogCategory.Vehicle,
                    $"Deleted vehicle {vehicle.Brand} {vehicle.Model} ({vehicle.PlateNumber}).",
                    "Vehicle",
                    vehicle.VehicleId);
                _context.Vehicles.Remove(vehicle);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateVehicleTypeOptionsAsync(string? currentType = null)
        {
            var existing = await _context.Vehicles
                .AsNoTracking()
                .Select(v => v.Type)
                .Where(t => t != null && t != "")
                .Distinct()
                .ToListAsync();
            ViewData["VehicleTypeOptions"] = VehicleTypes.Options(existing, currentType);
        }

        private void ApplyVehicleType(Vehicle vehicle)
        {
            vehicle.Type = VehicleTypes.Normalize(vehicle.Type);
            if (string.IsNullOrWhiteSpace(vehicle.Type))
            {
                ModelState.AddModelError(nameof(Vehicle.Type), "Please choose or add a vehicle type.");
            }
        }

        private bool VehicleExists(int id)
        {
            return _context.Vehicles.Any(e => e.VehicleId == id);
        }

		private static decimal PctChange(decimal current, decimal previous)
			=> Math.Round((current - previous) * 0.1m, 1);
    }
}
