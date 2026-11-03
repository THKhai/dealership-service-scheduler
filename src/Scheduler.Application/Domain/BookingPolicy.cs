using Scheduler.Application.Domain.Entities;
using Scheduler.Application.Domain.Enums;
using Scheduler.Application.Exceptions;

namespace Scheduler.Application.Domain;

public sealed class BookingPolicy(BookingRules rules)
{
    private  BookingRules Rules { get; } = rules;

    public void Validate(Dealership dealership, ServiceType serviceType, DateTimeOffset start, BookingChannel channel, DateTimeOffset now)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(dealership.TimeZone);
        var localStart = TimeZoneInfo.ConvertTime(start, timeZone);
        var localEnd = localStart.AddMinutes(serviceType.DurationMinutes);
        
        ValidateTime(localStart, channel, now);
        ValidateOpeningHours(dealership, localStart, localEnd);
    }

    private void ValidateTime(DateTimeOffset localStart, BookingChannel channel, DateTimeOffset now)
    {
        if (localStart <= now)
        {
            throw new InvalidTimeException("Start time must be in the future");
        }

        if (channel == BookingChannel.Online && localStart < now + Rules.OnlineLeadTime)
        {
            throw new InvalidTimeException($"Online bookings need at least {Rules.OnlineLeadTime.TotalMinutes:0} minutes notice");
        }

        if (localStart.Minute % Rules.SlotStepMinutes != 0 || localStart.Second != 0 || localStart.Millisecond != 0)
        {
            throw new InvalidTimeException($"Start time must be on a {Rules.SlotStepMinutes}-minute step.");
        }

        if (localStart > now.AddDays(Rules.MaxDaysAhead))
        {
            throw new InvalidTimeException($"Booking can be made up to {Rules.MaxDaysAhead} days ahead");
        }
    }

    private static void ValidateOpeningHours(Dealership dealership, DateTimeOffset localStart, DateTimeOffset localEnd)
    {
        var hours = dealership.OpeningHours.FirstOrDefault(h => h.DayOfWeek == localStart.DayOfWeek)
            ?? throw new OutsideOpeningHoursException($"The dealership is closed on {localStart.DayOfWeek}.");

        var fits = localEnd.Date == localStart.Date
                   && TimeOnly.FromDateTime(localStart.DateTime) >= hours.OpenTime
                   && TimeOnly.FromDateTime(localEnd.DateTime) <= hours.CloseTime;

        if (!fits)
        {
            throw new OutsideOpeningHoursException(
                $"The appointment must fit within opening hours ({hours.OpenTime:HH\\:mm}-{hours.CloseTime:HH\\:mm}).");
        }
    }
}