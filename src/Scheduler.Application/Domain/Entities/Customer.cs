namespace Scheduler.Application.Domain.Entities;

public class Customer
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required string Phone { get; init; }
}
