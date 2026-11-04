using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Scheduler.Application.Exceptions;

namespace Scheduler.Api.Errors;

/// <summary>
/// Translates exceptions into ProblemDetails in one place, so services throw domain exceptions
/// and never know about HTTP status codes.
/// </summary>
internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    private static readonly Dictionary<string, (int Status, string Title)> DomainErrors = new()
    {
        ["NOT_FOUND"] = (StatusCodes.Status404NotFound, "Resource not found"),
        ["NO_CAPACITY"] = (StatusCodes.Status409Conflict, "No capacity for the requested time"),
        ["VEHICLE_ALREADY_BOOKED"] = (StatusCodes.Status409Conflict, "Vehicle already booked"),
        ["INVALID_STATUS_TRANSITION"] = (StatusCodes.Status409Conflict, "Invalid status transition"),
        ["INVALID_TIME"] = (StatusCodes.Status422UnprocessableEntity, "Invalid appointment time"),
        ["OUTSIDE_OPENING_HOURS"] = (StatusCodes.Status422UnprocessableEntity, "Outside opening hours"),
        ["SERVICE_NOT_SUPPORTED"] = (StatusCodes.Status422UnprocessableEntity, "Service not supported"),
        ["VIN_OWNED_BY_OTHER_CUSTOMER"] = (StatusCodes.Status422UnprocessableEntity, "VIN owned by another customer"),
        ["IDEMPOTENCY_KEY_REUSED"] = (StatusCodes.Status422UnprocessableEntity, "Idempotency key reused")
    };

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var problem = exception switch
        {
            DomainException domain => FromDomain(context, domain),
            BadHttpRequestException bad => Problems.Create(context, StatusCodes.Status400BadRequest,
                "MALFORMED_REQUEST", "Malformed request", bad.Message),
            _ => null
        };

        if (problem is null)
        {
            logger.LogError(exception, "Unhandled exception");
            problem = Problems.Create(context, StatusCodes.Status500InternalServerError,
                "INTERNAL_ERROR", "An unexpected error occurred");
        }

        context.Response.StatusCode = problem.Status!.Value;
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", ct);
        return true;
    }

    private static ProblemDetails FromDomain(HttpContext context, DomainException exception)
    {
        var (status, title) = DomainErrors.GetValueOrDefault(
            exception.Code, (StatusCodes.Status422UnprocessableEntity, "Request rejected"));

        var problem = Problems.Create(context, status, exception.Code, title, exception.Message);

        if (exception is NoCapacityException noCapacity)
        {
            problem.Extensions["limitingResource"] = noCapacity.LimitingResource.ToString();
            problem.Extensions["suggestedSlots"] = noCapacity.SuggestedSlots;
        }

        return problem;
    }
}
