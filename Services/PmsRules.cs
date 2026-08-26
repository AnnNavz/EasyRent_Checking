using EasyRent_Checking.Models;

namespace EasyRent_Checking.Services
{
	public sealed record PmsRule(
		MaintenanceType Type,
		PmsTrigger Trigger,
		int? IntervalKilometers,
		int? IntervalMonths);

	public static class PmsRules
	{
		public const int DueSoonDays = 14;
		public const int DueSoonKilometers = 500;
		public const int DueNowKilometers = 100;
		public const string PresetSuv = "SUV";
		public const string PresetVan = "Van";

		public static readonly PmsRule[] SuvSchedule =
		[
			new(MaintenanceType.OilChange, PmsTrigger.Mileage, 5000, null),
			new(MaintenanceType.TireInspection, PmsTrigger.Time, null, 1),
			new(MaintenanceType.BrakeInspection, PmsTrigger.Mileage, 10000, null),
			new(MaintenanceType.GeneralCheckup, PmsTrigger.Time, null, 6),
			new(MaintenanceType.AirFilterReplacement, PmsTrigger.Mileage, 15000, null)
		];

		public static readonly PmsRule[] VanSchedule =
		[
			new(MaintenanceType.OilChange, PmsTrigger.Mileage, 4000, null),
			new(MaintenanceType.TireInspection, PmsTrigger.Time, null, 1),
			new(MaintenanceType.BrakeInspection, PmsTrigger.Mileage, 8000, null),
			new(MaintenanceType.GeneralCheckup, PmsTrigger.Time, null, 6),
			new(MaintenanceType.AirFilterReplacement, PmsTrigger.Mileage, 10000, null)
		];

		public static PmsRule[] ForPreset(string? preset)
			=> string.Equals(preset, PresetVan, StringComparison.OrdinalIgnoreCase)
				? VanSchedule
				: SuvSchedule;

		public static string DefaultPreset(string? type)
			=> VehicleTypes.IsVan(type) ? PresetVan : PresetSuv;

		public static MaintenancePlan CreatePlan(int vehicleId, Vehicle vehicle, PmsRule rule)
		{
			var plan = new MaintenancePlan
			{
				VehicleId = vehicleId,
				Type = rule.Type,
				Trigger = rule.Trigger,
				IntervalKilometers = rule.IntervalKilometers,
				IntervalMonths = rule.IntervalMonths
			};

			ApplyInitialDue(plan, vehicle);
			return plan;
		}

		public static void ApplyInitialDue(MaintenancePlan plan, Vehicle vehicle)
		{
			var enrolledOn = DateOnly.FromDateTime(DateTime.Today);

			plan.NextDueDate = plan.IntervalMonths is int months && months > 0
				? enrolledOn.AddMonths(months)
				: null;

			plan.NextDueOdometer = plan.IntervalKilometers is int km && km > 0
				? vehicle.Odometer + km
				: null;
		}

		public static void ApplyNextDue(MaintenancePlan plan, DateOnly completedOn, int odometer)
		{
			plan.LastCompletedDate = completedOn;
			plan.LastOdometer = odometer;

			plan.NextDueDate = plan.IntervalMonths is int months && months > 0
				? completedOn.AddMonths(months)
				: null;

			plan.NextDueOdometer = plan.IntervalKilometers is int km && km > 0
				? odometer + km
				: null;
		}

		public static void RecalculateDue(MaintenancePlan plan, Vehicle vehicle)
		{
			if (plan.LastCompletedDate != null)
			{
				ApplyNextDue(plan, plan.LastCompletedDate.Value, plan.LastOdometer ?? vehicle.Odometer);
				return;
			}

			ApplyInitialDue(plan, vehicle);
		}

		public static string FormatSchedule(MaintenancePlan plan)
			=> FormatSchedule(plan.IntervalKilometers, plan.IntervalMonths);

		public static string FormatSchedule(int? intervalKilometers, int? intervalMonths)
		{
			var parts = new List<string>();
			if (intervalKilometers is int km && km > 0)
			{
				parts.Add($"{km:N0}km");
			}

			if (intervalMonths is int months && months > 0)
			{
				parts.Add(months == 1 ? "1 month" : $"{months} months");
			}

			return parts.Count == 0 ? "—" : "Every " + string.Join("/", parts);
		}

		public static string FormatDue(MaintenancePlan plan)
		{
			var parts = new List<string>();
			if (plan.NextDueDate != null)
			{
				parts.Add(plan.NextDueDate.Value.ToString("MM-dd-yyyy"));
			}

			if (plan.NextDueOdometer != null)
			{
				parts.Add(plan.NextDueOdometer.Value.ToString("N0") + " km");
			}

			return parts.Count == 0 ? "—" : string.Join(" · ", parts);
		}

		public static PmsDueKind Classify(MaintenancePlan plan, Vehicle vehicle, bool inProgress)
		{
			if (inProgress)
			{
				return PmsDueKind.InProgress;
			}

			var today = DateOnly.FromDateTime(DateTime.Today);
			var odometer = vehicle.Odometer;

			var dateOverdue = plan.NextDueDate != null && plan.NextDueDate < today;
			var kmOverdue = plan.NextDueOdometer != null && odometer > plan.NextDueOdometer;

			if (IsMatch(plan.Trigger, dateOverdue, kmOverdue))
			{
				return PmsDueKind.Overdue;
			}

			var remainingKm = plan.NextDueOdometer != null
				? plan.NextDueOdometer.Value - odometer
				: (int?)null;
			var dateDueNow = plan.NextDueDate != null && plan.NextDueDate == today;
			var kmDueNow = remainingKm != null
				&& remainingKm >= 0
				&& remainingKm <= DueNowKilometers;

			if (IsMatch(plan.Trigger, dateDueNow, kmDueNow))
			{
				return PmsDueKind.DueNow;
			}

			var dateSoon = plan.NextDueDate != null
				&& plan.NextDueDate > today
				&& plan.NextDueDate <= today.AddDays(DueSoonDays);
			var kmSoon = plan.NextDueOdometer != null
				&& plan.NextDueOdometer > odometer
				&& plan.NextDueOdometer - odometer <= DueSoonKilometers;

			if (IsMatch(plan.Trigger, dateSoon, kmSoon))
			{
				return PmsDueKind.DueSoon;
			}

			return PmsDueKind.Upcoming;
		}

		public static bool CanStart(PmsDueKind kind)
			=> kind is PmsDueKind.Overdue or PmsDueKind.DueNow or PmsDueKind.DueSoon;

		private static bool IsMatch(PmsTrigger trigger, bool dateMatch, bool kmMatch)
			=> trigger switch
			{
				PmsTrigger.Time => dateMatch,
				PmsTrigger.Mileage => kmMatch,
				_ => dateMatch || kmMatch
			};
	}
}
