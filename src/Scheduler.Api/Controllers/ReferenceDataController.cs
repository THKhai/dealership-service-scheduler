using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Scheduler.Api.Contracts;
using Scheduler.Application.Abstractions;
using Scheduler.Application.Availability;
using Scheduler.Application.Domain.Enums;

namespace Scheduler.Api.Controllers;

[ApiController]
[Route("api/v1/dealerships")]
public sealed class DealershipsController(IReferenceDataRepository referenceData, AvailabilityService availability) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<DealershipResponse>> List(CancellationToken ct) =>
        (await referenceData.ListDealershipsAsync(ct)).Select(DealershipResponse.From);

    /// <summary>UC-02 View available start times for one service on one local date. A snapshot, not a hold (A-09).</summary>
    [HttpGet("{id:guid}/availability")]
    [ProducesResponseType<AvailabilityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<AvailabilityResponse> Availability(
        Guid id,
        [FromQuery, BindRequired] Guid serviceTypeId,
        [FromQuery, BindRequired] DateOnly date,
        [FromQuery] BookingChannel channel = BookingChannel.Online,
        CancellationToken ct = default) =>
        AvailabilityResponse.From(await availability.GetDailyAsync(id, serviceTypeId, date, channel, ct));
}

[ApiController]
[Route("api/v1/service-types")]
public sealed class ServiceTypesController(IReferenceDataRepository referenceData) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<ServiceTypeResponse>> List(CancellationToken ct) =>
        (await referenceData.ListServiceTypesAsync(ct)).Select(ServiceTypeResponse.From);
}
