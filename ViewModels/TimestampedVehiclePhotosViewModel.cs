namespace EasyRent_Checking.ViewModels;

public class TimestampedVehiclePhotosViewModel
{
	public string GridId { get; set; } = "transitPhotoGrid";

	public string AddButtonId { get; set; } = "transitPhotoAddBtn";

	public string TemplateId { get; set; } = "transitPhotoCardTemplate";

	public string InputId { get; set; } = "preTripImageInput";

	public string InputName { get; set; } = "Transit.PreTripImageFile";

	public string InputListName { get; set; } = "PreTripImageFiles";

	public int MaxPhotos { get; set; } = 4;
}
