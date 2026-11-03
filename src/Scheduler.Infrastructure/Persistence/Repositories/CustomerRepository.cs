using Microsoft.EntityFrameworkCore;
using Scheduler.Application.Abstractions;
using Scheduler.Application.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Repositories;

internal sealed class CustomerRepository(SchedulerDbContext db) : ICustomerRepository
{
    public Task<Customer?> GetByEmailAsync(string email, CancellationToken ct) =>
        db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Email == email, ct);

    public async Task AddAsync(Customer customer, CancellationToken ct)
    {
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);
    }
}

internal sealed class VehicleRepository(SchedulerDbContext db) : IVehicleRepository
{
    public Task<Vehicle?> GetByVinAsync(string vin, CancellationToken ct) =>
        db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Vin == vin, ct);

    public async Task AddAsync(Vehicle vehicle, CancellationToken ct)
    {
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(ct);
    }
}
