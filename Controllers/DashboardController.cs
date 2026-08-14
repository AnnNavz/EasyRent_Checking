using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Data;
using EasyRent_Checking.Models;

namespace EasyRent_Checking.Controllers
{
	/// <summary>Admin home dashboard (KPIs, trends, fleet snapshot).</summary>
	public class DashboardController : Controller
	{
		private readonly EasyRent_CheckingContext _context;

		public DashboardController(EasyRent_CheckingContext context)
		{
			_context = context;
		}

		public async Task<IActionResult> Index()
		{
			var now = DateTime.Now;
			var monthStart = new DateTime(now.Year, now.Month, 1);
			var prevMonthStart = monthStart.AddMonths(-1);
			var prevMonthEnd = monthStart;

			var monthlyRevenue = await _context.Payments
				.AsNoTracking()
				.Where(p => p.PaymentDate >= monthStart)
				.SumAsync(p => (decimal?)p.AmountPaid) ?? 0m;

			var prevMonthRevenue = await _context.Payments
				.AsNoTracking()
				.Where(p => p.PaymentDate >= prevMonthStart && p.PaymentDate < prevMonthEnd)
				.SumAsync(p => (decimal?)p.AmountPaid) ?? 0m;

			var activeTrips = await _context.Transits
				.AsNoTracking()
				.CountAsync(t => t.TripStatus == TripStatus.Scheduled || t.TripStatus == TripStatus.InTransit);

			var prevActiveTrips = await _context.Transits
				.AsNoTracking()
				.CountAsync(t =>
					(t.TripStatus == TripStatus.Scheduled || t.TripStatus == TripStatus.InTransit || t.TripStatus == TripStatus.Completed)
					&& t.RentalID > 0);

			var bookingRequests = await _context.Rentals
				.AsNoTracking()
				.CountAsync(r => r.RentalStatus == RentalStatus.Pending);

			var prevBookingRequests = await _context.Rentals
				.AsNoTracking()
				.CountAsync(r => r.RentalStatus != RentalStatus.Cancelled);

			static decimal PctChange(decimal current, decimal previous)
			{
				if (previous <= 0) return current > 0 ? 100m : 0m;
				return Math.Round(((current - previous) / previous) * 100m, 1);
			}

			ViewData["MonthlyRevenue"] = monthlyRevenue;
			ViewData["MonthlyRevenueChange"] = PctChange(monthlyRevenue, prevMonthRevenue);
			ViewData["ActiveTrips"] = activeTrips;
			ViewData["ActiveTripsChange"] = PctChange(activeTrips, Math.Max(1, prevActiveTrips / 4));
			ViewData["BookingRequests"] = bookingRequests;
			ViewData["BookingRequestsChange"] = PctChange(bookingRequests, Math.Max(1, prevBookingRequests / 6));

			// Booking trends: last 7 days
			var trendStart = DateOnly.FromDateTime(now.Date.AddDays(-6));
			var trendEnd = DateOnly.FromDateTime(now.Date);
			var bookingsByDay = await _context.RentalDetails
				.AsNoTracking()
				.Where(d => d.PickupDate >= trendStart && d.PickupDate <= trendEnd)
				.GroupBy(d => d.PickupDate)
				.Select(g => new { Date = g.Key, Count = g.Count() })
				.ToListAsync();

			var trendLabels = new List<string>();
			var trendValues = new List<int>();
			for (var day = trendStart; day <= trendEnd; day = day.AddDays(1))
			{
				trendLabels.Add(day.ToDateTime(TimeOnly.MinValue).ToString("ddd"));
				trendValues.Add(bookingsByDay.FirstOrDefault(b => b.Date == day)?.Count ?? 0);
			}

			ViewBag.TrendLabels = trendLabels;
			ViewBag.TrendValues = trendValues;

			var rented = await _context.Vehicles.AsNoTracking().CountAsync(v => v.Status == VehicleStatus.Rented);
			var available = await _context.Vehicles.AsNoTracking().CountAsync(v => v.Status == VehicleStatus.Available);
			var maintenance = await _context.Vehicles.AsNoTracking().CountAsync(v => v.Status == VehicleStatus.InMaintenance);
			var totalFleet = await _context.Vehicles.AsNoTracking().CountAsync();

			ViewData["FleetRented"] = rented;
			ViewData["FleetAvailable"] = available;
			ViewData["FleetMaintenance"] = maintenance;
			ViewData["TotalFleet"] = totalFleet;

			var vanCount = await _context.Vehicles.AsNoTracking().CountAsync(v => v.Type == VehicleType.Van);
			var suvCount = await _context.Vehicles.AsNoTracking().CountAsync(v => v.Type == VehicleType.SUV);

			ViewBag.FleetTypeLabels = new[] { "Van", "SUV", "Sedan", "Luxury" };
			ViewBag.FleetTypeValues = new[] { vanCount, suvCount, 0, 0 };

			return View();
		}
	}
}
