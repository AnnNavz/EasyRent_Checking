using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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
		private readonly IWebHostEnvironment _webHostEnvironment;

		public AdminsController(
			EasyRent_CheckingContext context,
			SystemLogService logs,
			IWebHostEnvironment webHostEnvironment)
		{
			_context = context;
			_logs = logs;
			_webHostEnvironment = webHostEnvironment;
		}

		public async Task<IActionResult> Index(string searchString, string sortBy, int? page)
		{
			const int pageSize = 10;
			var pageNumber = page.GetValueOrDefault(1);
			if (pageNumber < 1)
			{
				pageNumber = 1;
			}

			ViewData["Title"] = "Admin Management";
			ViewData["ActivePage"] = "Admins";
			ViewData["CurrentSearch"] = searchString;
			ViewData["CurrentSort"] = sortBy;
			ViewData["CurrentAdminId"] = CurrentUserId();

			var adminsQuery = _context.AdminProfiles
				.Include(a => a.User)
				.AsQueryable();

			var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
			var lastMonthStart = monthStart.AddMonths(-1);
			var totalAdminsCount = await adminsQuery.CountAsync();
			var addedThisMonth = await adminsQuery.CountAsync(a => a.User != null && a.User.CreatedAt >= monthStart);
			var totalAtMonthStart = await adminsQuery.CountAsync(a => a.User != null && a.User.CreatedAt < monthStart);
			var addedLastMonth = await adminsQuery.CountAsync(a =>
				a.User != null && a.User.CreatedAt >= lastMonthStart && a.User.CreatedAt < monthStart);

			ViewData["TotalAdminsCount"] = totalAdminsCount;
			ViewData["AddedThisMonthCount"] = addedThisMonth;
			ViewData["TotalAdminsChange"] = PctChange(totalAdminsCount, totalAtMonthStart);
			ViewData["AddedThisMonthChange"] = PctChange(addedThisMonth, addedLastMonth);
			ViewData["CanDeleteAdmins"] = totalAdminsCount > 1;

			if (!string.IsNullOrEmpty(searchString))
			{
				var term = searchString.Trim();
				adminsQuery = adminsQuery.Where(a =>
					a.FullName.Contains(term)
					|| (a.User != null && a.User.Email.Contains(term)));
			}

			adminsQuery = sortBy switch
			{
				"Name" => adminsQuery.OrderBy(a => a.FullName),
				"Email" => adminsQuery.OrderBy(a => a.User!.Email),
				_ => adminsQuery.OrderByDescending(a => a.AdminId)
			};

			var totalCount = await adminsQuery.CountAsync();
			var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
			if (pageNumber > totalPages)
			{
				pageNumber = totalPages;
			}

			ViewData["PageIndex"] = pageNumber;
			ViewData["TotalPages"] = totalPages;
			ViewData["TotalCount"] = totalCount;
			ViewData["PageSize"] = pageSize;

			var admins = await adminsQuery
				.Skip((pageNumber - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();

			return View(admins);
		}

		public async Task<IActionResult> Details(int? adminid)
		{
			if (adminid == null)
			{
				return NotFound();
			}

			var admin = await _context.AdminProfiles
				.Include(a => a.User)
				.FirstOrDefaultAsync(a => a.AdminId == adminid);
			if (admin == null)
			{
				return NotFound();
			}

			ViewData["CurrentAdminId"] = CurrentUserId();
			ViewData["CanDeleteAdmins"] = await _context.AdminProfiles.CountAsync() > 1;
			return View(admin);
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

			TempData["SuccessMessage"] = $"Admin account for {profile.FullName} was created.";
			return RedirectToAction(nameof(Index));
		}

		[HttpPost, ActionName("Delete")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteConfirmed(int? adminid)
		{
			var profile = await _context.AdminProfiles
				.Include(a => a.User)
				.FirstOrDefaultAsync(a => a.AdminId == adminid);

			if (profile?.User == null)
			{
				return RedirectToAction(nameof(Index));
			}

			if (profile.AdminId == CurrentUserId())
			{
				TempData["ErrorMessage"] = "You cannot delete the account you are signed in with.";
				return RedirectToAction(nameof(Index));
			}

			if (await _context.AdminProfiles.CountAsync() <= 1)
			{
				TempData["ErrorMessage"] = "The last admin account cannot be deleted.";
				return RedirectToAction(nameof(Index));
			}

			_logs.Record(
				SystemLogAction.Deleted,
				SystemLogCategory.Admin,
				$"Deleted admin account {profile.FullName}.",
				"Admin",
				profile.AdminId);
			_context.Users.Remove(profile.User);
			await _context.SaveChangesAsync();

			TempData["SuccessMessage"] = $"Admin account for {profile.FullName} was deleted.";
			return RedirectToAction(nameof(Index));
		}

		public async Task<IActionResult> Settings(string? tab, string? edit)
		{
			var admin = await GetLoggedInAdminAsync();
			if (admin?.AdminProfile == null)
			{
				return RedirectToAction("Index", "Dashboard");
			}

			return View(BuildSettingsViewModel(admin, admin.AdminProfile, tab, edit));
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UpdateFullName(string fullName)
		{
			var admin = await GetLoggedInAdminAsync();
			var profile = admin?.AdminProfile;
			if (admin == null || profile == null)
			{
				return RedirectToAction("Index", "Dashboard");
			}

			if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length > 100)
			{
				TempData["ErrorMessage"] = "Full name is required and cannot exceed 100 characters.";
				return RedirectToSettings("profile", "name");
			}

			profile.FullName = fullName.Trim();
			_logs.Record(
				SystemLogAction.Updated,
				SystemLogCategory.Admin,
				$"Updated admin name to {profile.FullName}.",
				"Admin",
				profile.AdminId);
			await _context.SaveChangesAsync();
			await SignInAdminAsync(admin, profile.FullName);
			TempData["SuccessMessage"] = "Your name has been updated.";
			return RedirectToSettings("profile");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UpdateEmail(string email)
		{
			var admin = await GetLoggedInAdminAsync();
			var profile = admin?.AdminProfile;
			if (admin == null || profile == null)
			{
				return RedirectToAction("Index", "Dashboard");
			}

			email = (email ?? string.Empty).Trim();
			if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
			{
				TempData["ErrorMessage"] = "Please enter a valid email address.";
				return RedirectToSettings("profile", "email");
			}

			var emailExists = await _context.Users
				.AnyAsync(u => u.Email == email && u.UserId != admin.UserId);
			if (emailExists)
			{
				TempData["ErrorMessage"] = "An account with this email already exists.";
				return RedirectToSettings("profile", "email");
			}

			admin.Email = email;
			_logs.Record(
				SystemLogAction.Updated,
				SystemLogCategory.Admin,
				$"Updated admin email for {profile.FullName}.",
				"Admin",
				profile.AdminId);
			await _context.SaveChangesAsync();
			await SignInAdminAsync(admin, profile.FullName);
			TempData["SuccessMessage"] = "Your email has been updated.";
			return RedirectToSettings("profile");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UpdateProfileImage(IFormFile? profileImageFile)
		{
			var admin = await GetLoggedInAdminAsync();
			var profile = admin?.AdminProfile;
			if (admin == null || profile == null)
			{
				return RedirectToAction("Index", "Dashboard");
			}

			if (profileImageFile == null || profileImageFile.Length == 0)
			{
				TempData["ErrorMessage"] = "Please choose a profile photo to upload.";
				return RedirectToSettings("profile");
			}

			if (profileImageFile.Length > 5 * 1024 * 1024)
			{
				TempData["ErrorMessage"] = "Profile photo must be a JPG, PNG, or WEBP image under 5 MB.";
				return RedirectToSettings("profile");
			}

			var contentType = (profileImageFile.ContentType ?? string.Empty).ToLowerInvariant();
			var extension = Path.GetExtension(profileImageFile.FileName)?.ToLowerInvariant();
			var typeOk = contentType is "image/jpeg" or "image/jpg" or "image/png" or "image/webp";
			var extensionOk = extension is ".jpg" or ".jpeg" or ".png" or ".webp";
			if (!typeOk && !extensionOk)
			{
				TempData["ErrorMessage"] = "Profile photo must be a JPG, PNG, or WEBP image under 5 MB.";
				return RedirectToSettings("profile");
			}

			profile.ProfileImagePath = await ImageStorage.SaveAsync(
				_webHostEnvironment,
				profileImageFile,
				ImageStorage.AdminsFolder);

			_logs.Record(
				SystemLogAction.Updated,
				SystemLogCategory.Admin,
				"Updated admin profile photo.",
				"Admin",
				profile.AdminId);
			await _context.SaveChangesAsync();
			TempData["SuccessMessage"] = "Your profile photo has been updated.";
			return RedirectToSettings("profile");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ChangePassword([Bind(Prefix = "Password")] ChangePasswordInputModel model)
		{
			var admin = await GetLoggedInAdminAsync();
			var profile = admin?.AdminProfile;
			if (admin == null || profile == null)
			{
				return RedirectToAction("Index", "Dashboard");
			}

			if (!ModelState.IsValid)
			{
				var viewModel = BuildSettingsViewModel(admin, profile, "password", null);
				viewModel.Password = model;
				return View(nameof(Settings), viewModel);
			}

			if (!admin.VerifyPassword(model.CurrentPassword))
			{
				ModelState.AddModelError("Password.CurrentPassword", "Current password is incorrect.");
				var viewModel = BuildSettingsViewModel(admin, profile, "password", null);
				viewModel.Password = model;
				return View(nameof(Settings), viewModel);
			}

			admin.SetPassword(model.NewPassword);
			_logs.Record(
				SystemLogAction.Updated,
				SystemLogCategory.Admin,
				$"Changed password for admin {profile.FullName}.",
				"Admin",
				profile.AdminId);
			await _context.SaveChangesAsync();
			TempData["SuccessMessage"] = "Your password has been updated.";
			return RedirectToSettings("password");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UpdateMfa(bool enabled)
		{
			var admin = await GetLoggedInAdminAsync();
			var profile = admin?.AdminProfile;
			if (admin == null || profile == null)
			{
				return RedirectToAction("Index", "Dashboard");
			}

			admin.LoginMfaEnabled = enabled;
			if (!enabled)
			{
				admin.ClearLoginOtp();
			}

			_logs.Record(
				SystemLogAction.Updated,
				SystemLogCategory.Admin,
				enabled
					? $"Turned on email sign-in codes for {profile.FullName}."
					: $"Turned off email sign-in codes for {profile.FullName}.",
				"Admin",
				profile.AdminId);
			await _context.SaveChangesAsync();
			TempData["SuccessMessage"] = enabled
				? "Email sign-in codes are now on. We'll send a code after your password."
				: "Email sign-in codes are now off.";
			return RedirectToSettings("security");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeactivateAccount()
		{
			var admin = await GetLoggedInAdminAsync();
			var profile = admin?.AdminProfile;
			if (admin == null || profile == null)
			{
				return RedirectToAction("Index", "Dashboard");
			}

			if (!await HasOtherActiveAdminsAsync(profile.AdminId))
			{
				TempData["ErrorMessage"] = "The last active admin account cannot be deactivated.";
				return RedirectToSettings("security");
			}

			profile.IsSelfDeactivated = true;
			_logs.Record(
				SystemLogAction.Deactivated,
				SystemLogCategory.Admin,
				$"Deactivated admin account {profile.FullName}.",
				"Admin",
				profile.AdminId);
			await _context.SaveChangesAsync();
			ClearLoginMfaCookie();
			await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
			TempData["SuccessMessage"] = "Your account is deactivated. Sign in again anytime to reactivate it.";
			return RedirectToAction("Login", "Account");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteAccount()
		{
			var admin = await GetLoggedInAdminAsync();
			var profile = admin?.AdminProfile;
			if (admin == null || profile == null)
			{
				return RedirectToAction("Index", "Dashboard");
			}

			if (!await HasOtherActiveAdminsAsync(profile.AdminId))
			{
				TempData["ErrorMessage"] = "The last active admin account cannot be deleted.";
				return RedirectToSettings("security");
			}

			_logs.Record(
				SystemLogAction.Deleted,
				SystemLogCategory.Admin,
				$"Deleted admin account {profile.FullName}.",
				"Admin",
				profile.AdminId);
			_context.Users.Remove(admin);
			await _context.SaveChangesAsync();
			ClearLoginMfaCookie();
			await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
			TempData["SuccessMessage"] = "Your account has been deleted.";
			return RedirectToAction("Login", "Account");
		}

		private async Task<User?> GetLoggedInAdminAsync()
		{
			var userId = CurrentUserId();
			if (userId == null)
			{
				return null;
			}

			return await _context.Users
				.Include(u => u.AdminProfile)
				.FirstOrDefaultAsync(u => u.UserId == userId && u.Role == UserRole.Admin);
		}

		private AccountSettingsViewModel BuildSettingsViewModel(
			User user,
			AdminProfile profile,
			string? tab,
			string? editField)
		{
			var normalizedTab = tab switch
			{
				"password" => "password",
				"security" => "security",
				_ => "profile"
			};

			return new AccountSettingsViewModel
			{
				Tab = normalizedTab,
				EditField = editField,
				FullName = profile.FullName,
				Email = user.Email,
				LoginMfaEnabled = user.LoginMfaEnabled,
				AvatarInitials = GetInitials(profile.FullName),
				ProfileImagePath = profile.ProfileImagePath,
				MemberSince = user.CreatedAt,
				CanCloseAccount = _context.AdminProfiles.Any(a => a.AdminId != profile.AdminId && !a.IsSelfDeactivated)
			};
		}

		private IActionResult RedirectToSettings(string tab, string? edit = null)
			=> RedirectToAction(nameof(Settings), new { tab, edit });

		private async Task SignInAdminAsync(User user, string displayName)
		{
			var claims = new List<Claim>
			{
				new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
				new(ClaimTypes.Name, displayName),
				new(ClaimTypes.Email, user.Email),
				new(ClaimTypes.Role, user.Role.ToString())
			};

			var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
			var principal = new ClaimsPrincipal(identity);
			await HttpContext.SignInAsync(
				CookieAuthenticationDefaults.AuthenticationScheme,
				principal,
				new AuthenticationProperties
				{
					IsPersistent = false,
					ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
				});
		}

		private static string GetInitials(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				return "AD";
			}

			var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length == 1)
			{
				return parts[0].Length >= 2
					? parts[0][..2].ToUpperInvariant()
					: parts[0].ToUpperInvariant();
			}

			return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
		}

		private async Task<bool> HasOtherActiveAdminsAsync(int adminId)
			=> await _context.AdminProfiles.AnyAsync(a => a.AdminId != adminId && !a.IsSelfDeactivated);

		private void ClearLoginMfaCookie()
		{
			Response.Cookies.Delete("EasyRent.LoginMfa", new CookieOptions
			{
				Path = "/",
				Secure = Request.IsHttps,
				SameSite = SameSiteMode.Lax
			});
		}

		private async Task<bool> RejectIfAdminSetupCompleteAsync()
		{
			if (User.IsInRole(nameof(UserRole.Admin)))
			{
				return false;
			}

			return await _context.Users.AnyAsync(u => u.Role == UserRole.Admin);
		}

		private int? CurrentUserId()
		{
			if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
			{
				return id;
			}

			return null;
		}

		private static decimal PctChange(decimal current, decimal previous)
			=> Math.Round((current - previous) * 0.1m, 1);
	}
}
