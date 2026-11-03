using Microsoft.EntityFrameworkCore;
using Scheduler.Application.Abstractions;
using Scheduler.Application.Domain.Enums;

namespace Scheduler.Infrastructure.Persistence.Repositories;

internal sealed class ResourceRepository(SchedulerDbContext db) : IResourceRepository
{
    public async Task<ResourceSnapshot> GetSnapshotAsync(
        Guid dealershipId,
        Guid serviceTypeId,
        BayType bayType,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken ct)
    {
        // Read-only: AsNoTracking skips change-tracking overhead.
        // The queries run one after another; a DbContext does not support parallel queries.
        var bays = await db.ServiceBays.AsNoTracking()
            .Where(b => b.DealershipId == dealershipId && b.IsActive && b.BayType == bayType)
            .OrderBy(b => b.Name)
            .ToListAsync(ct);

        var technicians = await db.Technicians.AsNoTracking()
            .Where(t => t.DealershipId == dealershipId && t.IsActive &&
                        t.Skills.Any(s => s.ServiceTypeId == serviceTypeId))
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        // Uses ix_appointment_dealership_id_start_utc. Projected straight to the record,
        // so no Appointment entities are created.
        var busy = await db.Appointments.AsNoTracking()
            .Where(a => a.DealershipId == dealershipId &&
                        a.Status == AppointmentStatus.Confirmed &&
                        a.StartUtc < toUtc &&
                        a.EndUtc > fromUtc)
            .Select(a => new BusyPeriod(a.ServiceBayId, a.TechnicianId, a.StartUtc, a.EndUtc))
            .ToListAsync(ct);

        return new ResourceSnapshot(bays, technicians, busy);
    }
}
