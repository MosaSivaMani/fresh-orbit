# Fresh Orbit

Our own seafood e-commerce store (India, multi-city): customer website, customer mobile app and admin dashboard,
all served by ONE ASP.NET Core modular monolith and ONE PostgreSQL database, in ONE repository.

Read before working: `docs/architecture/README.md`, `docs/database/README.md`, `docs/api/README.md`,
`docs/team/plan.md` (who owns what).

## Rules

1. **Modular monolith, never microservices.** One backend solution, hosts `FreshOrbit.Api`, `FreshOrbit.Worker`,
   `FreshOrbit.Migrations` built from the same code.
2. **Module shape.** Each module has `<Module>.Domain`, `<Module>.Application`, `<Module>.Infrastructure`,
   `<Module>.Public`. Another module may reference only `.Public` (an event handler may also reference another
   module's `.Domain` for event classes only). `.Domain` references only `FreshOrbit.SharedKernel`.
3. **One DbContext** (`FreshOrbitDbContext`); each module adds its tables through its `IModuleModel` in its own
   Postgres schema (lowercase module name). Only the owning module writes its tables.
4. **The server computes, the front end renders.** Never trust prices, discounts, stock, totals or payment
   status from a client. Clients never calculate business values.
5. **Command shape: Read → Decide → Commit → Follow-on.** No database call inside a loop. One transaction per
   command (`SaveChangesAsync` once). Another module's records change in a handler of our event (outbox), never
   inside our command — except the checkout, which reserves stock / slot / coupon through `.Public` in the same
   transaction (see `docs/database/README.md` §4).
6. **Movements are the record.** Stock on hand is a fold over `stock_movements`, kept as a summary in the same save.
7. **Configuration is data; missing configuration is refused.** No price at a location = not for sale there.
   Never guess, never fall back silently. No city names in code.
8. **Units.** Money is `long` paise (`_paise`), weight is `int` grams (`_grams`), rates are basis points (`_bp`).
   Times are UTC `DateTimeOffset`. Stored codes are UPPER_SNAKE strings, never ints.
9. **Concurrency.** Aggregate roots carry a row version (Postgres `xmin`). Updates quote it with `If-Match`.
   412 `CONCURRENCY_CONFLICT` for a stale quote, 409 `CONCURRENT_SAVE` for a lost race.
10. **HTTP.** REST under `/api/v1` (customer) and `/api/v1/admin` (staff). Errors are RFC 7807 Problem Details
    with `code` and field-keyed `errors`. 400 unreadable, 401, 403, 404, 409, 412, 422 rule refusal, 428 missing
    version, 429. Lists return `{ items, page, pageSize, totalCount }`. Retryable writes take `Idempotency-Key`.
11. **Tests prove behaviour.** xUnit + AwesomeAssertions (FluentAssertions-compatible, free) or plain Assert.
    Unit tests for domain rules; integration tests against real Postgres via Testcontainers, never an in-memory
    database. Assert codes and fields, never message wording.
12. **Names.** .NET Framework Design Guidelines. Requests and responses are `sealed record`s. Error message is one
    sentence naming the field to fix.
13. **Free and open-source first.** No paid service in code paths without an adapter and a free/fake implementation.
14. **Never copy code from other repositories** (including anything under `~/Developer`). Ideas only.
15. **Do not use AgentOps** (plugin, skills, CLI, MCP tools) in this project.
16. **Sample data** lives only in `backend/data/sample`, is refused in Production, and is tracked so it can be purged.
17. **Everything is documented.** Every decision goes into `docs/decisions/log.md`, every analysis into `docs/research/`, every phase result into `docs/team/progress.md`, and module behaviour into `docs/product/<module>.md` — in the same change that makes it. Start at `docs/README.md`.

## Commands

| Task | Command |
|---|---|
| Start Postgres, MinIO, Mailpit | `docker compose -f infra/compose/docker-compose.dev.yml up -d` |
| Build backend | `dotnet build backend/FreshOrbit.slnx` |
| Test backend | `dotnet test backend/FreshOrbit.slnx` (needs Docker running) |
| Run API | `dotnet run --project backend/src/Hosts/FreshOrbit.Api` |
