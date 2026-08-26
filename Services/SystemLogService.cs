using System.Security.Claims;
using EasyRent_Checking.Data;
using EasyRent_Checking.Models;
using Microsoft.AspNetCore.Http;

namespace EasyRent_Checking.Services
{
	public class SystemLogService
	{
		private readonly EasyRent_CheckingContext _context;
		private readonly IHttpContextAccessor _httpContextAccessor;

		public SystemLogService(EasyRent_CheckingContext context, IHttpContextAccessor httpContextAccessor)
		{
			_context = context;
			_httpContextAccessor = httpContextAccessor;
		}

		public void Record(
			SystemLogAction action,
			SystemLogCategory category,
			string summary,
			string? entityType = null,
			int? entityId = null)
		{
			var user = _httpContextAccessor.HttpContext?.User;
			int? actorId = null;
			var actorName = "Admin";

			if (user?.Identity?.IsAuthenticated == true)
			{
				if (int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
				{
					actorId = id;
				}

				actorName = user.Identity.Name
					?? user.FindFirstValue(ClaimTypes.Email)
					?? "Admin";
			}

			RecordAs(actorId, actorName, action, category, summary, entityType, entityId);
		}

		public void RecordAs(
			int? actorUserId,
			string? actorName,
			SystemLogAction action,
			SystemLogCategory category,
			string summary,
			string? entityType = null,
			int? entityId = null)
		{
			_context.SystemLogs.Add(new SystemLog
			{
				ActorUserId = actorUserId,
				ActorName = Truncate(string.IsNullOrWhiteSpace(actorName) ? "Admin" : actorName.Trim(), 100),
				Action = action,
				Category = category,
				EntityType = TruncateOptional(entityType, 40),
				EntityId = entityId,
				Summary = Truncate(string.IsNullOrWhiteSpace(summary) ? action.ToString() : summary.Trim(), 500),
				CreatedAt = DateTime.Now
			});
		}

		private static string Truncate(string value, int max)
			=> value.Length <= max ? value : value[..max];

		private static string? TruncateOptional(string? value, int max)
		{
			if (string.IsNullOrEmpty(value))
			{
				return value;
			}

			return Truncate(value, max);
		}
	}
}
