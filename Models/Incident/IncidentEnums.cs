using System.ComponentModel.DataAnnotations;

namespace EasyRent_Checking.Models
{
	public enum IncidentType
	{
		Accident,
		Damage,
		Breakdown,
		Theft,
		Other
	}

	public enum IncidentSeverity
	{
		Minor,
		Moderate,
		Major
	}

	public enum IncidentStatus
	{
		Reported,
		[Display(Name = "Under Review")]
		UnderReview,
		[Display(Name = "In Repair")]
		InRepair,
		Closed,
		Dismissed
	}
}
