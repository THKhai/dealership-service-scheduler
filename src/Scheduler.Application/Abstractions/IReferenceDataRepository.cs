using Scheduler.Application.Domain.Entities;

namespace Scheduler.Application.Abstractions;

public interface IReferenceDataRepository
{
    /// <summary>Includes opening hours.</summary>
    Task<Dealership?> GetDealershipAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<Dealership>> ListDealershipsAsync(CancellationToken ct);

    Task<ServiceType?> GetServiceTypeAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<ServiceType>> ListServiceTypesAsync(CancellationToken ct);
}
