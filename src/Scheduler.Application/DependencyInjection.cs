using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scheduler.Application.Appointments;
using Scheduler.Application.Availability;
using Scheduler.Application.Domain;

namespace Scheduler.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, BookingRules rules)
    {
        // Stateless, so one shared instance is enough.
        services.AddSingleton(rules);
        services.AddSingleton<BookingPolicy>();

        // TryAdd so that tests can register a fixed clock first.
        services.TryAddSingleton(TimeProvider.System);

        // Scoped, because they use the scoped repositories (and so the request's DbContext).
        services.AddScoped<AvailabilityService>();
        services.AddScoped<AppointmentService>();

        return services;
    }
}
