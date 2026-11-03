using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Application.Domain.Entities;
using Scheduler.Infrastructure.Persistence.Seed;

namespace Scheduler.Infrastructure.Persistence.Configurations;

internal sealed class DealershipConfiguration : IEntityTypeConfiguration<Dealership>
{
    public void Configure(EntityTypeBuilder<Dealership> builder)
    {
        builder.ToTable("dealership");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Name).HasMaxLength(200);
        builder.Property(d => d.TimeZone).HasMaxLength(64);

        builder.HasMany(d => d.OpeningHours)
            .WithOne()
            .HasForeignKey(h => h.DealershipId);

        builder.HasData(SeedData.Dealerships);
    }
}

internal sealed class OpeningHoursConfiguration : IEntityTypeConfiguration<OpeningHours>
{
    public void Configure(EntityTypeBuilder<OpeningHours> builder)
    {
        builder.ToTable("opening_hours", t =>
            t.HasCheckConstraint("ck_opening_hours_range", "close_time > open_time"));

        // One opening period per day (see BookingPolicy).
        builder.HasKey(h => new { h.DealershipId, h.DayOfWeek });

        builder.HasData(SeedData.OpeningHours);
    }
}
