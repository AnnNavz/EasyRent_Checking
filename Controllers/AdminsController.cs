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
	/// <summary>CRUD for admin/staff accounts. Account settings are available to both roles.</summary>
	[Authorize(Policy = "StaffArea")]
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

		[Authorize(Policy = "AdminOnly")]
		public async Task<IActionResult> Index(string searchString, string sortBy, string staffSortBy, int? page, int? staffPage)
		{
			const int pageSize = 10;
			var pageNumber = page.GetValueOrDefault(1);
			if (pageNumber < 1)
			{
				pageNumber = 1;
			}

			ViewData["Title"] = "Admin & Staff Management";
			ViewData["ActivePage"] = "Admins";
			ViewData["CurrentSearch"] = searchString;
			ViewData["CurrentSort"] = sortBy;
			ViewData["CurrentStaffSort"] = staffSortBy;
			ViewData["CurrentAdminId"] = CurrentUserId();

			var adminsQuery = _context.Users.Where(u => u.Role == UserRole.Admin);
			var staffQuery = _context.Users.Where(u => u.Role == UserRole.Staff);

			var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
			var lastMonthStart = monthStart.AddMonths(-1);
			var totalAdminsCount = await adminsQuery.CountAsync();
			var addedThisMonth = await adminsQuery.CountAsync(u => u.CreatedAt >= monthStart);
			var totalAtMonthStart = await adminsQuery.CountAsync(u => u.CreatedAt < monthStart);
			var addedLastMonth = await adminsQuery.CountAsync(u =>
				u.CreatedAt >= lastMonthStart && u.CreatedAt < monthStart);

			ViewData["TotalAdminsCount"] = totalAdminsCount;
			ViewData["AddedThisMonthCount"] = addedThisMonth;
			ViewData["TotalAdminsChange"] = PctChange(totalAdminsCount, totalAtMonthStart);
			ViewData["AddedThisMonthChange"] = PctChange(addedThisMonth, addedLastMonth);
			ViewData["CanDeleteAdmins"] = totalAdminsCount > 1;
			ViewData["TotalStaffCount"] = await staffQuery.CountAsync();

			if (!string.IsNullOrEmpty(searchString))
			{
				var term = searchString.Trim();
				adminsQuery = adminsQuery.Where(u =>
					u.FullName.Contains(term)
					|| u.ContactNumber.Contains(term)
					|| (u.Address != null && u.Address.Contains(term))
					|| u.Email.Contains(term));
				staffQuery = staffQuery.Where(u =>
					u.FullName.Contains(term)
					|| u.ContactNumber.Contains(term)
					|| (u.Address != null && u.Address.Contains(term))
					|| u.Email.Contains(term));
			}

			adminsQuery = sortBy switch
			{
				"Name" => adminsQuery.OrderBy(u => u.FullName),
				"Email" => adminsQuery.OrderBy(u => u.Email),
				_ => adminsQuery.OrderByDescending(u => u.UserId)
			};
			staffQuery = staffSortBy switch
			{
				"Name" => staffQuery.OrderBy(u => u.FullName),
				"Email" => staffQuery.OrderBy(u => u.Email),
				_ => staffQuery.OrderByDescending(u => u.UserId)
			};

			var totalCount = await adminsQuery.CountAsync();
			var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
			if (pageNumber > totalPages)
			{
				pageNumber = totalPages;
			}

			var staffPageNumber = staffPage.GetValueOrDefault(1);
			if (staffPageNumber < 1)
			{
				staffPageNumber = 1;
			}
			var staffTotalCount = await staffQuery.CountAsync();
			var staffTotalPages = staffTotalCount == 0 ? 1 : (int)Math.Ceiling(staffTotalCount / (double)pageSize);
			if (staffPageNumber > staffTotalPages)
			{
				staffPageNumber = staffTotalPages;
			}

			ViewData["PageIndex"] = pageNumber;
			ViewData["TotalPages"] = totalPages;
			ViewData["TotalCount"] = totalCount;
			ViewData["PageSize"] = pageSize;
			ViewData["StaffPageIndex"] = staffPageNumber;
			ViewData["StaffTotalPages"] = staffTotalPages;
			ViewData["StaffTotalCount"] = staffTotalCount;

			var admins = await adminsQuery
				.Skip((pageNumber - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();
			ViewData["StaffList"] = await staffQuery
				.Skip((staffPageNumber - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();

			return View(admins);
		}

		[Authorize(Policy = "AdminOnly")]
		public async Task<IActionResult> Details(int? adminid)
		{
			if (adminid == null)
			{
				return NotFound();
			}

			var member = await FindAdminOrStaffAsync(adminid.Value);
			if (member == null)
			{
				return NotFound();
			}

			ViewData["CurrentAdminId"] = CurrentUserId();
			ViewData["CanDeleteAdmins"] = await AdminRoleCountAsync() > 1;
			return View(AdminStaffMemberViewModel.FromUser(member));
		}

		[Authorize(Policy = "AdminOnly")]
		public async Task<IActionResult> Edit(int? adminid)
		{
			if (adminid == null)
			{
				return NotFound();
			}

			if (adminid == CurrentUserId())
			{
				TempData["ErrorMessage"] = "Use Account Settings to edit your own profile.";
				return RedirectToAction(nameof(Settings));
			}

			var member = await FindAdminOrStaffAsync(adminid.Value);
			if (member == null)
			{
				return NotFound();
			}

			return View(AdminAccountInputModel.FromUser(member));
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		[Authorize(Policy = "AdminOnly")]
		public async Task<IActionResult> Edit(int? adminid, [Bind("AdminId,FullName,Email,ContactNumber,Address,Password,ConfirmPassword,Role")] AdminAccountInputModel model)
		{
			if (adminid != model.AdminId)
			{
				return NotFound();
			}

			if (adminid == CurrentUserId())
			{
				TempData["ErrorMessage"] = "Use Account Settings to edit your own profile.";
				return RedirectToAction(nameof(Settings));
			}

			if (string.IsNullOrWhiteSpace(model.Password))
			{
				ModelState.Remove(nameof(model.Password));
				ModelState.Remove(nameof(model.ConfirmPassword));
			}

			NormalizeProfileContact(model);

			if (!string.IsNullOrWhiteSpace(model.Email))
			{
				var emailExists = await _context.Users
					.AnyAsync(u => u.Email == model.Email.Trim() && u.UserId != model.AdminId);
				if (emailExists)
				{
					ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
				}
			}

			if (!ModelState.IsValid)
			{
				return View(model);
			}

			var member = await FindAdminOrStaffAsync(model.AdminId);
			if (member == null)
			{
				return NotFound();
			}

			member.FullName = model.FullName.Trim();
			member.ContactNumber = model.ContactNumber.Trim();
			member.Address = model.Address.Trim();
			member.Email = model.Email.Trim();
			if (!string.IsNullOrWhiteSpace(model.Password))
			{
				member.SetPassword(model.Password);
			}

			var roleLabel = RoleLabel(member.Role);
			_logs.Record(
				SystemLogAction.Updated,
				SystemLogCategory.Admin,
				$"Updated {roleLabel.ToLowerInvariant()} account {member.FullName}.",
				"Admin",
				member.UserId);
			await _context.SaveChangesAsync();

			TempData["SuccessMessage"] = $"{roleLabel} account for {member.FullName} was updated.";
			return RedirectToAction(nameof(Details), new { adminid = member.UserId });
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
		public async Task<IActionResult> Create([Bind("FullName,Email,ContactNumber,Address,Password,ConfirmPassword")] AdminAccountInputModel model)
		{
			if (await RejectIfAdminSetupCompleteAsync())
			{
				return User.Identity?.IsAuthenticated == true ? Forbid() : Challenge();
			}

			var user = await CreateAccountAsync(model, UserRole.Admin);
			if (user == null)
			{
				return View(model);
			}

			TempData["SuccessMessage"] = $"Admin account for {user.FullName} was created.";
			return RedirectToAction(nameof(Index));
		}

		[Authorize(Policy = "AdminOnly")]
		public IActionResult CreateStaff()
		{
			return View(new AdminAccountInputModel());
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		[Authorize(Policy = "AdminOnly")]
		public async Task<IActionResult> CreateStaff([Bind("FullName,Email,ContactNumber,Address,Password,ConfirmPassword")] AdminAccountInputModel model)
		{
			var user = await CreateAccountAsync(model, UserRole.Staff);
			if (user == null)
			{
				return View(model);
			}

			TempData["SuccessMessage"] = $"Staff account for {user.FullName} was created.";
			return RedirectToAction(nameof(Index));
		}

		[HttpPost, ActionName("Delete")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteConfirmed(int? adminid)
		{
			var admin = await _context.Users
				.FirstOrDefaultAsync(u => u.UserId == adminid && u.Role == UserRole.Admin);

			if (admin == null)
			{
				return RedirectToAction(nameof(Index));
			}

			if (admin.UserId == CurrentUserId())
			{
				TempData["ErrorMessage"] = "You cannot delete the account you are signed in with.";
				return RedirectToAction(nameof(Index));
			}

			if (await AdminRoleCountAsync() <= 1)
			{
				TempData["ErrorMessage"] = "The last admin account cannot be deleted.";
				return RedirectToAction(nameof(Index));
			}

			_logs.Record(
				SystemLogAction.Deleted,
				SystemLogCategory.Admin,
				$"Deleted admin account {admin.FullName}.",
				"Admin",
				admin.UserId);
			_context.Users.Remove(admin);
			await _context.SaveChangesAsync();

			TempData["SuccessMessage"] = $"Admin account for {admin.FullName} was deleted.";
			return RedirectToAction(nameof(Index));
		}

		[HttpPost, ActionName("DeleteStaff")]
		[ValidateAntiForgeryToken]
		[Authorize(Policy = "AdminOnly")]
		public async Task<IActionResult> DeleteStaffConfirmed(int? staffid)
		{
			var staff = await _context.Users
				.FirstOrDefaultAsync(u => u.UserId == staffid && u.Role == UserRole.Staff);

			if (staff == null)
			{
				return RedirectToAction(nameof(Index));
			}

			if (staff.UserId == CurrentUserId())
			{
				TempData["ErrorMessage"] = "You cannot delete the account you are signed in with.";
				return RedirectToAction(nameof(Index));
			}

			_logs.Record(
				SystemLogAction.Deleted,
				SystemLogCategory.Admin,
				$"Deleted staff account {staff.FullName}.",
				"Admin",
				staff.UserId);
			_context.Users.Remove(staff);
			await _context.SaveChangesAsync();

			TempData["SuccessMessage"] = $"Staff account for {staff.FullName} was deleted.";
			return RedirectToAction(nameof(Index));
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		[Authorize(Policy = "AdminOnly")]
		public async Task<IActionResult> ToggleStaffActive(int staffid)
		{
			var staff = await _context.Users
				.FirstOrDefaultAsync(u => u.UserId == staffid && u.Role == UserRole.Staff);

			if (staff == null)
			{
				return NotFound();
			}

			if (staff.UserId == CurrentUserId())
			{
				TempData["ErrorMessage"] = "Use Account Settings to deactivate your own account.";
				return RedirectToAction(nameof(Details), new { adminid = staffid });
			}

			staff.IsSelfDeactivated = !staff.IsSelfDeactivated;
			_logs.Record(
				staff.IsSelfDeactivated ? SystemLogAction.Deactivated : SystemLogAction.Reactivated,
				SystemLogCategory.Admin,
				staff.IsSelfDeactivated
					? $"Deactivated staff account {staff.FullName}."
					: $"Reactivated staff account {staff.FullName}.",
				"Admin",
				staff.UserId);
			await _context.SaveChangesAsync();

			TempData["SuccessMessage"] = staff.IsSelfDeactivated
				? $"Staff account for {staff.FullName} was deactivated."
				: $"Staff account for {staff.FullName} was reactivated.";
			return RedirectToAction(nameof(Details), new { adminid = staffid });
		}

		public async Task<IActionResult> Settings(string? tab, string? edit)
		{
			var account = await GetLoggedInStaffAreaUserAsync();
			if (account == null)
			{
				return RedirectToAction("Index", "Dashboard");
			}

			return View(BuildSettingsViewModel(account, tab, edit));
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UpdateFullName(string fullName)
		{
			var account = await GetLoggedInStaffAreaUserAsync();
			if (account == null)
			{
				return RedirectToAction("Index", "Dashboard");
			}

			if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length > 100)
			{
				TempData["ErrorMessage"] = "Full name is required and cannot exceed 100 characters.";
				return RedirectToSettings("profile", "name");
			}

			account.FullName = fullName.Trim();
			_logs.Record(
				SystemLogAction.Updated,
				SystemLogCategory.Admin,
				$"Updated {RoleLabel(account.Role).ToLowerInvariant()} name to {account.FullName}.",
				"Admin",
				account.UserId);
			await _context.SaveChangesAsync();
			await SignInAdminAsync(account, account.FullName);
			TempData["SuccessMessage"] = "Your name has been updated.";
			return RedirectToSettings("profile");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UpdateEmail(string email)
		{
			var account = await GetLoggedInStaffAreaUserAsync();
			if (account == null)
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
				.AnyAsync(u => u.Email == email && u.UserId != account.UserId);
			if (emailExists)
			{
				TempData["ErrorMessage"] = "An account with this email already exists.";
				return RedirectToSettings("profile", "email");
			}

			account.Email = email;
			_logs.Record(
				SystemLogAction.Updated,
				SystemLogCategory.Admin,
				$"Updated {RoleLabel(account.Role).ToLowerInvariant()} email for {account.FullName}.",
				"Admin",
				account.UserId);
			await _context.SaveChangesAsync();
			await SignInAdminAsync(account, account.FullName);
			TempData["SuccessMessage"] = "Your email has been updated.";
			return RedirectToSettings("profile");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UpdatePhone(string contactNumber)
		{
			var account = await GetLoggedInStaffAreaUserAsync();
			if (account == null)
			{
				return RedirectToAction("Index", "Dashboard");
			}

			contactNumber = FieldRules.NormalizePhMobile(contactNumber);
			if (!FieldRules.IsPhMobile(contactNumber))
			{
				TempData["ErrorMessage"] = FieldRules.PhMobileMessage;
				return RedirectToSettings("profile", "phone");
			}

			account.ContactNumber = contactNumber;
			_logs.Record(
				SystemLogAction.Updated,
				SystemLogCategory.Admin,
				$"Updated {RoleLabel(account.Role).ToLowerInvariant()} contact number for {account.FullName}.",
				"Admin",
				account.UserId);
			await _context.SaveChangesAsync();
			TempData["SuccessMessage"] = "Your contact number has been updated.";
			return RedirectToSettings("profile");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UpdateAddress(string address)
		{
			var account = await GetLoggedInStaffAreaUserAsync();
			if (account == null)
			{
				return RedirectToAction("Index", "Dashboard");
			}

			address = (address ?? string.Empty).Trim();
			if (string.IsNullOrWhiteSpace(address) || address.Length > 255)
			{
				TempData["ErrorMessage"] = "Address is required and cannot exceed 255 characters.";
				return RedirectToSettings("profile", "address");
			}

			account.Address = address;
			_logs.Record(
				SystemLogAction.Updated,
				SystemLogCategory.Admin,
				$"Updated {RoleLabel(account.Role).ToLowerInvariant()} address for {account.FullName}.",
				"Admin",
				account.UserId);
			await _context.SaveChangesAsync();
			TempData["SuccessMessage"] = "Your address has been updated.";
			return RedirectToSettings("profile");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UpdateProfileImage(IFormFile? profileImageFile)
		{
			var account = await GetLoggedInStaffAreaUserAsync();
			if (account == null)
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

			account.ProfileImagePath = await ImageStorage.SaveAsync(
				_webHostEnvironment,
				profileImageFile,
				account.Role == UserRole.Staff ? ImageStorage.StaffsFolder : ImageStorage.AdminsFolder);

			_logs.Record(
				SystemLogAction.Updated,
				SystemLogCategory.Admin,
				$"Updated {RoleLabel(account.Role).ToLowerInvariant()} profile photo.",
				"Admin",
				account.UserId);
			await _context.SaveChangesAsync();
			TempData["SuccessMessage"] = "Your profile photo has been updated.";
			return RedirectToSettings("profile");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ChangePassword([Bind(Prefix = "Password")] ChangePasswordInputModel model)
		{
			var account = await GetLoggedInStaffAreaUserAsync();
			if (account == null)
			{
				return RedirectToAction("Index", "Dashboard");
			}

			if (!ModelState.IsValid)
			{
				var viewModel = BuildSettingsViewModel(account, "password", null);
				viewModel.Password = model;
				return View(nameof(Settings), viewModel);
			}

			if (!account.VerifyPassword(model.CurrentPassword))
			{
				ModelState.AddModelError("Password.CurrentPassword", "Current password is incorrect.");
				var viewModel = BuildSettingsViewModel(account, "password", null);
				viewModel.Password = model;
				return View(nameof(Settings), viewModel);
			}

			account.SetPassword(model.NewPassword);
			_logs.Record(
				SystemLogAction.Updated,
				SystemLogCategory.Admin,
				$"Changed password for {RoleLabel(account.Role).ToLowerInvariant()} {account.FullName}.",
				"Admin",
				account.UserId);
			await _context.SaveChangesAsync();
			TempData["SuccessMessage"] = "Your password has been updated.";
			return RedirectToSettings("password");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UpdateMfa(bool enabled)
		{
			var account = await GetLoggedInStaffAreaUserAsync();
			if (account == null)
			{
				return RedirectToAction("Index", "Dashboard");
			}

			account.LoginMfaEnabled = enabled;
			if (!enabled)
			{
				account.ClearLoginOtp();
			}

			_logs.Record(
				SystemLogAction.Updated,
				SystemLogCategory.Admin,
				enabled
					? $"Turned on email sign-in codes for {account.FullName}."
					: $"Turned off email sign-in codes for {account.FullName}.",
				"Admin",
				account.UserId);
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
			var account = await GetLoggedInStaffAreaUserAsync();
			if (account == null)
			{
				return RedirectToAction("Index", "Dashboard");
			}

			var isStaff = account.Role == UserRole.Staff;
			if (!isStaff && !await HasOtherActiveAdminsAsync(account.UserId))
			{
				TempData["ErrorMessage"] = "The last active admin account cannot be deactivated.";
				return RedirectToSettings("security");
			}

			account.IsSelfDeactivated = true;
			_logs.Record(
				SystemLogAction.Deactivated,
				SystemLogCategory.Admin,
				$"Deactivated {RoleLabel(account.Role).ToLowerInvariant()} account {account.FullName}.",
				"Admin",
				account.UserId);
			await _context.SaveChangesAsync();
			ClearLoginMfaCookie();
			await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
			TempData["SuccessMessage"] = isStaff
				? "Your staff account is deactivated. Contact an administrator to reactivate it."
				: "Your account is deactivated. Sign in again anytime to reactivate it.";
			return RedirectToAction("Login", "Account");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteAccount()
		{
			var account = await GetLoggedInStaffAreaUserAsync();
			if (account == null)
			{
				return RedirectToAction("Index", "Dashboard");
			}

			if (account.Role != UserRole.Staff && !await HasOtherActiveAdminsAsync(account.UserId))
			{
				TempData["ErrorMessage"] = "The last active admin account cannot be deleted.";
				return RedirectToSettings("security");
			}

			_logs.Record(
				SystemLogAction.Deleted,
				SystemLogCategory.Admin,
				$"Deleted {RoleLabel(account.Role).ToLowerInvariant()} account {account.FullName}.",
				"Admin",
				account.UserId);
			_context.Users.Remove(account);
			await _context.SaveChangesAsync();
			ClearLoginMfaCookie();
			await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
			TempData["SuccessMessage"] = "Your account has been deleted.";
			return RedirectToAction("Login", "Account");
		}

		private async Task<User?> CreateAccountAsync(AdminAccountInputModel model, UserRole role)
		{
			NormalizeProfileContact(model);

			if (!string.IsNullOrWhiteSpace(model.Email))
			{
				var emailExists = await _context.Users.AnyAsync(u => u.Email == model.Email.Trim());
				if (emailExists)
				{
					ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
				}
			}

			if (string.IsNullOrWhiteSpace(model.Password))
			{
				ModelState.AddModelError(nameof(model.Password), "Password is required.");
			}

			if (!ModelState.IsValid)
			{
				return null;
			}

			var user = new User
			{
				Email = model.Email.Trim(),
				FullName = model.FullName.Trim(),
				ContactNumber = model.ContactNumber.Trim(),
				Address = model.Address.Trim(),
				Role = role,
				Status = Status.Active,
				CreatedAt = DateTime.Now,
				EmailConfirmed = true
			};
			user.SetPassword(model.Password!);

			_context.Users.Add(user);
			await _context.SaveChangesAsync();
			_logs.Record(
				SystemLogAction.Created,
				SystemLogCategory.Admin,
				$"Created {RoleLabel(role).ToLowerInvariant()} account {user.FullName}.",
				"Admin",
				user.UserId);
			await _context.SaveChangesAsync();
			return user;
		}

		private Task<User?> FindAdminOrStaffAsync(int userId)
			=> _context.Users.FirstOrDefaultAsync(u =>
				u.UserId == userId && (u.Role == UserRole.Admin || u.Role == UserRole.Staff));

		private async Task<User?> GetLoggedInStaffAreaUserAsync()
		{
			var userId = CurrentUserId();
			return userId == null ? null : await FindAdminOrStaffAsync(userId.Value);
		}

		private AccountSettingsViewModel BuildSettingsViewModel(
			User account,
			string? tab,
			string? editField)
		{
			var normalizedTab = tab switch
			{
				"password" => "password",
				"security" => "security",
				_ => "profile"
			};

			var canClose = account.Role == UserRole.Staff
				|| _context.Users.Any(u =>
					u.Role == UserRole.Admin && u.UserId != account.UserId && !u.IsSelfDeactivated);

			return new AccountSettingsViewModel
			{
				Tab = normalizedTab,
				EditField = editField,
				FullName = account.FullName,
				Email = account.Email,
				ContactNumber = account.ContactNumber,
				Address = account.Address ?? string.Empty,
				LoginMfaEnabled = account.LoginMfaEnabled,
				AvatarInitials = GetInitials(account.FullName),
				ProfileImagePath = account.ProfileImagePath,
				MemberSince = account.CreatedAt,
				CanCloseAccount = canClose
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

		private void NormalizeProfileContact(AdminAccountInputModel model)
		{
			model.ContactNumber = FieldRules.NormalizePhMobile(model.ContactNumber);
			ModelState.Remove(nameof(model.ContactNumber));
			if (!FieldRules.IsPhMobile(model.ContactNumber))
			{
				ModelState.AddModelError(nameof(model.ContactNumber), FieldRules.PhMobileMessage);
			}

			model.Address = (model.Address ?? string.Empty).Trim();
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
			=> await _context.Users.AnyAsync(u =>
				u.Role == UserRole.Admin && u.UserId != adminId && !u.IsSelfDeactivated);

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

		private async Task<int> AdminRoleCountAsync()
			=> await _context.Users.CountAsync(u => u.Role == UserRole.Admin);

		private static string RoleLabel(UserRole role)
			=> role switch
			{
				UserRole.Admin => "Admin",
				UserRole.Staff => "Staff",
				_ => role.ToString()
			};
	}
}
