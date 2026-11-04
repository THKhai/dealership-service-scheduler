using System.Diagnostics;
using Scheduler.Application.Abstractions;
using Scheduler.Application.Diagnostics;
using Scheduler.Application.Domain;
using Scheduler.Application.Domain.Entities;
using Scheduler.Application.Domain.Enums;
using Scheduler.Application.Exceptions;

namespace Scheduler.Application.Availability;

public sealed record TimeSlot(DateTimeOffset Start, DateTimeOffset End);

/// <param name="Reason">"CLOSED" when the dealership is closed that day; otherwise null.</param>
public sealed record DailyAvailability(DateOnly Date, ServiceType ServiceType, IReadOnlyList<TimeSlot> Slots, string? Reason);

/// <summary>UC-02 View Available Slots, and the suggestions returned with NO_CAPACITY.</summary>
public sealed class AvailabilityService(
    IReferenceDataRepository referenceData,
    IResourceRepository resources,
    BookingRules rules,
    TimeProvider clock)
{
    /// <summary>How many days, starting with the requested one, are searched for alternatives.</summary>
    private const int SuggestionDays = 7;

    public async Task<DailyAvailability> GetDailyAsync(
        Guid dealershipId, Guid serviceTypeId, DateOnly date, BookingChannel channel, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        using var activity = SchedulerTelemetry.ActivitySource.StartActivity("Availability.GetDaily");

        var dealership = await referenceData.GetDealershipAsync(dealershipId, ct)
            ?? throw new NotFoundException("Dealership", dealershipId);
        var serviceType = await referenceData.GetServiceTypeAsync(serviceTypeId, ct)
            ?? throw new NotFoundException("Service type", serviceTypeId);

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(dealership.TimeZone);
        var starts = SlotFinder.CandidateStarts(dealership, serviceType, date, timeZone, rules.SlotStepMinutes);
        if (starts.Count == 0)
        {
            return new DailyAvailability(date, serviceType, [], "CLOSED");
        }

        var slots = await FreeSlotsAsync(dealership, serviceType, starts, channel, ct);
        activity?.SetTag("slot.count", slots.Count);
        SchedulerTelemetry.AvailabilityDuration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds);
        return new DailyAvailability(date, serviceType, slots, null);
    }

    /// <summary>Up to <paramref name="count"/> free start times nearest to the requested one, in time order.</summary>
    public async Task<IReadOnlyList<DateTimeOffset>> SuggestSlotsAsync(
        Dealership dealership, ServiceType serviceType, DateTimeOffset requested, BookingChannel channel,
        CancellationToken ct, int count = 3)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(dealership.TimeZone);
        var firstDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(requested, timeZone).DateTime);

        var starts = Enumerable.Range(0, SuggestionDays)
            .SelectMany(i => SlotFinder.CandidateStarts(dealership, serviceType, firstDay.AddDays(i), timeZone, rules.SlotStepMinutes))
            .ToList();

        var free = await FreeSlotsAsync(dealership, serviceType, starts, channel, ct);

        return free
            .Select(s => s.Start)
            .Where(s => s != requested)
            .OrderBy(s => (s - requested).Duration())
            .Take(count)
            .Order()
            .ToList();
    }

    /// <summary>Filters start times by the time rules, then by free resources, using one snapshot query.</summary>
    private async Task<IReadOnlyList<TimeSlot>> FreeSlotsAsync(
        Dealership dealership, ServiceType serviceType, IReadOnlyList<DateTimeOffset> starts, BookingChannel channel,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var duration = TimeSpan.FromMinutes(serviceType.DurationMinutes);
        var bookable = starts.Where(s => IsBookable(s, channel, now)).ToList();
        if (bookable.Count == 0)
        {
            return [];
        }

        var snapshot = await resources.GetSnapshotAsync(
            dealership.Id, serviceType.Id, serviceType.RequiredBayType,
            bookable[0].ToUniversalTime(), bookable[^1].Add(duration).ToUniversalTime(), ct);

        if (snapshot.Bays.Count == 0 || snapshot.Technicians.Count == 0)
        {
            throw new ServiceNotSupportedException(serviceType.Code);
        }

        return bookable
            .Where(s => SlotFinder.FindCandidates(snapshot, s.ToUniversalTime(), s.Add(duration).ToUniversalTime()).Count > 0)
            .Select(s => new TimeSlot(s, s.Add(duration)))
            .ToList();
    }

    // Same time rules as BookingPolicy (past, lead time, horizon). The step grid and opening
    // hours are already guaranteed by CandidateStarts.
    private bool IsBookable(DateTimeOffset start, BookingChannel channel, DateTimeOffset now) =>
        start > now
        && (channel != BookingChannel.Online || start >= now + rules.OnlineLeadTime)
        && start <= now.AddDays(rules.MaxDaysAhead);
}
