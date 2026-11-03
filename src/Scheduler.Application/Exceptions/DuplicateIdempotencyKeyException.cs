namespace Scheduler.Application.Exceptions;

/// <summary>
/// Another request with the same Idempotency-Key was saved first (unique index, 8.4).
/// The service re-reads that appointment and replays it with 200 instead of creating a duplicate.
/// </summary>
public class DuplicateIdempotencyKeyException(string idempotencyKey)
    : Exception($"An appointment with idempotency key '{idempotencyKey}' already exists.")
{
    public string IdempotencyKey { get; } = idempotencyKey;
}
