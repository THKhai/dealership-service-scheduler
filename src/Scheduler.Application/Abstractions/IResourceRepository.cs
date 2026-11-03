using Scheduler.Application.Domain.Entities;
using Scheduler.Application.Domain.Enums;

namespace Scheduler.Application.Abstractions;

public interface IResourceRepository
{
    /// <summary>
    /// Everything availability needs in one round trip (section 7): active bays of the required type,
    /// active technicians qualified for the service, and Confirmed bookings overlapping the window.
    /// </summary>
    Task<ResourceSnapshot> GetSnapshotAsync(
        Guid dealershipId,
        Guid serviceTypeId,
        BayType bayType,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken ct);
}

public sealed record ResourceSnapshot(
    IReadOnlyList<ServiceBay> Bays,
    IReadOnlyList<Technician> Technicians,
    IReadOnlyList<BusyPeriod> BusyPeriods);

/// <summary>A Confirmed appointment holding a bay and a technician.</summary>
public sealed record BusyPeriod(Guid ServiceBayId, Guid TechnicianId, DateTimeOffset StartUtc, DateTimeOffset EndUtc);
