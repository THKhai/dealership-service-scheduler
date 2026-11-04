using Microsoft.EntityFrameworkCore;
using Npgsql;
using Scheduler.Application.Abstractions;
using Scheduler.Application.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Repositories;

internal sealed class CustomerRepository(SchedulerDbContext db) : ICustomerRepository
{
    public Task<Customer?> GetByEmailAsync(string email, CancellationToken ct) =>
        db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Email == email, ct);

    public async Task<Customer> AddAsync(Customer customer, CancellationToken ct)
    {
        db.Customers.Add(customer);
        try
        {
            await db.SaveChangesAsync(ct);
            return customer;
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // A concurrent request registered the same email first: use that row.
            db.ChangeTracker.Clear();
            return await GetByEmailAsync(customer.Email, ct)
                ?? throw new InvalidOperationException("Unique violation but no customer found.", ex);
        }
    }
}

internal sealed class VehicleRepository(SchedulerDbContext db) : IVehicleRepository
{
    public Task<Vehicle?> GetByVinAsync(string vin, CancellationToken ct) =>
        db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Vin == vin, ct);

    public async Task<Vehicle> AddAsync(Vehicle vehicle, CancellationToken ct)
    {
        db.Vehicles.Add(vehicle);
        try
        {
            await db.SaveChangesAsync(ct);
            return vehicle;
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // A concurrent request registered the same VIN first: use that row.
            db.ChangeTracker.Clear();
            return await GetByVinAsync(vehicle.Vin, ct)
                ?? throw new InvalidOperationException("Unique violation but no vehicle found.", ex);
        }
    }
}

internal static class DbUpdateExceptionExtensions
{
    public static bool IsUniqueViolation(this DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
