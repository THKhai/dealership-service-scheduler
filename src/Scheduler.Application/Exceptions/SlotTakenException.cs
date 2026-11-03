using Scheduler.Application.Domain.Enums;

namespace Scheduler.Application.Exceptions;

/// <summary>An exclusion constraint rejected the insert. Thrown by the repository, handled by the retry loop (8.3).</summary>
public class SlotTakenException(Resource resource)
    : Exception($"{resource} is already booked for this time.")
{
    public Resource Resource { get; } = resource;
}
