namespace Scheduler.Application.Domain.Entities;

public class Vehicle
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CustomerId { get; init; }
    public required string Vin { get; init; }
    public required string Make { get; init; }
    public required string Model { get; init; }
    public int Year { get; init; }
}
