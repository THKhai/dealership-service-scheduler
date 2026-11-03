using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Application.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customer");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(200);
        builder.Property(c => c.Email).HasMaxLength(254);
        builder.Property(c => c.Phone).HasMaxLength(32);

        // Customers are matched by email (9.3), so it must identify one customer.
        builder.HasIndex(c => c.Email).IsUnique();
    }
}

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicle");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Vin).HasMaxLength(17);
        builder.HasIndex(v => v.Vin).IsUnique();
        builder.Property(v => v.Make).HasMaxLength(100);
        builder.Property(v => v.Model).HasMaxLength(100);

        builder.HasOne<Customer>().WithMany().HasForeignKey(v => v.CustomerId);
    }
}
