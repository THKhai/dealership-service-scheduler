using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Application.Domain.Entities;
using Scheduler.Infrastructure.Persistence.Seed;

namespace Scheduler.Infrastructure.Persistence.Configurations;

internal sealed class ServiceTypeConfiguration : IEntityTypeConfiguration<ServiceType>
{
    public void Configure(EntityTypeBuilder<ServiceType> builder)
    {
        builder.ToTable("service_type", t =>
            t.HasCheckConstraint("ck_service_type_duration", "duration_minutes > 0"));
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Code).HasMaxLength(50);
        builder.HasIndex(s => s.Code).IsUnique();
        builder.Property(s => s.Name).HasMaxLength(200);
        builder.Property(s => s.RequiredBayType).HasConversion<string>().HasMaxLength(20);

        builder.HasData(SeedData.ServiceTypes);
    }
}

internal sealed class ServiceBayConfiguration : IEntityTypeConfiguration<ServiceBay>
{
    public void Configure(EntityTypeBuilder<ServiceBay> builder)
    {
        builder.ToTable("service_bay");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Name).HasMaxLength(100);
        builder.Property(b => b.BayType).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<Dealership>().WithMany().HasForeignKey(b => b.DealershipId);

        builder.HasData(SeedData.ServiceBays);
    }
}

internal sealed class TechnicianConfiguration : IEntityTypeConfiguration<Technician>
{
    public void Configure(EntityTypeBuilder<Technician> builder)
    {
        builder.ToTable("technician");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(200);

        builder.HasOne<Dealership>().WithMany().HasForeignKey(t => t.DealershipId);
        builder.HasMany(t => t.Skills).WithOne().HasForeignKey(s => s.TechnicianId);

        builder.HasData(SeedData.Technicians);
    }
}

internal sealed class TechnicianSkillConfiguration : IEntityTypeConfiguration<TechnicianSkill>
{
    public void Configure(EntityTypeBuilder<TechnicianSkill> builder)
    {
        builder.ToTable("technician_skill");
        builder.HasKey(s => new { s.TechnicianId, s.ServiceTypeId });

        builder.HasOne<ServiceType>().WithMany().HasForeignKey(s => s.ServiceTypeId);

        builder.HasData(SeedData.TechnicianSkills);
    }
}
