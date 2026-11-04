using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Scheduler.Application.Diagnostics;

/// <summary>
/// Custom spans and metrics from section 11. Both use only types built into .NET,
/// so the Application layer does not depend on OpenTelemetry; the API registers the
/// "Scheduler" source and meter with its exporters.
/// </summary>
public static class SchedulerTelemetry
{
    public const string Name = "Scheduler";

    public static readonly ActivitySource ActivitySource = new(Name);

    private static readonly Meter Meter = new(Name);

    /// <summary>Tags: result (created, replayed, no_capacity, vehicle_booked, invalid), channel, dealership.</summary>
    public static readonly Counter<long> Bookings =
        Meter.CreateCounter<long>("scheduler.bookings", description: "Booking attempts by outcome.");

    /// <summary>Contention signal: an insert lost the race for a bay or technician (8.3).</summary>
    public static readonly Counter<long> BookingRetries =
        Meter.CreateCounter<long>("scheduler.booking.retries", description: "Inserts rejected by an exclusion constraint.");

    public static readonly Histogram<double> BookingDuration =
        Meter.CreateHistogram<double>("scheduler.booking.duration", unit: "s", description: "End-to-end booking latency.");

    public static readonly Histogram<double> AvailabilityDuration =
        Meter.CreateHistogram<double>("scheduler.availability.duration", unit: "s", description: "Slot calculation latency.");

    /// <summary>Tag: to (Cancelled, Completed, NoShow).</summary>
    public static readonly Counter<long> Transitions =
        Meter.CreateCounter<long>("scheduler.appointment.transitions", description: "Appointment status changes.");

    /// <summary>11.1: never log a full VIN.</summary>
    public static string MaskVin(string vin) => vin.Length <= 6 ? vin : new string('*', vin.Length - 6) + vin[^6..];
}
