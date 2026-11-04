using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scheduler.Application.Domain.Enums;
using Scheduler.Infrastructure.Persistence;
using Scheduler.IntegrationTests.Infrastructure;
using static Scheduler.IntegrationTests.Infrastructure.TestData;

namespace Scheduler.IntegrationTests;

/// <summary>Section 8.6: proves the database, not the application, prevents double-booking.</summary>
[Collection(ApiCollection.Name)]
public sealed class ConcurrencyTests(SchedulerApiFactory factory)
{
    private const int ParallelRequests = 10;

    [Fact]
    public async Task Parallel_requests_for_the_last_free_slot_book_it_exactly_once()
    {
        // Oil change needs the only General bay, so exactly one booking can fit at 09:00.
        var start = At(NextOpenDay(), "09:00");
        var client = factory.CreateClient();

        // Different vehicles and customers, so the only thing they compete for is the bay.
        var responses = await Task.WhenAll(Enumerable.Range(0, ParallelRequests)
            .Select(_ => client.PostBookingAsync(Booking(OilChange, start))));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        var conflicts = responses.Where(r => r.StatusCode == HttpStatusCode.Conflict).ToList();
        Assert.Equal(ParallelRequests - 1, conflicts.Count);
        foreach (var conflict in conflicts)
        {
            Assert.Equal("NO_CAPACITY", (await conflict.ReadJsonAsync()).Code());
        }

        Assert.Equal(1, await CountConfirmedAsync(OilChange, DateTimeOffset.Parse(start)));
    }

    [Fact]
    public async Task Parallel_requests_with_two_free_pairs_both_succeed_on_different_resources()
    {
        // Brake service has two Lift bays and three qualified technicians.
        var start = At(NextOpenDay(), "09:00");
        var client = factory.CreateClient();

        var responses = await Task.WhenAll(
            client.PostBookingAsync(Booking(BrakeService, start)),
            client.PostBookingAsync(Booking(BrakeService, start)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));

        var first = await responses[0].ReadJsonAsync();
        var second = await responses[1].ReadJsonAsync();
        Assert.NotEqual(BayId(first), BayId(second));
        Assert.NotEqual(TechnicianId(first), TechnicianId(second));
    }

    [Fact]
    public async Task Parallel_retries_with_the_same_idempotency_key_create_one_appointment()
    {
        // A client that times out and retries sends the same key and body (8.4).
        var start = At(NextOpenDay(), "10:00");
        var body = Booking(BrakeService, start);
        var key = Guid.NewGuid().ToString();
        var client = factory.CreateClient();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => client.PostBookingAsync(body.DeepClone().AsObject(), key)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.OK }));

        var ids = await Task.WhenAll(responses.Select(async r => (await r.ReadJsonAsync()).GetProperty("id").GetGuid()));
        Assert.Single(ids.Distinct());
        Assert.Equal(1, await CountConfirmedAsync(BrakeService, DateTimeOffset.Parse(start)));
    }

    private async Task<int> CountConfirmedAsync(Guid serviceTypeId, DateTimeOffset start)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        var startUtc = start.ToUniversalTime();
        return await db.Appointments.CountAsync(a =>
            a.ServiceTypeId == serviceTypeId && a.StartUtc == startUtc && a.Status == AppointmentStatus.Confirmed);
    }

    private static Guid BayId(System.Text.Json.JsonElement appointment) =>
        appointment.GetProperty("serviceBay").GetProperty("id").GetGuid();

    private static Guid TechnicianId(System.Text.Json.JsonElement appointment) =>
        appointment.GetProperty("technician").GetProperty("id").GetGuid();
}
