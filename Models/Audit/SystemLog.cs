using System.ComponentModel.DataAnnotations;

namespace EasyRent_Checking.Models
{
	public class SystemLog
	{
		[Key]
		public int SystemLogId { get; set; }

		[Display(Name = "Admin")]
		public int? ActorUserId { get; set; }

		[Required]
		[StringLength(100)]
		[Display(Name = "Admin")]
		public string ActorName { get; set; } = "Admin";

		[Required]
		[Display(Name = "Action")]
		public SystemLogAction Action { get; set; }

		[Required]
		[Display(Name = "Category")]
		public SystemLogCategory Category { get; set; }

		[StringLength(40)]
		public string? EntityType { get; set; }

		public int? EntityId { get; set; }

		[Required]
		[StringLength(500)]
		[Display(Name = "Details")]
		public string Summary { get; set; } = string.Empty;

		[Required]
		[Display(Name = "When")]
		public DateTime CreatedAt { get; set; } = DateTime.Now;
	}
}
