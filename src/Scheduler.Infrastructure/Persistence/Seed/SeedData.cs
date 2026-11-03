using Scheduler.Application.Domain.Entities;
using Scheduler.Application.Domain.Enums;

namespace Scheduler.Infrastructure.Persistence.Seed;

/// <summary>
/// Reference data for one demo dealership. IDs are fixed so that migrations stay stable
/// and the .http examples can refer to them.
/// </summary>
internal static class SeedData
{
    public static readonly Guid DealershipId = Guid.Parse("d0000000-0000-0000-0000-000000000001");

    public static readonly Guid OilChangeId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    public static readonly Guid BrakeServiceId = Guid.Parse("50000000-0000-0000-0000-000000000002");
    public static readonly Guid EvBatteryCheckId = Guid.Parse("50000000-0000-0000-0000-000000000003");

    private static readonly Guid AnId = Guid.Parse("70000000-0000-0000-0000-000000000001");
    private static readonly Guid BinhId = Guid.Parse("70000000-0000-0000-0000-000000000002");
    private static readonly Guid ChiId = Guid.Parse("70000000-0000-0000-0000-000000000003");

    public static readonly Dealership[] Dealerships =
    [
        new() { Id = DealershipId, Name = "Keyloop Motors District 1", TimeZone = "Asia/Ho_Chi_Minh" }
    ];

    // Monday to Saturday 08:00-17:00; closed on Sunday.
    public static readonly OpeningHours[] OpeningHours = Enumerable.Range(1, 6)
        .Select(day => new OpeningHours
        {
            DealershipId = DealershipId,
            DayOfWeek = (DayOfWeek)day,
            OpenTime = new TimeOnly(8, 0),
            CloseTime = new TimeOnly(17, 0)
        })
        .ToArray();

    public static readonly ServiceType[] ServiceTypes =
    [
        new() { Id = OilChangeId, Code = "oil-change", Name = "Oil change", DurationMinutes = 45, RequiredBayType = BayType.General },
        new() { Id = BrakeServiceId, Code = "brake-service", Name = "Brake service", DurationMinutes = 90, RequiredBayType = BayType.Lift },
        new() { Id = EvBatteryCheckId, Code = "ev-battery-check", Name = "EV battery check", DurationMinutes = 60, RequiredBayType = BayType.EV }
    ];

    public static readonly ServiceBay[] ServiceBays =
    [
        new() { Id = Guid.Parse("b0000000-0000-0000-0000-000000000001"), DealershipId = DealershipId, Name = "Bay 1", BayType = BayType.Lift },
        new() { Id = Guid.Parse("b0000000-0000-0000-0000-000000000002"), DealershipId = DealershipId, Name = "Bay 2", BayType = BayType.Lift },
        new() { Id = Guid.Parse("b0000000-0000-0000-0000-000000000003"), DealershipId = DealershipId, Name = "Bay 3", BayType = BayType.General },
        new() { Id = Guid.Parse("b0000000-0000-0000-0000-000000000004"), DealershipId = DealershipId, Name = "Bay 4", BayType = BayType.EV }
    ];

    public static readonly Technician[] Technicians =
    [
        new() { Id = AnId, DealershipId = DealershipId, Name = "An" },
        new() { Id = BinhId, DealershipId = DealershipId, Name = "Binh" },
        new() { Id = ChiId, DealershipId = DealershipId, Name = "Chi" }
    ];

    // An: oil + brake. Binh: brake + EV. Chi: all three.
    public static readonly TechnicianSkill[] TechnicianSkills =
    [
        new() { TechnicianId = AnId, ServiceTypeId = OilChangeId },
        new() { TechnicianId = AnId, ServiceTypeId = BrakeServiceId },
        new() { TechnicianId = BinhId, ServiceTypeId = BrakeServiceId },
        new() { TechnicianId = BinhId, ServiceTypeId = EvBatteryCheckId },
        new() { TechnicianId = ChiId, ServiceTypeId = OilChangeId },
        new() { TechnicianId = ChiId, ServiceTypeId = BrakeServiceId },
        new() { TechnicianId = ChiId, ServiceTypeId = EvBatteryCheckId }
    ];
}
