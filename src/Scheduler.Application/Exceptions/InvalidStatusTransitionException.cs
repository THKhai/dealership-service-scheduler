using Scheduler.Application.Domain.Enums;

namespace Scheduler.Application.Exceptions;

public class InvalidStatusTransitionException(AppointmentStatus from, AppointmentStatus to)
    : DomainException("INVALID_STATUS_TRANSITION", $"Cannot change appointment from {from} to {to}.");
