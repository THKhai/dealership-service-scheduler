using System.Net;
using Scheduler.IntegrationTests.Infrastructure;
using static Scheduler.IntegrationTests.Infrastructure.TestData;

namespace Scheduler.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ReferenceDataApiTests(SchedulerApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Lists_seeded_dealerships_and_service_types()
    {
        var dealerships = await (await _client.GetAsync("/api/v1/dealerships")).ReadJsonAsync();
        var serviceTypes = await (await _client.GetAsync("/api/v1/service-types")).ReadJsonAsync();

        var dealership = Assert.Single(dealerships.EnumerateArray());
        Assert.Equal(DealershipId, dealership.GetProperty("id").GetGuid());
        Assert.Equal(6, dealership.GetProperty("openingHours").GetArrayLength());
        Assert.Equal(3, serviceTypes.GetArrayLength());
    }

    [Fact]
    public async Task Availability_excludes_times_that_overlap_a_booking()
    {
        // EV check: 60 minutes, one EV bay, open 08:00-17:00, so starts 08:00..16:00 = 33 slots.
        var day = NextOpenDay();
        await _client.PostBookingAsync(Booking(EvBatteryCheck, At(day, "09:00")));

        var availability = await (await _client.GetAsync(
            $"/api/v1/dealerships/{DealershipId}/availability?serviceTypeId={EvBatteryCheck}&date={day:yyyy-MM-dd}")).ReadJsonAsync();

        var starts = availability.GetProperty("slots").EnumerateArray()
            .Select(s => s.GetProperty("start").GetDateTimeOffset())
            .ToList();

        // 08:15 to 09:45 would overlap 09:00-10:00; 08:00 and 10:00 are back-to-back and allowed.
        Assert.Equal(33 - 7, starts.Count);
        Assert.Contains(DateTimeOffset.Parse(At(day, "08:00")), starts);
        Assert.DoesNotContain(DateTimeOffset.Parse(At(day, "09:00")), starts);
        Assert.Contains(DateTimeOffset.Parse(At(day, "10:00")), starts);
    }

    [Fact]
    public async Task Availability_on_a_closed_day_is_empty_with_reason()
    {
        var response = await _client.GetAsync(
            $"/api/v1/dealerships/{DealershipId}/availability?serviceTypeId={BrakeService}&date=2026-11-15"); // Sunday

        var body = await response.ReadJsonAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("CLOSED", body.GetProperty("reason").GetString());
        Assert.Equal(0, body.GetProperty("slots").GetArrayLength());
    }

    [Fact]
    public async Task Availability_without_a_date_returns_400()
    {
        var response = await _client.GetAsync($"/api/v1/dealerships/{DealershipId}/availability?serviceTypeId={BrakeService}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MALFORMED_REQUEST", (await response.ReadJsonAsync()).Code());
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_checks_report_healthy(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
