using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Models;
using EasyRent_Checking.Data;
using EasyRent_Checking.Services;

namespace EasyRent_Checking.Controllers
{
	public class AccountController : Controller
	{
		private readonly EasyRent_CheckingContext _context;
		private readonly IWebHostEnvironment _webHostEnvironment;
		private readonly IEmailSender _emailSender;
		private readonly ILogger<AccountController> _logger;

		public AccountController(
			EasyRent_CheckingContext context,
			IWebHostEnvironment webHostEnvironment,
			IEmailSender emailSender,
			ILogger<AccountController> logger)
		{
			_context = context;
			_webHostEnvironment = webHostEnvironment;
			_emailSender = emailSender;
			_logger = logger;
		}

		[HttpGet]
		public IActionResult Registration()
		{
			return View(new CustomerAccountInputModel { Status = Status.Pending });
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Registration([Bind("FullName,ContactNumber,Email,Password,ConfirmPassword,ValidIDtype,ValidIDImageFile")] CustomerAccountInputModel model)
		{
			model.Status = Status.Pending;

			if (string.IsNullOrWhiteSpace(model.Password))
			{
				ModelState.AddModelError(nameof(model.Password), "Password is required.");
			}

			if (string.IsNullOrWhiteSpace(model.ValidIDtype))
			{
				ModelState.AddModelError(nameof(model.ValidIDtype), "Please choose a valid ID type.");
			}
			else if (model.ValidIDImageFile == null)
			{
				ModelState.AddModelError(nameof(model.ValidIDImageFile), "Please upload a picture of your valid ID.");
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

			var imagePath = await ImageStorage.SaveAsync(
				_webHostEnvironment,
				model.ValidIDImageFile!,
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
				ValidIDImagePath = imagePath,
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

			return RedirectToAction(nameof(Login));
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
		public IActionResult Login()
		{
			return View();
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Login(string email, string password, bool rememberMe)
		{
			if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
			{
				ModelState.AddModelError(string.Empty, "Email and password are required.");
				return View();
			}

			var user = await _context.Users
				.Include(u => u.CustomerProfile)
				.Include(u => u.AdminProfile)
				.FirstOrDefaultAsync(u => u.Email == email.Trim());

			if (user == null || !user.VerifyPassword(password))
			{
				ModelState.AddModelError(string.Empty, "Invalid email or password.");
				return View();
			}

			if (!user.EmailConfirmed)
			{
				ModelState.AddModelError(string.Empty, "Please verify your email before logging in. Check your inbox for the confirmation link.");
				return View();
			}

			if (user.Role == UserRole.Customer)
			{
				if (user.CustomerProfile == null || user.CustomerProfile.Status != Status.Active)
				{
					ModelState.AddModelError(string.Empty, "Your account is not approved yet. Please wait for admin approval.");
					return View();
				}
			}

			var displayName = user.Role switch
			{
				UserRole.Customer => user.CustomerProfile?.FullName ?? user.Email,
				UserRole.Admin or UserRole.Staff => user.AdminProfile?.FullName ?? user.Email,
				_ => user.Email
			};

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
				IsPersistent = rememberMe,
				ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(8)
			};

			await HttpContext.SignInAsync(
				CookieAuthenticationDefaults.AuthenticationScheme,
				principal,
				authProperties);

			TempData["SuccessMessage"] = "Signed in successfully.";
			return user.Role == UserRole.Customer
				? RedirectToAction("Homepage", "Vehicles")
				: RedirectToAction("Index", "Reservations");
		}

		[HttpGet]
		public async Task<IActionResult> Logout()
		{
			await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
			TempData["SuccessMessage"] = "You have been logged out.";
			return RedirectToAction(nameof(Login));
		}
	}
}
