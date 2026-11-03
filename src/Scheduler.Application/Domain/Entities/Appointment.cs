using Scheduler.Application.Domain.Enums;
using Scheduler.Application.Exceptions;

namespace Scheduler.Application.Domain.Entities;

public class Appointment
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid DealershipId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid ServiceTypeId { get; private set; }
    public Guid ServiceBayId { get; private set; }
    public Guid TechnicianId { get; private set; }
    public DateTimeOffset StartUtc { get; private set; }
    public DateTimeOffset EndUtc { get; private set; }
    public AppointmentStatus Status { get; private set; }
    public BookingChannel Channel { get; private set; }
    public string CreatedBy { get; private set; } = null!;
    public string IdempotencyKey { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    // EF Core uses this when loading rows; application code must use Confirm.
    private Appointment() { }

    public static Appointment Confirm(
        Guid dealershipId,
        Guid customerId,
        Guid vehicleId,
        ServiceType serviceType,
        Guid serviceBayId,
        Guid technicianId,
        DateTimeOffset start,
        BookingChannel channel,
        string createdBy,
        string idempotencyKey,
        DateTimeOffset now)
    {
        var startUtc = start.ToUniversalTime();

        return new Appointment
        {
            DealershipId = dealershipId,
            CustomerId = customerId,
            VehicleId = vehicleId,
            ServiceTypeId = serviceType.Id,
            ServiceBayId = serviceBayId,
            TechnicianId = technicianId,
            StartUtc = startUtc,
            EndUtc = startUtc.AddMinutes(serviceType.DurationMinutes),
            Status = AppointmentStatus.Confirmed,
            Channel = channel,
            CreatedBy = createdBy,
            IdempotencyKey = idempotencyKey,
            CreatedAt = now
        };
    }

    public void Cancel(DateTimeOffset now)
    {
        EnsureConfirmed(AppointmentStatus.Cancelled);

        // UC-04: only a future appointment can be cancelled.
        if (StartUtc <= now)
        {
            throw new InvalidStatusTransitionException(Status, AppointmentStatus.Cancelled);
        }

        Status = AppointmentStatus.Cancelled;
    }

    public void Complete()
    {
        EnsureConfirmed(AppointmentStatus.Completed);
        Status = AppointmentStatus.Completed;
    }

    public void MarkNoShow()
    {
        EnsureConfirmed(AppointmentStatus.NoShow);
        Status = AppointmentStatus.NoShow;
    }

    private void EnsureConfirmed(AppointmentStatus target)
    {
        if (Status != AppointmentStatus.Confirmed)
        {
            throw new InvalidStatusTransitionException(Status, target);
        }
    }
}
