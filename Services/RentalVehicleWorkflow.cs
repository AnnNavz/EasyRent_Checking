using System.Text.Json;
using EasyRent_Checking.Data;
using EasyRent_Checking.Models;
using Microsoft.EntityFrameworkCore;

namespace EasyRent_Checking.Services
{
	public static class RentalVehicleWorkflow
	{
		public static string SerializeVehicleIds(IEnumerable<int> vehicleIds)
		{
			var ids = vehicleIds.Where(id => id > 0).Distinct().ToList();
			return ids.Count == 0 ? string.Empty : JsonSerializer.Serialize(ids);
		}

		public static List<int> DeserializeVehicleIds(string? json, int fallbackVehicleId = 0)
		{
			if (!string.IsNullOrWhiteSpace(json))
			{
				try
				{
					var ids = JsonSerializer.Deserialize<List<int>>(json)?
						.Where(id => id > 0)
						.Distinct()
						.ToList();
					if (ids is { Count: > 0 })
					{
						return ids;
					}
				}
				catch (JsonException)
				{
					// Fall through to fallback.
				}
			}

			return fallbackVehicleId > 0 ? [fallbackVehicleId] : [];
		}

		public static List<int> ResolveVehicleIds(Rental rental, IEnumerable<RentalVehicle>? rentalVehicles = null)
		{
			var lines = rentalVehicles?
				.OrderBy(rv => rv.SortOrder)
				.ThenBy(rv => rv.RentalVehicleId)
				.Select(rv => rv.VehicleId)
				.Where(id => id > 0)
				.Distinct()
				.ToList() ?? [];

			if (lines.Count > 0)
			{
				return lines;
			}

			return DeserializeVehicleIds(rental.PendingVehicleIdsJson, 0);
		}

		public static int GetPrimaryVehicleId(Rental rental, IEnumerable<RentalVehicle>? rentalVehicles = null)
			=> ResolveVehicleIds(rental, rentalVehicles).FirstOrDefault();

		/// <summary>
		/// Unpaid pay-later hold: store selected vehicles on the rental header until payment confirms the booking.
		/// Does not insert <see cref="RentalVehicle"/> rows.
		/// </summary>
		public static async Task SyncPendingSelectionAsync(
			EasyRent_CheckingContext context,
			Rental rental,
			IReadOnlyList<Vehicle> orderedVehicles,
			IReadOnlyList<int> vehicleIds)
		{
			if (orderedVehicles.Count == 0)
			{
				return;
			}

			RentalFareCalculator.ApplyTo(rental, orderedVehicles);
			rental.PendingVehicleIdsJson = SerializeVehicleIds(vehicleIds);

			var existing = await context.RentalVehicles
				.Where(rv => rv.RentalId == rental.RentalId)
				.ToListAsync();
			if (existing.Count > 0)
			{
				context.RentalVehicles.RemoveRange(existing);
			}
		}

		/// <summary>
		/// Approved booking: write <see cref="RentalVehicle"/> line rows and clear pending selection.
		/// </summary>
		public static async Task MaterializeAsync(
			EasyRent_CheckingContext context,
			Rental rental,
			IReadOnlyList<int> vehicleIds)
		{
			var vehicles = await context.Vehicles.AsNoTracking()
				.Where(v => vehicleIds.Contains(v.VehicleId))
				.ToListAsync();

			var ordered = vehicleIds
				.Select(id => vehicles.FirstOrDefault(v => v.VehicleId == id))
				.Where(v => v != null)
				.Cast<Vehicle>()
				.ToList();

			if (ordered.Count == 0)
			{
				return;
			}

			RentalFareCalculator.ApplyTo(rental, ordered);

			var existing = await context.RentalVehicles
				.Where(rv => rv.RentalId == rental.RentalId)
				.ToListAsync();
			if (existing.Count > 0)
			{
				context.RentalVehicles.RemoveRange(existing);
			}

			var lines = RentalFareCalculator.BuildRentalVehicles(ordered, rental);
			foreach (var line in lines)
			{
				line.RentalId = rental.RentalId;
				context.RentalVehicles.Add(line);
			}

			rental.PendingVehicleIdsJson = null;
		}

