namespace Scheduler.Application.Domain;
/// <summary>
/// Tunable booking limits from A-06. bound from configuration later.
/// </summary>
public class BookingRules
{
    public int SlotStepMinutes { get; init; } = 15;
    public TimeSpan OnlineLeadTime { get; init; } = TimeSpan.FromHours(1);
    public int MaxDaysAhead { get; init; } = 60;
}