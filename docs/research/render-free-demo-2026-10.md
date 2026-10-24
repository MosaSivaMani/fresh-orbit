# Free public demo on Render + Neon (October 2026)

Goal (owner, option B): a free, shareable demo of the website, admin and API with the Visakhapatnam sample store.
Not for real customers. Owner steps: `runbooks/render-demo.md`.

## Constraints (as published by the providers in 2026; free plans can change)

| Provider | Free plan | What it means for us |
|---|---|---|
| Render web service | 512 MB RAM, a small share of one CPU, sleeps after 15 min idle, ephemeral disk, no free background workers, no pre-deploy command, 750 instance hours a month per workspace | Api and Worker cannot be two services; migrations cannot be a release step; media files vanish on every restart; cold start on each wake |
| Render static site | Free, global CDN, rewrite rules, headers | Admin (Vite SPA) fits; no proxy needed if the Api allows its origin (CORS) |
| Neon | 0.5 GB storage, compute suspends after 5 min idle, TLS required, no card | Fits the sample store; first query after a pause waits about a second; direct (unpooled) endpoint for EF migrations and Hangfire |

## Options weighed

| Question | Options | Chosen | Why |
|---|---|---|---|
| Worker | a) second Render service (paid), b) Worker process next to the Api in one container, c) the Worker's registrations inside the Api | c, behind `Hosting:RunBackgroundJobsInApi` (Demo only) | One .NET process in 512 MB; the registrations move to `FreshOrbit.Hosting` and are shared, not copied |
| Migrations and sample data | a) Render one-off job (not on free), b) migrations inside the Api process, c) the Migrations host run before the Api in the same container | c (`api-demo-start.sh`) | Reuses the Migrations host unchanged; one process at a time, so the memory peaks do not add up; the Api never references the Migrations assembly (an Api → Migrations project reference would copy two `appsettings.json` into one publish folder) |
| Dev SMS outbox exposed | a) refuse when any non-sample row exists, b) a database marker set only on an empty database | b | Testers create customers and orders, so (a) would refuse after the first sign-in; (b) still makes a mistaken connection to a real database stop with nothing changed |
| Media on an ephemeral disk | a) re-render missing files in the Catalog loader, b) wipe and reload the demo on each start, c) a database or S3 file store, d) draw the placeholder for any missing image key in Demo | d | Host-only change; data survives sleeps (b would lose testers' orders every 15 minutes); the sample pictures are text-free placeholders, so the redraw looks the same |
| Service addresses | Unknown until Render creates the services | Plain names in `render.yaml`, one table in the runbook to fix four values if Render adds a suffix | Render cannot reference another service's public URL in a Blueprint |
| Data Protection keys | Required outside Development and Testing | Not required in Demo; Staging and Production unchanged | Nothing protects data with them yet; a restart would only sign users out once a feature uses them |

## Risks

- **Memory.** The Api with every module, Hangfire (2 workers) and the outbox loop must stay under 512 MB. The image uses
  workstation GC and `DOTNET_GCConserveMemory`; CI (`render-demo.yml`) runs it with `--memory 512m` and prints the
  usage after start, after requests and after a restart.
- **Cold start.** Each wake starts a container, runs the Migrations host (everything skipped, but EF model building
  and about a hundred existence checks) and then the Api. ReadyToRun shortens JIT time. Expect 1 to 3 minutes on the
  free CPU; the website waits for the Api on its first page.
- **Shared client IP.** Testers reach the Api through Render's proxy or the website's server, so per-IP limits are
  shared; `appsettings.Demo.json` raises them.
