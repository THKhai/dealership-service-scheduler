namespace Scheduler.Application.Domain.Entities;

public class OpeningHours
{
    public Guid DealershipId { get; init; }
    public DayOfWeek DayOfWeek { get; init; }
    public TimeOnly OpenTime { get; init; }
    public TimeOnly CloseTime { get; init; }
}
