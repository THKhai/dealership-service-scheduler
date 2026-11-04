namespace Scheduler.Application.Exceptions;

public class NotFoundException(string resource, object id)
    : DomainException("NOT_FOUND", $"{resource} '{id}' was not found.");
