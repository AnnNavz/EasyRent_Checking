using EasyRent_Checking.Models;

namespace EasyRent_Checking.ViewModels
{
	public class DashboardViewModel
	{
		public decimal MonthlyRevenue { get; set; }
		public decimal RevenueChange { get; set; }
		public int ActiveTrips { get; set; }
		public decimal ActiveTripsChange { get; set; }
		public int BookingRequests { get; set; }
		public decimal BookingRequestsChange { get; set; }
		public int TotalBookings { get; set; }
		public decimal TotalBookingsChange { get; set; }

		public string Period { get; set; } = "week";
		public string PeriodLabel { get; set; } = "This Week";
		public string RangeLabel { get; set; } = "";

		public IList<string> TrendLabels { get; set; } = new List<string>();
		public IList<int> TrendValues { get; set; } = new List<int>();

		public int FleetRented { get; set; }
		public int FleetAvailable { get; set; }
		public int FleetMaintenance { get; set; }
		public int TotalFleet { get; set; }

		public IList<string> FleetTypeLabels { get; set; } = new List<string>();
		public IList<int> FleetTypeValues { get; set; } = new List<int>();

		public int MaintOverdue { get; set; }
		public int MaintDueNow { get; set; }
		public int MaintDueSoon { get; set; }
		public int MaintInProgress { get; set; }
		public int MaintUpToDate { get; set; }
		public int MaintDueBadge => MaintOverdue + MaintDueNow + MaintDueSoon;

		public IList<DashboardMaintenanceRow> MaintenanceRows { get; set; } = new List<DashboardMaintenanceRow>();

		public decimal MaintenanceCostTotal { get; set; }
		public IList<DashboardCostSlice> MaintenanceCosts { get; set; } = new List<DashboardCostSlice>();

		public int IncidentNew { get; set; }
		public int IncidentInProgress { get; set; }
		public int IncidentClosed { get; set; }
	}

	public class DashboardMaintenanceRow
	{
		public int VehicleId { get; set; }
		public string VehicleName { get; set; } = "";
		public string PlateNumber { get; set; } = "";
		public string? ImagePath { get; set; }
		public string CheckupSummary { get; set; } = "";
		public string NextDue { get; set; } = "";
		public PmsDueKind Status { get; set; }
	}

	public class DashboardCostSlice
	{
		public string Label { get; set; } = "";
		public decimal Amount { get; set; }
		public decimal Percent { get; set; }
		public string Color { get; set; } = "#1E3A8A";
	}
}
