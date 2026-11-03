namespace Scheduler.Application.Exceptions;

/// <summary>A broken business rule. Code is the stable value returned to API clients (9.5).</summary>
public abstract class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
