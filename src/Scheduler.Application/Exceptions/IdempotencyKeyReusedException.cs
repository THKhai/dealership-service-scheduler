namespace Scheduler.Application.Exceptions;

/// <summary>The Idempotency-Key was already used for a booking with different details (8.4).</summary>
public class IdempotencyKeyReusedException()
    : DomainException("IDEMPOTENCY_KEY_REUSED", "This Idempotency-Key was already used for a different booking.");
