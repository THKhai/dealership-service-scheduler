using System.Diagnostics;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;

namespace Scheduler.Api.Errors;

/// <summary>Builds every error response: RFC 7807 ProblemDetails plus a stable "code" and the "traceId" (9.1, 9.5).</summary>
internal static class Problems
{
    public static ProblemDetails Create(HttpContext context, int status, string code, string title, string? detail = null)
    {
        var problem = new ProblemDetails
        {
            Type = $"https://api.example.com/problems/{code.ToLowerInvariant().Replace('_', '-')}",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
        return problem;
    }

    public static ObjectResult ToResult(ProblemDetails problem) => new(problem)
    {
        StatusCode = problem.Status,
        ContentTypes = { "application/problem+json" }
    };

    public static ObjectResult Malformed(HttpContext context, string detail) =>
        ToResult(Create(context, StatusCodes.Status400BadRequest, "MALFORMED_REQUEST", "Malformed request", detail));

    public static ObjectResult Validation(HttpContext context, ValidationResult result)
    {
        var problem = Create(context, StatusCodes.Status422UnprocessableEntity, "VALIDATION_FAILED",
            "One or more fields are invalid", "See 'errors' for details.");
        problem.Extensions["errors"] = result.Errors
            .GroupBy(e => ToCamelCasePath(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        return ToResult(problem);
    }

    /// <summary>Model binding failures (invalid JSON, wrong types, bad query values) are 400, not 422.</summary>
    public static IActionResult FromModelState(ActionContext context)
    {
        var problem = Create(context.HttpContext, StatusCodes.Status400BadRequest, "MALFORMED_REQUEST",
            "Malformed request", "The request could not be read.");
        problem.Extensions["errors"] = context.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .ToDictionary(e => e.Key, e => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray());
        return ToResult(problem);
    }

    // "Customer.Email" -> "customer.email", matching the JSON field names.
    private static string ToCamelCasePath(string path) =>
        string.Join('.', path.Split('.').Select(p => p.Length == 0 ? p : char.ToLowerInvariant(p[0]) + p[1..]));
}
