# Deploy gaps E2E-3, E2E-5, E2E-6, E2E-7: analysis, 2026-10-24

These are the follow-ups from the end-to-end deploy check (`e2e-deploy-check-2026-10.md` §3), done on the branch
`backend/deploy-gaps-ops`. E2E-1 (DLT SMS adapter) is handled separately. For each item, this note records what was
found, the options, what was chosen and how it was proven. How to repeat the proofs is in `runbooks/e2e-check.md`.

## 1. E2E-3: ASP.NET Core Data Protection keys

### What uses Data Protection today

| Candidate | Api | Worker | Migrations |
|---|---|---|---|
| Cookie authentication (`AddCookie`) | Not used. Authentication is JWT bearer only (`AuthenticationExtensions.AddFreshOrbitAuthentication`) | Not used | Not used |
| JWT bearer | Does not use Data Protection. Tokens are HMAC-signed with `Jwt:SigningKey` | No authentication | No authentication |
| Antiforgery | Not registered: there is no MVC, Razor or `AddAntiforgery`. The `DisableAntiforgery()` on the catalog image upload is metadata only | — | — |
| `IDataProtector` / `Protect` in our code | None (searched every module) | None | None |
| Refresh tokens | Opaque random values, stored hashed in `identity.refresh_tokens`. The website keeps them in an httpOnly cookie set by Next.js (`/bff/auth`, D37), not by ASP.NET | — | — |
| Registered at all? | Yes: `AddAuthentication` calls `AddDataProtection`, and its hosted service loads the key ring at start-up. That is where the warning came from | No (`Host.CreateApplicationBuilder`, no authentication) | No |

So **nothing protects a payload today**, and losing the keys on a deploy breaks nothing yet. The risk is in the
future. The later D40 hardening item (staff refresh tokens in httpOnly cookies) would break if it used ASP.NET cookie
authentication or `IDataProtector`. So would any antiforgery or `TempData` added later, because every deploy would
invalidate those cookies. The same goes for a second api instance, whose keys would differ from the first one's.
Persisting the keys now costs one volume and removes that trap and the start-up warning.

### Options

| Option | New package? | Verdict |
|---|---|---|
| `PersistKeysToFileSystem` on a named volume | No (in the shared framework) | **Chosen.** One volume, shared by every api container on the host |
| `PersistKeysToDbContext` (EF store) | Yes: `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` | Refused (rule 18: no new packages) |
| A custom `IXmlRepository` over `FreshOrbitDbContext` | No | Not needed while there is one server. It would add a table, a migration and a module owner. Revisit only for several hosts (see §5) |
| Redis / Azure / AWS key stores | Yes, and a paid or extra service | Refused (rule 13, rule 18) |

**Encryption at rest.** `ProtectKeysWithCertificate` (X.509) is in the shared framework, but it needs a certificate
and a password to manage, rotate and back up. While no payload is protected, that is cost with no benefit. Decided
for now: no XML encryptor. The keys are protected by the following:
- The volume directory is owned by the app user (UID 1654), mode `700`. `api.Dockerfile` creates `/keys` that way,
  and a new named volume inherits the owner and mode.
- Only the api container mounts the volume. The server's disk encryption covers it where the provider offers it.
  Oracle Cloud encrypts block volumes by default. For Hostinger it is not confirmed: check before go-live.
- The keys are **not** in the backups. A lost key ring only invalidates protected payloads, and there are none. When a
  feature starts relying on Data Protection, decide again (X.509 encryption and backing up the volume).

Data Protection still logs one Warning each time it creates a key: on the first start, then about every 90 days when
the key rolls. The warning reads `No XML encryptor configured. Key … may be persisted to storage in unencrypted form.`
It is expected and accepted, for the reason above. The warning that mattered (`Storing
keys in a directory '/home/app/.aspnet/DataProtection-Keys' that may not be persisted outside of the container`) is
gone.

### What was built

- `FreshOrbit.Hosting/DataProtectionSetup.cs`: `AddFreshOrbitDataProtection()` (called by the Api only) sets
  `SetApplicationName("FreshOrbit")` and points the key repository at `DataProtection:KeysPath`. With the same
  application name, every release and instance shares one key ring.
- Rule 7: `DataProtection:KeysPath` is **required outside Development and Testing** and must be an absolute path. A
  missing or relative path is refused at start-up (`OptionsValidationException`; the host logs one Critical line and
  exits 1, E2E-2). In Development and Testing an empty path keeps the framework's per-user default, so `dotnet run`
  and the integration tests need nothing.
- `docker-compose.prod.yml`: `DataProtection__KeysPath: /keys` on the api, and a new named volume
  `dataprotection-keys`. The Worker and Migrations hosts do not use Data Protection, so they get no volume.
- Tests: the shared `ApiFactory` (`FreshOrbit.Testing`) sets `DataProtection:KeysPath` to a temporary folder. Tests
  that start the Api as Production, such as `Notifications.Tests` `SmsSenderStartupTests`, therefore still start. A
  new test host outside `ApiFactory` that runs the Api outside Development or Testing must set it too.

### Proof

- `ArchitectureTests/DataProtectionSetupTests` (9 tests):
  - the configured directory is the key repository;
  - a payload protected before a host restart is readable after it;
  - two hosts running at the same time on one directory read each other's payloads;
  - two separate directories cannot read each other's payloads (so the test proves something);
  - Production or Staging with no path, an empty path or a relative path is refused;
  - Development and Testing may leave the path empty.
- `e2e-check.sh` (step `up`, both passes):
  - a `key-*.xml` file exists in the api's `/keys`;
  - the api logs no "may not be persisted outside of the container" warning;
  - after the api container is recreated (as a deploy does), the key files are the same, so no new key was made.

