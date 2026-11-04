using Scheduler.Application.Abstractions;
using Scheduler.Application.Availability;
using Scheduler.Application.Domain.Entities;
using Scheduler.Application.Domain.Enums;

namespace Scheduler.UnitTests.Availability;

public class SlotFinderTests
{
    private static readonly TimeZoneInfo HoChiMinh = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    private static readonly ServiceBay Bay1 = new() { Name = "Bay 1", BayType = BayType.Lift };
    private static readonly ServiceBay Bay2 = new() { Name = "Bay 2", BayType = BayType.Lift };
    private static readonly Technician An = new() { Name = "An" };
    private static readonly Technician Binh = new() { Name = "Binh" };

    private static readonly Dealership Dealership = new()
    {
        Name = "Test",
        TimeZone = "Asia/Ho_Chi_Minh",
        OpeningHours = Enumerable.Range(1, 6)
            .Select(day => new OpeningHours { DayOfWeek = (DayOfWeek)day, OpenTime = new(8, 0), CloseTime = new(17, 0) })
            .ToList()
    };

    private static readonly ServiceType BrakeService = new()
    {
        Code = "brake-service", Name = "Brake service", DurationMinutes = 90, RequiredBayType = BayType.Lift
    };

    private static DateTimeOffset T(string localTime) => DateTimeOffset.Parse($"2026-11-11T{localTime}:00+07:00");

    private static BusyPeriod Busy(ServiceBay bay, Technician technician, string start, string end) =>
        new(bay.Id, technician.Id, T(start), T(end));

    private static ResourceSnapshot Snapshot(params BusyPeriod[] busy) => new([Bay1, Bay2], [An, Binh], busy);

    [Fact]
    public void Every_pair_is_a_candidate_when_nothing_is_booked()
    {
        var candidates = SlotFinder.FindCandidates(Snapshot(), T("09:00"), T("10:30"));

        Assert.Equal(4, candidates.Count);
    }

    [Fact]
    public void Busy_bay_and_technician_are_excluded()
    {
        var snapshot = Snapshot(Busy(Bay1, An, "09:30", "10:00"));

        var candidate = Assert.Single(SlotFinder.FindCandidates(snapshot, T("09:00"), T("10:30")));

        Assert.Equal(new Candidate(Bay2.Id, Binh.Id), candidate);
    }

    [Theory]
    [InlineData("07:30", "09:00")] // ends exactly when the new one starts
    [InlineData("10:30", "12:00")] // starts exactly when the new one ends
    public void Back_to_back_bookings_do_not_conflict(string busyStart, string busyEnd)
    {
        var snapshot = Snapshot(Busy(Bay1, An, busyStart, busyEnd));

        Assert.Equal(4, SlotFinder.FindCandidates(snapshot, T("09:00"), T("10:30")).Count);
    }

    [Fact]
    public void Least_loaded_technician_comes_first()
    {
        // An already has two bookings today, Binh none.
        var snapshot = Snapshot(Busy(Bay1, An, "08:00", "08:30"), Busy(Bay2, An, "13:00", "14:00"));

        var first = SlotFinder.FindCandidates(snapshot, T("09:00"), T("10:30"))[0];

        Assert.Equal(Binh.Id, first.TechnicianId);
    }

    [Fact]
    public void Limiting_resource_is_bay_when_every_bay_is_busy()
    {
        var snapshot = Snapshot(Busy(Bay1, An, "09:00", "10:30"), Busy(Bay2, Binh, "09:00", "10:30"));

        Assert.Equal(Resource.Bay, SlotFinder.LimitingResource(snapshot, T("09:00"), T("10:30")));
    }

    [Fact]
    public void Limiting_resource_is_technician_when_a_bay_is_free()
    {
        var snapshot = new ResourceSnapshot([Bay1, Bay2], [An], [Busy(Bay1, An, "09:00", "10:30")]);

        Assert.Empty(SlotFinder.FindCandidates(snapshot, T("09:00"), T("10:30")));
        Assert.Equal(Resource.Technician, SlotFinder.LimitingResource(snapshot, T("09:00"), T("10:30")));
    }

    [Fact]
    public void Candidate_starts_fit_the_whole_service_inside_opening_hours()
    {
        var starts = SlotFinder.CandidateStarts(Dealership, BrakeService, new DateOnly(2026, 11, 11), HoChiMinh, 15);

        // 08:00 to 15:30 in 15-minute steps; 15:30 + 90 minutes ends exactly at 17:00.
        Assert.Equal(31, starts.Count);
        Assert.Equal(T("08:00"), starts[0]);
        Assert.Equal(T("15:30"), starts[^1]);
        Assert.All(starts, s => Assert.Equal(TimeSpan.FromHours(7), s.Offset));
    }

    [Fact]
    public void Closed_day_has_no_candidate_starts()
    {
        var sunday = new DateOnly(2026, 11, 15);

        Assert.Empty(SlotFinder.CandidateStarts(Dealership, BrakeService, sunday, HoChiMinh, 15));
    }

    [Fact]
    public void Local_day_range_is_midnight_to_midnight_in_dealership_time()
    {
        var (from, to) = SlotFinder.LocalDayRange(T("09:00"), HoChiMinh);

        Assert.Equal(DateTimeOffset.Parse("2026-11-10T17:00:00Z"), from);
        Assert.Equal(DateTimeOffset.Parse("2026-11-11T17:00:00Z"), to);
    }
}
