namespace EasyRent_Checking.Services
{
	public static class ImageStorage
	{
		public const string DriversFolder = "Drivers";
		public const string VehiclesFolder = "Vehicles";
		public const string CustomersFolder = "Customers";
		public const string ReservationsFolder = "Reservations";
		public const string SystemImagesFolder = "System Images";

		public static string SystemImageUrl(string fileName)
			=> $"/images/{SystemImagesFolder}/{fileName}";

		public static async Task<string> SaveAsync(IWebHostEnvironment env, IFormFile file, string relativeFolder)
		{
			ArgumentNullException.ThrowIfNull(file);

			var folderParts = relativeFolder
				.Replace('\\', '/')
				.Split('/', StringSplitOptions.RemoveEmptyEntries);

			string folder = Path.Combine(new[] { env.WebRootPath, "images" }.Concat(folderParts).ToArray());
			Directory.CreateDirectory(folder);

			string safeName = Path.GetFileName(file.FileName);
			string fileName = $"{Guid.NewGuid():N}_{safeName}";
			string filePath = Path.Combine(folder, fileName);

			await using (var stream = new FileStream(filePath, FileMode.Create))
			{
				await file.CopyToAsync(stream);
			}

			return string.Join('/', folderParts.Append(fileName));
		}
	}
}
