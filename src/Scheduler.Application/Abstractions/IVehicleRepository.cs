using Scheduler.Application.Domain.Entities;

namespace Scheduler.Application.Abstractions;

public interface IVehicleRepository
{
    /// <summary>Vehicles are matched by VIN (A-07).</summary>
    Task<Vehicle?> GetByVinAsync(string vin, CancellationToken ct);

    /// <summary>Registers an unknown vehicle (UC-01 «extend» Register Vehicle). If the VIN was registered concurrently, returns that vehicle instead.</summary>
    Task<Vehicle> AddAsync(Vehicle vehicle, CancellationToken ct);
}
