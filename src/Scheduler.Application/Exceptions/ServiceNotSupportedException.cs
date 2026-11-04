namespace Scheduler.Application.Exceptions;

/// <summary>The dealership has no active bay of the required type, or no technician with the skill.</summary>
public class ServiceNotSupportedException(string serviceType)
    : DomainException("SERVICE_NOT_SUPPORTED", $"This dealership cannot perform '{serviceType}'.");
