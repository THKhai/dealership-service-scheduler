using System.Diagnostics;
using Npgsql;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scheduler.Application.Diagnostics;

namespace Scheduler.Api.Observability;

internal static class TelemetryExtensions
{
    /// <summary>
    /// Traces go to Jaeger and metrics to Prometheus, both over OTLP (section 11).
    /// Exporters are only added when an endpoint is configured, so tests and plain
    /// `dotnet run` work without either of them running.
    /// </summary>
    public static IServiceCollection AddSchedulerTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var tracesEndpoint = configuration["Otel:TracesEndpoint"];
        var metricsEndpoint = configuration["Otel:MetricsEndpoint"];

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("scheduler-api"))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(SchedulerTelemetry.Name)
                    .AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"))
                    .AddNpgsql();

                if (!string.IsNullOrWhiteSpace(tracesEndpoint))
                {
                    tracing.AddOtlpExporter(o => o.Endpoint = new Uri(tracesEndpoint));
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(SchedulerTelemetry.Name)
                    .AddMeter("Npgsql")
                    .AddAspNetCoreInstrumentation()
                    .AddRuntimeInstrumentation();

                if (!string.IsNullOrWhiteSpace(metricsEndpoint))
                {
                    // Prometheus' OTLP receiver accepts HTTP/protobuf only; the endpoint is the full path.
                    metrics.AddOtlpExporter(o =>
                    {
                        o.Endpoint = new Uri(metricsEndpoint);
                        o.Protocol = OtlpExportProtocol.HttpProtobuf;
                    });
                }
            });

        return services;
    }

    /// <summary>Adds the W3C traceparent header to every response, so support can find the trace (11.3).</summary>
    public static IApplicationBuilder UseTraceparentHeader(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var activity = Activity.Current;
            context.Response.OnStarting(() =>
            {
                if (activity?.Id is { } id)
                {
                    context.Response.Headers["traceparent"] = id;
                }

                return Task.CompletedTask;
            });
            await next(context);
        });
}
