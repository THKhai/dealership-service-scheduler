using Scheduler.Application.Domain.Enums;

namespace Scheduler.Api.Contracts;

// Properties are nullable on purpose: a missing field reaches FluentValidation and comes back
// as 422 VALIDATION_FAILED with the field name, instead of a generic 400 from model binding.

public sealed record BookAppointmentRequest(
    Guid DealershipId,
    Guid ServiceTypeId,
    DateTimeOffset? StartTime,
    BookingChannel? Channel,
    CustomerRequest? Customer,
    VehicleRequest? Vehicle);

public sealed record CustomerRequest(string? Name, string? Email, string? Phone);

public sealed record VehicleRequest(string? Vin, string? Make, string? Model, int Year);
