using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Models;

namespace EasyRent_Checking.Data
{
	public class EasyRent_CheckingContext : DbContext
	{
		public DbSet<EasyRent_Checking.Models.Transit> Transit { get; set; } = default!;
		public EasyRent_CheckingContext(DbContextOptions<EasyRent_CheckingContext> options)
			: base(options)
		{
		}

		public DbSet<User> Users { get; set; } = default!;
		public DbSet<CustomerProfile> CustomerProfiles { get; set; } = default!;
		public DbSet<AdminProfile> AdminProfiles { get; set; } = default!;
		public DbSet<Reservation> Reservation { get; set; } = default!;
		public DbSet<ReservationDetails> ReservationDetails { get; set; } = default!;
		public DbSet<Payment> Payment { get; set; } = default!;
		public DbSet<Driver> Driver { get; set; } = default!;
		public DbSet<Vehicle> Vehicle { get; set; } = default!;

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<User>(entity =>
			{
				entity.HasKey(e => e.UserId);
				entity.HasIndex(e => e.Email).IsUnique();
				entity.Ignore(e => e.Password);
				entity.Ignore(e => e.ConfirmPassword);
			});

			modelBuilder.Entity<CustomerProfile>(entity =>
			{
				entity.HasKey(e => e.CustomerId);

				entity.HasOne(e => e.User)
					.WithOne(u => u.CustomerProfile)
					.HasForeignKey<CustomerProfile>(e => e.CustomerId)
					.OnDelete(DeleteBehavior.Cascade);
			});

			modelBuilder.Entity<AdminProfile>(entity =>
			{
				entity.HasKey(e => e.AdminId);

				entity.HasOne(e => e.User)
					.WithOne(u => u.AdminProfile)
					.HasForeignKey<AdminProfile>(e => e.AdminId)
					.OnDelete(DeleteBehavior.Cascade);
			});

			modelBuilder.Entity<Payment>(entity =>
			{
				entity.HasOne(e => e.Reservation)
					.WithMany()
					.HasForeignKey(e => e.ReservationId)
					.OnDelete(DeleteBehavior.Restrict);
			});

			modelBuilder.Entity<ReservationDetails>(entity =>
			{
				entity.HasKey(e => e.ReservationDetailsID);

				entity.HasOne(e => e.Reservation)
					.WithOne(r => r.Details)
					.HasForeignKey<ReservationDetails>(e => e.ReservationID)
					.OnDelete(DeleteBehavior.Cascade);

				entity.HasOne(e => e.Vehicle)
					.WithMany()
					.HasForeignKey(e => e.VehicleId)
					.OnDelete(DeleteBehavior.Restrict);

				entity.HasIndex(e => e.ReservationID).IsUnique();
			});

			modelBuilder.Entity<Transit>(entity =>
			{
				entity.HasKey(e => e.TransitID);

				entity.HasOne(e => e.Reservation)
					.WithMany()
					.HasForeignKey(e => e.ReservationID)
					.OnDelete(DeleteBehavior.Restrict);

				entity.HasOne(e => e.Driver)
					.WithMany()
					.HasForeignKey(e => e.DriverID)
					.OnDelete(DeleteBehavior.SetNull);

				entity.HasOne(e => e.Vehicle)
					.WithMany()
					.HasForeignKey(e => e.VehicleID)
					.OnDelete(DeleteBehavior.Restrict);

				entity.HasIndex(e => e.ReservationID).IsUnique();
			});
		}
	}
}
