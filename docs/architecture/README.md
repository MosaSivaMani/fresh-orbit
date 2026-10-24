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
| Identity | `identity` | users, OTP, refresh tokens, staff, roles, permissions | Audit |
| Customers | `customers` | customers, addresses, wishlist | Identity, Catalog, Audit, Locations |
| Locations | `locations` | cities, fulfillment locations, service areas, pincodes, hours, closures | — |
| Catalog | `catalog` | categories, products, images, variants, preparation methods | — |
| Pricing | `pricing` | variant prices, preparation charges, tax rules | Catalog, Locations |
| Inventory | `inventory` | stock items, batches, movements, reservations, cold-chain logs | Catalog, Locations, Ordering (events) |
| Cart | `cart` | carts, cart items (no prices) | Catalog, Pricing, Inventory, Locations, Promotions, Settings, Customers, Ordering, Identity (events) |
| Promotions | `promotions` | coupons, scopes, redemptions, promotions (home offers) | Catalog, Locations, Ordering (events) |
| Ordering | `ordering` | checkout, orders, items, history, adjustments, substitutions, complaints, invoices | Cart, Catalog, Pricing, Inventory, Delivery, Promotions, Locations, Customers, Settings, Payments |
| Payments | `payments` | payments, refunds, webhook events, cash settlements, reconciliation issues, provider adapters | Ordering, Delivery, Identity |
| Delivery | `delivery` | slot templates, slots, riders, shipments, attempts | Locations, Ordering (events) |
| Notifications | `notifications` | templates, messages, device tokens, preferences | Identity, Audit, Settings, Customers, Ordering, Payments, Delivery |
| Reporting | `reporting` | read-only views over other schemas | — |
| Audit | `audit` | append-only audit entries | — |
| Settings | `settings` | business profile, order policies, number series | Audit, Delivery (implements `IDeliveryAttemptPolicy`) |

## Projects

```
backend/
  FreshOrbit.slnx  global.json  Directory.Build.props  Directory.Packages.props
  src/Hosts/FreshOrbit.Api            HTTP host: auth, Problem Details, Swagger, maps every module's endpoints
  src/Hosts/FreshOrbit.Worker         Hangfire server, outbox dispatcher, recurring jobs
  src/Hosts/FreshOrbit.Migrations     EF migrations, module database objects, reference data, --create-admin, sample data; then exits
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
| Catalog browse shows bestsellers, freshness and an offer line owned by Ordering, Inventory, Promotions | Interfaces in `Catalog.Public` (`IBestsellerSource`, `IVariantFreshnessSource`, `IProductOfferSource`) implemented by those modules; Catalog asks every registered one and shows `null`/empty when none (CAT-R24–R26) |
| City readiness, retirement and next-slot need modules that depend on Locations | Interfaces in `Locations.Public` (`ICityActivationCheck`, `ICityRetirementCheck`, `INextSlotProvider`) implemented by Pricing, Inventory, Delivery, Ordering |
| Delivery needs `max_delivery_attempts` from the order policy but does not use Settings | `IDeliveryAttemptPolicy` in `Delivery.Public`, implemented by Settings (same pattern as D4) |
| Customer deletion must be refused while orders are open | `ICustomerDeletionCheck` in `Customers.Public`, implemented by Ordering |
| Events another module consumes (D30) | Sealed records in `<Module>.Public/Events`; the consumer's `IEventHandler<T>` adapter lives in its `.Application/Handlers` and calls the module's own service (no SaveChanges; the outbox dispatcher saves) |
| Images | SkiaSharp (MIT), WebP at 320/640/1280 px, MinIO first, cleanup job for orphans |

## Cross-cutting

| Concern | Choice |
|---|---|
| Customer auth | Phone + OTP (SMS through `ISmsSender`: the DLT sender with the Fast2SMS adapter in Production, E2E-1; the dev sender keeps the code in an in-memory outbox), JWT access token (15 min) + rotating refresh token |
| Staff auth | Email + password (ASP.NET Core `PasswordHasher`), same JWT; permissions in claims; optional location scope |
| Authorization | Policies per permission code (`orders.pack`); endpoints declare `.RequirePermission("...")` |
| Validation | FluentValidation, field-keyed 422 |
| Events | Aggregate raises `DomainEvent`; `SaveChanges` writes `platform.outbox` in the same transaction; Worker dispatches to `IEventHandler<T>`; `platform.inbox` makes each handler run once; failures go to `platform.dead_letters` |
| Jobs | Hangfire with PostgreSQL storage, in the Worker |
| Files | `IFileStorage` with MinIO (S3 API) implementation |
| Payments | `IPaymentProvider`: `CashOnDelivery` (launch), `Fake` (tests), gateway adapter later |
| Logging | Serilog to console (JSON in production) + OpenTelemetry traces |
| Rate limits | ASP.NET Core rate limiter (Api host): OTP and login strict, checkout moderate, `refresh` and `search` per IP (API §1.9) |

## Key flows

See `docs/database/README.md` §4 for transaction boundaries (place order, payment callback, weigh and pack,
cancel, refund, receive stock, expire reservations, issue invoice).

Order status: `PENDING_PAYMENT → CONFIRMED → PREPARING → PACKED → OUT_FOR_DELIVERY → DELIVERED`, with
`PAYMENT_FAILED`, `CANCELLED`, `DELIVERY_FAILED`, `RETURNED`. COD orders start at `CONFIRMED`.
