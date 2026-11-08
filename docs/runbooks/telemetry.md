# Runbook: telemetry (OpenTelemetry traces and metrics) and the retention purges

The Api and the Worker can send OpenTelemetry traces and metrics to any OTLP/HTTP receiver (BE-8, decisions
D332 and D333). The code is in `backend/src/Hosts/FreshOrbit.Hosting/TelemetrySetup.cs` and `Telemetry/`.

## 1. The one switch: `Telemetry:OtlpEndpoint`

| Value (`TELEMETRY_OTLP_ENDPOINT` in `.env.prod`) | Result |
|---|---|
| empty (the default in `appsettings.json` and `.env.prod.example`) | Telemetry is **off on purpose**. No OpenTelemetry provider, exporter or outbox sampler is registered; the host pays nothing. This is explicit configuration, not a silent fallback: the value is present and empty. The free 512 MB demo runs this way. |
| an absolute `http://` or `https://` URL, e.g. `http://alloy:4318` | Traces go to `<url>/v1/traces` and metrics to `<url>/v1/metrics` with OTLP over HTTP/protobuf (port 4318 is the usual one). Metrics are pushed every 60 s. |
| anything else (`alloy:4318`, `ftp://…`) | Start-up is refused with one Critical log line naming `Telemetry:OtlpEndpoint` and exit code 1 (rule 7). |

The same value is used by both hosts; they appear as services `fresh-orbit-api` and `fresh-orbit-worker`.

## 2. What is sent

Traces:

- ASP.NET Core requests of the Api (the `/health` probes are left out);
- outgoing HttpClient calls (SMS gateway, payment provider);
- Npgsql commands, which are the SQL statements behind EF Core (Npgsql publishes them itself, no extra package);
- each recurring job run (`job <id>`, tag `job.id`; status Error when it throws).

Metrics:

| Instrument | Meaning | Alert idea |
|---|---|---|
| `freshorbit.outbox.pending` | outbox rows waiting for delivery (not processed, not dead-lettered) | above a few hundred for 10 min |
| `freshorbit.outbox.oldest_pending_age` (s) | age of the oldest waiting row: the outbox lag | above 600 s |
| `freshorbit.outbox.dead_letters_24h` | dead letters written in the last 24 h | above 0 (follow-ons were dropped; replay them) |
| `freshorbit.jobs.runs`, `freshorbit.jobs.failures`, `freshorbit.jobs.duration` (s) | recurring job runs, failed runs and run time per `job.id` | any failure of a `*.purge-*` job two days running |
| ASP.NET Core / Kestrel (`http.server.request.duration`, …), HttpClient, `System.Runtime`, Npgsql pool | standard .NET meters | p95 request time, GC heap, pool exhaustion |

The three outbox gauges come from a small sampler in the Worker (three indexed reads every 30 s). It runs only while
telemetry is on, so a server with telemetry off never polls for it.

## 3. Choosing a receiver (free options)

The monitoring profile (Grafana + Loki + Alloy) stores logs only today; it has no metrics or trace store. Options:

1. **A free hosted OTLP endpoint** (for example Grafana Cloud's free tier): set `TELEMETRY_OTLP_ENDPOINT` to its
   OTLP/HTTP URL. If the provider needs an authorization header, put Alloy in between (below); the hosts do not send
   headers.
2. **Alloy as the local receiver**: add an `otelcol.receiver.otlp` block with an HTTP listener on `0.0.0.0:4318` to
   `infra/monitoring/config.alloy` and forward it (`otelcol.exporter.otlphttp`) to the hosted store, then set
   `TELEMETRY_OTLP_ENDPOINT=http://alloy:4318`. This needs the `monitoring` profile on.

Then `docker compose --env-file .env.prod -f docker-compose.prod.yml up -d api worker` to apply the setting.

Memory: with telemetry on, expect roughly 10–20 MB more per host (the SDK, batch buffers and metric aggregation).
The 8 GB server budget (`runbooks/deploy.md` §5) has room; the free demo keeps it off.

## 4. Checking it works

1. `docker compose logs api worker | grep -i "Start-up refused"` prints nothing.
2. In the receiver, look for the services `fresh-orbit-api` and `fresh-orbit-worker`, then the metric
   `freshorbit.outbox.pending` (it appears within about 90 s of the Worker starting).
3. Open any admin page and find its request trace with its SQL spans.

## 5. Retention purges (SEC-13, D334–D336)

Daily recurring jobs (UTC) remove personal data that is no longer needed. Each owner purges only its own tables, with
set-based statements (no rows loaded):

| Job id | Time (UTC) | Removes | Kept |
|---|---|---|---|
| `identity.purge-retention` | 02:20 | `identity.otp_challenges` requested more than 24 h ago; `identity.staff_login_failures` older than 1 day (or than the login-failure window, if that is longer) | newer rows, which the OTP and login limits count |
| `platform.purge-outbox` | 02:30 | `platform.outbox` rows processed more than 7 days ago, and `platform.inbox` rows older than 7 days whose event is not still waiting | waiting and dead-lettered rows (still replayable) |
| `payments.purge-webhook-payloads` | 02:40 | the raw body and headers of PROCESSED, IGNORED and REJECTED webhook events received more than 30 days ago (body becomes `"PURGED"`, headers `{}`) | the row and its summary: provider, event id, type, status, body SHA-256, times, error; RECEIVED and ERROR rows whole |

A failed purge shows as `freshorbit.jobs.failures{job.id=…}` and as an error in the Worker log; it simply runs again the
next night. The periods are the ones the privacy policy states; the owner confirms them (`docs/legal/README.md`).
