using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
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

		// GET: Vehicles/MyBookings
		[Authorize]
		public async Task<IActionResult> MyBookings(string? filter)
		{
			var profile = await GetLoggedInCustomerProfileAsync();
			if (profile == null)
			{
				return RedirectToAction("Login", "Account");
			}

			var today = DateOnly.FromDateTime(DateTime.Today);
			var currentFilter = string.IsNullOrWhiteSpace(filter) ? "all" : filter.Trim().ToLowerInvariant();

			var reservations = await _context.Reservation
				.AsNoTracking()
				.Include(r => r.Details)!
					.ThenInclude(d => d!.Vehicle)
				.Where(r => r.ContactNumber == profile.ContactNumber)
				.OrderByDescending(r => r.ReservationId)
				.ToListAsync();

			var reservationIds = reservations.Select(r => r.ReservationId).ToList();
			var payments = await _context.Payment
				.AsNoTracking()
				.Where(p => reservationIds.Contains(p.ReservationId))
				.OrderByDescending(p => p.PaymentId)
				.ToListAsync();
			var paymentByReservation = payments
				.GroupBy(p => p.ReservationId)
				.ToDictionary(g => g.Key, g => g.First());

			var transits = await _context.Transit
				.AsNoTracking()
				.Include(t => t.Driver)
				.Where(t => reservationIds.Contains(t.ReservationID))
				.ToListAsync();
			var transitByReservation = transits.ToDictionary(t => t.ReservationID);

			var items = reservations
				.Where(r => r.Details != null)
				.Select(r =>
				{
					var details = r.Details!;
					paymentByReservation.TryGetValue(r.ReservationId, out var payment);
					transitByReservation.TryGetValue(r.ReservationId, out var transit);
					var vehicle = details.Vehicle;
					var isPast = details.ReturnDate < today || r.ReservationStatus == ReservationStatus.Cancelled;

					return new MyBookingListItem
					{
						ReservationId = r.ReservationId,
						BookingLabel = $"BK-{r.ReservationId:D6}",
						ReservationStatus = r.ReservationStatus,
						VehicleTitle = vehicle != null ? $"{vehicle.Model} {vehicle.Brand}".Trim() : "Vehicle",
						VehicleImagePath = vehicle?.ImagePath,
						PickupDate = details.PickupDate,
						PickupTime = details.PickupTime,
						ReturnTime = details.ReturnTime,
						AmountPaid = payment?.AmountPaid ?? 0m,
						PaymentMethod = payment?.PaymentMethod,
						PaymentStatus = payment?.PaymentStatus,
						DriverName = transit?.Driver?.Name,
						IsPast = isPast
					};
				})
				.ToList();

			items = currentFilter switch
			{
				"upcoming" => items.Where(i => !i.IsPast).ToList(),
				"completed" => items.Where(i => i.IsPast).ToList(),
				_ => items
			};

			ViewData["ActiveNav"] = "";
			ViewData["CurrentFilter"] = currentFilter is "upcoming" or "completed" ? currentFilter : "all";
			return View("~/Views/ClientSide/MyBookings.cshtml", items);
		}

		// GET: Vehicles/MyBookingDetails/5
		[Authorize]
		public async Task<IActionResult> MyBookingDetails(int? id)
		{
			if (id == null)
			{
				return NotFound();
			}

			var profile = await GetLoggedInCustomerProfileAsync();
			if (profile == null)
			{
				return RedirectToAction("Login", "Account");
			}

			var reservation = await _context.Reservation
				.AsNoTracking()
				.Include(r => r.Details)!
					.ThenInclude(d => d!.Vehicle)
				.FirstOrDefaultAsync(r => r.ReservationId == id && r.ContactNumber == profile.ContactNumber);

			if (reservation == null)
			{
				return NotFound();
			}

			var payment = await _context.Payment
				.AsNoTracking()
				.Where(p => p.ReservationId == reservation.ReservationId)
				.OrderByDescending(p => p.PaymentId)
				.FirstOrDefaultAsync();

			var transit = await _context.Transit
				.AsNoTracking()
				.Include(t => t.Driver)
				.FirstOrDefaultAsync(t => t.ReservationID == reservation.ReservationId);

			ViewData["LatestPayment"] = payment;
			ViewData["Transit"] = transit;
			ViewData["ActiveNav"] = "";
			return View("~/Views/ClientSide/MyBookingDetails.cshtml", reservation);
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
			ViewData["LiveRateSummary"] = true;

			var model = new ReservationInputModel
			{
				VehicleId = vehicle.VehicleId,
				PickupTime = new TimeOnly(9, 0),
				ReturnTime = new TimeOnly(17, 0),
				PassengerCount = 1,
				Discount = Discount.No,
				ReservationStatus = ReservationStatus.Pending
			};

			await ApplyLoggedInCustomerAsync(model);

			return View("~/Views/ClientSide/Reservation.cshtml", model);
		}

		// POST: Vehicles/Reservation/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Reservation(int id, [Bind("VehicleId,CustomerName,ContactNumber,PickupLocation,DropoffLocation,PickupDate,ReturnDate,PickupTime,ReturnTime,PassengerCount,Notes,Discount,DiscountImageFile")] ReservationInputModel model)
		{
			var vehicle = await _context.Vehicle.FirstOrDefaultAsync(v => v.VehicleId == id);
			if (vehicle == null)
			{
				return NotFound();
			}

			model.VehicleId = id;
			model.ReservationStatus = ReservationStatus.Pending;

			// Keep reservation contact details tied to the signed-in customer account.
			await ApplyLoggedInCustomerAsync(model);

			var today = DateOnly.FromDateTime(DateTime.Today);
			if (model.PickupDate < today)
			{
				ModelState.AddModelError(nameof(model.PickupDate), "Please select a pick-up date on the calendar.");
			}

			if (model.ReturnDate < model.PickupDate)
			{
				ModelState.AddModelError(nameof(model.ReturnDate), "Return date cannot be earlier than pick-up date.");
			}

			if (model.PassengerCount < 1 || model.PassengerCount > vehicle.PassengersCount)
			{
				ModelState.AddModelError(nameof(model.PassengerCount), $"Passenger count must be between 1 and {vehicle.PassengersCount}.");
			}

			if (model.PickupDate >= today && model.ReturnDate >= model.PickupDate)
			{
				var hasConflict = await _context.ReservationDetails.AnyAsync(d =>
					d.VehicleId == id
					&& d.Reservation != null
					&& d.Reservation.ReservationStatus != ReservationStatus.Cancelled
					&& d.PickupDate <= model.ReturnDate
					&& d.ReturnDate >= model.PickupDate);

				if (hasConflict)
				{
					ModelState.AddModelError(nameof(model.PickupDate), "Selected dates overlap an existing rental for this vehicle.");
				}
			}

			if (model.Discount == Discount.Yes && model.DiscountImageFile == null)
			{
				ModelState.AddModelError(nameof(model.DiscountImageFile), "Please upload a Senior/PWD ID image.");
			}

			if (ModelState.IsValid)
			{
				if (model.DiscountImageFile != null)
				{
					model.DiscountImagePath = await ImageStorage.SaveAsync(
						_webHostEnvironment,
						model.DiscountImageFile,
						ImageStorage.ReservationsFolder);
				}

				if (model.Discount == Discount.No)
				{
					model.DiscountImagePath = null;
				}

				var reservation = new Reservation();
				var details = new ReservationDetails();
				model.ApplyTo(reservation, details);

				_context.Reservation.Add(reservation);
				await _context.SaveChangesAsync();

				details.ReservationID = reservation.ReservationId;
				_context.ReservationDetails.Add(details);
				await _context.SaveChangesAsync();

				TempData["ReservationSuccess"] = "Your reservation request was submitted and is pending staff approval.";
				return RedirectToAction(nameof(VehicleDetails), new { id });
			}

			ViewData["ActiveNav"] = "Vehicles";
			ViewData["Vehicle"] = vehicle;
			ViewData["HideReserveButton"] = true;
			ViewData["LiveRateSummary"] = true;
			return View("~/Views/ClientSide/Reservation.cshtml", model);
		}

		private async Task ApplyLoggedInCustomerAsync(ReservationInputModel model)
		{
			var profile = await GetLoggedInCustomerProfileAsync();
			if (profile == null)
			{
				ViewData["CustomerFieldsLocked"] = false;
				return;
			}

			model.CustomerName = profile.FullName;
			model.ContactNumber = profile.ContactNumber;
			ViewData["CustomerFieldsLocked"] = true;
		}

		private async Task<CustomerProfile?> GetLoggedInCustomerProfileAsync()
		{
			if (User.Identity?.IsAuthenticated != true)
			{
				return null;
			}

			var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (!int.TryParse(userIdValue, out var userId))
			{
				return null;
			}

			return await _context.CustomerProfiles
				.AsNoTracking()
				.FirstOrDefaultAsync(c => c.CustomerId == userId);
		}
	}
}
