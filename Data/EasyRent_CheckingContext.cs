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
		public DbSet<Rental> Rentals { get; set; } = default!;
		public DbSet<RentalVehicle> RentalVehicles { get; set; } = default!;
		public DbSet<Payment> Payments { get; set; } = default!;
		public DbSet<Driver> Drivers { get; set; } = default!;
		public DbSet<Vehicle> Vehicles { get; set; } = default!;
		public DbSet<Transit> Transits { get; set; } = default!;
		public DbSet<Feedback> Feedbacks { get; set; } = default!;
		public DbSet<MaintenanceLog> MaintenanceLogs { get; set; } = default!;
		public DbSet<MaintenancePlan> MaintenancePlans { get; set; } = default!;
		public DbSet<IncidentReport> IncidentReports { get; set; } = default!;
		public DbSet<TransitIssueLink> TransitIssueLinks { get; set; } = default!;
		public DbSet<VehicleFavorite> VehicleFavorites { get; set; } = default!;
		public DbSet<SystemLog> SystemLogs { get; set; } = default!;

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

			modelBuilder.Entity<RentalVehicle>(entity =>
			{
				entity.ToTable("RentalVehicle");
				entity.HasKey(e => e.RentalVehicleId);

				entity.HasOne(e => e.Rental)
					.WithMany(r => r.RentalVehicles)
					.HasForeignKey(e => e.RentalId)
					.OnDelete(DeleteBehavior.Cascade);

				entity.HasOne(e => e.Vehicle)
					.WithMany()
					.HasForeignKey(e => e.VehicleId)
					.OnDelete(DeleteBehavior.Restrict);

				entity.HasIndex(e => new { e.RentalId, e.VehicleId }).IsUnique();
				entity.HasIndex(e => e.VehicleId);
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

				entity.HasOne(e => e.RentalVehicle)
					.WithMany()
					.HasForeignKey(e => e.RentalVehicleId)
					.OnDelete(DeleteBehavior.SetNull);

				// Multi-vehicle: one transit per vehicle line (not unique on RentalID alone).
				entity.HasIndex(e => new { e.RentalID, e.VehicleID }).IsUnique();
				entity.HasIndex(e => e.RentalVehicleId);
			});

			modelBuilder.Entity<Driver>().ToTable("Driver");
			modelBuilder.Entity<Vehicle>(entity =>
			{
				entity.ToTable("Vehicle");
				entity.Property(e => e.Type).HasMaxLength(30).IsRequired();
			});

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

			modelBuilder.Entity<MaintenancePlan>(entity =>
			{
				entity.ToTable("MaintenancePlan");
				entity.HasKey(e => e.MaintenancePlanId);

				entity.HasOne(e => e.Vehicle)
					.WithMany()
					.HasForeignKey(e => e.VehicleId)
					.OnDelete(DeleteBehavior.Restrict);

				entity.HasIndex(e => new { e.VehicleId, e.Type }).IsUnique();
			});

			modelBuilder.Entity<MaintenanceLog>(entity =>
			{
				entity.ToTable("MaintenanceLog");
				entity.HasKey(e => e.MaintenanceLogId);

				entity.HasOne(e => e.Vehicle)
					.WithMany()
					.HasForeignKey(e => e.VehicleId)
					.OnDelete(DeleteBehavior.Restrict);

				entity.HasOne(e => e.Plan)
					.WithMany(p => p.Logs)
					.HasForeignKey(e => e.MaintenancePlanId)
					.OnDelete(DeleteBehavior.SetNull);

				entity.HasIndex(e => e.VehicleId);
				entity.HasIndex(e => e.MaintenancePlanId);
			});

			modelBuilder.Entity<VehicleFavorite>(entity =>
			{
				entity.ToTable("VehicleFavorite");
				entity.HasKey(e => e.FavoriteId);

				entity.HasOne(e => e.Customer)
					.WithMany()
					.HasForeignKey(e => e.CustomerId)
					.OnDelete(DeleteBehavior.Cascade);

				entity.HasOne(e => e.Vehicle)
					.WithMany()
					.HasForeignKey(e => e.VehicleId)
					.OnDelete(DeleteBehavior.Cascade);

				entity.HasIndex(e => new { e.CustomerId, e.VehicleId }).IsUnique();
			});

			modelBuilder.Entity<IncidentReport>(entity =>
			{
				entity.ToTable("IncidentReport");
				entity.HasKey(e => e.IncidentReportId);

				entity.HasOne(e => e.Vehicle)
					.WithMany()
					.HasForeignKey(e => e.VehicleId)
					.OnDelete(DeleteBehavior.Restrict);

				entity.HasOne(e => e.Transit)
					.WithMany()
					.HasForeignKey(e => e.TransitID)
					.OnDelete(DeleteBehavior.SetNull);

				entity.HasOne(e => e.Driver)
					.WithMany()
					.HasForeignKey(e => e.DriverID)
					.OnDelete(DeleteBehavior.SetNull);

				entity.HasIndex(e => e.VehicleId);
				entity.HasIndex(e => e.TransitID);
				entity.HasIndex(e => e.DriverID);
				entity.HasIndex(e => e.Status);
			});

			modelBuilder.Entity<TransitIssueLink>(entity =>
			{
				entity.ToTable("TransitIssueLink");
				entity.HasKey(e => e.TransitIssueLinkId);

				entity.HasOne(e => e.Transit)
					.WithMany()
					.HasForeignKey(e => e.TransitID)
					.OnDelete(DeleteBehavior.Cascade);

				entity.HasOne(e => e.IncidentReport)
					.WithMany()
					.HasForeignKey(e => e.IncidentReportId)
					.OnDelete(DeleteBehavior.SetNull);

				entity.HasOne(e => e.MaintenanceLog)
					.WithMany()
					.HasForeignKey(e => e.MaintenanceLogId)
					.OnDelete(DeleteBehavior.SetNull);

				entity.HasIndex(e => e.TransitID);
				entity.HasIndex(e => e.IncidentReportId);
				entity.HasIndex(e => e.MaintenanceLogId);
			});

			modelBuilder.Entity<SystemLog>(entity =>
			{
				entity.ToTable("SystemLog");
				entity.HasKey(e => e.SystemLogId);
				entity.HasIndex(e => e.CreatedAt);
				entity.HasIndex(e => new { e.Category, e.Action });
			});
		}
	}
}
