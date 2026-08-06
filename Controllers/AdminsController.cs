using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Data;
using EasyRent_Checking.Models;

namespace EasyRent_Checking.Controllers
{
	public class AdminsController : Controller
	{
		private readonly EasyRent_CheckingContext _context;

		public AdminsController(EasyRent_CheckingContext context)
		{
			_context = context;
		}

		public async Task<IActionResult> Index()
		{
			var admins = await _context.AdminProfiles
				.Include(a => a.User)
				.ToListAsync();
			return View(admins);
		}

		public IActionResult Create()
		{
			return View(new AdminAccountInputModel());
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create([Bind("FullName,Email,Password,ConfirmPassword")] AdminAccountInputModel model)
		{
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

			return RedirectToAction(nameof(Index));
		}
	}
}
