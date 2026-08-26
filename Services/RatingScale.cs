namespace EasyRent_Checking.Services
{
	public static class RatingScale
	{
		public const int MaxStars = 5;

		public static double ToTen(double stars)
			=> Math.Round(stars * 2.0, 1, MidpointRounding.AwayFromZero);

		public static double ToTen(IEnumerable<int> stars)
		{
			var values = stars as IList<int> ?? stars.ToList();
			if (values.Count == 0)
			{
				return 0d;
			}

			return ToTen(values.Average());
		}

		public static double FillPercentFromTen(double scoreOutOfTen)
			=> Math.Clamp(scoreOutOfTen, 0d, 10d) * 10d;

		public static int OverallStars(int first, int second, int third)
			=> (int)Math.Round((first + second + third) / 3.0, MidpointRounding.AwayFromZero);
	}
}
