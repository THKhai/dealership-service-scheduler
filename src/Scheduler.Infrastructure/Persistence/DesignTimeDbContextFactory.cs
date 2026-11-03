using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Scheduler.Infrastructure.Persistence;

/// <summary>
/// Used only by "dotnet ef" tooling. Creating a migration does not connect to the database,
/// so a placeholder is enough; set ConnectionStrings__Scheduler to run "database update".
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SchedulerDbContext>
{
    public SchedulerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Scheduler")
                               ?? "Host=localhost;Port=5432;Database=scheduler;Username=scheduler";

        var options = new DbContextOptionsBuilder<SchedulerDbContext>();
        SchedulerDbContext.Configure(options, connectionString);
        return new SchedulerDbContext(options.Options);
    }
}
