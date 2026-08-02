using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Data;
using EasyRent_Checking.Models;
using EasyRent_Checking.Services;
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
        public async Task<IActionResult> Create([Bind("VehicleId,Model,PlateNumber,Brand,Color,Type,Status,RegistrationDate,RegistrationExpiry,BasePrice,SucceedingFee,PassengersCount,Description,ImagePath,ImageFile")] Vehicle vehicle)
        {
            if (ModelState.IsValid)
            {
                if (vehicle.ImageFile != null)
                {
                    vehicle.ImagePath = await ImageStorage.SaveAsync(_webHostEnvironment, vehicle.ImageFile, ImageStorage.VehiclesFolder);
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
        public async Task<IActionResult> Edit(int id, [Bind("VehicleId,Model,PlateNumber,Brand,Color,Type,Status,RegistrationDate,RegistrationExpiry,BasePrice,SucceedingFee,PassengersCount,Description,ImagePath,ImageFile")] Vehicle vehicle, string? returnUrl)
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

		// GET: Vehicles/Homepage
		public IActionResult Homepage()
		{
			ViewData["ActiveNav"] = "Home";
			return View("~/Views/ClientSide/Home.cshtml");
		}

		// GET: Vehicles/Browse
		public async Task<IActionResult> Browse(string? category, string? sortBy)
		{
			var vehiclesQuery = _context.Vehicle.AsQueryable();

			if (!string.IsNullOrEmpty(category) && !string.Equals(category, "All", StringComparison.OrdinalIgnoreCase))
			{
				if (Enum.TryParse(category, true, out VehicleType filterType))
				{
					vehiclesQuery = vehiclesQuery.Where(v => v.Type == filterType);
				}
			}

			vehiclesQuery = sortBy switch
			{
				"Model" => vehiclesQuery.OrderBy(v => v.Model),
				"Brand" => vehiclesQuery.OrderBy(v => v.Brand),
				_ => vehiclesQuery.OrderByDescending(v => v.VehicleId)
			};

			ViewData["CurrentCategory"] = string.IsNullOrEmpty(category) ? "All" : category;
			ViewData["CurrentSort"] = string.IsNullOrEmpty(sortBy) ? "Default" : sortBy;
			ViewData["ActiveNav"] = "Vehicles";

			return View("~/Views/ClientSide/Vehicles.cshtml", await vehiclesQuery.ToListAsync());
		}

		// GET: Vehicles/VehicleDetails/5
		public async Task<IActionResult> VehicleDetails(int? id)
		{
			if (id == null)
			{
				return NotFound();
			}

			var vehicle = await _context.Vehicle.FirstOrDefaultAsync(v => v.VehicleId == id);
			if (vehicle == null)
			{
				return NotFound();
			}

			ViewData["ActiveNav"] = "Vehicles";
			return View("~/Views/ClientSide/VehicleDetails.cshtml", vehicle);
		}

		// GET: Vehicles/Reservation/5
		public async Task<IActionResult> Reservation(int? id)
		{
			if (id == null)
			{
				return NotFound();
			}

			var vehicle = await _context.Vehicle.FirstOrDefaultAsync(v => v.VehicleId == id);
			if (vehicle == null)
			{
				return NotFound();
			}

			if (vehicle.Status != VehicleStatus.Available)
			{
				TempData["ReservationError"] = "This vehicle is not available for reservation right now.";
				return RedirectToAction(nameof(VehicleDetails), new { id });
			}

			ViewData["ActiveNav"] = "Vehicles";
			ViewData["Vehicle"] = vehicle;
			ViewData["HideReserveButton"] = true;

			var model = new Reservation
			{
				VehicleId = vehicle.VehicleId,
				PickupTime = new TimeOnly(9, 0),
				ReturnTime = new TimeOnly(17, 0),
				PassengerCount = 1,
				Discount = Discount.No,
				ReservationStatus = ReservationStatus.Pending
			};

			return View("~/Views/ClientSide/Reservation.cshtml", model);
		}

		// POST: Vehicles/Reservation/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Reservation(int id, [Bind("VehicleId,CustomerName,ContactNumber,PickupLocation,DropoffLocation,PickupDate,ReturnDate,PickupTime,ReturnTime,PassengerCount,Notes,Discount,DiscountImageFile")] Reservation reservation)
		{
			var vehicle = await _context.Vehicle.FirstOrDefaultAsync(v => v.VehicleId == id);
			if (vehicle == null)
			{
				return NotFound();
			}

			reservation.VehicleId = id;
			reservation.ReservationStatus = ReservationStatus.Pending;

			var today = DateOnly.FromDateTime(DateTime.Today);
			if (reservation.PickupDate < today)
			{
				ModelState.AddModelError(nameof(reservation.PickupDate), "Please select a pick-up date on the calendar.");
			}

			if (reservation.ReturnDate < reservation.PickupDate)
			{
				ModelState.AddModelError(nameof(reservation.ReturnDate), "Return date cannot be earlier than pick-up date.");
			}

			if (reservation.PassengerCount < 1 || reservation.PassengerCount > vehicle.PassengersCount)
			{
				ModelState.AddModelError(nameof(reservation.PassengerCount), $"Passenger count must be between 1 and {vehicle.PassengersCount}.");
			}

			if (reservation.PickupDate >= today && reservation.ReturnDate >= reservation.PickupDate)
			{
				var hasConflict = await _context.Reservation.AnyAsync(r =>
					r.VehicleId == id
					&& r.ReservationStatus != ReservationStatus.Cancelled
					&& r.PickupDate <= reservation.ReturnDate
					&& r.ReturnDate >= reservation.PickupDate);

				if (hasConflict)
				{
					ModelState.AddModelError(nameof(reservation.PickupDate), "Selected dates overlap an existing rental for this vehicle.");
				}
			}

			if (reservation.Discount == Discount.Yes && reservation.DiscountImageFile == null)
			{
				ModelState.AddModelError(nameof(reservation.DiscountImageFile), "Please upload a Senior/PWD ID image.");
			}

			if (ModelState.IsValid)
			{
				if (reservation.DiscountImageFile != null)
				{
					reservation.DiscountImagePath = await ImageStorage.SaveAsync(
						_webHostEnvironment,
						reservation.DiscountImageFile,
						ImageStorage.ReservationsFolder);
				}

				if (reservation.Discount == Discount.No)
				{
					reservation.DiscountImagePath = null;
				}

				_context.Add(reservation);
				await _context.SaveChangesAsync();
				TempData["ReservationSuccess"] = "Your reservation request was submitted and is pending staff approval.";
				return RedirectToAction(nameof(VehicleDetails), new { id });
			}

			ViewData["ActiveNav"] = "Vehicles";
			ViewData["Vehicle"] = vehicle;
			ViewData["HideReserveButton"] = true;
			return View("~/Views/ClientSide/Reservation.cshtml", reservation);
		}
	}
}
