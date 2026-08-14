using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Models;

namespace EasyRent_Checking.Data
{
	public class EasyRent_CheckingContext : DbContext
	{
		public EasyRent_CheckingContext(DbContextOptions<EasyRent_CheckingContext> options)
			: base(options)
		{
		}

		public DbSet<User> Users { get; set; } = default!;
		public DbSet<CustomerProfile> CustomerProfiles { get; set; } = default!;
		public DbSet<AdminProfile> AdminProfiles { get; set; } = default!;
		public DbSet<Rental> Rentals { get; set; } = default!;
		public DbSet<RentalDetails> RentalDetails { get; set; } = default!;
		public DbSet<Payment> Payments { get; set; } = default!;
		public DbSet<Driver> Drivers { get; set; } = default!;
		public DbSet<Vehicle> Vehicles { get; set; } = default!;
		public DbSet<Transit> Transits { get; set; } = default!;
		public DbSet<Feedback> Feedbacks { get; set; } = default!;

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
				entity.ToTable("Payment");
				entity.HasOne(e => e.Rental)
					.WithMany()
					.HasForeignKey(e => e.RentalId)
					.OnDelete(DeleteBehavior.Restrict);
			});

			modelBuilder.Entity<Rental>(entity =>
			{
				entity.ToTable("Rental");
				entity.HasOne(e => e.Customer)
					.WithMany()
					.HasForeignKey(e => e.CustomerId)
					.OnDelete(DeleteBehavior.SetNull);

				entity.HasIndex(e => e.CustomerId);
			});

			modelBuilder.Entity<RentalDetails>(entity =>
			{
				entity.ToTable("RentalDetails");
				entity.HasKey(e => e.RentalDetailsID);

				entity.HasOne(e => e.Rental)
					.WithOne(r => r.Details)
					.HasForeignKey<RentalDetails>(e => e.RentalID)
					.OnDelete(DeleteBehavior.Cascade);

				entity.HasOne(e => e.Vehicle)
					.WithMany()
					.HasForeignKey(e => e.VehicleId)
					.OnDelete(DeleteBehavior.Restrict);

				entity.HasIndex(e => e.RentalID).IsUnique();
			});

			modelBuilder.Entity<Transit>(entity =>
			{
				entity.ToTable("Transit");
				entity.HasKey(e => e.TransitID);

				entity.HasOne(e => e.Rental)
					.WithMany()
					.HasForeignKey(e => e.RentalID)
					.OnDelete(DeleteBehavior.Restrict);

				entity.HasOne(e => e.Driver)
					.WithMany()
					.HasForeignKey(e => e.DriverID)
					.OnDelete(DeleteBehavior.SetNull);

				entity.HasOne(e => e.Vehicle)
					.WithMany()
					.HasForeignKey(e => e.VehicleID)
					.OnDelete(DeleteBehavior.Restrict);

				entity.HasIndex(e => e.RentalID).IsUnique();
			});

			modelBuilder.Entity<Driver>().ToTable("Driver");
			modelBuilder.Entity<Vehicle>().ToTable("Vehicle");

			modelBuilder.Entity<Feedback>(entity =>
			{
				entity.ToTable("Feedback");
				entity.HasKey(e => e.FeedbackId);

				entity.HasOne(e => e.Transit)
					.WithOne(t => t.Feedback)
					.HasForeignKey<Feedback>(e => e.TransitID)
					.OnDelete(DeleteBehavior.Restrict);

				entity.HasOne(e => e.Customer)
					.WithMany()
					.HasForeignKey(e => e.CustomerId)
					.OnDelete(DeleteBehavior.Restrict);

				entity.HasIndex(e => e.TransitID).IsUnique();
			});
		}
	}
}
