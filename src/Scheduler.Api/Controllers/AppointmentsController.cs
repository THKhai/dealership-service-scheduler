using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Scheduler.Api.Contracts;
using Scheduler.Api.Errors;
using Scheduler.Application.Appointments;
using Scheduler.Application.Domain.Enums;

namespace Scheduler.Api.Controllers;

/// <summary>
/// Thin by design: translate HTTP to a command, call the service, translate the result back.
/// Errors are thrown as domain exceptions and mapped by ApiExceptionHandler.
/// </summary>
[ApiController]
[Route("api/v1/appointments")]
public sealed class AppointmentsController(AppointmentService appointments) : ControllerBase
{
    private const int MaxIdempotencyKeyLength = 100;

    /// <summary>UC-01 Book an appointment. The system assigns the bay and technician.</summary>
    [HttpPost]
    [ProducesResponseType<AppointmentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<AppointmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Book(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] BookAppointmentRequest request,
        [FromServices] IValidator<BookAppointmentRequest> validator,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > MaxIdempotencyKeyLength)
        {
            return Problems.Malformed(HttpContext, $"An Idempotency-Key header of 1-{MaxIdempotencyKeyLength} characters is required.");
        }

        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Problems.Validation(HttpContext, validation);
        }

        var channel = request.Channel!.Value;
        var command = new BookAppointmentCommand(
            request.DealershipId,
            request.ServiceTypeId,
            request.StartTime!.Value,
            channel,
            new CustomerInfo(request.Customer!.Name!, request.Customer.Email!, request.Customer.Phone!),
            new VehicleInfo(request.Vehicle!.Vin!, request.Vehicle.Make!, request.Vehicle.Model!, request.Vehicle.Year),
            idempotencyKey,
            // No authentication in scope; record which channel created the booking.
            CreatedBy: channel == BookingChannel.Advisor ? "advisor" : "online");

        var result = await appointments.BookAsync(command, ct);
        var response = AppointmentResponse.From(result.Appointment);

        // A replayed key returns the original appointment with 200, not a second 201 (8.4).
        return result.Replayed
            ? Ok(response)
            : CreatedAtAction(nameof(Get), new { id = response.Id }, response);
    }

    /// <summary>UC-03 View an appointment.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<AppointmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<AppointmentResponse> Get(Guid id, CancellationToken ct) =>
        AppointmentResponse.From(await appointments.GetAsync(id, ct));

    /// <summary>UC-04 Cancel a future Confirmed appointment. Releases its bay and technician.</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<AppointmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<AppointmentResponse> Cancel(Guid id, CancellationToken ct) =>
        AppointmentResponse.From(await appointments.CancelAsync(id, ct));

    /// <summary>UC-05 Mark a Confirmed appointment as completed.</summary>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType<AppointmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<AppointmentResponse> Complete(Guid id, CancellationToken ct) =>
        AppointmentResponse.From(await appointments.CompleteAsync(id, ct));

    /// <summary>UC-05 Mark a Confirmed appointment as a no-show.</summary>
    [HttpPost("{id:guid}/no-show")]
    [ProducesResponseType<AppointmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<AppointmentResponse> NoShow(Guid id, CancellationToken ct) =>
        AppointmentResponse.From(await appointments.MarkNoShowAsync(id, ct));
}
