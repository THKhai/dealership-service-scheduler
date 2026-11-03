using Scheduler.Application.Domain;
using Scheduler.Application.Domain.Entities;
using Scheduler.Application.Domain.Enums;
using Scheduler.Application.Exceptions;

namespace Scheduler.UnitTests.Domain;

public class BookingPolicyTests
{
    // Tuesday 10 Nov 2026, 10:00 in Ho Chi Minh City (UTC+7, no daylight saving).
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-10T10:00:00+07:00");

    private readonly BookingPolicy _policy = new(new BookingRules());

    private static readonly Dealership Dealership = new()
    {
        Name = "Keyloop Motors District 1",
        TimeZone = "Asia/Ho_Chi_Minh",
        OpeningHours = Enumerable.Range(1, 6) // Monday to Saturday; closed Sunday
            .Select(day => new OpeningHours
            {
                DayOfWeek = (DayOfWeek)day,
                OpenTime = new TimeOnly(8, 0),
                CloseTime = new TimeOnly(17, 0)
            })
            .ToList()
    };

    private static readonly ServiceType BrakeService = new()
    {
        Code = "brake-service",
        Name = "Brake service",
        DurationMinutes = 90,
        RequiredBayType = BayType.Lift
    };

    private void Validate(string start, BookingChannel channel = BookingChannel.Online) =>
        _policy.Validate(Dealership, BrakeService, DateTimeOffset.Parse(start), channel, Now);

    [Theory]
    [InlineData("2026-11-11T09:00:00+07:00")] // normal booking
    [InlineData("2026-11-10T11:00:00+07:00")] // exactly one hour of lead time
    [InlineData("2026-11-11T08:00:00+07:00")] // starts at opening time
    [InlineData("2026-11-11T15:30:00+07:00")] // ends exactly at closing time
    [InlineData("2026-11-11T02:00:00+00:00")] // same as 09:00 local, sent in UTC
    public void Accepts_valid_online_booking(string start)
    {
        var exception = Record.Exception(() => Validate(start));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("2026-11-10T09:00:00+07:00")] // in the past
    [InlineData("2026-11-10T10:30:00+07:00")] // less than one hour notice
    [InlineData("2026-11-11T09:10:00+07:00")] // not on a 15-minute step
    [InlineData("2027-01-11T09:00:00+07:00")] // more than 60 days ahead
    [InlineData("2026-11-11T09:00:00.500+07:00")] // not a whole minute
    public void Rejects_invalid_time(string start)
    {
        var exception = Assert.Throws<InvalidTimeException>(() => Validate(start));

        Assert.Equal("INVALID_TIME", exception.Code);
    }

    [Theory]
    [InlineData("2026-11-11T07:45:00+07:00")] // before opening
    [InlineData("2026-11-11T16:00:00+07:00")] // would end at 17:30, after closing
    [InlineData("2026-11-15T09:00:00+07:00")] // Sunday, closed
    public void Rejects_booking_outside_opening_hours(string start)
    {
        var exception = Assert.Throws<OutsideOpeningHoursException>(() => Validate(start));

        Assert.Equal("OUTSIDE_OPENING_HOURS", exception.Code);
    }

    [Fact]
    public void Advisor_can_book_walk_in_without_lead_time()
    {
        var exception = Record.Exception(() => Validate("2026-11-10T10:15:00+07:00", BookingChannel.Advisor));

        Assert.Null(exception);
    }
}
