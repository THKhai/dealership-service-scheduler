using Microsoft.EntityFrameworkCore;
using Scheduler.Application.Abstractions;
using Scheduler.Application.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Repositories;

internal sealed class ReferenceDataRepository(SchedulerDbContext db) : IReferenceDataRepository
{
    public Task<Dealership?> GetDealershipAsync(Guid id, CancellationToken ct) =>
        db.Dealerships.AsNoTracking()
            .Include(d => d.OpeningHours)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IReadOnlyList<Dealership>> ListDealershipsAsync(CancellationToken ct) =>
        await db.Dealerships.AsNoTracking()
            .Include(d => d.OpeningHours)
            .OrderBy(d => d.Name)
            .ToListAsync(ct);

    public Task<ServiceType?> GetServiceTypeAsync(Guid id, CancellationToken ct) =>
        db.ServiceTypes.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<ServiceType>> ListServiceTypesAsync(CancellationToken ct) =>
        await db.ServiceTypes.AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync(ct);
}
