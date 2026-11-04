using Scheduler.Application.Domain.Enums;

namespace Scheduler.Application.Exceptions;

/// <summary>No free bay/technician pair for the requested time. Carries alternatives for the client (9.3).</summary>
public class NoCapacityException(Resource limitingResource, IReadOnlyList<DateTimeOffset> suggestedSlots)
    : DomainException("NO_CAPACITY", $"No {limitingResource.ToString().ToLowerInvariant()} is free for the requested time.")
{
    public Resource LimitingResource { get; } = limitingResource;

    /// <summary>Start times in the dealership's timezone, nearest to the requested time.</summary>
    public IReadOnlyList<DateTimeOffset> SuggestedSlots { get; } = suggestedSlots;
}
