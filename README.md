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

## Trying the API

**Rider / IntelliJ:** open [`requests/appointments.http`](requests/appointments.http) and click the green arrow next to each request, top to bottom. It covers every endpoint and most error codes; later requests reuse the ID of the booked appointment.

**curl:**

```bash
curl http://localhost:5057/api/v1/service-types

curl "http://localhost:5057/api/v1/dealerships/d0000000-0000-0000-0000-000000000001/availability?serviceTypeId=50000000-0000-0000-0000-000000000002&date=2026-11-20"

curl -i -X POST http://localhost:5057/api/v1/appointments \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: $(uuidgen)" \
  -d '{"dealershipId":"d0000000-0000-0000-0000-000000000001","serviceTypeId":"50000000-0000-0000-0000-000000000002",
       "startTime":"2026-11-20T09:00:00+07:00","channel":"Online",
       "customer":{"name":"Nguyen Van An","email":"an@example.com","phone":"+84901234567"},
       "vehicle":{"vin":"1HGCM82633A004352","make":"Honda","model":"Civic","year":2021}}'
```

**Postman / Insomnia:** import `http://localhost:5057/openapi/v1.json`.

To start again from clean seed data: `docker compose down -v && docker compose up -d`.

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

## AI Collaboration Narrative

I used Claude throughout the project, for both the design and the implementation. The full account, with concrete examples, is in [section 13 of the design document](Docs/System-design.md#13-genai-assisted-design).

### Strategy: AI drafts, I decide

- **I set the method, not the AI.** The AI's first instinct was to jump to a technical solution (stack, constraints, endpoints). I made it follow the order I trust: business process → use cases → three-layer architecture → sequence diagrams → code. Anything it had drafted earlier was treated as provisional and re-derived from the business process.
- **I owned every decision.** The AI produced options, drafts and diagrams quickly; scope, trade-offs and design choices were mine. For example, I chose PostgreSQL exclusion constraints over locking only after comparing the alternatives ([section 8.2](Docs/System-design.md#82-options-considered)).
- **Small steps, explained before moving on.** Implementation went step by step: domain, rules, repositories, services, API, tests, observability. For each step I asked why each part was there, and did not move on until I could explain and change it myself.
- **Domain questions before code.** Asking "what does a service advisor actually do?" added walk-in bookings without lead time, a `NoShow` status and the booking channel, which neither the brief nor the AI's first draft contained.

### Verifying and refining the output

| Check | What it caught |
| --- | --- |
| **Challenge assumptions** | A proposed .NET 8 runtime (support ends November 2026), so I switched to .NET 10 LTS; about 20 assumptions cut to the 10 that change behaviour; two error codes merged into one `NO_CAPACITY` with a `limitingResource` field |
| **See it work before accepting it** | I did not accept the exclusion-constraint design until a step-by-step simulation of two concurrent requests showed the race and how the constraint resolves it |
| **Rewrite core logic myself** | I rewrote `BookingRules` and `BookingPolicy` by hand; the unit tests caught two bugs in my version (`Validate` never called the checks, and `Microsecond` was used instead of `Millisecond`) |
| **Tests against a real database** | Designing the parallel-retry concurrency test exposed an idempotency race in AI-written code: the vehicle constraint could fire before the idempotency index, returning `409` instead of replaying the booking |
| **Run it like a user** | Running the app and the client stub by hand found issues no automated test would: the `.http` file used the wrong IDE syntax, a Kerberos warning in the container logs, and an architecture diagram that did not match the code |

### Ensuring final quality

- **Correctness is enforced by the database, not by trust in the code.** Double-booking is prevented by PostgreSQL constraints, so a bug in application code cannot cause it.
- **50 automated tests on every push.** 24 unit tests cover the booking rules and slot finding; 26 integration tests run every endpoint and error code against a real PostgreSQL (Testcontainers), including three concurrency tests: 10 parallel requests for the last slot produce exactly one booking.
- **The build fails on warnings.** `TreatWarningsAsErrors` and nullable reference types are on, which also caught a vulnerable package (NU1903).
- **CI on GitHub Actions** builds in Release and runs all tests against a real database on every push.
- **The documentation was checked against the running system.** The design document was updated wherever the implementation differed from it (endpoints, error formats, metrics, the architecture diagram).
- **Understanding is the bar for accepting code.** If I could not explain why a line was there (for example `ChangeTracker.Clear()` after a failed insert), it was not done.

The main lesson: AI made me much faster, but tests, running the app myself and questioning the output are what made the result trustworthy.

## Configuration

| Key | Default | |
| --- | --- | --- |
| `ConnectionStrings:Scheduler` | (required) | User secrets locally, `ConnectionStrings__Scheduler` in Docker |
| `BookingRules:SlotStepMinutes` · `OnlineLeadTime` · `MaxDaysAhead` | `15` · `01:00:00` · `60` | Booking limits (A-06) |
| `Otel:TracesEndpoint` · `Otel:MetricsEndpoint` | unset (set in Development) | Exporters are off when unset |

Authentication is out of scope; run the API behind a private network or tunnel.
