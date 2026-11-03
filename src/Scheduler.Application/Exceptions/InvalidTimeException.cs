namespace Scheduler.Application.Exceptions;

public class InvalidTimeException(string message) : DomainException("INVALID_TIME", message);
