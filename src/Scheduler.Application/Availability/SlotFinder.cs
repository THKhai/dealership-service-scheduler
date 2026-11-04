using Scheduler.Application.Abstractions;
using Scheduler.Application.Domain.Entities;
using Scheduler.Application.Domain.Enums;

namespace Scheduler.Application.Availability;

/// <summary>A free bay and technician that could take the appointment.</summary>
public sealed record Candidate(Guid ServiceBayId, Guid TechnicianId);

/// <summary>
/// Pure slot arithmetic with no I/O, so it is unit-tested without a database.
/// All overlap checks use half-open intervals [start, end), matching the '[)' constraints (A-05).
/// </summary>
public static class SlotFinder
{
    /// <summary>
    /// Free bay/technician pairs for [startUtc, endUtc), least-loaded first, so work spreads across
    /// technicians and bays. Load is the number of bookings the resource has in the snapshot window.
    /// </summary>
    public static IReadOnlyList<Candidate> FindCandidates(ResourceSnapshot snapshot, DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        var overlapping = snapshot.BusyPeriods.Where(p => Overlaps(p, startUtc, endUtc)).ToList();
        var busyBays = overlapping.Select(p => p.ServiceBayId).ToHashSet();
        var busyTechnicians = overlapping.Select(p => p.TechnicianId).ToHashSet();

        var bayLoad = snapshot.BusyPeriods.CountBy(p => p.ServiceBayId).ToDictionary();
        var technicianLoad = snapshot.BusyPeriods.CountBy(p => p.TechnicianId).ToDictionary();

        return (
                from bay in snapshot.Bays
                where !busyBays.Contains(bay.Id)
                from technician in snapshot.Technicians
                where !busyTechnicians.Contains(technician.Id)
                orderby technicianLoad.GetValueOrDefault(technician.Id),
                    bayLoad.GetValueOrDefault(bay.Id),
                    technician.Name,
                    bay.Name
                select new Candidate(bay.Id, technician.Id))
            .ToList();
    }

    /// <summary>Which resource ran out, for the NO_CAPACITY response.</summary>
    public static Resource LimitingResource(ResourceSnapshot snapshot, DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        var busyBays = snapshot.BusyPeriods
            .Where(p => Overlaps(p, startUtc, endUtc))
            .Select(p => p.ServiceBayId)
            .ToHashSet();

        return snapshot.Bays.All(b => busyBays.Contains(b.Id)) ? Resource.Bay : Resource.Technician;
    }

    /// <summary>
    /// Every start time on the step grid where the whole service fits inside opening hours (A-05, A-06),
    /// in the dealership's local offset. A closed day returns an empty list.
    /// </summary>
    public static IReadOnlyList<DateTimeOffset> CandidateStarts(
        Dealership dealership, ServiceType serviceType, DateOnly localDate, TimeZoneInfo timeZone, int stepMinutes)
    {
        var hours = dealership.OpeningHours.FirstOrDefault(h => h.DayOfWeek == localDate.DayOfWeek);
        if (hours is null)
        {
            return [];
        }

        var duration = TimeSpan.FromMinutes(serviceType.DurationMinutes);
        var step = TimeSpan.FromMinutes(stepMinutes);
        var close = hours.CloseTime.ToTimeSpan();
        var midnight = localDate.ToDateTime(TimeOnly.MinValue);

        var starts = new List<DateTimeOffset>();
        for (var time = hours.OpenTime.ToTimeSpan(); time + duration <= close; time += step)
        {
            var local = midnight + time;
            starts.Add(new DateTimeOffset(local, timeZone.GetUtcOffset(local)));
        }

        return starts;
    }

    /// <summary>The local calendar day containing <paramref name="instant"/>, as a UTC range.</summary>
    public static (DateTimeOffset FromUtc, DateTimeOffset ToUtc) LocalDayRange(DateTimeOffset instant, TimeZoneInfo timeZone)
    {
        var midnight = TimeZoneInfo.ConvertTime(instant, timeZone).Date;
        var from = new DateTimeOffset(midnight, timeZone.GetUtcOffset(midnight));
        return (from.ToUniversalTime(), from.AddDays(1).ToUniversalTime());
    }

    private static bool Overlaps(BusyPeriod period, DateTimeOffset startUtc, DateTimeOffset endUtc) =>
        period.StartUtc < endUtc && period.EndUtc > startUtc;
}
