namespace Scheduler.Application.Domain.Entities;

public class Dealership
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }

    /// <summary>IANA name, e.g. Asia/Ho_Chi_Minh.</summary>
    public required string TimeZone { get; init; }

    public List<OpeningHours> OpeningHours { get; init; } = [];
}
