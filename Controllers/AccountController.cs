using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Models;
using EasyRent_Checking.ViewModels;
using EasyRent_Checking.Data;
using EasyRent_Checking.Services;

namespace EasyRent_Checking.Controllers
{
	public class AccountController : Controller
	{
		private const string BookingAccountReminder = "Please log in or create an account to continue booking. An EasyRent account is required before you can rent a vehicle.";
		private const string MfaPendingCookieName = "EasyRent.LoginMfa";
		private const string MfaProtectorPurpose = "EasyRent.LoginMfa.v1";

		private readonly EasyRent_CheckingContext _context;
		private readonly IWebHostEnvironment _webHostEnvironment;
		private readonly IEmailSender _emailSender;
		private readonly ILogger<AccountController> _logger;
		private readonly IDataProtector _mfaProtector;

		public AccountController(
			EasyRent_CheckingContext context,
			IWebHostEnvironment webHostEnvironment,
			IEmailSender emailSender,
			ILogger<AccountController> logger,
			IDataProtectionProvider dataProtectionProvider)
		{
			_context = context;
			_webHostEnvironment = webHostEnvironment;
			_emailSender = emailSender;
			_logger = logger;
			_mfaProtector = dataProtectionProvider.CreateProtector(MfaProtectorPurpose);
		}

