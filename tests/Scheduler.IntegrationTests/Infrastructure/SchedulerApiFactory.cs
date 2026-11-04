using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Scheduler.IntegrationTests.Infrastructure;

/// <summary>
/// Runs the real API in memory against a real PostgreSQL in Docker. A real database is the only way
/// to test the exclusion constraints; the EF Core InMemory provider would silently allow overlaps.
/// The app applies the migration (and seed data) itself at startup.
/// </summary>
public sealed class SchedulerApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Tuesday 10 Nov 2026, 10:00 in Ho Chi Minh City. Fixed so that tests never depend on today's date.</summary>
    public static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-10T10:00:00+07:00");

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17").Build();

    public Task InitializeAsync() => _database.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Scheduler", _database.GetConnectionString());
        builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now)));
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
    }
}

/// <summary>One container for every test class; xUnit runs tests in a collection one at a time.</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<SchedulerApiFactory>
{
    public const string Name = "api";
}
