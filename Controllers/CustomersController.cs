using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Models;
using EasyRent_Checking.ViewModels;
using EasyRent_Checking.Data;
using EasyRent_Checking.Services;

[Authorize(Policy = "StaffArea")]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class CustomersController : Controller
{
	private readonly EasyRent_CheckingContext _context;
	private readonly IWebHostEnvironment _webHostEnvironment;
	private readonly BookingEmailService _bookingEmailService;
	private readonly SystemLogService _logs;

	public CustomersController(
		EasyRent_CheckingContext context,
		IWebHostEnvironment webHostEnvironment,
		BookingEmailService bookingEmailService,
		SystemLogService logs)
	{
		_context = context;
		_webHostEnvironment = webHostEnvironment;
		_bookingEmailService = bookingEmailService;
		_logs = logs;
	}

	// GET: CUSTOMERS
	public async Task<IActionResult> Index(string searchString, string sortBy, string currentFilter, int? page)
	{
		const int pageSize = 10;
		var pageNumber = page.GetValueOrDefault(1);
		if (pageNumber < 1)
		{
			pageNumber = 1;
		}

		ViewData["CurrentSearch"] = searchString;
		ViewData["CurrentSort"] = sortBy;
		ViewData["CurrentFilter"] = currentFilter;

		var customersQuery = _context.CustomerProfiles
			.Include(c => c.User)
			.AsQueryable();

		var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
		var totalCustomersCount = await customersQuery.CountAsync();
		var pendingCustomersCount = await customersQuery.CountAsync(c => c.User!.Status == Status.Pending);
		var totalAtMonthStart = await customersQuery.CountAsync(c => c.User != null && c.User.CreatedAt < monthStart);
		var pendingAtMonthStart = await customersQuery.CountAsync(c =>
			c.User!.Status == Status.Pending && c.User != null && c.User.CreatedAt < monthStart);

		ViewData["TotalCustomersCount"] = totalCustomersCount;
		ViewData["PendingCustomersCount"] = pendingCustomersCount;
		ViewData["TotalCustomersChange"] = PctChange(totalCustomersCount, totalAtMonthStart);
		ViewData["PendingCustomersChange"] = PctChange(pendingCustomersCount, pendingAtMonthStart);

		if (!string.IsNullOrEmpty(searchString))
		{
			var term = searchString.Trim();
			customersQuery = customersQuery.Where(c =>
				c.User!.FullName.Contains(term)
				|| c.User!.ContactNumber.Contains(term)
				|| (c.User != null && c.User.Email.Contains(term)));
		}

		if (!string.IsNullOrEmpty(currentFilter))
		{
			if (currentFilter == "Approved")
			{
				customersQuery = customersQuery.Where(c => c.User!.Status == Status.Active);
			}
			else if (Enum.TryParse(currentFilter, true, out Status filterStatus))
			{
				customersQuery = customersQuery.Where(c => c.User!.Status == filterStatus);
			}
		}

		customersQuery = sortBy switch
		{
			"Name" => customersQuery.OrderBy(c => c.User!.FullName),
			"Email" => customersQuery.OrderBy(c => c.User!.Email),
			"Status" => customersQuery.OrderBy(c => c.User!.Status),
			_ => customersQuery.OrderByDescending(c => c.CustomerId)
		};

		var totalCount = await customersQuery.CountAsync();
		var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
		if (pageNumber > totalPages)
		{
			pageNumber = totalPages;
		}

		ViewData["PageIndex"] = pageNumber;
		ViewData["TotalPages"] = totalPages;
		ViewData["TotalCount"] = totalCount;
		ViewData["PageSize"] = pageSize;

		var customers = await customersQuery
			.Skip((pageNumber - 1) * pageSize)
			.Take(pageSize)
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

	// POST: CUSTOMERS/Approve/5
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Approve(int customerid)
	{
		var customer = await _context.CustomerProfiles
			.Include(c => c.User)
			.FirstOrDefaultAsync(c => c.CustomerId == customerid);
		if (customer == null)
		{
			return NotFound();
		}

		customer.User!.Status = Status.Active;
		_logs.Record(
			SystemLogAction.Approved,
			SystemLogCategory.Customer,
			$"Approved customer {customer.User.FullName}.",
			"Customer",
			customer.CustomerId);
		await _context.SaveChangesAsync();

		var loginUrl = Url.Action("Login", "Account", null, Request.Scheme);
		await _bookingEmailService.SendAccountApprovedAsync(customer, loginUrl);

		TempData["SuccessMessage"] = "Customer account activated.";
		return RedirectToAction(nameof(Details), new { customerid });
	}

	// POST: CUSTOMERS/Reject/5
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Reject(int customerid)
	{
		var customer = await _context.CustomerProfiles
			.Include(c => c.User)
			.FirstOrDefaultAsync(c => c.CustomerId == customerid);
		if (customer == null)
		{
			return NotFound();
		}

		customer.User!.Status = Status.Inactive;
		_logs.Record(
			SystemLogAction.Deactivated,
			SystemLogCategory.Customer,
			$"Deactivated customer {customer.User.FullName}.",
			"Customer",
			customer.CustomerId);
		await _context.SaveChangesAsync();
		await _bookingEmailService.SendAccountDeactivatedAsync(customer);
		TempData["SuccessMessage"] = "Customer account deactivated. A notification email was sent if an email is on file.";
		return RedirectToAction(nameof(Details), new { customerid });
	}

	// GET: CUSTOMERS/Create
	public IActionResult Create()
	{
		return View(new CustomerAccountInputModel { Status = Status.Active });
	}

	// POST: CUSTOMERS/Create
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Create([Bind("FullName,ContactNumber,Email,Password,ConfirmPassword,ValidIDtype,FrontValidIDImagePath,BackValidIDImagePath,FrontValidIDImageFile,BackValidIDImageFile,Status")] CustomerAccountInputModel model)
	{
		model.ContactNumber = FieldRules.NormalizePhMobile(model.ContactNumber);
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

		if (model.FrontValidIDImageFile != null)
		{
			model.FrontValidIDImagePath = await ImageStorage.SaveAsync(
				_webHostEnvironment,
				model.FrontValidIDImageFile,
				ImageStorage.CustomersFolder);
		}

		if (model.BackValidIDImageFile != null)
		{
			model.BackValidIDImagePath = await ImageStorage.SaveAsync(
				_webHostEnvironment,
				model.BackValidIDImageFile,
				ImageStorage.CustomersFolder);
		}

		var user = new User
		{
			Email = model.Email.Trim(),
			FullName = model.FullName.Trim(),
			ContactNumber = FieldRules.NormalizePhMobile(model.ContactNumber),
			Role = UserRole.Customer,
			Status = model.Status,
			CreatedAt = DateTime.Now,
			EmailConfirmed = true
		};
		user.SetPassword(model.Password!);

		_context.Users.Add(user);
		await _context.SaveChangesAsync();

		var profile = new CustomerProfile
		{
			CustomerId = user.UserId,
			ValidIDtype = model.ValidIDtype,
			FrontValidIDImagePath = model.FrontValidIDImagePath,
			BackValidIDImagePath = model.BackValidIDImagePath
		};

		_context.CustomerProfiles.Add(profile);
		await _context.SaveChangesAsync();
		_logs.Record(
			SystemLogAction.Created,
			SystemLogCategory.Customer,
			$"Created customer {user.FullName}.",
			"Customer",
			profile.CustomerId);
		await _context.SaveChangesAsync();
		return RedirectToAction(nameof(CreateSuccess), new { customerid = profile.CustomerId });
	}

	// GET: CUSTOMERS/CreateSuccess/5
	public async Task<IActionResult> CreateSuccess(int? customerid)
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

		var model = new CreateSuccessViewModel
		{
			PageTitle = "Add New Customer",
			ActivePage = "Customers",
			Heading = "Customer Profile Created",
			MessageHtml = $"<strong>{customer.User.FullName}</strong> has been successfully created and added to the system.",
			PrimaryActionText = "View Customer's Profile",
			PrimaryActionUrl = Url.Action(nameof(Details), new { customerid = customer.CustomerId }) ?? "",
			SecondaryActionText = "Add Another Customer",
			SecondaryActionUrl = Url.Action(nameof(Create)) ?? ""
		};

		return View("CreateSuccess", model);
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
	public async Task<IActionResult> Edit(int? customerid, [Bind("CustomerId,FullName,ContactNumber,Email,Password,ConfirmPassword,ValidIDtype,FrontValidIDImagePath,BackValidIDImagePath,FrontValidIDImageFile,BackValidIDImageFile,Status")] CustomerAccountInputModel model)
	{
		model.ContactNumber = FieldRules.NormalizePhMobile(model.ContactNumber);
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
			if (model.FrontValidIDImageFile != null)
			{
				model.FrontValidIDImagePath = await ImageStorage.SaveAsync(
					_webHostEnvironment,
					model.FrontValidIDImageFile,
					ImageStorage.CustomersFolder);
			}

			if (model.BackValidIDImageFile != null)
			{
				model.BackValidIDImagePath = await ImageStorage.SaveAsync(
					_webHostEnvironment,
					model.BackValidIDImageFile,
					ImageStorage.CustomersFolder);
			}

			profile.User.Email = model.Email.Trim();
			profile.User.FullName = model.FullName.Trim();
			profile.User.ContactNumber = FieldRules.NormalizePhMobile(model.ContactNumber);
			profile.ValidIDtype = model.ValidIDtype;
			profile.FrontValidIDImagePath = model.FrontValidIDImagePath;
			profile.BackValidIDImagePath = model.BackValidIDImagePath;
			profile.User.Status = model.Status;

			if (!string.IsNullOrWhiteSpace(model.Password))
			{
				profile.User.SetPassword(model.Password);
			}

			_logs.Record(
				SystemLogAction.Updated,
				SystemLogCategory.Customer,
				$"Updated customer {profile.User.FullName}.",
				"Customer",
				profile.CustomerId);
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
			_logs.Record(
				SystemLogAction.Deleted,
				SystemLogCategory.Customer,
				$"Deleted customer {profile.User.FullName}.",
				"Customer",
				profile.CustomerId);
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

	private static decimal PctChange(decimal current, decimal previous)
		=> Math.Round((current - previous) * 0.1m, 1);
}
