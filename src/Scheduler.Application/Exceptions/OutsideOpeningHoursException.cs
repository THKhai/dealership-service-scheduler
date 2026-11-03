namespace Scheduler.Application.Exceptions;

public class OutsideOpeningHoursException(string message) : DomainException("OUTSIDE_OPENING_HOURS", message);
