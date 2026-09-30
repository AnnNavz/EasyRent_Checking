using EasyRent_Checking.Models;

namespace EasyRent_Checking.Services;

public static class TransitPreparation
{
	public static bool IsPrepared(Transit transit)
		=> transit.DriverID != null
			&& transit.DepartureTime != null
			&& transit.FuelLevelStart != null
			&& transit.OdometerStart != null;
}
