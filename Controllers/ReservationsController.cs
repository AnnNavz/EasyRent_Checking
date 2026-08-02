using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Models;
using EasyRent_Checking.Data;
using EasyRent_Checking.Services;

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

        // GET: Reservations
        public async Task<IActionResult> Index(string searchString, string sortBy, string currentFilter, int? page)
        {
            const int pageSize = 10;
            var pageNumber = page.GetValueOrDefault(1);
            if (pageNumber < 1)
            {
                pageNumber = 1;
            }

            ViewData["CurrentSearch"] = searchString;
            ViewData["CurrentSort"] = sortBy;
            ViewData["CurrentFilter"] = currentFilter;

            var reservationsQuery = _context.Reservation
                .Include(r => r.Vehicle)
                .AsQueryable();

            var today = DateOnly.FromDateTime(DateTime.Today);

            ViewData["TotalBookingsCount"] = await reservationsQuery.CountAsync();
            ViewData["PendingApprovalsCount"] = await reservationsQuery.CountAsync(r => r.ReservationStatus == ReservationStatus.Pending);
            ViewData["ActiveTripsCount"] = await reservationsQuery.CountAsync(r =>
                r.ReservationStatus == ReservationStatus.Approved
                && r.PickupDate <= today
                && r.ReturnDate >= today);

            if (!string.IsNullOrEmpty(searchString))
            {
                var term = searchString.Trim();
                var idToken = term.StartsWith("BK-", StringComparison.OrdinalIgnoreCase)
                    ? term[3..]
                    : term;
                var hasBookingId = int.TryParse(idToken, out var bookingId);

                reservationsQuery = reservationsQuery.Where(r =>
                    r.CustomerName.Contains(term)
                    || r.ContactNumber.Contains(term)
                    || (r.Vehicle != null && (
                        r.Vehicle.Brand.Contains(term)
                        || r.Vehicle.Model.Contains(term)
                        || r.Vehicle.PlateNumber.Contains(term)))
                    || (hasBookingId && r.ReservationId == bookingId));
            }

            if (!string.IsNullOrEmpty(currentFilter) && Enum.TryParse(currentFilter, true, out ReservationStatus filterStatus))
            {
                reservationsQuery = reservationsQuery.Where(r => r.ReservationStatus == filterStatus);
            }

            reservationsQuery = sortBy switch
            {
                "CustomerName" => reservationsQuery.OrderBy(r => r.CustomerName),
                "PickupDate" => reservationsQuery.OrderBy(r => r.PickupDate).ThenBy(r => r.PickupTime),
                "Status" => reservationsQuery.OrderBy(r => r.ReservationStatus),
                "Vehicle" => reservationsQuery.OrderBy(r => r.Vehicle!.Brand).ThenBy(r => r.Vehicle!.Model),
                _ => reservationsQuery.OrderByDescending(r => r.ReservationId)
            };

            var totalCount = await reservationsQuery.CountAsync();
            var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
            if (pageNumber > totalPages)
            {
                pageNumber = totalPages;
            }

            ViewData["PageIndex"] = pageNumber;
            ViewData["TotalPages"] = totalPages;
            ViewData["TotalCount"] = totalCount;
            ViewData["PageSize"] = pageSize;

            var reservations = await reservationsQuery
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return View(reservations);
        }

        // GET: Reservations/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reservation = await _context.Reservation
                .Include(r => r.Vehicle)
                .FirstOrDefaultAsync(m => m.ReservationId == id);
            if (reservation == null)
            {
                return NotFound();
            }

            return View(reservation);
        }

        // GET: Reservations/Create
        public async Task<IActionResult> Create(int? vehicleId)
        {
            await PopulateVehicleListAsync(vehicleId);
            var model = new Reservation();
            if (vehicleId.HasValue && await _context.Vehicle.AnyAsync(v => v.VehicleId == vehicleId.Value))
            {
                model.VehicleId = vehicleId.Value;
            }
            return View(model);
        }

        // POST: Reservations/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ReservationId,VehicleId,CustomerName,ContactNumber,PickupLocation,DropoffLocation,PickupDate,ReturnDate,PickupTime,ReturnTime,PassengerCount,Notes,Discount,DiscountImagePath,DiscountImageFile,ReservationStatus")] Reservation reservation)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (reservation.PickupDate < today)
            {
                ModelState.AddModelError(nameof(reservation.PickupDate), "Please select a pick-up date on the calendar.");
            }

            if (reservation.ReturnDate < reservation.PickupDate)
            {
                ModelState.AddModelError(nameof(reservation.ReturnDate), "Return date cannot be earlier than pick-up date.");
            }

            if (reservation.VehicleId > 0
                && reservation.PickupDate >= DateOnly.FromDateTime(DateTime.Today)
                && reservation.ReturnDate >= reservation.PickupDate)
            {
                var hasConflict = await _context.Reservation.AnyAsync(r =>
                    r.VehicleId == reservation.VehicleId
                    && r.ReservationStatus != ReservationStatus.Cancelled
                    && r.PickupDate <= reservation.ReturnDate
                    && r.ReturnDate >= reservation.PickupDate);

                if (hasConflict)
                {
                    ModelState.AddModelError(nameof(reservation.PickupDate), "Selected dates overlap an existing rental for this vehicle.");
                }
            }

            if (reservation.Discount == Discount.Yes && reservation.DiscountImageFile == null && string.IsNullOrEmpty(reservation.DiscountImagePath))
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

                reservation.ReservationStatus = ReservationStatus.Pending;
                _context.Add(reservation);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            await PopulateVehicleListAsync(reservation.VehicleId);
            return View(reservation);
        }

        // GET: Reservations/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reservation = await _context.Reservation.FindAsync(id);
            if (reservation == null)
            {
                return NotFound();
            }
            await PopulateVehicleListAsync(reservation.VehicleId);
            return View(reservation);
        }

        // POST: Reservations/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ReservationId,VehicleId,CustomerName,ContactNumber,PickupLocation,DropoffLocation,PickupDate,ReturnDate,PickupTime,ReturnTime,PassengerCount,Notes,Discount,DiscountImagePath,DiscountImageFile,ReservationStatus")] Reservation reservation)
        {
            if (id != reservation.ReservationId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (reservation.DiscountImageFile != null)
                    {
                        reservation.DiscountImagePath = await ImageStorage.SaveAsync(
                            _webHostEnvironment,
                            reservation.DiscountImageFile,
                            ImageStorage.ReservationsFolder);
                    }

                    _context.Update(reservation);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ReservationExists(reservation.ReservationId))
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
            await PopulateVehicleListAsync(reservation.VehicleId);
            return View(reservation);
        }

        // GET: Reservations/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reservation = await _context.Reservation
                .Include(r => r.Vehicle)
                .FirstOrDefaultAsync(m => m.ReservationId == id);
            if (reservation == null)
            {
                return NotFound();
            }

            return View(reservation);
        }

        // POST: Reservations/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var reservation = await _context.Reservation.FindAsync(id);
            if (reservation != null)
            {
                _context.Reservation.Remove(reservation);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Reservations/Availability?vehicleId=1
        [HttpGet]
        public async Task<IActionResult> Availability(int vehicleId)
        {
            if (vehicleId <= 0)
            {
                return Json(new { unavailableDates = Array.Empty<string>() });
            }

            var reservations = await _context.Reservation
                .AsNoTracking()
                .Where(r => r.VehicleId == vehicleId
                    && r.ReservationStatus != ReservationStatus.Cancelled)
                .Select(r => new { r.PickupDate, r.ReturnDate })
                .ToListAsync();

            var unavailableDates = new List<string>();
            foreach (var reservation in reservations)
            {
                for (var day = reservation.PickupDate; day <= reservation.ReturnDate; day = day.AddDays(1))
                {
                    unavailableDates.Add(day.ToString("yyyy-MM-dd"));
                }
            }

            return Json(new { unavailableDates = unavailableDates.Distinct().OrderBy(d => d).ToList() });
        }

        private bool ReservationExists(int id)
        {
            return _context.Reservation.Any(e => e.ReservationId == id);
        }

        private async Task PopulateVehicleListAsync(int? selectedVehicleId = null)
        {
            var vehicles = await _context.Vehicle
                .OrderBy(v => v.Brand)
                .ThenBy(v => v.Model)
                .ToListAsync();

            ViewBag.Vehicles = vehicles;
            ViewData["VehicleId"] = new SelectList(
                vehicles.Select(v => new
                {
                    v.VehicleId,
                    Label = $"{v.Brand} {v.Model} ({v.PlateNumber})"
                }),
                "VehicleId",
                "Label",
                selectedVehicleId);
        }
    }
}
