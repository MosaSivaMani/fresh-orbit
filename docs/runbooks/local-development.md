# Local development (backend)

Run the Api and Worker against PostgreSQL and Mailpit in Docker, then check everything end to end with the smoke script.

## Prerequisites

.NET SDK per `backend/global.json`, Docker, `bash`, `curl` and `jq`.

## 1. Start PostgreSQL and Mailpit

```bash
docker compose -f infra/compose/docker-compose.dev.yml up -d --wait
```

Postgres listens on 5432, Mailpit SMTP on 1025 and its web UI on http://localhost:8025. When a port is already taken, choose others and also give the stack its own project name, so it cannot touch another `freshorbit` stack's containers or volume:

```bash
POSTGRES_PORT=55432 MAILPIT_SMTP_PORT=51025 MAILPIT_UI_PORT=58025 \
  docker compose -p freshorbit-local -f infra/compose/docker-compose.dev.yml up -d --wait
export ConnectionStrings__FreshOrbit="Host=localhost;Port=55432;Database=freshorbit;Username=freshorbit;Password=freshorbit_dev"
export Notifications__Smtp__Port=51025
```

`appsettings.Development.json` of the Api, Worker and Migrations hosts points at the default ports.

## 2. Migrate, load reference data, create the first admin

```bash
cd backend
dotnet build FreshOrbit.slnx
DOTNET_ENVIRONMENT=Development dotnet run --project src/Hosts/FreshOrbit.Migrations -- \
  --create-admin --email admin@freshorbit.local --name "Local Admin" --password '<at least 12 characters>'
```

The Migrations host applies EF migrations, runs every module's database objects (views, functions, exclusion constraints), loads `backend/data/reference`, then creates the first ADMIN. Run it again after pulling new migrations (without `--create-admin`; a second admin is refused). `--sample` loads `backend/data/sample` when present (never in Production).

## 3. Run the Api and the Worker

```bash
dotnet run --project src/Hosts/FreshOrbit.Api        # http://localhost:5080, API reference at /docs
dotnet run --project src/Hosts/FreshOrbit.Worker     # outbox dispatcher and recurring jobs
```

The Worker must run for anything driven by events: slot generation, stock and shipment updates after an order, payments, invoices, notifications. In Development (`Notifications:Sms:Provider` = `DEVELOPMENT`, set in `appsettings.Development.json`) SMS messages are kept in an in-memory outbox and never logged with their text (SEC-1): read OTP codes from `GET /api/v1/dev/sms?phone=+91…` (newest first). Push messages are written to the log; e-mail goes to Mailpit. Other settings an operator must get right are in `security-configuration.md`.

## 4. Smoke test

With the Api running in Development (the script reads the OTP from `GET /api/v1/dev/sms`):

```bash
API_URL=http://localhost:5080 \
  ADMIN_EMAIL=admin@freshorbit.local ADMIN_PASSWORD='<password>' infra/scripts/smoke.sh
```

It creates a new city with a store, a product and stock, activates the city, signs a new customer in by OTP, places a COD order, packs, dispatches and delivers it, and checks the invoice, the COLLECTED payment, the SALE stock movement and the customer's notifications. Each run uses fresh codes, so it can be repeated; the default rate limits allow a few runs per five minutes (`RateLimiting:Login` 10 / 5 min, `RateLimiting:OtpRequest` 20 / hour and `RateLimiting:OtpVerify` 30 / 10 min per IP; Identity also caps OTP requests at 3 per phone per 10 minutes).

## 5. Stop

```bash
docker compose -f infra/compose/docker-compose.dev.yml down        # add -v to delete the database volume
```

Use the same `-p` project name you started with.
