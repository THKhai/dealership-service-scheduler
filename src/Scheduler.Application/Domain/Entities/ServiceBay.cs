using Scheduler.Application.Domain.Enums;

namespace Scheduler.Application.Domain.Entities;

public class ServiceBay
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid DealershipId { get; init; }
    public required string Name { get; init; }
    public BayType BayType { get; init; }
    public bool IsActive { get; init; } = true;
}
