using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Models;
using EasyRent_Checking.Data;
using EasyRent_Checking.Services;

public class CustomersController : Controller
{
	private readonly EasyRent_CheckingContext _context;
	private readonly IWebHostEnvironment _webHostEnvironment;

	public CustomersController(EasyRent_CheckingContext context, IWebHostEnvironment webHostEnvironment)
	{
		_context = context;
		_webHostEnvironment = webHostEnvironment;
	}

	// GET: CUSTOMERS
	public async Task<IActionResult> Index()
	{
		var customers = await _context.CustomerProfiles
			.Include(c => c.User)
			.ToListAsync();
		return View(customers);
	}

	// GET: CUSTOMERS/Details/5
	public async Task<IActionResult> Details(int? customerid)
	{
		if (customerid == null)
		{
			return NotFound();
		}

		var customer = await _context.CustomerProfiles
			.Include(c => c.User)
			.FirstOrDefaultAsync(m => m.CustomerId == customerid);
		if (customer == null)
		{
			return NotFound();
		}

		return View(customer);
	}

	// GET: CUSTOMERS/Create
	public IActionResult Create()
	{
		return View(new CustomerAccountInputModel { Status = Status.Pending });
	}

	// POST: CUSTOMERS/Create
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Create([Bind("FullName,ContactNumber,Email,Password,ConfirmPassword,ValidIDtype,ValidIDImagePath,ValidIDImageFile,Status")] CustomerAccountInputModel model)
	{
		if (string.IsNullOrWhiteSpace(model.Password))
		{
			ModelState.AddModelError(nameof(model.Password), "Password is required.");
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

		if (model.ValidIDImageFile != null)
		{
			model.ValidIDImagePath = await ImageStorage.SaveAsync(
				_webHostEnvironment,
				model.ValidIDImageFile,
				ImageStorage.CustomersFolder);
		}

		var user = new User
		{
			Email = model.Email.Trim(),
			Role = UserRole.Customer,
			CreatedAt = DateTime.Now,
			EmailConfirmed = true
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
			ValidIDImagePath = model.ValidIDImagePath,
			Status = model.Status
		};

		_context.CustomerProfiles.Add(profile);
		await _context.SaveChangesAsync();
		return RedirectToAction(nameof(Index));
	}

	// GET: CUSTOMERS/Edit/5
	public async Task<IActionResult> Edit(int? customerid)
	{
		if (customerid == null)
		{
			return NotFound();
		}

		var customer = await _context.CustomerProfiles
			.Include(c => c.User)
			.FirstOrDefaultAsync(c => c.CustomerId == customerid);
		if (customer?.User == null)
		{
			return NotFound();
		}

		return View(CustomerAccountInputModel.FromEntities(customer.User, customer));
	}

	// POST: CUSTOMERS/Edit/5
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Edit(int? customerid, [Bind("CustomerId,FullName,ContactNumber,Email,Password,ConfirmPassword,ValidIDtype,ValidIDImagePath,ValidIDImageFile,Status")] CustomerAccountInputModel model)
	{
		if (customerid != model.CustomerId)
		{
			return NotFound();
		}

		// Password is optional on edit.
		if (string.IsNullOrWhiteSpace(model.Password))
		{
			ModelState.Remove(nameof(model.Password));
			ModelState.Remove(nameof(model.ConfirmPassword));
		}

		if (!string.IsNullOrWhiteSpace(model.Email))
		{
			var emailExists = await _context.Users
				.AnyAsync(u => u.Email == model.Email && u.UserId != model.CustomerId);
			if (emailExists)
			{
				ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
			}
		}

		if (!ModelState.IsValid)
		{
			return View(model);
		}

		var profile = await _context.CustomerProfiles
			.Include(c => c.User)
			.FirstOrDefaultAsync(c => c.CustomerId == model.CustomerId);
		if (profile?.User == null)
		{
			return NotFound();
		}

		try
		{
			if (model.ValidIDImageFile != null)
			{
				model.ValidIDImagePath = await ImageStorage.SaveAsync(
					_webHostEnvironment,
					model.ValidIDImageFile,
					ImageStorage.CustomersFolder);
			}

			profile.User.Email = model.Email.Trim();
			profile.FullName = model.FullName.Trim();
			profile.ContactNumber = model.ContactNumber.Trim();
			profile.ValidIDtype = model.ValidIDtype;
			profile.ValidIDImagePath = model.ValidIDImagePath;
			profile.Status = model.Status;

			if (!string.IsNullOrWhiteSpace(model.Password))
			{
				profile.User.SetPassword(model.Password);
			}

			await _context.SaveChangesAsync();
		}
		catch (DbUpdateConcurrencyException)
		{
			if (!CustomerExists(model.CustomerId))
			{
				return NotFound();
			}

			throw;
		}

		return RedirectToAction(nameof(Index));
	}

	// GET: CUSTOMERS/Delete/5
	public async Task<IActionResult> Delete(int? customerid)
	{
		if (customerid == null)
		{
			return NotFound();
		}

		var customer = await _context.CustomerProfiles
			.Include(c => c.User)
			.FirstOrDefaultAsync(m => m.CustomerId == customerid);
		if (customer == null)
		{
			return NotFound();
		}

		return View(customer);
	}

	// POST: CUSTOMERS/Delete/5
	[HttpPost, ActionName("Delete")]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> DeleteConfirmed(int? customerid)
	{
		var profile = await _context.CustomerProfiles
			.Include(c => c.User)
			.FirstOrDefaultAsync(c => c.CustomerId == customerid);

		if (profile?.User != null)
		{
			// Cascade removes CustomerProfile with User.
			_context.Users.Remove(profile.User);
		}

		await _context.SaveChangesAsync();
		return RedirectToAction(nameof(Index));
	}

	private bool CustomerExists(int? customerid)
	{
		return _context.CustomerProfiles.Any(e => e.CustomerId == customerid);
	}
}
