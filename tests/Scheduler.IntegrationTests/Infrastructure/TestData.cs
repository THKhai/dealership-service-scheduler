using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Scheduler.IntegrationTests.Infrastructure;

/// <summary>Seed IDs (see SeedData) and builders that keep tests independent of each other.</summary>
public static class TestData
{
    public static readonly Guid DealershipId = Guid.Parse("d0000000-0000-0000-0000-000000000001");
    public static readonly Guid OilChange = Guid.Parse("50000000-0000-0000-0000-000000000001");       // 45 min, General: 1 bay
    public static readonly Guid BrakeService = Guid.Parse("50000000-0000-0000-0000-000000000002");    // 90 min, Lift: 2 bays
    public static readonly Guid EvBatteryCheck = Guid.Parse("50000000-0000-0000-0000-000000000003");  // 60 min, EV: 1 bay

    private static int _dayIndex;

    /// <summary>
    /// A fresh opening day (Mon-Sat) for each test, after the fixed "now". Technicians are shared
    /// across service types, so giving each test its own day stops tests affecting each other.
    /// </summary>
    public static DateOnly NextOpenDay()
    {
        while (true)
        {
            var day = new DateOnly(2026, 11, 11).AddDays(Interlocked.Increment(ref _dayIndex));
            if (day.DayOfWeek != DayOfWeek.Sunday)
            {
                return day;
            }
        }
    }

    public static string At(DateOnly day, string localTime) => $"{day:yyyy-MM-dd}T{localTime}:00+07:00";

    /// <summary>A valid VIN that no other test uses (hex digits never include I, O or Q).</summary>
    public static string NewVin() => Guid.NewGuid().ToString("N")[..17].ToUpperInvariant();

    public static JsonObject Booking(Guid serviceTypeId, string start, string? vin = null, string? email = null) => new()
    {
        ["dealershipId"] = DealershipId,
        ["serviceTypeId"] = serviceTypeId,
        ["startTime"] = start,
        ["channel"] = "Online",
        ["customer"] = new JsonObject
        {
            ["name"] = "Test Customer",
            ["email"] = email ?? $"{Guid.NewGuid():N}@example.com",
            ["phone"] = "+84901234567"
        },
        ["vehicle"] = new JsonObject
        {
            ["vin"] = vin ?? NewVin(),
            ["make"] = "Honda",
            ["model"] = "Civic",
            ["year"] = 2021
        }
    };

    public static Task<HttpResponseMessage> PostBookingAsync(this HttpClient client, JsonObject body, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/appointments")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString());
        return client.SendAsync(request);
    }

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    public static string Code(this JsonElement problem) => problem.GetProperty("code").GetString()!;
}
