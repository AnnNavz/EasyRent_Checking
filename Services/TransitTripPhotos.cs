using System.Text.Json;
using EasyRent_Checking.Models;
using Microsoft.AspNetCore.Http;

namespace EasyRent_Checking.Services;

public static class TransitTripPhotos
{
	public const int MaxPhotos = 4;

	public static IReadOnlyList<string> GetPreTripPaths(Transit transit)
		=> ResolvePaths(transit.PreTripImagePathsJson, transit.PreTripImagePath);

	public static IReadOnlyList<string> GetPostTripPaths(Transit transit)
		=> ResolvePaths(transit.PostTripImagePathsJson, transit.PostTripImagePath);

	public static List<IFormFile> CollectFiles(HttpRequest request, string primaryFieldName, string extraFieldName)
	{
		var files = new List<IFormFile>();

		if (request.Form.Files[primaryFieldName] is { Length: > 0 } primary)
		{
			files.Add(primary);
		}

		foreach (var file in request.Form.Files.Where(f => f.Name == extraFieldName && f.Length > 0))
		{
			files.Add(file);
		}

		return files.Take(MaxPhotos).ToList();
	}

	public static string Serialize(IEnumerable<string> paths)
	{
		var list = paths
			.Where(p => !string.IsNullOrWhiteSpace(p))
			.Select(p => p.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Take(MaxPhotos)
			.ToList();

		return list.Count == 0 ? string.Empty : JsonSerializer.Serialize(list);
	}

	private static IReadOnlyList<string> ResolvePaths(string? json, string? legacyPath)
	{
		var fromJson = Deserialize(json);
		if (fromJson.Count > 0)
		{
			return fromJson;
		}

		if (!string.IsNullOrWhiteSpace(legacyPath))
		{
			return [legacyPath.Trim()];
		}

		return [];
	}

	private static List<string> Deserialize(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return [];
		}

		try
		{
			return JsonSerializer.Deserialize<List<string>>(json)?
				.Where(p => !string.IsNullOrWhiteSpace(p))
				.Select(p => p.Trim())
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.Take(MaxPhotos)
				.ToList() ?? [];
		}
		catch (JsonException)
		{
			return [];
		}
	}
}
