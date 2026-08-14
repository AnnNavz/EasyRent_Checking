using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Data;
using EasyRent_Checking.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<EasyRent_CheckingContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("EasyRent_CheckingContext") ?? throw new InvalidOperationException("Connection string 'EasyRent_CheckingContext' not found.")));

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<BookingEmailService>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
	.AddCookie(options =>
	{
		options.LoginPath = "/Account/Login";
		options.LogoutPath = "/Account/Logout";
		options.AccessDeniedPath = "/Account/Login";
		options.SlidingExpiration = true;
		options.ExpireTimeSpan = TimeSpan.FromDays(14);
	});
builder.Services.AddAuthorization();

var app = builder.Build();

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
    constraints: new { action = "Homepage|Browse|VehicleDetails|Rental|PayRental|MyBookings|MyBookingDetails|RateTrip" });

app.MapControllerRoute(
    name: "legacy-admin-dashboard",
    pattern: "Admin/{action=Dashboard}/{id?}",
    defaults: new { controller = "Dashboard", action = "Index" },
    constraints: new { action = "Dashboard|Index" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=ClientSide}/{action=Homepage}/{id?}");

app.Run();
