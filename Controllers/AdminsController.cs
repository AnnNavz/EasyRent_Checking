using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Data;
using EasyRent_Checking.Models;
using EasyRent_Checking.ViewModels;
using EasyRent_Checking.Services;

namespace EasyRent_Checking.Controllers
{
	/// <summary>CRUD for admin user accounts.</summary>
	[Authorize(Policy = "AdminOnly")]
	[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
	public class AdminsController : Controller
	{
		private readonly EasyRent_CheckingContext _context;
		private readonly SystemLogService _logs;

		public AdminsController(EasyRent_CheckingContext context, SystemLogService logs)
		{
			_context = context;
			_logs = logs;
		}

		public async Task<IActionResult> Index()
		{
			var admins = await _context.AdminProfiles
				.Include(a => a.User)
				.ToListAsync();
			return View(admins);
		}

		[AllowAnonymous]
		public async Task<IActionResult> Create()
		{
			if (await RejectIfAdminSetupCompleteAsync())
			{
				return User.Identity?.IsAuthenticated == true ? Forbid() : Challenge();
			}

			return View(new AdminAccountInputModel());
		}

		[HttpPost]
		[AllowAnonymous]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create([Bind("FullName,Email,Password,ConfirmPassword")] AdminAccountInputModel model)
		{
			if (await RejectIfAdminSetupCompleteAsync())
			{
				return User.Identity?.IsAuthenticated == true ? Forbid() : Challenge();
			}

			if (!string.IsNullOrWhiteSpace(model.Email))
			{
				var emailExists = await _context.Users.AnyAsync(u => u.Email == model.Email);
				if (emailExists)
				{
					ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
				}
			}

			if (!ModelState.IsValid)
			{
				return View(model);
			}

			var user = new User
			{
				Email = model.Email.Trim(),
				Role = UserRole.Admin,
				CreatedAt = DateTime.Now,
				EmailConfirmed = true
			};
			user.SetPassword(model.Password);

			_context.Users.Add(user);
			await _context.SaveChangesAsync();

			var profile = new AdminProfile
			{
				AdminId = user.UserId,
				FullName = model.FullName.Trim()
			};

			_context.AdminProfiles.Add(profile);
			await _context.SaveChangesAsync();
			_logs.Record(
				SystemLogAction.Created,
				SystemLogCategory.Admin,
				$"Created admin account {profile.FullName}.",
				"Admin",
				profile.AdminId);
			await _context.SaveChangesAsync();

			return RedirectToAction(nameof(Index));
		}

		private async Task<bool> RejectIfAdminSetupCompleteAsync()
		{
			if (User.IsInRole(nameof(UserRole.Admin)))
			{
				return false;
			}

			return await _context.Users.AnyAsync(u => u.Role == UserRole.Admin);
		}
	}
}
