using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Scheduler.Application.Abstractions;
using Scheduler.Application.Availability;
using Scheduler.Application.Diagnostics;
using Scheduler.Application.Domain;
using Scheduler.Application.Domain.Entities;
using Scheduler.Application.Domain.Enums;
using Scheduler.Application.Exceptions;

namespace Scheduler.Application.Appointments;

public sealed record CustomerInfo(string Name, string Email, string Phone);

public sealed record VehicleInfo(string Vin, string Make, string Model, int Year);

public sealed record BookAppointmentCommand(
    Guid DealershipId,
    Guid ServiceTypeId,
    DateTimeOffset Start,
    BookingChannel Channel,
    CustomerInfo Customer,
    VehicleInfo Vehicle,
    string IdempotencyKey,
    string CreatedBy);

/// <param name="Replayed">True when the Idempotency-Key was already used: the API returns 200 instead of 201.</param>
public sealed record BookingResult(AppointmentDetails Appointment, bool Replayed);

/// <summary>UC-01 Book, UC-03 View, UC-04 Cancel, UC-05 Complete / No-show. Follows the flow in section 7.</summary>
public sealed class AppointmentService(
    IAppointmentRepository appointments,
    IReferenceDataRepository referenceData,
    IResourceRepository resources,
    ICustomerRepository customers,
    IVehicleRepository vehicles,
    AvailabilityService availability,
    BookingPolicy policy,
    TimeProvider clock,
    ILogger<AppointmentService> logger)
{
    /// <summary>Upper bound on inserts per request, so a contended slot cannot make one request loop for long.</summary>
    private const int MaxAttempts = 5;

    public async Task<BookingResult> BookAsync(BookAppointmentCommand command, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        var result = "invalid";
        try
        {
            var booking = await BookCoreAsync(command, ct);
            result = booking.Replayed ? "replayed" : "created";
            return booking;
        }
        catch (NoCapacityException ex)
        {
            result = "no_capacity";
            logger.LogInformation("BookingRejected {Code} {DealershipId} {LimitingResource}",
                ex.Code, command.DealershipId, ex.LimitingResource);
            throw;
        }
        catch (DomainException ex)
        {
            result = ex is VehicleAlreadyBookedException ? "vehicle_booked" : "invalid";
            logger.LogInformation("BookingRejected {Code} {DealershipId}", ex.Code, command.DealershipId);
            throw;
        }
        finally
        {
            var tags = new TagList
            {
                { "result", result },
                { "channel", command.Channel.ToString() },
                { "dealership", command.DealershipId.ToString() }
            };
            SchedulerTelemetry.Bookings.Add(1, tags);
            SchedulerTelemetry.BookingDuration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, new TagList { { "result", result } });
        }
    }

    private async Task<BookingResult> BookCoreAsync(BookAppointmentCommand command, CancellationToken ct)
    {
        // 1. Idempotency (8.4): a retried request gets the original appointment back.
        var existing = await appointments.GetByIdempotencyKeyAsync(command.IdempotencyKey, ct);
        if (existing is not null)
        {
            return await ReplayAsync(existing, command, ct);
        }

        // 2. Reference data and business rules.
        var dealership = await referenceData.GetDealershipAsync(command.DealershipId, ct)
            ?? throw new NotFoundException("Dealership", command.DealershipId);
        var serviceType = await referenceData.GetServiceTypeAsync(command.ServiceTypeId, ct)
            ?? throw new NotFoundException("Service type", command.ServiceTypeId);

        var now = clock.GetUtcNow();
        using (SchedulerTelemetry.ActivitySource.StartActivity("Booking.Validate"))
        {
            policy.Validate(dealership, serviceType, command.Start, command.Channel, now);
        }

        // 3. Customer and vehicle (registered on first booking, UC-01 «extend»).
        var customer = await GetOrRegisterCustomerAsync(command.Customer, ct);
        var vehicle = await GetOrRegisterVehicleAsync(command.Vehicle, customer, ct);

        var startUtc = command.Start.ToUniversalTime();
        var endUtc = startUtc.AddMinutes(serviceType.DurationMinutes);

        // Fast, friendly check. The ex_appointment_vehicle constraint is still the real guarantee.
        if (await appointments.VehicleHasOverlapAsync(vehicle.Id, startUtc, endUtc, ct))
        {
            return await ReplayOrVehicleBookedAsync(command, ct);
        }

        // 4. Candidates. The snapshot covers the whole local day so that "least loaded" is meaningful.
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(dealership.TimeZone);
        var (dayFrom, dayTo) = SlotFinder.LocalDayRange(command.Start, timeZone);
        var snapshot = await resources.GetSnapshotAsync(
            dealership.Id, serviceType.Id, serviceType.RequiredBayType, dayFrom, dayTo, ct);

        if (snapshot.Bays.Count == 0 || snapshot.Technicians.Count == 0)
        {
            throw new ServiceNotSupportedException(serviceType.Code);
        }

        IReadOnlyList<Candidate> candidates;
        using (var activity = SchedulerTelemetry.ActivitySource.StartActivity("Availability.FindCandidates"))
        {
            candidates = SlotFinder.FindCandidates(snapshot, startUtc, endUtc);
            activity?.SetTag("candidate.count", candidates.Count);
        }

        var limitingResource = candidates.Count == 0
            ? SlotFinder.LimitingResource(snapshot, startUtc, endUtc)
            : Resource.Technician;

        // 5. Optimistic insert with retry (8.3). The database decides who wins.
        var attempt = 0;
        foreach (var candidate in candidates.Take(MaxAttempts))
        {
            attempt++;
            using var activity = SchedulerTelemetry.ActivitySource.StartActivity("Booking.Insert");
            activity?.SetTag("attempt", attempt);

            var appointment = Appointment.Confirm(
                dealership.Id, customer.Id, vehicle.Id, serviceType,
                candidate.ServiceBayId, candidate.TechnicianId,
                command.Start, command.Channel, command.CreatedBy, command.IdempotencyKey, now);

            try
            {
                await appointments.InsertAsync(appointment, ct);
                activity?.SetTag("outcome", "committed");

                logger.LogInformation(
                    "AppointmentBooked {AppointmentId} {DealershipId} {ServiceType} {Vin} after {Attempts} attempt(s)",
                    appointment.Id, dealership.Id, serviceType.Code, SchedulerTelemetry.MaskVin(vehicle.Vin), attempt);

                return new BookingResult(await GetAsync(appointment.Id, ct), Replayed: false);
            }
            catch (SlotTakenException ex) when (ex.Resource is Resource.Bay or Resource.Technician)
            {
                // Another request took this pair between our read and our insert: try the next one.
                activity?.SetTag("outcome", "slot_taken");
                limitingResource = ex.Resource;
                SchedulerTelemetry.BookingRetries.Add(1, new KeyValuePair<string, object?>("dealership", dealership.Id.ToString()));
                logger.LogInformation("BookingRetry {DealershipId} {Resource} attempt {Attempt}", dealership.Id, ex.Resource, attempt);
            }
            catch (Exception ex) when (ex is SlotTakenException or DuplicateIdempotencyKeyException)
            {
                // ex_appointment_vehicle or the idempotency index rejected the insert. Retrying cannot help.
                return await ReplayOrVehicleBookedAsync(command, ct);
            }
        }

        if (attempt > 0)
        {
            logger.LogWarning("BookingCandidatesExhausted {DealershipId} after {Attempts} attempts", dealership.Id, attempt);
        }

        // 6. Nothing free: offer alternatives.
        var suggestions = await availability.SuggestSlotsAsync(dealership, serviceType, command.Start, command.Channel, ct);
        throw new NoCapacityException(limitingResource, suggestions);
    }

    public async Task<AppointmentDetails> GetAsync(Guid id, CancellationToken ct) =>
        await appointments.GetDetailsAsync(id, ct) ?? throw new NotFoundException("Appointment", id);

    public Task<AppointmentDetails> CancelAsync(Guid id, CancellationToken ct) =>
        TransitionAsync(id, AppointmentStatus.Cancelled, a => a.Cancel(clock.GetUtcNow()), ct);

    public Task<AppointmentDetails> CompleteAsync(Guid id, CancellationToken ct) =>
        TransitionAsync(id, AppointmentStatus.Completed, a => a.Complete(), ct);

    public Task<AppointmentDetails> MarkNoShowAsync(Guid id, CancellationToken ct) =>
        TransitionAsync(id, AppointmentStatus.NoShow, a => a.MarkNoShow(), ct);

    private async Task<AppointmentDetails> TransitionAsync(
        Guid id, AppointmentStatus target, Action<Appointment> transition, CancellationToken ct)
    {
        var appointment = await appointments.GetByIdAsync(id, ct) ?? throw new NotFoundException("Appointment", id);

        // The entity enforces the state machine; it throws InvalidStatusTransitionException.
        transition(appointment);
        await appointments.UpdateAsync(appointment, ct);

        SchedulerTelemetry.Transitions.Add(1, new KeyValuePair<string, object?>("to", target.ToString()));
        logger.LogInformation("Appointment{Status} {AppointmentId}", target, id);

        return await GetAsync(id, ct);
    }

    /// <summary>
    /// The vehicle is already booked for this time. If that booking was made by this same request
    /// (a client retry that raced its first attempt), replay it; otherwise it is a real conflict.
    /// </summary>
    private async Task<BookingResult> ReplayOrVehicleBookedAsync(BookAppointmentCommand command, CancellationToken ct)
    {
        var winner = await appointments.GetByIdempotencyKeyAsync(command.IdempotencyKey, ct);
        return winner is not null
            ? await ReplayAsync(winner, command, ct)
            : throw new VehicleAlreadyBookedException();
    }

    /// <summary>Same key with the same booking returns the original; with a different booking it is a client bug.</summary>
    private async Task<BookingResult> ReplayAsync(Appointment existing, BookAppointmentCommand command, CancellationToken ct)
    {
        var vehicle = await vehicles.GetByVinAsync(NormalizeVin(command.Vehicle.Vin), ct);

        var samePayload = existing.DealershipId == command.DealershipId
                          && existing.ServiceTypeId == command.ServiceTypeId
                          && existing.StartUtc == command.Start.ToUniversalTime()
                          && vehicle?.Id == existing.VehicleId;

        if (!samePayload)
        {
            throw new IdempotencyKeyReusedException();
        }

        return new BookingResult(await GetAsync(existing.Id, ct), Replayed: true);
    }

    private async Task<Customer> GetOrRegisterCustomerAsync(CustomerInfo info, CancellationToken ct)
    {
        var email = info.Email.Trim().ToLowerInvariant();
        var customer = await customers.GetByEmailAsync(email, ct);
        if (customer is not null)
        {
            return customer;
        }

        return await customers.AddAsync(
            new Customer { Name = info.Name.Trim(), Email = email, Phone = info.Phone.Trim() }, ct);
    }

    private async Task<Vehicle> GetOrRegisterVehicleAsync(VehicleInfo info, Customer owner, CancellationToken ct)
    {
        var vin = NormalizeVin(info.Vin);
        var vehicle = await vehicles.GetByVinAsync(vin, ct)
            ?? await vehicles.AddAsync(
                new Vehicle { CustomerId = owner.Id, Vin = vin, Make = info.Make.Trim(), Model = info.Model.Trim(), Year = info.Year }, ct);

        return vehicle.CustomerId == owner.Id ? vehicle : throw new VinOwnedByOtherCustomerException();
    }

    private static string NormalizeVin(string vin) => vin.Trim().ToUpperInvariant();
}