		[HttpGet]
		public IActionResult Registration(string? returnUrl)
		{
			PrepareAuthReminder(returnUrl);
			return View(new CustomerAccountInputModel { Status = Status.Pending });
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Registration([Bind("FullName,ContactNumber,Email,Password,ConfirmPassword,ValidIDtype,FrontValidIDImageFile,BackValidIDImageFile")] CustomerAccountInputModel model, string? returnUrl)
		{
			model.Status = Status.Pending;
			PrepareAuthReminder(returnUrl);

			if (string.IsNullOrWhiteSpace(model.Password))
			{
				ModelState.AddModelError(nameof(model.Password), "Password is required.");
			}

			if (string.IsNullOrWhiteSpace(model.ValidIDtype))
			{
				ModelState.AddModelError(nameof(model.ValidIDtype), "Please choose a valid ID type.");
			}
			else if (model.FrontValidIDImageFile == null)
			{
				ModelState.AddModelError(nameof(model.FrontValidIDImageFile), "Please upload the front of your valid ID.");
			}
			else if (model.BackValidIDImageFile == null)
			{
				ModelState.AddModelError(nameof(model.BackValidIDImageFile), "Please upload the back of your valid ID.");
			}

			if (!string.IsNullOrWhiteSpace(model.Email))
			{
				var emailExists = await _context.Users
					.AnyAsync(u => u.Email == model.Email);
				if (emailExists)
				{
					ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
				}
			}

			if (!ModelState.IsValid)
			{
				return View(model);
			}

			var frontImagePath = await ImageStorage.SaveAsync(
				_webHostEnvironment,
				model.FrontValidIDImageFile!,
				ImageStorage.CustomersFolder);
			var backImagePath = await ImageStorage.SaveAsync(
				_webHostEnvironment,
				model.BackValidIDImageFile!,
				ImageStorage.CustomersFolder);

			var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

			var user = new User
			{
				Email = model.Email.Trim(),
				Role = UserRole.Customer,
				CreatedAt = DateTime.Now,
				EmailConfirmed = false,
				EmailVerificationToken = token,
				EmailVerificationTokenExpires = DateTime.UtcNow.AddHours(24)
			};
			user.SetPassword(model.Password!);

			_context.Users.Add(user);
			await _context.SaveChangesAsync();

			var profile = new CustomerProfile
			{
				CustomerId = user.UserId,
				FullName = model.FullName.Trim(),
				ContactNumber = model.ContactNumber.Trim(),
				ValidIDtype = model.ValidIDtype,
				FrontValidIDImagePath = frontImagePath,
				BackValidIDImagePath = backImagePath,
				Status = Status.Pending
			};

			_context.CustomerProfiles.Add(profile);
			await _context.SaveChangesAsync();

			var confirmUrl = Url.Action(
				nameof(ConfirmEmail),
				"Account",
				new { userId = user.UserId, token },
				Request.Scheme)!;

			try
			{
				await _emailSender.SendAsync(
					user.Email,
					"Verify your EasyRent email",
					$"""
					<p>Hi {profile.FullName},</p>
					<p>Thanks for registering with EasyRent. Please confirm your email address by clicking the link below:</p>
					<p><a href="{confirmUrl}">Confirm email</a></p>
					<p>This link expires in 24 hours.</p>
					""");

				TempData["SuccessMessage"] = "Account created. Please check your email to verify your account, then wait for admin approval before logging in.";
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Verification email failed for user {UserId}", user.UserId);
				TempData["SuccessMessage"] = "Account created, but we could not send the verification email. Please contact support.";
			}

			return RedirectToAction(nameof(Login), new { returnUrl });
		}

		[HttpGet]
		public async Task<IActionResult> ConfirmEmail(int userId, string token)
		{
			if (string.IsNullOrWhiteSpace(token))
			{
				TempData["ErrorMessage"] = "Invalid verification link.";
				return RedirectToAction(nameof(Login));
			}

			var user = await _context.Users.FindAsync(userId);
			if (user == null
				|| user.EmailVerificationToken != token
				|| user.EmailVerificationTokenExpires == null
				|| user.EmailVerificationTokenExpires < DateTime.UtcNow)
			{
				TempData["ErrorMessage"] = "Invalid or expired verification link.";
				return RedirectToAction(nameof(Login));
			}

			user.EmailConfirmed = true;
			user.EmailVerificationToken = null;
			user.EmailVerificationTokenExpires = null;
			await _context.SaveChangesAsync();

			TempData["SuccessMessage"] = "Email verified successfully. You can log in after admin approval.";
			return RedirectToAction(nameof(Login));
		}

		[HttpGet]
		public IActionResult Login(string? returnUrl)
		{
			ClearMfaPendingCookie();
			PrepareAuthReminder(returnUrl);
			return View();
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Login(string email, string password, string? returnUrl)
		{
			PrepareAuthReminder(returnUrl);

			if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
			{
				ModelState.AddModelError(string.Empty, "Email and password are required.");
				return View();
			}

			var user = await _context.Users
				.Include(u => u.CustomerProfile)
				.Include(u => u.AdminProfile)
				.FirstOrDefaultAsync(u => u.Email == email.Trim());

			if (user == null)
			{
				ModelState.AddModelError(string.Empty, "Invalid email or password.");
				return View();
			}

			var utcNow = DateTime.UtcNow;
			if (user.LockoutEndUtc.HasValue && user.LockoutEndUtc.Value <= utcNow)
			{
				user.ResetLockout();
				await _context.SaveChangesAsync();
			}

			if (user.IsLockedOut(utcNow))
			{
				AddLockoutError(user.RemainingLockoutMinutes(utcNow));
				return View();
			}

			if (!user.VerifyPassword(password))
			{
				if (user.RegisterFailedAttempt(utcNow))
				{
					await _context.SaveChangesAsync();
					AddLockoutError(user.RemainingLockoutMinutes(utcNow));
					return View();
				}

				await _context.SaveChangesAsync();
				ModelState.AddModelError(string.Empty, "Invalid email or password.");
				return View();
			}

			if (user.AccessFailedCount > 0 || user.LockoutEndUtc != null)
			{
				user.ResetLockout();
				await _context.SaveChangesAsync();
			}

			if (!user.EmailConfirmed)
			{
				ModelState.AddModelError(string.Empty, "Please verify your email before logging in. Check your inbox for the confirmation link.");
				return View();
			}

			if (user.Role == UserRole.Customer)
			{
				if (user.CustomerProfile == null || user.CustomerProfile.Status == Status.Pending)
				{
					ModelState.AddModelError(string.Empty, "Your account is not approved yet. Please wait for admin approval.");
					return View();
				}

				if (user.CustomerProfile.Status != Status.Active)
				{
					ModelState.AddModelError(string.Empty, "Your account is not approved yet. Please wait for admin approval.");
					return View();
				}

				if (user.CustomerProfile.IsSelfDeactivated)
				{
					user.CustomerProfile.IsSelfDeactivated = false;
					await _context.SaveChangesAsync();
					TempData["SuccessMessage"] = "Your account has been reactivated.";
				}
			}

			if (user.LoginMfaEnabled)
			{
				await StartLoginMfaAsync(user, returnUrl);
				return RedirectToAction(nameof(VerifyLoginCode));
			}

			await SignInUserAsync(user, GetDisplayName(user));
			return RedirectAfterLogin(user, returnUrl);
		}

		[HttpGet]
		public async Task<IActionResult> VerifyLoginCode()
		{
			var user = await GetMfaPendingUserAsync();
			if (user == null)
			{
				TempData["ErrorMessage"] = "Your verification session expired. Please sign in again.";
				return RedirectToAction(nameof(Login));
			}

			if (!user.LoginMfaEnabled)
			{
				return await CompletePendingLoginWithoutMfaAsync(user);
			}

			return View(BuildVerifyLoginCodeModel(user));
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> VerifyLoginCode(VerifyLoginCodeInputModel model)
		{
			var pending = TryReadMfaPending();
			var user = await GetMfaPendingUserAsync();
			if (pending == null || user == null)
			{
				TempData["ErrorMessage"] = "Your verification session expired. Please sign in again.";
				return RedirectToAction(nameof(Login));
			}

			if (!user.LoginMfaEnabled)
			{
				return await CompletePendingLoginWithoutMfaAsync(user);
			}

			model.MaskedEmail = MaskEmail(user.Email);
			model.ResendWaitSeconds = user.LoginOtpResendWaitSeconds(DateTime.UtcNow);

			var code = NormalizeOtpCode(model.Code);
			if (string.IsNullOrEmpty(code) || code.Length != LoginMfaRules.CodeLength)
			{
				ModelState.AddModelError(nameof(model.Code), "Enter the 6-digit code from your email.");
			}

			if (!ModelState.IsValid)
			{
				return View(model);
			}

			var utcNow = DateTime.UtcNow;
			if (user.LoginOtpExpiresUtc == null || user.LoginOtpExpiresUtc.Value < utcNow)
			{
				user.ClearLoginOtp();
				await _context.SaveChangesAsync();
				ClearMfaPendingCookie();
				TempData["ErrorMessage"] = "That code has expired. Please sign in again.";
				return RedirectToAction(nameof(Login));
			}

			if (!user.VerifyLoginOtp(code, utcNow))
			{
				user.LoginOtpFailedCount++;
				if (user.LoginOtpFailedCount >= LoginMfaRules.MaxFailedAttempts)
				{
					user.ClearLoginOtp();
					await _context.SaveChangesAsync();
					ClearMfaPendingCookie();
					TempData["ErrorMessage"] = "Too many incorrect codes. Please sign in again.";
					return RedirectToAction(nameof(Login));
				}

				await _context.SaveChangesAsync();
				ModelState.AddModelError(nameof(model.Code), "That code is incorrect. Please try again.");
				model.ResendWaitSeconds = user.LoginOtpResendWaitSeconds(utcNow);
				return View(model);
			}

			user.ClearLoginOtp();
			await _context.SaveChangesAsync();
			ClearMfaPendingCookie();

			await SignInUserAsync(user, GetDisplayName(user));
			return RedirectAfterLogin(user, pending.ReturnUrl);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ResendLoginCode()
		{
			var pending = TryReadMfaPending();
			var user = await GetMfaPendingUserAsync();
			if (pending == null || user == null)
			{
				TempData["ErrorMessage"] = "Your verification session expired. Please sign in again.";
				return RedirectToAction(nameof(Login));
			}

			if (!user.LoginMfaEnabled)
			{
				return await CompletePendingLoginWithoutMfaAsync(user);
			}

			var utcNow = DateTime.UtcNow;
			var waitSeconds = user.LoginOtpResendWaitSeconds(utcNow);
			if (waitSeconds > 0)
			{
				TempData["ErrorMessage"] = $"Please wait {waitSeconds} seconds before requesting a new code.";
				return RedirectToAction(nameof(VerifyLoginCode));
			}

			await IssueLoginOtpAsync(user, pending.ReturnUrl, utcNow);
			TempData["SuccessMessage"] = "We sent a new code to your email.";
			return RedirectToAction(nameof(VerifyLoginCode));
		}

		[Authorize]
		[HttpGet]
		public async Task<IActionResult> Settings(string? tab, string? edit)
		{
			var customer = await GetLoggedInCustomerUserAsync();
			if (customer?.CustomerProfile == null)
			{
				return RedirectAwayFromCustomerSettings();
			}

			return View(await BuildSettingsViewModelAsync(customer, customer.CustomerProfile, tab, edit));
		}

		[Authorize]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UpdateFullName(string fullName)
		{
			var customer = await GetLoggedInCustomerUserAsync();
			var profile = customer?.CustomerProfile;
			if (customer == null || profile == null)
			{
				return RedirectAwayFromCustomerSettings();
			}

			if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length > 100)
			{
				TempData["ErrorMessage"] = "Full name is required and cannot exceed 100 characters.";
				return RedirectToSettings("profile", "name");
			}

			profile.FullName = fullName.Trim();
			await _context.SaveChangesAsync();
			await SignInUserAsync(customer, profile.FullName);
			TempData["SuccessMessage"] = "Your name has been updated.";
			return RedirectToSettings("profile");
		}

		[Authorize]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UpdateEmail(string email)
		{
			var customer = await GetLoggedInCustomerUserAsync();
			var profile = customer?.CustomerProfile;
			if (customer == null || profile == null)
			{
				return RedirectAwayFromCustomerSettings();
			}

			email = (email ?? string.Empty).Trim();
			if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
			{
				TempData["ErrorMessage"] = "Please enter a valid email address.";
				return RedirectToSettings("profile", "email");
			}

			var emailExists = await _context.Users
				.AnyAsync(u => u.Email == email && u.UserId != customer.UserId);
			if (emailExists)
			{
				TempData["ErrorMessage"] = "An account with this email already exists.";
				return RedirectToSettings("profile", "email");
			}

			customer.Email = email;
			await _context.SaveChangesAsync();
			await SignInUserAsync(customer, profile.FullName);
			TempData["SuccessMessage"] = "Your email has been updated.";
			return RedirectToSettings("profile");
		}

		[Authorize]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UpdatePhone(string contactNumber)
		{
			var customer = await GetLoggedInCustomerUserAsync();
			var profile = customer?.CustomerProfile;
			if (customer == null || profile == null)
			{
				return RedirectAwayFromCustomerSettings();
			}

			contactNumber = (contactNumber ?? string.Empty).Trim();
			if (!FieldRules.IsPhMobile(contactNumber))
			{
				TempData["ErrorMessage"] = FieldRules.PhMobileMessage;
				return RedirectToSettings("profile", "phone");
			}

			profile.ContactNumber = contactNumber;
			await _context.SaveChangesAsync();
			TempData["SuccessMessage"] = "Your phone number has been updated.";
			return RedirectToSettings("profile");
		}

		[Authorize]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UpdateIdentity(
			string validIDtype,
			string? frontValidIDImagePath,
			string? backValidIDImagePath,
			IFormFile? frontValidIDImageFile,
			IFormFile? backValidIDImageFile)
		{
			var customer = await GetLoggedInCustomerUserAsync();
			var profile = customer?.CustomerProfile;
			if (customer == null || profile == null)
			{
				return RedirectAwayFromCustomerSettings();
			}

			if (string.IsNullOrWhiteSpace(validIDtype)
				|| !Enum.TryParse<ValidIDtype>(validIDtype, out _))
			{
				TempData["ErrorMessage"] = "Please choose a valid ID type.";
				return RedirectToSettings("profile", "identity");
			}

			if (frontValidIDImageFile != null)
			{
				frontValidIDImagePath = await ImageStorage.SaveAsync(
					_webHostEnvironment,
					frontValidIDImageFile,
					ImageStorage.CustomersFolder);
			}

			if (backValidIDImageFile != null)
			{
				backValidIDImagePath = await ImageStorage.SaveAsync(
					_webHostEnvironment,
					backValidIDImageFile,
					ImageStorage.CustomersFolder);
			}

			profile.ValidIDtype = validIDtype;
			profile.FrontValidIDImagePath = string.IsNullOrWhiteSpace(frontValidIDImagePath)
				? profile.FrontValidIDImagePath
				: frontValidIDImagePath;
			profile.BackValidIDImagePath = string.IsNullOrWhiteSpace(backValidIDImagePath)
				? profile.BackValidIDImagePath
				: backValidIDImagePath;

			await _context.SaveChangesAsync();
			TempData["SuccessMessage"] = "Your identity details have been updated.";
			return RedirectToSettings("profile");
		}

		[Authorize]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ChangePassword([Bind(Prefix = "Password")] ChangePasswordInputModel model)
		{
			var customer = await GetLoggedInCustomerUserAsync();
			var profile = customer?.CustomerProfile;
			if (customer == null || profile == null)
			{
				return RedirectAwayFromCustomerSettings();
			}

			if (!ModelState.IsValid)
			{
				var viewModel = await BuildSettingsViewModelAsync(customer, profile, "password", null);
				viewModel.Password = model;
				return View(nameof(Settings), viewModel);
			}

			if (!customer.VerifyPassword(model.CurrentPassword))
			{
				ModelState.AddModelError("Password.CurrentPassword", "Current password is incorrect.");
				var viewModel = await BuildSettingsViewModelAsync(customer, profile, "password", null);
				viewModel.Password = model;
				return View(nameof(Settings), viewModel);
			}

			customer.SetPassword(model.NewPassword);
			await _context.SaveChangesAsync();
			TempData["SuccessMessage"] = "Your password has been updated.";
			return RedirectToSettings("password");
		}

		[Authorize]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UpdateMfa(bool enabled)
		{
			var customer = await GetLoggedInCustomerUserAsync();
			var profile = customer?.CustomerProfile;
			if (customer == null || profile == null)
			{
				return RedirectAwayFromCustomerSettings();
			}

			customer.LoginMfaEnabled = enabled;
			if (!enabled)
			{
				customer.ClearLoginOtp();
			}

			await _context.SaveChangesAsync();
			TempData["SuccessMessage"] = enabled
				? "Email sign-in codes are now on. We'll send a code after your password."
				: "Email sign-in codes are now off.";
			return RedirectToSettings("security");
		}

		[Authorize]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeactivateAccount()
		{
			var customer = await GetLoggedInCustomerUserAsync();
			var profile = customer?.CustomerProfile;
			if (customer == null || profile == null)
			{
				return RedirectAwayFromCustomerSettings();
			}

			profile.IsSelfDeactivated = true;
			await _context.SaveChangesAsync();
			ClearMfaPendingCookie();
			await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
			TempData["SuccessMessage"] = "Your account is deactivated. Sign in again anytime to reactivate it.";
			return RedirectToAction(nameof(Login));
		}

		[Authorize]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteAccount()
		{
			var customer = await GetLoggedInCustomerUserAsync();
			var profile = customer?.CustomerProfile;
			if (customer == null || profile == null)
			{
				return RedirectAwayFromCustomerSettings();
			}

			var hasOpenRentals = await _context.Rentals.AnyAsync(r =>
				r.CustomerId == profile.CustomerId
				&& (r.RentalStatus == RentalStatus.Pending || r.RentalStatus == RentalStatus.Approved));
			if (hasOpenRentals)
			{
				TempData["ErrorMessage"] = "Finish or cancel your open rentals before deleting your account.";
				return RedirectToSettings("security");
			}

			_context.Users.Remove(customer);
			await _context.SaveChangesAsync();
			ClearMfaPendingCookie();
			await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
			TempData["SuccessMessage"] = "Your account has been deleted.";
			return RedirectToAction(nameof(Login));
		}

		[HttpGet]
		public async Task<IActionResult> Logout()
		{
			ClearMfaPendingCookie();
			await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
			TempData["SuccessMessage"] = "You have been logged out.";
			return RedirectToAction(nameof(Login));
		}

		[HttpGet]
		public IActionResult ForgotPassword()
		{
			return View(new ForgotPasswordInputModel());
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ForgotPassword(ForgotPasswordInputModel model)
		{
			if (!ModelState.IsValid)
			{
				return View(model);
			}

			var email = model.Email.Trim();
			var user = await _context.Users
				.Include(u => u.CustomerProfile)
				.Include(u => u.AdminProfile)
				.FirstOrDefaultAsync(u => u.Email == email);

			if (user != null)
			{
				var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
				user.PasswordResetToken = token;
				user.PasswordResetTokenExpires = DateTime.UtcNow.AddHours(1);
				await _context.SaveChangesAsync();

				var resetUrl = Url.Action(
					nameof(ResetPassword),
					"Account",
					new { userId = user.UserId, token },
					Request.Scheme)!;

				var displayName = user.Role switch
				{
					UserRole.Customer => user.CustomerProfile?.FullName ?? user.Email,
					UserRole.Admin or UserRole.Staff => user.AdminProfile?.FullName ?? user.Email,
					_ => user.Email
				};

				try
				{
					await _emailSender.SendAsync(
						user.Email,
						"Reset your EasyRent password",
						$"""
						<p>Hi {displayName},</p>
						<p>We received a request to reset your EasyRent password. Verify your email by clicking the link below, then you can choose a new password:</p>
						<p><a href="{resetUrl}">Verify email and reset password</a></p>
						<p>This link expires in 1 hour. If you did not request a password reset, you can ignore this email.</p>
						""");
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Password reset email failed for user {UserId}", user.UserId);
				}
			}

			TempData["SuccessMessage"] = "If an account exists for that email, we sent a verification link. Check your inbox to continue.";
			return RedirectToAction(nameof(ForgotPassword));
		}

		[HttpGet]
		public async Task<IActionResult> ResetPassword(int userId, string token)
		{
			if (!await IsPasswordResetTokenValidAsync(userId, token))
			{
				TempData["ErrorMessage"] = "Invalid or expired password reset link. Please request a new one.";
				return RedirectToAction(nameof(ForgotPassword));
			}

			return View(new ResetPasswordInputModel
			{
				UserId = userId,
				Token = token
			});
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ResetPassword(ResetPasswordInputModel model)
		{
			if (!ModelState.IsValid)
			{
				return View(model);
			}

			if (!await IsPasswordResetTokenValidAsync(model.UserId, model.Token))
			{
				TempData["ErrorMessage"] = "Invalid or expired password reset link. Please request a new one.";
				return RedirectToAction(nameof(ForgotPassword));
			}

			var user = await _context.Users.FindAsync(model.UserId);
			if (user == null)
			{
				TempData["ErrorMessage"] = "Invalid or expired password reset link. Please request a new one.";
				return RedirectToAction(nameof(ForgotPassword));
			}

			user.SetPassword(model.Password);
			user.PasswordResetToken = null;
			user.PasswordResetTokenExpires = null;
			user.EmailConfirmed = true;
			user.ResetLockout();
			await _context.SaveChangesAsync();

			TempData["SuccessMessage"] = "Your password has been changed. You can now log in with your new password.";
			return RedirectToAction(nameof(Login));
		}

		private async Task<bool> IsPasswordResetTokenValidAsync(int userId, string? token)
		{
			if (string.IsNullOrWhiteSpace(token))
			{
				return false;
			}

			var user = await _context.Users.FindAsync(userId);
			return user != null
				&& user.PasswordResetToken == token
				&& user.PasswordResetTokenExpires != null
				&& user.PasswordResetTokenExpires >= DateTime.UtcNow;
		}

		private void AddLockoutError(int remainingMinutes)
		{
			var minuteLabel = remainingMinutes == 1 ? "minute" : "minutes";
			ModelState.AddModelError(
				string.Empty,
				$"Too many failed login attempts. Your account is locked. Please try again in {remainingMinutes} {minuteLabel}.");
		}

		private void PrepareAuthReminder(string? returnUrl)
		{
			ViewData["ReturnUrl"] = returnUrl;
			if (IsBookingReturnUrl(returnUrl))
			{
				ViewData["LoginReminder"] = BookingAccountReminder;
			}
		}

		private IActionResult RedirectAwayFromCustomerSettings()
		{
			if (User.IsInRole(nameof(UserRole.Admin)) || User.IsInRole(nameof(UserRole.Staff)))
			{
				return RedirectToAction("Index", "Dashboard");
			}

			return RedirectToAction(nameof(Login));
		}

		private IActionResult RedirectToSettings(string tab, string? edit = null)
		{
			return RedirectToAction(nameof(Settings), new { tab, edit });
		}

		private async Task<AccountSettingsViewModel> BuildSettingsViewModelAsync(
			User user,
			CustomerProfile profile,
			string? tab,
			string? editField)
		{
			var rentals = await _context.Rentals
				.AsNoTracking()
				.Where(r => r.CustomerId == profile.CustomerId)
				.Select(r => new { r.RentalId, r.RentalStatus, r.TotalAmount })
				.ToListAsync();
			var rentalIds = rentals.Select(r => r.RentalId).ToList();
			var payments = await _context.Payments
				.AsNoTracking()
				.Where(p => rentalIds.Contains(p.RentalId))
				.Select(p => new { p.RentalId, p.AmountPaid })
				.ToListAsync();

			decimal outstanding = 0m;
			foreach (var rental in rentals)
			{
				if (rental.RentalStatus is RentalStatus.Cancelled or RentalStatus.Expired)
				{
					continue;
				}

				var paid = payments.Where(p => p.RentalId == rental.RentalId).Sum(p => p.AmountPaid);
				outstanding += Math.Max(0, rental.TotalAmount - paid);
			}

			var completedTrips = await _context.Transits
				.AsNoTracking()
				.CountAsync(t => t.Rental.CustomerId == profile.CustomerId
					&& t.TripStatus == TripStatus.Completed);

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
				ContactNumber = profile.ContactNumber,
				ValidIDtype = profile.ValidIDtype,
				ValidIdTypeLabel = FormatValidIdType(profile.ValidIDtype),
				IsIdentityVerified = profile.Status == Status.Active
					&& !string.IsNullOrWhiteSpace(profile.FrontValidIDImagePath)
					&& !string.IsNullOrWhiteSpace(profile.BackValidIDImagePath),
				LoginMfaEnabled = user.LoginMfaEnabled,
				AvatarInitials = GetInitials(profile.FullName),
				MemberSince = user.CreatedAt,
				TotalRentals = rentals.Count,
				CompletedTrips = completedTrips,
				OutstandingBalance = outstanding,
				FrontValidIDImagePath = profile.FrontValidIDImagePath,
				BackValidIDImagePath = profile.BackValidIDImagePath
			};
		}

		private static string FormatValidIdType(string? value)
		{
			return value switch
			{
				nameof(ValidIDtype.NationalID) => "National ID",
				nameof(ValidIDtype.Passport) => "Passport",
				nameof(ValidIDtype.UMID) => "UMID",
				nameof(ValidIDtype.DriverLicense) => "Driver's License",
				nameof(ValidIDtype.VotersID) => "Voter's ID",
				_ => string.IsNullOrWhiteSpace(value) ? "Not set" : value
			};
		}

		private static string GetInitials(string name)
		{
			var parts = (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length == 0)
			{
				return "U";
			}

			if (parts.Length == 1)
			{
				return char.ToUpperInvariant(parts[0][0]).ToString();
			}

			return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
		}

		private async Task<User?> GetLoggedInCustomerUserAsync()
		{
			if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
			{
				return null;
			}

			return await _context.Users
				.Include(u => u.CustomerProfile)
				.FirstOrDefaultAsync(u => u.UserId == userId && u.Role == UserRole.Customer);
		}

		private async Task SignInUserAsync(User user, string displayName)
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
			var authProperties = new AuthenticationProperties
			{
				IsPersistent = false,
				ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
			};

			await HttpContext.SignInAsync(
				CookieAuthenticationDefaults.AuthenticationScheme,
				principal,
				authProperties);
		}

		private async Task StartLoginMfaAsync(User user, string? returnUrl)
		{
			await IssueLoginOtpAsync(user, returnUrl, DateTime.UtcNow);
		}

		private async Task<IActionResult> CompletePendingLoginWithoutMfaAsync(User user)
		{
			var pending = TryReadMfaPending();
			user.ClearLoginOtp();
			await _context.SaveChangesAsync();
			ClearMfaPendingCookie();
			await SignInUserAsync(user, GetDisplayName(user));
			return RedirectAfterLogin(user, pending?.ReturnUrl);
		}

		private async Task IssueLoginOtpAsync(User user, string? returnUrl, DateTime utcNow)
		{
			var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
			user.SetLoginOtp(code, utcNow);
			await _context.SaveChangesAsync();
			SetMfaPendingCookie(user.UserId, returnUrl);

			var displayName = GetDisplayName(user);
			try
			{
				await _emailSender.SendAsync(
					user.Email,
					"Your EasyRent sign-in code",
					$"""
					<p>Hi {displayName},</p>
					<p>Your EasyRent sign-in code is:</p>
					<p style="font-size:28px;font-weight:700;letter-spacing:6px;">{code}</p>
					<p>This code expires in {LoginMfaRules.CodeLifetimeMinutes} minutes. If you did not try to sign in, you can ignore this email.</p>
					""");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Login verification email failed for user {UserId}", user.UserId);
				TempData["ErrorMessage"] = "We could not send the verification email. Please try resending the code.";
			}
		}

		private async Task<User?> GetMfaPendingUserAsync()
		{
			var pending = TryReadMfaPending();
			if (pending == null)
			{
				return null;
			}

			return await _context.Users
				.Include(u => u.CustomerProfile)
				.Include(u => u.AdminProfile)
				.FirstOrDefaultAsync(u => u.UserId == pending.UserId);
		}

		private VerifyLoginCodeInputModel BuildVerifyLoginCodeModel(User user)
		{
			return new VerifyLoginCodeInputModel
			{
				MaskedEmail = MaskEmail(user.Email),
				ResendWaitSeconds = user.LoginOtpResendWaitSeconds(DateTime.UtcNow)
			};
		}

		private void SetMfaPendingCookie(int userId, string? returnUrl)
		{
			var payload = JsonSerializer.Serialize(new MfaPendingPayload(userId, returnUrl));
			var protectedPayload = _mfaProtector.Protect(payload);
			Response.Cookies.Append(MfaPendingCookieName, protectedPayload, new CookieOptions
			{
				HttpOnly = true,
				IsEssential = true,
				SameSite = SameSiteMode.Lax,
				Secure = Request.IsHttps,
				Path = "/",
				Expires = DateTimeOffset.UtcNow.AddMinutes(LoginMfaRules.CodeLifetimeMinutes)
			});
		}

		private MfaPendingPayload? TryReadMfaPending()
		{
			if (!Request.Cookies.TryGetValue(MfaPendingCookieName, out var cookie) || string.IsNullOrWhiteSpace(cookie))
			{
				return null;
			}

			try
			{
				var json = _mfaProtector.Unprotect(cookie);
				return JsonSerializer.Deserialize<MfaPendingPayload>(json);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Ignored invalid MFA pending cookie.");
				return null;
			}
		}

		private void ClearMfaPendingCookie()
		{
			Response.Cookies.Delete(MfaPendingCookieName, new CookieOptions
			{
				Path = "/",
				Secure = Request.IsHttps,
				SameSite = SameSiteMode.Lax
			});
		}

		private IActionResult RedirectAfterLogin(User user, string? returnUrl)
		{
			if (user.Role == UserRole.Customer
				&& !string.IsNullOrWhiteSpace(returnUrl)
				&& Url.IsLocalUrl(returnUrl))
			{
				return Redirect(returnUrl);
			}

			return user.Role == UserRole.Customer
				? RedirectToAction("Homepage", "ClientSide")
				: RedirectToAction("Index", "Dashboard");
		}

		private static string GetDisplayName(User user)
		{
			return user.Role switch
			{
				UserRole.Customer => user.CustomerProfile?.FullName ?? user.Email,
				UserRole.Admin or UserRole.Staff => user.AdminProfile?.FullName ?? user.Email,
				_ => user.Email
			};
		}

		private static string NormalizeOtpCode(string? code)
		{
			if (string.IsNullOrWhiteSpace(code))
			{
				return string.Empty;
			}

			return new string(code.Where(char.IsDigit).ToArray());
		}

		private static string MaskEmail(string email)
		{
			var at = email.IndexOf('@');
			if (at <= 0 || at == email.Length - 1)
			{
				return "***";
			}

			var local = email[..at];
			var domain = email[(at + 1)..];
			var visible = local.Length == 1 ? local : local[..Math.Min(2, local.Length)];
			return $"{visible}***@{domain}";
		}

		private sealed record MfaPendingPayload(int UserId, string? ReturnUrl);

		private static bool IsBookingReturnUrl(string? returnUrl)
		{
			if (string.IsNullOrWhiteSpace(returnUrl))
			{
				return false;
			}

			return returnUrl.Contains("/ClientSide/Rental", StringComparison.OrdinalIgnoreCase)
				|| returnUrl.Contains("/ClientSide/Reservation", StringComparison.OrdinalIgnoreCase)
				|| returnUrl.Contains("/Vehicles/Rental", StringComparison.OrdinalIgnoreCase)
				|| returnUrl.Contains("/Vehicles/Reservation", StringComparison.OrdinalIgnoreCase);
		}
	}
}
