using System.Net;
using System.Net.Http.Json;
using Scheduler.IntegrationTests.Infrastructure;
using static Scheduler.IntegrationTests.Infrastructure.TestData;

namespace Scheduler.IntegrationTests;

/// <summary>Every booking flow and error code from section 9.5, through HTTP against a real database.</summary>
[Collection(ApiCollection.Name)]
public sealed class AppointmentApiTests(SchedulerApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Booking_returns_201_with_location_and_assigned_resources()
    {
        var day = NextOpenDay();

        var response = await _client.PostBookingAsync(Booking(BrakeService, At(day, "09:00")));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync();
        var id = body.GetProperty("id").GetGuid();
        Assert.EndsWith($"/api/v1/appointments/{id}", response.Headers.Location!.ToString());
        Assert.Equal("Confirmed", body.GetProperty("status").GetString());
        Assert.Equal(DateTimeOffset.Parse(At(day, "10:30")), body.GetProperty("end").GetDateTimeOffset());
        Assert.EndsWith("+07:00", body.GetProperty("start").GetString()); // returned in dealership time
        Assert.NotEqual(Guid.Empty, body.GetProperty("serviceBay").GetProperty("id").GetGuid());
        Assert.NotEqual(Guid.Empty, body.GetProperty("technician").GetProperty("id").GetGuid());

        var fetched = await _client.GetAsync($"/api/v1/appointments/{id}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
    }

    [Fact]
    public async Task Repeating_the_idempotency_key_returns_200_with_the_original()
    {
        var body = Booking(BrakeService, At(NextOpenDay(), "09:00"));
        var key = Guid.NewGuid().ToString();

        var first = await _client.PostBookingAsync(body, key);
        var replay = await _client.PostBookingAsync(body, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal((await first.ReadJsonAsync()).GetProperty("id").GetGuid(),
            (await replay.ReadJsonAsync()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Reusing_the_idempotency_key_for_a_different_booking_returns_422()
    {
        var day = NextOpenDay();
        var key = Guid.NewGuid().ToString();
        await _client.PostBookingAsync(Booking(BrakeService, At(day, "09:00")), key);

        var response = await _client.PostBookingAsync(Booking(BrakeService, At(day, "13:00")), key);

        await AssertProblem(response, HttpStatusCode.UnprocessableEntity, "IDEMPOTENCY_KEY_REUSED");
    }

    [Fact]
    public async Task Missing_idempotency_key_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/appointments", Booking(BrakeService, At(NextOpenDay(), "09:00")));

        await AssertProblem(response, HttpStatusCode.BadRequest, "MALFORMED_REQUEST");
    }

    [Fact]
    public async Task Invalid_json_returns_400()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/appointments")
        {
            Content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await _client.SendAsync(request);

        await AssertProblem(response, HttpStatusCode.BadRequest, "MALFORMED_REQUEST");
    }

    [Fact]
    public async Task Invalid_fields_return_422_with_field_errors()
    {
        var body = Booking(BrakeService, At(NextOpenDay(), "09:00"), vin: "TOO-SHORT");
        body["customer"]!["email"] = "not-an-email";

        var response = await _client.PostBookingAsync(body);

        var problem = await AssertProblem(response, HttpStatusCode.UnprocessableEntity, "VALIDATION_FAILED");
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("vehicle.vin", out _));
        Assert.True(errors.TryGetProperty("customer.email", out _));
    }

    [Fact]
    public async Task Start_in_the_past_returns_422_invalid_time()
    {
        var response = await _client.PostBookingAsync(Booking(BrakeService, "2026-11-10T09:00:00+07:00"));

        await AssertProblem(response, HttpStatusCode.UnprocessableEntity, "INVALID_TIME");
    }

    [Fact]
    public async Task Appointment_running_past_closing_returns_422()
    {
        // 16:00 + 90 minutes ends at 17:30; the dealership closes at 17:00.
        var response = await _client.PostBookingAsync(Booking(BrakeService, At(NextOpenDay(), "16:00")));

        await AssertProblem(response, HttpStatusCode.UnprocessableEntity, "OUTSIDE_OPENING_HOURS");
    }

    [Fact]
    public async Task Unknown_dealership_returns_404()
    {
        var body = Booking(BrakeService, At(NextOpenDay(), "09:00"));
        body["dealershipId"] = Guid.NewGuid();

        var response = await _client.PostBookingAsync(body);

        await AssertProblem(response, HttpStatusCode.NotFound, "NOT_FOUND");
    }

    [Fact]
    public async Task Overlapping_booking_for_the_same_vehicle_returns_409()
    {
        var day = NextOpenDay();
        var vin = NewVin();
        var email = $"{Guid.NewGuid():N}@example.com";
        await _client.PostBookingAsync(Booking(BrakeService, At(day, "09:00"), vin, email));

        // Different service and bay type, but the car cannot be in two places (A-07).
        var response = await _client.PostBookingAsync(Booking(OilChange, At(day, "10:00"), vin, email));

        await AssertProblem(response, HttpStatusCode.Conflict, "VEHICLE_ALREADY_BOOKED");
    }

    [Fact]
    public async Task Vin_registered_to_another_customer_returns_422()
    {
        var day = NextOpenDay();
        var vin = NewVin();
        await _client.PostBookingAsync(Booking(BrakeService, At(day, "09:00"), vin));

        var response = await _client.PostBookingAsync(Booking(BrakeService, At(day, "13:00"), vin));

        await AssertProblem(response, HttpStatusCode.UnprocessableEntity, "VIN_OWNED_BY_OTHER_CUSTOMER");
    }

    [Fact]
    public async Task Full_slot_returns_409_with_limiting_resource_and_suggestions()
    {
        // One EV bay: the second booking at the same time cannot fit.
        var day = NextOpenDay();
        await _client.PostBookingAsync(Booking(EvBatteryCheck, At(day, "09:00")));

        var response = await _client.PostBookingAsync(Booking(EvBatteryCheck, At(day, "09:00")));

        var problem = await AssertProblem(response, HttpStatusCode.Conflict, "NO_CAPACITY");
        Assert.Equal("Bay", problem.GetProperty("limitingResource").GetString());
        var suggestions = problem.GetProperty("suggestedSlots").EnumerateArray().Select(s => s.GetDateTimeOffset()).ToList();
        Assert.Equal(3, suggestions.Count);
        // Nearest free starts: 08:00 (ends exactly when the booking starts) and 10:00, 10:15 (back-to-back after it).
        Assert.Equal(
            new[] { At(day, "08:00"), At(day, "10:00"), At(day, "10:15") }.Select(DateTimeOffset.Parse),
            suggestions);
    }

    [Fact]
    public async Task Cancelling_releases_the_slot_and_cannot_be_repeated()
    {
        var day = NextOpenDay();
        var booked = await (await _client.PostBookingAsync(Booking(EvBatteryCheck, At(day, "09:00")))).ReadJsonAsync();
        var id = booked.GetProperty("id").GetGuid();

        var cancelled = await _client.PostAsync($"/api/v1/appointments/{id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal("Cancelled", (await cancelled.ReadJsonAsync()).GetProperty("status").GetString());

        // The only EV bay is free again (A-08).
        var rebooked = await _client.PostBookingAsync(Booking(EvBatteryCheck, At(day, "09:00")));
        Assert.Equal(HttpStatusCode.Created, rebooked.StatusCode);

        var again = await _client.PostAsync($"/api/v1/appointments/{id}/cancel", null);
        await AssertProblem(again, HttpStatusCode.Conflict, "INVALID_STATUS_TRANSITION");
    }

    [Theory]
    [InlineData("complete", "Completed")]
    [InlineData("no-show", "NoShow")]
    public async Task Advisor_can_close_an_appointment(string action, string expectedStatus)
    {
        var booked = await (await _client.PostBookingAsync(Booking(BrakeService, At(NextOpenDay(), "09:00")))).ReadJsonAsync();
        var id = booked.GetProperty("id").GetGuid();

        var response = await _client.PostAsync($"/api/v1/appointments/{id}/{action}", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedStatus, (await response.ReadJsonAsync()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Unknown_appointment_returns_404()
    {
        var response = await _client.GetAsync($"/api/v1/appointments/{Guid.NewGuid()}");

        await AssertProblem(response, HttpStatusCode.NotFound, "NOT_FOUND");
    }

    [Fact]
    public async Task Errors_carry_a_trace_id_that_matches_the_traceparent_header()
    {
        var response = await _client.GetAsync($"/api/v1/appointments/{Guid.NewGuid()}");

        var problem = await response.ReadJsonAsync();
        Assert.Equal(response.Headers.GetValues("traceparent").Single(), problem.GetProperty("traceId").GetString());
    }

    private static async Task<System.Text.Json.JsonElement> AssertProblem(
        HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.ReadJsonAsync();
        Assert.Equal(code, problem.Code());
        return problem;
    }
}