## 2. E2E-5: an Error log on an empty database

**Cause.** With Npgsql 10.0.3 and EF Core 10.0.13, `MigrateAsync` runs
`SELECT migration_id, product_version FROM platform.__ef_migrations_history` once before it runs the history table's
`CREATE … IF NOT EXISTS` script. On an empty database that query fails (`42P01`), and EF's command logger writes
`Failed executing DbCommand` at **Error** level before the migrator handles the failure. The run is still correct
and exits 0.

Also found: the Npgsql history repository's `ExistsAsync()` returned **true** on that empty database. So "check
whether the table exists first" cannot use it.

**Options.**

| Option | Verdict |
|---|---|
| Filter `RelationalEventId.CommandError` in the Migrations host | Refused. It would also hide real failed commands during migrations and reference loading |
| Downgrade that event with `ConfigureWarnings` | Same objection. It applies to every command of the context |
| Check `ExistsAsync()` first | Does not work: it answers true on an empty database |
| Run `IHistoryRepository.CreateIfNotExistsAsync()` before `MigrateAsync` | **Chosen.** This is EF's own idempotent script: a `DO` block that creates schema `platform` if it is missing, then `CREATE TABLE IF NOT EXISTS`. `MigrateAsync` runs the same script again before it takes its `LOCK TABLE`, so nothing changes about locking or concurrency. On an existing database it does nothing. No log event is filtered |

**Proof.**
- `SampleData.Tests/MigrationRunnerTests` creates a brand-new database in the Testcontainers server. It runs the
  real `MigrationRunner` twice with a log provider that keeps every entry, then asserts:
  - both runs exit 0;
  - no entry is Error or worse;
  - `InitialCreate` was applied;
  - the history table has rows.

  The test **fails** if the call is removed: it catches the `Failed executing DbCommand` Error.
- `e2e-check.sh` (step `migrate`): migrations runs 1 and 2 in the production image log no `"@l":"Error"` or `Fatal`
  line. The third run is a deliberately refused second `--create-admin`, so its Error line is expected and that run
  is not checked.

## 3. E2E-6: the website footer's city list

The footer (`apps/web/src/components/site-footer.tsx`) is rendered on the server inside the root layout. It is cached
in two places:
- its `GET /cities` call (`publicHttp(3600)`);
- static pages, through the root layout's `revalidate = 3600`.

A city that staff activate could therefore be missing for up to one hour.

| Option | Verdict |
|---|---|
| Tag-based on-demand revalidation (`revalidateTag` behind an authenticated endpoint, called when `CityActivated` is handled) | Not now. It needs a new shared secret between api and web, a new outbound HTTP call from a module event handler, and a retry path. Next.js's cache is also per web instance. That is a lot of new surface for a list that changes a few times a year |
| Client-side fetch of the footer cities | No. Every page view would make one more request, the city names would leave the server-rendered HTML (SEO, no-JS), and the page would shift as the list loads |
| **Shorter revalidate: 300 s** for both the fetch and the root layout | **Chosen.** A newly activated city shows within about 5 minutes. The category list already uses 300 s. The cost is one cheap cached `GET /cities` per 5 minutes per web instance, plus regenerating the static pages at most every 5 minutes |

Rule 4 holds: the server decides which cities are active, and the web only lists what the API returns.

**Proof.** `apps/web/src/components/site-footer.test.tsx` (3 tests):
- the footer lists the API's cities and asks for at most 300 s of caching;
- the root layout's literal `revalidate` equals the footer's constant (Next.js needs a literal there, so a test stops
  the two from drifting apart);
- the footer still renders the pincode hint when the API is down.

`next build` shows the static pages with `Revalidate 5m`.

## 4. E2E-7: running `deploy.sh` off Linux

`deploy.sh` needed `flock` and `mapfile` (bash 4). It also expanded a possibly empty array under `set -u`, which
bash before 4.4 treats as unbound. macOS ships bash 3.2 and no `flock`. Production behaviour on Linux stays exactly
as it was:

| Change | Linux server | macOS |
|---|---|---|
| Lock | `flock -n` on `.deploy/deploy.lock`, as before | Fallback: atomic `mkdir .deploy/deploy.lock.d` holding the PID, removed on exit. A lock whose PID no longer runs (the deploy was killed with `-9`) is reported and taken over |
| Service list | `while read` over `compose config --services` (was `mapfile`) | Same |
| Migration arguments | `${a[@]+"${a[@]}"}` (same result) | No "unbound variable" when `SEED_SAMPLE_DATA=false` |
| Shell check | POSIX check before `set -o pipefail`: not bash, or bash older than 3.2, exits 1 with a clear message | Same |
| `usage` | Prints the whole header comment (awk) instead of a fixed line range | Same |

`require_command` no longer demands `flock`, because the fallback covers its absence.

The proof is recorded in `runbooks/e2e-check.md` §5: real `deploy.sh` runs on the Mac (bash 3.2, no `flock`)
against the `fo-e2e` project.

## 5. Open points

- When a feature starts relying on Data Protection (cookie authentication for staff refresh tokens, D40 hardening;
  antiforgery), decide on key encryption (X.509 from a file, a secret in `.env.prod`) and on backing up the
  `dataprotection-keys` volume.
- With **more than one server**, a host volume is not shared. At that point, use a database-backed `IXmlRepository`
  (a small table in the `platform` schema, no new package) or a shared file system.
- The footer can still be up to 5 minutes late. If the owner wants activation to show instantly, on-demand
  revalidation (above) is the next step.
