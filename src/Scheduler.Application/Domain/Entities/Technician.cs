namespace Scheduler.Application.Domain.Entities;

public class Technician
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid DealershipId { get; init; }
    public required string Name { get; init; }
    public bool IsActive { get; init; } = true;

    public List<TechnicianSkill> Skills { get; init; } = [];
}
