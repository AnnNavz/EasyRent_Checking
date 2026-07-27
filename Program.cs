using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EasyRent_Checking.Data;
using EasyRent_Checking.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<EasyRent_CheckingContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("EasyRent_CheckingContext") ?? throw new InvalidOperationException("Connection string 'EasyRent_CheckingContext' not found.")));

builder.Services.AddScoped<ReservationAvailabilityService>();
builder.Services.AddHostedService<AbandonedReservationCleanupService>();

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
	options.IdleTimeout = TimeSpan.FromHours(2);
	options.Cookie.HttpOnly = true;
	options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Admin}/{action=Dashboard}/{id?}");

app.Run();
