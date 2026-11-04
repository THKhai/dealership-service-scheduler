using Scheduler.Application.Domain.Entities;

namespace Scheduler.Application.Abstractions;

public interface IAppointmentRepository
{
    Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Read-only view with dealership, service, bay, technician and vehicle names.</summary>
    Task<AppointmentDetails?> GetDetailsAsync(Guid id, CancellationToken ct);

    /// <summary>Idempotency check before booking (section 7, 8.4).</summary>
    Task<Appointment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct);

    /// <summary>A-07: a vehicle cannot have overlapping Confirmed appointments.</summary>
    Task<bool> VehicleHasOverlapAsync(Guid vehicleId, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken ct);

    /// <summary>Saves a new appointment in its own transaction.</summary>
    /// <exception cref="Exceptions.SlotTakenException">An exclusion constraint rejected the insert.</exception>
    /// <exception cref="Exceptions.DuplicateIdempotencyKeyException">The idempotency key was saved by a concurrent request.</exception>
    Task InsertAsync(Appointment appointment, CancellationToken ct);

    /// <summary>Saves a status change (cancel, complete, no-show).</summary>
    Task UpdateAsync(Appointment appointment, CancellationToken ct);
}