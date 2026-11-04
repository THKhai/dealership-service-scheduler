# Unified Service Scheduler

A backend service for booking dealership service appointments. Each booking automatically reserves a suitable **service bay** and a **qualified technician** for the full service duration, and **double-booking is impossible**: PostgreSQL exclusion constraints reject overlapping bookings, whatever the number of API instances.

.NET 10 · ASP.NET Core · EF Core · PostgreSQL 17 · OpenTelemetry · xUnit + Testcontainers

Full design: [`Docs/System-design.md`](Docs/System-design.md)

## Quick start

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download) and Docker.

```bash
# 1. Start PostgreSQL, Jaeger and Prometheus
cp .env.example .env                      # then set POSTGRES_PASSWORD
docker compose up -d

# 2. Point the API at the database (stored outside the repo)
dotnet user-secrets set "ConnectionStrings:Scheduler" \
  "Host=localhost;Port=5432;Database=scheduler;Username=scheduler;Password=<POSTGRES_PASSWORD>" \
  --project src/Scheduler.Api

# 3. Run. The schema and seed data are applied at startup.
dotnet run --project src/Scheduler.Api    # http://localhost:5057
```

Then open [`requests/appointments.http`](requests/appointments.http) in Rider, Visual Studio or VS Code and run the requests top to bottom. The OpenAPI document is at `http://localhost:5057/openapi/v1.json`.

**Everything in containers** (as on the server):

```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build   # API on http://localhost:8080
```

## Tests

```bash
dotnet test
```

Docker must be running: integration tests start their own PostgreSQL with Testcontainers. A real database is the only way to verify the exclusion constraints.

| Project | What it covers |
| --- | --- |
| `Scheduler.UnitTests` | Booking rules (lead time, 15-minute steps, opening hours, timezones), and slot finding (overlaps, back-to-back, least-loaded assignment) |
| `Scheduler.IntegrationTests` | Every endpoint and error code through HTTP; **concurrency**: 10 parallel requests for the last slot produce exactly one booking; parallel retries with one `Idempotency-Key` produce one appointment |

CI (GitHub Actions) builds and runs all tests on every push.

## API

| Method | Path | |
| --- | --- | --- |
| GET | `/api/v1/dealerships` | Dealerships and opening hours |
| GET | `/api/v1/service-types` | Services, durations, required bay type |
| GET | `/api/v1/dealerships/{id}/availability?serviceTypeId=&date=` | Free start times for a day |
| POST | `/api/v1/appointments` | Book (requires `Idempotency-Key`); the system assigns bay and technician |
| GET | `/api/v1/appointments/{id}` | View |
| POST | `/api/v1/appointments/{id}/cancel` · `/complete` · `/no-show` | Status changes |
| GET | `/health/live` · `/health/ready` | Health checks |

Errors are RFC 7807 ProblemDetails with a stable `code` and a `traceId`. A full slot returns `409 NO_CAPACITY` with the limiting resource and the three nearest free times. All codes: [section 9.5](Docs/System-design.md#95-error-codes).

Seed data: one dealership (Asia/Ho_Chi_Minh, Mon-Sat 08:00-17:00), four bays (2 Lift, 1 General, 1 EV), three technicians with different skills, and three service types. IDs are fixed; see the variables at the top of the `.http` file.

## Project structure

```
src/
  Scheduler.Api             Presentation: controllers, request validation, ProblemDetails, telemetry setup
  Scheduler.Application     Business: domain entities and rules, AppointmentService, AvailabilityService,
                            repository interfaces. No EF Core or HTTP references.
  Scheduler.Infrastructure  Data access: EF Core DbContext, repositories, migrations (incl. the exclusion
                            constraints in raw SQL), seed data, PostgreSQL error mapping
tests/
  Scheduler.UnitTests
  Scheduler.IntegrationTests
requests/appointments.http  Client stub
```

Dependencies point inwards: `Api → Application ← Infrastructure`. The application defines the repository interfaces and Infrastructure implements them, so business logic is tested without a database and the database could be swapped without touching it.

## How double-booking is prevented

1. Availability is calculated from one read-only snapshot query; nothing is locked.
2. The insert is the arbiter. Three PostgreSQL exclusion constraints reject any `Confirmed` appointment whose `[start, end)` range overlaps another on the same bay, technician or vehicle.
3. If the insert loses a race (`23P01`), the repository raises `SlotTakenException` and the service tries the next free bay/technician pair; when none are left it returns `409 NO_CAPACITY` with suggestions.

Details and the alternatives considered: [section 8](Docs/System-design.md#8-concurrency-and-consistency).

## Observability

- **Logs**: Serilog JSON to stdout, with trace and span IDs on every line. VINs are masked and customer details are never logged.
- **Traces**: OpenTelemetry to Jaeger (`http://localhost:16686`): HTTP, every SQL command, and custom spans per booking attempt.
- **Metrics**: OpenTelemetry to Prometheus (`http://localhost:9090`): bookings by result, retries (contention), latency, status transitions.
- Every response carries a `traceparent` header, and every error body a matching `traceId`.

## Configuration

| Key | Default | |
| --- | --- | --- |
| `ConnectionStrings:Scheduler` | (required) | User secrets locally, `ConnectionStrings__Scheduler` in Docker |
| `BookingRules:SlotStepMinutes` · `OnlineLeadTime` · `MaxDaysAhead` | `15` · `01:00:00` · `60` | Booking limits (A-06) |
| `Otel:TracesEndpoint` · `Otel:MetricsEndpoint` | unset (set in Development) | Exporters are off when unset |

Authentication is out of scope; run the API behind a private network or tunnel.
