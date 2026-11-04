using Scheduler.Application.Abstractions;
using Scheduler.Application.Availability;
using Scheduler.Application.Domain.Entities;
using Scheduler.Application.Domain.Enums;

namespace Scheduler.Api.Contracts;

public sealed record NamedRef(Guid Id, string Name);

public sealed record ServiceTypeRef(Guid Id, string Code, string Name, int DurationMinutes);

public sealed record VehicleRef(string Vin);

/// <summary>Times are returned in the dealership's timezone (9.1).</summary>
public sealed record AppointmentResponse(
    Guid Id,
    AppointmentStatus Status,
    BookingChannel Channel,
    DateTimeOffset Start,
    DateTimeOffset End,
    NamedRef Dealership,
    ServiceTypeRef ServiceType,
    NamedRef ServiceBay,
    NamedRef Technician,
    VehicleRef Vehicle)
{
    public static AppointmentResponse From(AppointmentDetails d)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(d.TimeZone);
        return new AppointmentResponse(
            d.Id,
            d.Status,
            d.Channel,
            TimeZoneInfo.ConvertTime(d.StartUtc, timeZone),
            TimeZoneInfo.ConvertTime(d.EndUtc, timeZone),
            new NamedRef(d.DealershipId, d.DealershipName),
            new ServiceTypeRef(d.ServiceTypeId, d.ServiceTypeCode, d.ServiceTypeName, d.DurationMinutes),
            new NamedRef(d.ServiceBayId, d.ServiceBayName),
            new NamedRef(d.TechnicianId, d.TechnicianName),
            new VehicleRef(d.Vin));
    }
}

/// <param name="Reason">"CLOSED" on a closed day; omitted otherwise.</param>
public sealed record AvailabilityResponse(
    DateOnly Date,
    Guid ServiceTypeId,
    int DurationMinutes,
    IReadOnlyList<TimeSlot> Slots,
    string? Reason)
{
    public static AvailabilityResponse From(DailyAvailability a) =>
        new(a.Date, a.ServiceType.Id, a.ServiceType.DurationMinutes, a.Slots, a.Reason);
}

public sealed record OpeningHoursResponse(DayOfWeek DayOfWeek, TimeOnly Open, TimeOnly Close);

public sealed record DealershipResponse(Guid Id, string Name, string TimeZone, IReadOnlyList<OpeningHoursResponse> OpeningHours)
{
    public static DealershipResponse From(Dealership d) => new(
        d.Id,
        d.Name,
        d.TimeZone,
        d.OpeningHours
            .OrderBy(h => ((int)h.DayOfWeek + 6) % 7) // Monday first
            .Select(h => new OpeningHoursResponse(h.DayOfWeek, h.OpenTime, h.CloseTime))
            .ToList());
}

public sealed record ServiceTypeResponse(Guid Id, string Code, string Name, int DurationMinutes, BayType RequiredBayType)
{
    public static ServiceTypeResponse From(ServiceType s) => new(s.Id, s.Code, s.Name, s.DurationMinutes, s.RequiredBayType);
}
