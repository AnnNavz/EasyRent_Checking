using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Data;
using EasyRent_Checking.Models;

namespace EasyRent_Checking.Controllers
{
	[Authorize(Policy = "StaffArea")]
	[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
	public class SystemLogsController : Controller
	{
		private readonly EasyRent_CheckingContext _context;

		public SystemLogsController(EasyRent_CheckingContext context)
		{
			_context = context;
		}

		public async Task<IActionResult> Index(string searchString, string currentFilter, string actionFilter, string sortBy, int? page)
		{
			const int pageSize = 10;
			var pageNumber = page.GetValueOrDefault(1);
			if (pageNumber < 1)
			{
				pageNumber = 1;
			}

			ViewData["Title"] = "Activity Logs";
			ViewData["ActivePage"] = "SystemLogs";
			ViewData["CurrentSearch"] = searchString;
			ViewData["CurrentFilter"] = currentFilter;
			ViewData["CurrentAction"] = actionFilter;
			ViewData["CurrentSort"] = sortBy;

			var query = _context.SystemLogs.AsNoTracking().AsQueryable();

			if (!string.IsNullOrWhiteSpace(searchString))
			{
				var term = searchString.Trim();
				query = query.Where(l =>
					l.Summary.Contains(term)
					|| l.ActorName.Contains(term)
					|| (l.EntityType != null && l.EntityType.Contains(term)));
			}

			if (!string.IsNullOrWhiteSpace(currentFilter)
				&& Enum.TryParse(currentFilter, true, out SystemLogCategory category))
			{
				query = query.Where(l => l.Category == category);
			}

			if (!string.IsNullOrWhiteSpace(actionFilter)
				&& Enum.TryParse(actionFilter, true, out SystemLogAction action))
			{
				query = query.Where(l => l.Action == action);
			}

			query = sortBy switch
			{
				"DateOldest" => query.OrderBy(l => l.CreatedAt).ThenBy(l => l.SystemLogId),
				"User" => query.OrderBy(l => l.ActorName).ThenByDescending(l => l.CreatedAt),
				"Action" => query.OrderBy(l => l.Action).ThenByDescending(l => l.CreatedAt),
				_ => query.OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.SystemLogId)
			};

			var totalCount = await query.CountAsync();
			var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
			if (pageNumber > totalPages)
			{
				pageNumber = totalPages;
			}

			ViewData["PageIndex"] = pageNumber;
			ViewData["TotalPages"] = totalPages;
			ViewData["TotalCount"] = totalCount;
			ViewData["PageSize"] = pageSize;

			var logs = await query
				.Skip((pageNumber - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();

			return View(logs);
		}
	}
}
