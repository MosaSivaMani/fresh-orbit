# Architecture

## Shape

```
 Website (Next.js)   Mobile (Flutter)   Admin (React + Vite)
          \               |               /
           \______ HTTPS / REST JSON ____/
                          |
        ┌─────────────────▼──────────────────┐
        │   FreshOrbit backend (one solution) │
        │   Hosts: Api | Worker | Migrations  │
        │   15 modules, one DbContext         │
        └───────┬──────────────┬─────────────┘
           PostgreSQL 17     MinIO (images)    SMTP / SMS / payment gateway via adapters
```

Deployed with Docker Compose behind Caddy on one server (Oracle Always Free first, Hostinger VPS later).

## Modules

| Module | Schema | Owns | Uses (.Public) |
|---|---|---|---|
| Identity | `identity` | users, OTP, refresh tokens, staff, roles, permissions | — |
| Customers | `customers` | customers, addresses, wishlist | Identity, Catalog |
| Locations | `locations` | cities, fulfillment locations, service areas, pincodes, hours, closures | — |
| Catalog | `catalog` | categories, products, images, variants, preparation methods | — |
| Pricing | `pricing` | variant prices, preparation charges, tax rules | Catalog, Locations |
| Inventory | `inventory` | stock items, batches, movements, reservations, cold-chain logs | Catalog, Locations |
| Cart | `cart` | carts, cart items (no prices) | Catalog, Pricing, Inventory, Locations, Promotions, Settings, Customers, Ordering |
| Promotions | `promotions` | coupons, scopes, redemptions, promotions (home offers) | Catalog, Locations |
| Ordering | `ordering` | checkout, orders, items, history, adjustments, substitutions, complaints, invoices | Cart, Catalog, Pricing, Inventory, Delivery, Promotions, Locations, Customers, Settings, Payments |
| Payments | `payments` | payments, refunds, webhook events, cash settlements, reconciliation issues, provider adapters | Ordering, Delivery, Identity |
| Delivery | `delivery` | slot templates, slots, riders, shipments, attempts | Locations |
| Notifications | `notifications` | templates, messages, device tokens, preferences | Identity |
| Reporting | `reporting` | read-only views over other schemas | — |
| Audit | `audit` | append-only audit entries | — |
| Settings | `settings` | business profile, order policies, number series | — |

## Projects

```
backend/
  FreshOrbit.slnx  global.json  Directory.Build.props  Directory.Packages.props
  src/Hosts/FreshOrbit.Api            HTTP host: auth, Problem Details, Swagger, maps every module's endpoints
  src/Hosts/FreshOrbit.Worker         Hangfire server, outbox dispatcher, recurring jobs
  src/Hosts/FreshOrbit.Migrations     EF migrations + reference/sample data loader, then exits
  src/SharedKernel/FreshOrbit.SharedKernel                 Money, Weight, AggregateRoot, DomainEvent, Result, errors, EnumCodes, IClock
  src/SharedKernel/FreshOrbit.SharedKernel.EntityFramework FreshOrbitDbContext, IModuleModel, conventions, outbox/inbox
  src/SharedKernel/FreshOrbit.SharedKernel.Web             Problem Details, exception handler, If-Match, Idempotency-Key, paging, current user
  src/Modules/<Module>/<Module>.{Domain,Application,Infrastructure,Public}
  data/reference  data/sample
  tests/FreshOrbit.Testing  tests/Modules/<Module>.Tests  tests/FreshOrbit.Api.Tests  tests/FreshOrbit.ArchitectureTests
```

### Inside a module

```
<Module>.Domain/<Area>/            Aggregate root, I<Aggregate>Repository, Entities/, Values/, Enums/, Events/
<Module>.Domain/Shared/            <Module>Errors (codes)
<Module>.Application/<Area>/       Requests/ (+Validator), Responses/, I<Aggregate>Service, <Aggregate>Service, I<Aggregate>Reads, Mappings
<Module>.Application/Handlers/     Handlers of other modules' events
<Module>.Infrastructure/           <Module>Module.cs (DI + endpoints), Endpoints/, Persistence/ (ModuleModel, Configurations, Repositories), Reads/, Jobs/, Public/ (implementation of the Public API)
<Module>.Public/                   I<Module>Api + narrow sealed records. Nothing else.
```

## Dependency exceptions (decided)

| Need | Solution |
|---|---|
| Catalog browse shows price and stock | Read-only SQL view `catalog.storefront_offers` joining pricing and inventory tables; no project reference |
| City readiness, retirement and next-slot need modules that depend on Locations | Interfaces in `Locations.Public` (`ICityActivationCheck`, `ICityRetirementCheck`, `INextSlotProvider`) implemented by Pricing, Inventory, Delivery, Ordering |
| Images | SkiaSharp (MIT), WebP at 320/640/1280 px, MinIO first, cleanup job for orphans |

## Cross-cutting

| Concern | Choice |
|---|---|
| Customer auth | Phone + OTP (SMS through `ISmsSender`; dev sender logs the code), JWT access token (15 min) + rotating refresh token |
| Staff auth | Email + password (ASP.NET Core `PasswordHasher`), same JWT; permissions in claims; optional location scope |
| Authorization | Policies per permission code (`orders.pack`); endpoints declare `.RequirePermission("...")` |
| Validation | FluentValidation, field-keyed 422 |
| Events | Aggregate raises `DomainEvent`; `SaveChanges` writes `platform.outbox` in the same transaction; Worker dispatches to `IEventHandler<T>`; `platform.inbox` makes each handler run once; failures go to `platform.dead_letters` |
| Jobs | Hangfire with PostgreSQL storage, in the Worker |
| Files | `IFileStorage` with MinIO (S3 API) implementation |
| Payments | `IPaymentProvider`: `CashOnDelivery` (launch), `Fake` (tests), gateway adapter later |
| Logging | Serilog to console (JSON in production) + OpenTelemetry traces |
| Rate limits | ASP.NET Core rate limiter: OTP and login strict, checkout moderate |

## Key flows

See `docs/database/README.md` §4 for transaction boundaries (place order, payment callback, weigh and pack,
cancel, refund, receive stock, expire reservations, issue invoice).

Order status: `PENDING_PAYMENT → CONFIRMED → PREPARING → PACKED → OUT_FOR_DELIVERY → DELIVERED`, with
`PAYMENT_FAILED`, `CANCELLED`, `DELIVERY_FAILED`, `RETURNED`. COD orders start at `CONFIRMED`.
