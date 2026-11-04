using Scheduler.Application.Domain.Enums;

namespace Scheduler.Application.Abstractions;

/// <summary>
/// Read model for API responses: the appointment joined with the names of everything it references.
/// Loaded with one projected query instead of several entity loads.
/// </summary>
public sealed record AppointmentDetails(
    Guid Id,
    AppointmentStatus Status,
    BookingChannel Channel,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    Guid DealershipId,
    string DealershipName,
    string TimeZone,
    Guid ServiceTypeId,
    string ServiceTypeCode,
    string ServiceTypeName,
    int DurationMinutes,
    Guid ServiceBayId,
    string ServiceBayName,
    Guid TechnicianId,
    string TechnicianName,
    string Vin);
