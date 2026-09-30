using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Data;
using EasyRent_Checking.Models;
using EasyRent_Checking.Services;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

var keysPath = builder.Environment.IsDevelopment()
	? Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "private", "aspnet-keys"))
	: Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys");
try
{
	Directory.CreateDirectory(keysPath);
}
catch when (!builder.Environment.IsDevelopment())
{
	keysPath = Path.Combine(Path.GetTempPath(), "EasyRent_Checking", "keys");
	Directory.CreateDirectory(keysPath);
}

builder.Services.AddDataProtection()
	.SetApplicationName("EasyRent_Checking")
	.PersistKeysToFileSystem(new DirectoryInfo(keysPath));

builder.Services.AddDbContext<EasyRent_CheckingContext>(options =>
	options.UseSqlServer(
		builder.Configuration.GetConnectionString("EasyRent_CheckingContext")
			?? throw new InvalidOperationException("Connection string 'EasyRent_CheckingContext' not found."),
		sql => sql.EnableRetryOnFailure(maxRetryCount: 3)));

// Add services to the container.
builder.Services.AddControllersWithViews()
	.AddRazorRuntimeCompilation();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<ReceiptPdfService>();
builder.Services.AddScoped<BookingEmailService>();
builder.Services.AddScoped<AdminNotificationService>();
builder.Services.AddScoped<SystemLogService>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
	.AddCookie(options =>
	{
		options.LoginPath = "/Account/Login";
		options.LogoutPath = "/Account/Logout";
		options.AccessDeniedPath = "/Account/Login";
		options.SlidingExpiration = true;
		options.ExpireTimeSpan = TimeSpan.FromDays(14);
	});
builder.Services.AddAuthorization(options =>
{
	options.AddPolicy("StaffArea", policy =>
		policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Staff)));
	options.AddPolicy("AdminOnly", policy =>
		policy.RequireRole(nameof(UserRole.Admin)));
});

var app = builder.Build();

var errorLogPath = Path.Combine(app.Environment.ContentRootPath, "App_Data", "error-log.txt");
Directory.CreateDirectory(Path.GetDirectoryName(errorLogPath)!);

app.Use(async (context, next) =>
{
	try
	{
		await next();
	}
	catch (Exception ex)
	{
		var entry = $"[{DateTime.UtcNow:u}] {context.Request.Method} {context.Request.Path}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}";
		try
		{
			await File.AppendAllTextAsync(errorLogPath, entry);
		}
		catch
		{
			// Ignore logging failures.
		}

		throw;
	}
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
	var path = context.Request.Path.Value;
	if (!string.IsNullOrEmpty(path)
		&& path.StartsWith("/images/Reservations", StringComparison.OrdinalIgnoreCase))
	{
		context.Request.Path = "/images/Rentals" + path["/images/Reservations".Length..];
	}

	await next();
});

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "legacy-client-reservation",
    pattern: "ClientSide/Reservation/{id?}",
    defaults: new { controller = "ClientSide", action = "Rental" });

app.MapControllerRoute(
    name: "legacy-client-pay-reservation",
    pattern: "ClientSide/PayReservation/{id?}",
    defaults: new { controller = "ClientSide", action = "PayRental" });

app.MapControllerRoute(
    name: "legacy-vehicles-reservation",
    pattern: "Vehicles/Reservation/{id?}",
    defaults: new { controller = "ClientSide", action = "Rental" });

app.MapControllerRoute(
    name: "legacy-vehicles-pay-reservation",
    pattern: "Vehicles/PayReservation/{id?}",
    defaults: new { controller = "ClientSide", action = "PayRental" });

app.MapControllerRoute(
    name: "legacy-client-from-vehicles",
    pattern: "Vehicles/{action}/{id?}",
    defaults: new { controller = "ClientSide" },
    constraints: new { action = "Homepage|HowItWorks|AboutUs|ContactUs|PrivacyPolicy|TermsConditions|Browse|VehicleDetails|Rental|PayRental|MyBookings|MyBookingDetails|RateTrip|MyFavorites|ToggleFavorite" });

app.MapControllerRoute(
    name: "legacy-admin-dashboard",
    pattern: "Admin/Dashboard/{id?}",
    defaults: new { controller = "Dashboard", action = "Index" });

app.MapControllerRoute(
    name: "legacy-admin",
    pattern: "Admin",
    defaults: new { controller = "Dashboard", action = "Index" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=ClientSide}/{action=Homepage}/{id?}");

app.Run();
