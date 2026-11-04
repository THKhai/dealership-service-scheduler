namespace Scheduler.Application.Exceptions;

/// <summary>A-07: the vehicle already has a Confirmed appointment that overlaps the requested time.</summary>
public class VehicleAlreadyBookedException()
    : DomainException("VEHICLE_ALREADY_BOOKED", "This vehicle already has an appointment that overlaps the requested time.");
