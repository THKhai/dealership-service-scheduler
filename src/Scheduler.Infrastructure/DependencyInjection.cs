using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scheduler.Application.Abstractions;
using Scheduler.Infrastructure.Persistence;
using Scheduler.Infrastructure.Persistence.Repositories;

namespace Scheduler.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<SchedulerDbContext>(options => SchedulerDbContext.Configure(options, connectionString));

        // Scoped, the same as the DbContext they wrap: one instance per HTTP request.
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IResourceRepository, ResourceRepository>();
        services.AddScoped<IReferenceDataRepository, ReferenceDataRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();

        // Used by /health/ready: runs a trivial query against the database.
        services.AddHealthChecks().AddDbContextCheck<SchedulerDbContext>("database", tags: ["ready"]);

        return services;
    }

    /// <summary>
    /// Applies pending migrations at startup. Safe for a single API instance; with several
    /// instances, run migrations as a separate deploy step instead (see section 12).
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        await db.Database.MigrateAsync();
    }
}
