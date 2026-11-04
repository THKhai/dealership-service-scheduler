using Microsoft.EntityFrameworkCore;
using Npgsql;
using Scheduler.Application.Abstractions;
using Scheduler.Application.Domain.Entities;
using Scheduler.Application.Domain.Enums;
using Scheduler.Application.Exceptions;

namespace Scheduler.Infrastructure.Persistence.Repositories;

internal sealed class AppointmentRepository(SchedulerDbContext db) : IAppointmentRepository
{
    // Tracked, because cancel/complete/no-show load the entity, change it, then call UpdateAsync.
    public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Appointments.FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<AppointmentDetails?> GetDetailsAsync(Guid id, CancellationToken ct) =>
        (from a in db.Appointments
         join d in db.Dealerships on a.DealershipId equals d.Id
         join s in db.ServiceTypes on a.ServiceTypeId equals s.Id
         join b in db.ServiceBays on a.ServiceBayId equals b.Id
         join t in db.Technicians on a.TechnicianId equals t.Id
         join v in db.Vehicles on a.VehicleId equals v.Id
         where a.Id == id
         select new AppointmentDetails(
             a.Id, a.Status, a.Channel, a.StartUtc, a.EndUtc,
             d.Id, d.Name, d.TimeZone,
             s.Id, s.Code, s.Name, s.DurationMinutes,
             b.Id, b.Name,
             t.Id, t.Name,
             v.Vin))
        .AsNoTracking()
        .FirstOrDefaultAsync(ct);

    public Task<Appointment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) =>
        db.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.IdempotencyKey == idempotencyKey, ct);

    // Half-open overlap, the same rule as the '[)' ranges in the constraints:
    // two periods overlap when each one starts before the other ends.
    public Task<bool> VehicleHasOverlapAsync(Guid vehicleId, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken ct) =>
        db.Appointments.AnyAsync(a =>
            a.VehicleId == vehicleId &&
            a.Status == AppointmentStatus.Confirmed &&
            a.StartUtc < endUtc &&
            a.EndUtc > startUtc, ct);

    public async Task InsertAsync(Appointment appointment, CancellationToken ct)
    {
        db.Appointments.Add(appointment);
        try
        {
            // SaveChanges wraps the INSERT in its own transaction.
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg)
        {
            // The failed entity stays in the tracker as Added. Without Clear(), the retry's
            // SaveChanges would try to insert it again and fail the same way (8.3).
            db.ChangeTracker.Clear();

            throw pg.SqlState switch
            {
                PostgresErrorCodes.ExclusionViolation => new SlotTakenException(ToResource(pg.ConstraintName)),
                PostgresErrorCodes.UniqueViolation when pg.ConstraintName == "ix_appointment_idempotency_key"
                    => new DuplicateIdempotencyKeyException(appointment.IdempotencyKey),
                _ => ex
            };
        }
    }

    public Task UpdateAsync(Appointment appointment, CancellationToken ct)
    {
        // Attach covers an entity that was loaded by another context; for a tracked one it is a no-op.
        if (db.Entry(appointment).State == EntityState.Detached)
        {
            db.Appointments.Update(appointment);
        }

        return db.SaveChangesAsync(ct);
    }

    private static Resource ToResource(string? constraintName) => constraintName switch
    {
        "ex_appointment_bay" => Resource.Bay,
        "ex_appointment_technician" => Resource.Technician,
        "ex_appointment_vehicle" => Resource.Vehicle,
        _ => throw new InvalidOperationException($"Unknown exclusion constraint '{constraintName}'.")
    };
}
