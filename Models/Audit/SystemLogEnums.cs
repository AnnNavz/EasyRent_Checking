using System.ComponentModel.DataAnnotations;

namespace EasyRent_Checking.Models
{
	public enum SystemLogAction
	{
		Created,
		Updated,
		Approved,
		Rejected,
		Deactivated,
		Reactivated,
		Deleted,
		Assigned,
		Started,
		Completed,
		Cancelled,
		Closed,
		Dismissed
	}

	public enum SystemLogCategory
	{
		Customer,
		Driver,
		Vehicle,
		Booking,
		Trip,
		Maintenance,
		Incident,
		Admin
	}
}
