using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Application.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Configurations;

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        // The slot column and exclusion constraints (8.3) cannot be modelled by EF Core;
        // they are added with raw SQL in the InitialCreate migration.
        builder.ToTable("appointment", t =>
            t.HasCheckConstraint("ck_appointment_range", "end_utc > start_utc"));

        builder.HasKey(a => a.Id);

        // Stored as text so that the constraints can filter on status = 'Confirmed'.
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Channel).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.CreatedBy).HasMaxLength(100);
        builder.Property(a => a.IdempotencyKey).HasMaxLength(100);

        builder.HasIndex(a => a.IdempotencyKey).IsUnique();
        builder.HasIndex(a => new { a.DealershipId, a.StartUtc });

        builder.HasOne<Dealership>().WithMany().HasForeignKey(a => a.DealershipId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(a => a.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(a => a.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ServiceType>().WithMany().HasForeignKey(a => a.ServiceTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ServiceBay>().WithMany().HasForeignKey(a => a.ServiceBayId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Technician>().WithMany().HasForeignKey(a => a.TechnicianId).OnDelete(DeleteBehavior.Restrict);
    }
}
