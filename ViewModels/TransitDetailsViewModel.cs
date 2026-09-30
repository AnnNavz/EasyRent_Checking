using EasyRent_Checking.Models;
using EasyRent_Checking.Services;

namespace EasyRent_Checking.ViewModels;

public class TransitDetailsViewModel
{
	public Transit PrimaryTransit { get; set; } = null!;

	public IList<Transit> AllTransits { get; set; } = new List<Transit>();

	public IList<RentalVehicle> RentalVehicles { get; set; } = new List<RentalVehicle>();

	public IList<TransitVehicleTripViewModel> VehicleTrips { get; set; } = new List<TransitVehicleTripViewModel>();

	public Payment? LatestPayment { get; set; }

	public TripVehicleAvailabilityResult? VehicleAvailabilityAlert { get; set; }

	public bool CanResolveBooking { get; set; }

	public IList<TransitIssueAlertViewModel> IssueAlerts { get; set; } = new List<TransitIssueAlertViewModel>();

	public bool CanDispatchAllTrips { get; set; }

	public bool HasBlockingIssue => IssueAlerts.Any(a => a.BlocksTrip);
}

public class TransitVehicleTripViewModel
{
	public Transit Transit { get; set; } = null!;

	public bool CanPrepareTrip { get; set; }

	public bool CanDispatchTrip { get; set; }

	public bool CanCompleteTrip { get; set; }

	public TripVehicleAvailabilityResult? VehicleAlert { get; set; }
}
