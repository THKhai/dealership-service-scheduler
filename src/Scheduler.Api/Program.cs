using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Scheduler.Api.Errors;
using Scheduler.Api.Observability;
using Scheduler.Application;
using Scheduler.Application.Domain;
using Scheduler.Infrastructure;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Scheduler")
    ?? throw new InvalidOperationException(
        "Connection string 'Scheduler' is not configured. Set it with user secrets (local) or ConnectionStrings__Scheduler (Docker).");

// Structured JSON logs to stdout (11.1). Trace and span IDs are added to every event automatically.
builder.Services.AddSerilog(logger => logger
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new RenderedCompactJsonFormatter()));

// Composition root: each layer registers its own services.
var bookingRules = builder.Configuration.GetSection("BookingRules").Get<BookingRules>() ?? new BookingRules();
builder.Services.AddApplication(bookingRules);
builder.Services.AddInfrastructure(connectionString);
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.Configure<ApiBehaviorOptions>(options =>
    options.InvalidModelStateResponseFactory = Problems.FromModelState);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddSchedulerTelemetry(builder.Configuration);

var app = builder.Build();

await app.Services.MigrateDatabaseAsync();

app.UseExceptionHandler();
app.UseTraceparentHeader();
app.UseSerilogRequestLogging();

// The OpenAPI document is the client stub (9.6), so it is served in every environment.
app.MapOpenApi();
app.MapControllers();

// Liveness has no checks: it only proves the process answers. Readiness checks the database (11.4).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();

// Lets the integration tests start the app with WebApplicationFactory<Program>.
public partial class Program;