		public static async Task<List<Vehicle>> LoadSelectedVehiclesAsync(
			EasyRent_CheckingContext context,
			Rental rental,
			IEnumerable<RentalVehicle>? rentalVehicles = null)
		{
			var lines = rentalVehicles?
				.Where(rv => rv.Vehicle != null)
				.OrderBy(rv => rv.SortOrder)
				.ThenBy(rv => rv.RentalVehicleId)
				.ToList()
				?? rental.RentalVehicles?
					.Where(rv => rv.Vehicle != null)
					.OrderBy(rv => rv.SortOrder)
					.ThenBy(rv => rv.RentalVehicleId)
					.ToList()
				?? [];

			if (lines.Count > 0)
			{
				return lines.Select(rv => rv.Vehicle!).ToList();
			}

			var ids = ResolveVehicleIds(rental, lines);
			if (ids.Count == 0)
			{
				return [];
			}

			var vehicles = await context.Vehicles.AsNoTracking()
				.Where(v => ids.Contains(v.VehicleId))
				.ToListAsync();

			return ids
				.Select(id => vehicles.FirstOrDefault(v => v.VehicleId == id))
				.Where(v => v != null)
				.Cast<Vehicle>()
				.ToList();
		}

		public static async Task<bool> HasScheduleConflictAsync(
			EasyRent_CheckingContext context,
			IReadOnlyList<int> vehicleIds,
			DateOnly pickupDate,
			DateOnly returnDate,
			int? excludeRentalId)
		{
			if (vehicleIds.Count == 0)
			{
				return false;
			}

			var conflictFromLines =
				from rv in context.RentalVehicles
				join rental in context.Rentals on rv.RentalId equals rental.RentalId
				where vehicleIds.Contains(rv.VehicleId)
					&& rental.RentalStatus != RentalStatus.Cancelled
					&& rental.RentalStatus != RentalStatus.Expired
					&& rental.PickupDate <= returnDate
					&& rental.ReturnDate >= pickupDate
				select rv.RentalId;

			var query = conflictFromLines;
			if (excludeRentalId.HasValue)
			{
				query = query.Where(rentalId => rentalId != excludeRentalId.Value);
			}

			if (await query.AnyAsync())
			{
				return true;
			}

			var pendingCandidates = await (
				from rental in context.Rentals.AsNoTracking()
				where rental.RentalStatus != RentalStatus.Cancelled
					&& rental.RentalStatus != RentalStatus.Expired
					&& !string.IsNullOrEmpty(rental.PendingVehicleIdsJson)
					&& !context.RentalVehicles.Any(rv => rv.RentalId == rental.RentalId)
					&& rental.PickupDate <= returnDate
					&& rental.ReturnDate >= pickupDate
					&& (!excludeRentalId.HasValue || rental.RentalId != excludeRentalId.Value)
				select new { rental.RentalId, rental.PendingVehicleIdsJson }
			).ToListAsync();

			foreach (var pending in pendingCandidates)
			{
				var pendingIds = DeserializeVehicleIds(pending.PendingVehicleIdsJson, 0);
				if (pendingIds.Any(vehicleIds.Contains))
				{
					return true;
				}
			}

			return false;
		}

		public static async Task<List<string>> GetUnavailableDatesAsync(
			EasyRent_CheckingContext context,
			int vehicleId)
		{
			if (vehicleId <= 0)
			{
				return [];
			}

			var rangesFromLines = await (
				from rv in context.RentalVehicles.AsNoTracking()
				join rental in context.Rentals.AsNoTracking() on rv.RentalId equals rental.RentalId
				where rv.VehicleId == vehicleId
					&& rental.RentalStatus != RentalStatus.Cancelled
					&& rental.RentalStatus != RentalStatus.Expired
				select new { rental.PickupDate, rental.ReturnDate }
			).ToListAsync();

			var pendingCandidates = await (
				from rental in context.Rentals.AsNoTracking()
				where !string.IsNullOrEmpty(rental.PendingVehicleIdsJson)
					&& !context.RentalVehicles.Any(rv => rv.RentalId == rental.RentalId)
					&& rental.RentalStatus != RentalStatus.Cancelled
					&& rental.RentalStatus != RentalStatus.Expired
				select new { rental.PendingVehicleIdsJson, rental.PickupDate, rental.ReturnDate }
			).ToListAsync();

			var ranges = rangesFromLines.ToList();

			foreach (var pending in pendingCandidates)
			{
				var ids = DeserializeVehicleIds(pending.PendingVehicleIdsJson, 0);
				if (ids.Contains(vehicleId))
				{
					ranges.Add(new { pending.PickupDate, pending.ReturnDate });
				}
			}

			var unavailableDates = new List<string>();
			foreach (var range in ranges)
			{
				for (var day = range.PickupDate; day <= range.ReturnDate; day = day.AddDays(1))
				{
					unavailableDates.Add(day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
				}
			}

			return unavailableDates.Distinct().OrderBy(d => d).ToList();
		}
	}
}
