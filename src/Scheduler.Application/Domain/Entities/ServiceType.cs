using Scheduler.Application.Domain.Enums;

namespace Scheduler.Application.Domain.Entities;

public class ServiceType
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Code { get; init; }
    public required string Name { get; init; }
    public int DurationMinutes { get; init; }
    public BayType RequiredBayType { get; init; }
}
