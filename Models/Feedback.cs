using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyRent_Checking.Models
{
	public class Feedback
	{
		[Key]
		public int FeedbackId { get; set; }

		[Required]
		[Display(Name = "Transit")]
		public int TransitID { get; set; }

		[ForeignKey(nameof(TransitID))]
		public Transit? Transit { get; set; }

		[Required]
		[Display(Name = "Customer")]
		public int CustomerId { get; set; }

		[ForeignKey(nameof(CustomerId))]
		public CustomerProfile? Customer { get; set; }

		[Required(ErrorMessage = "Please select a star rating.")]
		[Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars.")]
		[Display(Name = "Rating")]
		public int Rating { get; set; }

		[StringLength(1000, ErrorMessage = "Comment cannot exceed 1000 characters.")]
		[DataType(DataType.MultilineText)]
		[Display(Name = "Comment")]
		public string? Comment { get; set; }

		[Display(Name = "Submitted At")]
		public DateTime CreatedAt { get; set; } = DateTime.Now;
	}
}
