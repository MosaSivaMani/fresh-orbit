# Backend gap audit (2026-10-29)

Scope: `backend/src` (all 15 modules, SharedKernel, hosts Api / Worker / Migrations / Hosting) and `backend/tests`.
This was a read-only review against `main` at `5e0907d`. Nothing was built or run (rule 18). Every row was checked in the code; paths are relative to `backend/src/` unless they start with `backend/`, `infra/` or `docs/`.

## Summary

Most of the core is in good shape. These were checked and found correct:
- Checkout reserves stock, slot and coupon in one transaction, with xmin and a 3× retry.
- Idempotency keys work on place order, cancel, complaint and refund.
- Every admin group or route has `RequirePermission`, and location scope is enforced.
- Page size is capped at 100.
- The weight cap is applied at pack.
- Invoice numbers are gapless.
- Refunds have maker-checker approval and a cap at what was paid.
- Webhooks are verified with HMAC and limited in size.
- PII is masked in SMS logs.
- There are concurrency tests for stock, slots, refunds and webhooks.

The gaps that would hurt at launch fall into four groups:

1. **The outbox gives up too fast and nobody can replay it.**
   - A handler that fails 5 times, 5 s apart, is dead-lettered after about 25 s.
   - Any short outage (a deploy, a DB restart, the SMS gateway down) therefore permanently loses stock and slot releases, refunds and invoices.
   - There is no admin view or replay for dead letters.
2. **Configuration that is missing is either found too late or quietly replaced.**
   - A missing GSTIN dead-letters every delivery.
   - Email falls back to a logger.
   - Push is a logger in every environment, and its messages are marked SENT.
3. **Some money paths have no refund or no settlement.**
   - Goodwill on a COD order whose cash was already collected creates no refund and no issue.
   - A same-day cash settlement leaves later collections unsettleable.
   - A complaint refund with method ORIGINAL on COD is silently dropped.
4. **Operations has little to work with.**
   - No metrics or tracing (the architecture doc promises OpenTelemetry).
   - No Worker health check.
   - No retention purges for the platform and identity tables.

**Counts:** Blocker 2 · High 6 · Medium 30 · Low 51 (89 gaps), plus 22 missing features.

## Known open items (next-steps §5, §8, §9): status today

| Item | Status | Evidence |
|---|---|---|
| §5 SEC-3: global per-IP limit and per-user cart limit | **Open** | No `GlobalLimiter` (`Hosts/FreshOrbit.Api/RateLimitingSetup.cs:70-101`); the cart uses the per-IP `Public` policy (`Modules/Cart/Cart.Infrastructure/Endpoints/CartEndpoints.cs:26`) |
| §5 SEC-6: webhook limit counting rejections per IP | **Open** | The webhook route has no rate limit (`Modules/Payments/Payments.Infrastructure/Endpoints/PaymentsEndpoints.cs:63-71`) |
| §5 SEC-5 guard for complaint photos | Done | Size, type and width rules in `Modules/Ordering/Ordering.Application/Complaints/ComplaintService.cs:129-135`; request limit at `OrderingEndpoints.cs:89` |
| §5 Purge of `identity.staff_login_failures` (SEC-13) and of 72-month snapshots | **Open** | No `ExecuteDelete` or purge job outside sample data; the only purge jobs are complaint photos and orphan images |
| §5 Break-glass admin reset | **Open** | Nothing in `Hosts/FreshOrbit.Migrations/MigrationRunner.cs` beyond `--create-admin`, which is refused once an admin exists |
| §5 Reference loader does not add new permissions to existing system roles | **Open** | Only the ADMIN role gets `GrantMissing` (`Modules/Identity/Identity.Infrastructure/Seeding/IdentityReferenceDataLoader.cs:~103-108`) |
| §5 Audit `actorName` is null | **Open** | `ActorName` is never set outside the Audit domain mapping (grep: no producer) |
| §5 Admin refresh tokens in httpOnly cookies | **Open** | No cookie handling in Identity or SharedKernel.Web |
| §8 Wishlist with prices, `isWishlisted`, `/home` banners | Done | `Modules/Customers/Customers.Application/Wishlist/WishlistService.cs:18-21`; `Catalog.Application/Storefront/StorefrontContracts.cs:204` |
| §8 `QuantityRules` in both Catalog and Cart | **Open** | `Modules/Cart/Cart.Domain/Carts/ShoppingCart.cs:17` and `Modules/Catalog/Catalog.Domain/Products/VariantRules.cs` |
| §8 Missing GSTIN: delivery retries then dead-letters | **Open (Blocker, BE-1)** | `Modules/Ordering/Ordering.Application/Events/OrderEventService.cs:84-88` |
| §9 E2E-1 to E2E-7 | Done in code; E2E-3 key ring unencrypted and not backed up (BE-86); E2E-7 not yet run on a real server (ops) | `research/deploy-gaps-ops-2026-10.md` |

## Gaps

| ID | Severity | Module | Gap | Evidence (`path:line`) | Suggested fix |
|---|---|---|---|---|---|
| BE-1 | Blocker | Ordering / Settings | A business profile with no GSTIN is found only at delivery. `ShipmentDelivered` throws `CONFIGURATION_MISSING`, is retried 5× and is dead-lettered. The goods are delivered, but the order stays OUT_FOR_DELIVERY with no invoice, no `OrderDelivered` (COD follow-up, complaint window) and no alert | `Modules/Ordering/Ordering.Application/Events/OrderEventService.cs:84-88`; `SharedKernel/FreshOrbit.SharedKernel.EntityFramework/Outbox/OutboxOptions.cs:12` | Refuse checkout (and fail readiness) while the profile lacks a GSTIN or legal name; test it |
| BE-2 | Blocker | SharedKernel (outbox) | No backoff between retries. A failing row is retried on every poll (5 s) and dead-lettered after 5 attempts, so any outage of about 25 s or more permanently drops the follow-ons: stock and slot release, coupon reversal, refunds, invoice, notifications. Nothing replays dead letters | `SharedKernel/FreshOrbit.SharedKernel.EntityFramework/Outbox/OutboxDispatcher.cs:53,84-94`; `Outbox/OutboxOptions.cs:12,15`; `Hosts/FreshOrbit.Hosting/BackgroundJobs/OutboxDispatcherService.cs:34-38` | Add `next_attempt_at` with exponential backoff (up to hours), raise the max attempts, alert on a dead letter, and add an admin list/replay of `platform.dead_letters` |
| BE-3 | High | Notifications | Push always uses `LoggingPushSender` (no FCM adapter), in Production too, and the dispatcher marks those messages SENT. App users get no order updates by push, and the data says they did | `Modules/Notifications/Notifications.Infrastructure/NotificationsModule.cs:86`; `Senders/Senders.cs:71-79` | Refuse the logging sender outside Development/Testing/Demo (as SMS does), or do not queue PUSH until FCM exists |
| BE-4 | High | Notifications | With no `Smtp:Host`, email silently uses `LoggingEmailSender` in every environment (breaks rule 7), and that sender logs the full recipient address (PII) | `NotificationsModule.cs:87-90`; `Senders/Senders.cs:37,43-44` | Refuse start-up without SMTP outside Development/Testing; mask the address |
| BE-5 | High | Payments | Goodwill on a delivered COD order whose cash was already collected sets a refund due on the order, but Payments refunds only ONLINE payments. No refund and no reconciliation issue are created, so the customer is never paid | `Modules/Payments/Payments.Application/Handlers/OrderPaymentHandlers.cs:75-84,216-217`; `Modules/Ordering/Ordering.Domain/Orders/Order.Pack.cs:105-113` | For a COLLECTED delivery payment, open `REFUND_METHOD_REQUIRED` (as complaints do) or a REQUESTED CASH/UPI refund |
| BE-6 | High | Payments | A settlement for today is allowed. Later collections that same day can never be settled, because the date is unique per rider and the collection query is limited to that date's window, so UNSETTLED_CASH stays open | `Modules/Payments/Payments.Application/Settlements/CashServices.cs:67-75`; `Infrastructure/Persistence/Repositories/PaymentsRepositories.cs:61-66` | Refuse today until the rider's shift ends, or allow a top-up settlement |
| BE-7 | High | Payments | A complaint refund with method ORIGINAL on a COD payment (no provider refunds) returns silently: no refund and no issue | `Modules/Payments/Payments.Application/Handlers/OrderPaymentHandlers.cs:205-208` | Open `REFUND_METHOD_REQUIRED` instead of returning |
| BE-8 | High | Platform / Ops | No metrics and no tracing. The architecture doc promises OpenTelemetry, but no OpenTelemetry package is referenced. Queue depth, dead letters, job failures and latency are invisible | `docs/architecture/README.md:93`; `backend/Directory.Packages.props` (no OpenTelemetry) | Add OpenTelemetry (free) with the OTLP exporter, plus meters for outbox lag, dead letters, notification failures and checkout retries |
| BE-9 | Medium | Platform / Ops | Health is one `/health` with a DB check only. There is no liveness/readiness split. The Worker has no health endpoint, so a stuck dispatcher or Hangfire is not detected | `Hosts/FreshOrbit.Api/Program.cs:24,50`; `Hosts/FreshOrbit.Worker/Program.cs:6-16` | Split `/health/live` and `/health/ready`; give the Worker a heartbeat check (last outbox batch, Hangfire server) |
| BE-10 | Medium | SharedKernel (idempotency) | If the process dies after a key is claimed and before release, the row stays with status NULL for 24 h, and every retry gets 409 `IDEMPOTENCY_KEY_IN_PROGRESS` | `SharedKernel/FreshOrbit.SharedKernel.Web/Idempotency/IdempotencyFilter.cs:71-91,40` | Treat a claim older than N minutes as abandoned and allow it to be re-claimed |
| BE-11 | Medium | SharedKernel / platform | No retention purge for `platform.outbox` (processed), `platform.inbox`, `platform.idempotency_keys` (only the same key is deleted, on reuse), `platform.dead_letters` or Hangfire history; the tables grow without limit | `IdempotencyFilter.cs:68-70`; no `ExecuteDelete` in non-seeding code | Add a daily purge job with retention as configuration |
| BE-12 | Medium | Identity | No purge for `otp_challenges`, expired or revoked `refresh_tokens`, or `staff_login_failures` (SEC-13, a 1-day retention is documented) | `docs/database/README.md` §3 identity; no purge job in `Modules/Identity` | Add a daily Identity retention job |
| BE-13 | Medium | Cart | Abandoned carts are marked ABANDONED but never deleted (customer and pincode data kept for ever) | `Modules/Cart/Cart.Infrastructure/Jobs/AbandonCartsJob.cs:26-31` | Delete carts abandoned more than N days ago |
| BE-14 | Medium | Payments | Every invalid-signature webhook stores a REJECTED row of up to 4 KB, and the anonymous route has no rate limit (SEC-6), so the table can be flooded | `Modules/Payments/Payments.Infrastructure/Endpoints/PaymentsEndpoints.cs:63-71`; `Payments.Application/Webhooks/WebhookService.cs:44-51` | Add `RequireRateLimiting`, plus a per-IP limiter counting only rejections; keep one REJECTED row per hash |
| BE-15 | Medium | Api host | No global per-IP limit, and no per-user limit on cart writes (SEC-3 remainder). A signed-in user can hammer every route that has no policy | `Hosts/FreshOrbit.Api/RateLimitingSetup.cs:70-101`; `Modules/Cart/Cart.Infrastructure/Endpoints/CartEndpoints.cs:26` | Add `options.GlobalLimiter` per IP and a per-user cart policy |
| BE-16 | Medium | Api host | No global request body limit. JSON endpoints accept Kestrel's default 30 MB, and idempotency buffers the body | `Hosts/FreshOrbit.Api/Program.cs` (no `MaxRequestBodySize`); `IdempotencyFilter.cs:232-241` | Set Kestrel `MaxRequestBodySize` to about 1 MB; keep the per-route larger limits for uploads |
| BE-17 | Medium | Notifications | The dispatcher makes SMS, email and push calls while holding the `FOR UPDATE` batch transaction. If the final save fails, every message in the batch is sent again, and a `DomainException` rolls back the whole batch on every run | `Modules/Notifications/Notifications.Application/Sending/MessageDispatcher.cs:25-44,91` | Claim the batch (SENDING) and commit; send outside the transaction; commit the results per message |
| BE-18 | Medium | Notifications | Rule 5: the dispatcher queries the DB inside its per-message loop (templates, device tokens) | `MessageDispatcher.cs:53,61` | Load templates and tokens for the whole batch before the loop |
| BE-19 | Medium | Payments | The hourly reconciliation reads (delivered-not-collected, unsettled cash, unresolved, open issues) have no `Take()`, and the payment reads include refunds | `Modules/Payments/Payments.Infrastructure/Persistence/Repositories/PaymentsRepositories.cs:85-94` (and `:121-124`, `:144-145` per the Payments read) | Order and batch-limit like the other reads (`:75`, `:82`) |
| BE-20 | Medium | Payments | Reconciliation auto-resolves OPEN WEBHOOK_ERROR / REFUND_STUCK issues whose subject simply fell outside the 500-row cap of this run | `Modules/Payments/Payments.Application/Reconciliation/ReconciliationService.cs:51,55,156-161,202-212` | Auto-resolve only subjects re-checked in this run |
| BE-21 | Medium | Payments | One provider exception aborts the whole reconciliation run (the only save is at the end) | `ReconciliationService.cs:75,118,166` | Catch per call, count failures, continue |
| BE-22 | Medium | Payments | The Fake gateway is refused only when the environment is exactly `Production`, so a Staging host with FAKE enabled accepts fake captures | `Modules/Payments/Payments.Infrastructure/Providers/ProviderCatalog.cs:40` | Allow FAKE only in Development/Testing/Demo (an allow-list) |
| BE-23 | Medium | Ordering | Rule 7: a missing or unknown `cancelAllowedUntilStatus` silently becomes CONFIRMED | `Modules/Ordering/Ordering.Application/Orders/OrderQueryService.cs:30-32` | Throw `CONFIGURATION_MISSING` |
| BE-24 | Medium | Ordering | No sweep for CONFIRMED or PREPARING orders whose slot ended without packing. Their stock reservations and slot capacity are held for ever, and the customer hears nothing | `Modules/Ordering/Ordering.Infrastructure/Jobs/OrderingJobs.cs:25-27` (only unpaid and substitution expiry); reservations expire only via `OrderPaymentFailed` (`Inventory.Application/Handlers/OrderStockHandlers.cs:38`) | Add a job raising an overdue issue or alert (and an optional auto-cancel by policy) |
| BE-25 | Medium | Ordering / Inventory / Promotions | On `OrderReturned` only Payments reacts. The stock consumed at pack is neither restocked nor written off, and the coupon redemption is not reversed | `Modules/Payments/Payments.Infrastructure/PaymentsModule.cs:87` (the only `OrderReturned` handler) | Add an Inventory RETURN/WASTE movement handler and a Promotions reversal |
| BE-26 | Medium | Ordering / Inventory | A staff cancel of a PACKED order leaves the stock consumed, with no restock or write-off step, so on-hand drifts from the shelf | `Modules/Inventory/Inventory.Application/Handlers/OrderStockHandlers.cs:41` | Add an admin "return to stock / write off" action linked to the order |
| BE-27 | Medium | Ordering | Packed quantity has no upper bound (only `< 0`). Money is capped, but a typo such as 10 000 g consumes 10× stock through the SALE movement | `Modules/Ordering/Ordering.Domain/Orders/Order.Pack.cs:179-186` | Refuse more than a set multiple of `MaxChargeableQuantity` |
| BE-28 | Medium | Ordering | The complaint refund cap does not subtract earlier complaint refunds, goodwill or refund due, so several complaints can exceed the order total (COD refunds are manual) | `Modules/Ordering/Ordering.Domain/Complaints/Complaint.cs:254-260` | Subtract what was already refunded or given on the order |
| BE-29 | Medium | Ordering | Accepting or rejecting a substitution is a retryable write (accept reserves stock) with no Idempotency-Key and no If-Match | `Modules/Ordering/Ordering.Infrastructure/Endpoints/OrderingEndpoints.cs:62-65` | Add `.WithIdempotency(required: true)` |
| BE-30 | Medium | Ordering | The invoice is always split CGST/SGST. There is no place-of-supply check (GSTIN state against the address state) and no IGST | `Modules/Ordering/Ordering.Domain/Invoices/Invoice.cs:130-131` | Compare the GSTIN state code with the address `state_code`; refuse or issue IGST |
| BE-31 | Medium | Ordering | No bill-of-supply path: nil-rated fresh fish still gets a "tax invoice" (the CA question in next-steps §8) | `Invoice.cs` (no document type); grep `BillOfSupply`: none | Record the CA's answer as a decision, then add a document type per invoice |
| BE-32 | Medium | Identity | The reference loader grants new permissions only to ADMIN, so an existing FINANCE role never gets `tax.manage` | `Modules/Identity/Identity.Infrastructure/Seeding/IdentityReferenceDataLoader.cs:96-108`; `backend/data/reference/identity/roles.json:23` | Grant permissions that are missing for every `is_system` role (add only, never remove) |
| BE-33 | Medium | Identity | No break-glass recovery for a locked or forgotten last admin (`--create-admin` is refused once an admin exists) | `Hosts/FreshOrbit.Migrations/MigrationRunner.cs:67-83` | Add `--reset-admin` to the Migrations host (it needs server shell access), and audit it |
| BE-34 | Medium | Tests / Security | The authorization test covers only admin writes and checks only "authenticated", not "has a staff permission". No test proves every `/api/v1/admin` GET needs a permission | `backend/tests/Modules/Audit.Tests/Integration/AdminWriteCoverageTests.cs:38-39,66-68` | Add an endpoint-sweep test: every `/admin` route needs a `perm` or `actor_type=STAFF` policy |
| BE-35 | Medium | Tests | No test for reconciliation issue kinds DELIVERED_NOT_COLLECTED, UNSETTLED_CASH, SETTLEMENT_AGED, REFUND_STUCK or auto-resolve, and none for the `RefundProcessor` worker path | `backend/tests/Modules/Payments.Tests` (grep: no match) | Add one integration test per issue kind, plus refund re-entry |
| BE-36 | Medium | Tests | No test for the missing-GSTIN path at delivery (BE-1) | `backend/tests/Modules/Ordering.Tests` (grep `GSTIN`: none) | Add a test once BE-1 is fixed |
| BE-37 | Medium | Tests | No end-to-end test of the expire-unpaid and expire-substitutions jobs; only the domain methods are tested | `Modules/Ordering/Ordering.Application/Events/OrderExpiryService.cs:16-60` | Add integration tests that run the jobs |
| BE-38 | Medium | Tests | No test that `OrderReturned` causes its follow-ons | `backend/tests/Modules/*` (grep `OrderReturned`: Payments only) | Add a test with BE-25 |
| BE-39 | Low | Ordering | One `CONCURRENT_SAVE` in the expiry loop throws and skips the rest of the batch until the next minute | `OrderExpiryService.cs:20-35` | Catch and log per order |
| BE-40 | Low | Ordering | Reorder has no Idempotency-Key, so a double tap may add the lines twice | `OrderingEndpoints.cs:104-105` | Add an optional key |
| BE-41 | Low | Ordering | Customer cancel takes a key but no If-Match (rule 9) | `OrderingEndpoints.cs:59-61` | Accept an optional If-Match, or log a decision for the exception |
| BE-42 | Low | Ordering | Wrong code: a pack blocked by a proposed substitution returns `INVALID_STATUS_TRANSITION` | `Order.Pack.cs:29` | Use `SUBSTITUTION_PENDING` |
| BE-43 | Low | Ordering | Wrong code: goodwill outside PACKED..DELIVERED returns `INVALID_STATUS_TRANSITION` | `Order.Pack.cs:111-113` | Use `GOODWILL_NOT_ALLOWED` |
| BE-44 | Low | Ordering | Wrong field: the missing-time-zone error names `deliveryAddressId`, but the request field is `addressId` | `Modules/Ordering/Ordering.Domain/Orders/Order.cs:236`; `CheckoutService.cs:235` | Use `addressId` |
| BE-45 | Low | Ordering | The complaint decision note is not length-checked. The column holds 500, so a longer note fails as a DB error (500), not a 422 | `Complaint.cs:193`; `Ordering.Infrastructure/Persistence/OrderingConfigurations.cs:143` | Validate at most 500 characters |
| BE-46 | Low | Ordering | Rule 12: the wrapper message "Some packed quantities are invalid." names no field | `Order.Pack.cs:196` | Name the field (`lines`) |
| BE-47 | Low | Ordering | Rule 12: "Some items are out of stock." names no field | `Modules/Ordering/Ordering.Application/Checkout/CheckoutService.cs:104` | Name `items` |
| BE-48 | Low | Ordering | Rule 12: "Some items cannot be ordered." names no field | `CheckoutService.cs:219` | Name `items` |
| BE-49 | Low | Ordering | The GSTIN `CONFIGURATION_MISSING` error has no field key | `OrderEventService.cs:87` | Add field `gstin` |
| BE-50 | Low | Ordering | Customer order and checkout routes have no endpoint auth policy; they rely on `CustomerOf(actor)` in the handler, so OpenAPI shows them as anonymous | `OrderingEndpoints.cs:40-46,216-217` | Add a customer `RequireAuthorization` policy on the group (as `/me` does) |
| BE-51 | Low | Payments | A cash collection on a shipment with no rider records a null rider, so it can never be settled | `Payments.Application/Settlements/CashServices.cs:40`; `PaymentsRepositories.cs:63` | Refuse the collection when there is no rider |
| BE-52 | Low | Payments | Assigning a non-cash payment to a settlement throws `InvalidOperationException` (500), not a typed error | `Modules/Payments/Payments.Domain/Payments/Payment.cs:305-307` | Throw a `ConflictException` with a code |
| BE-53 | Low | Payments | Rule 12: the webhook "cannot be read" message appends the raw exception text | `Payments.Application/Webhooks/WebhookService.cs:62` | Give a fixed sentence and log the exception |
| BE-54 | Low | Payments | Webhook replay protection is by event id only (no signed timestamp with a tolerance); it matters when a real gateway lands | `WebhookService.cs:67` | Verify the timestamp in the gateway adapter |
| BE-55 | Low | Delivery | Deliver, attempts, reattempt and return have no Idempotency-Key; a retried deliver gets 412 instead of the first result | `Modules/Delivery/Delivery.Infrastructure/Endpoints/DeliveryEndpoints.cs:63-68` | Add `.WithIdempotency()` to deliver and attempts |
| BE-56 | Low | Delivery | `NotFoundException` without a code (the module uses codes elsewhere; Payments uses `SHIPMENT_NOT_FOUND`) | `DeliveryEndpoints.cs:214` | Pass `SHIPMENT_NOT_FOUND` |
| BE-57 | Low | Delivery | The ETag is set by hand (`response.Headers.ETag = ...`) instead of the shared `SetETag` helper | `DeliveryEndpoints.cs:216` | Use `IfMatch.SetETag` |
| BE-58 | Low | Delivery | The delivery-code guard throws `ArgumentException` (500 if ever hit) | `Modules/Delivery/Delivery.Domain/Shipments/Shipment.cs:217-220` | Use a typed domain error |
| BE-59 | Low | Delivery | Delivery codes are stored in plain text (the compare is constant-time) | `Shipment.cs:108,226` | Optional: hash them |
| BE-60 | Low | Delivery | The 5-try delivery-code limit resets on every reattempt | `Shipment.cs:56,322` | Keep a total limit per shipment |
| BE-61 | Low | Notifications | The email `FromAddress` silently defaults to `no-reply@freshorbit.local` | `Senders/Senders.cs:26` | Require it outside Development |
| BE-62 | Low | Notifications | The send job runs once a minute, so order SMS and email wait up to about 65 s (outbox plus cron) | `Modules/Notifications/Notifications.Infrastructure/Senders/SendNotificationsJob.cs:28` | Run it as a loop service like the outbox, or kick it from the handler |
| BE-63 | Low | Promotions | Coupon code uniqueness is checked before insert. A race gives an unmapped `DbUpdateException` (500) | `Modules/Promotions/Promotions.Application/Coupons/CouponAdminService.cs:73`; `SharedKernel/FreshOrbit.SharedKernel.Web/Errors/FreshOrbitExceptionHandler.cs:61` | Map unique violation 23505 to 409 DUPLICATE |
| BE-64 | Low | Identity | Staff email uniqueness: the same race gives a 500 | `Modules/Identity/Identity.Application/Staff/StaffService.cs:39` | Map 23505 to 409 |
| BE-65 | Low | Identity | Role code uniqueness: the same race gives a 500 | `Modules/Identity/Identity.Application/Roles/RoleService.cs:66` | Map 23505 to 409 |
| BE-66 | Low | Identity | Two concurrent first logins for one phone both call `RegisterCustomer`; the loser gets a 500 | `Modules/Identity/Identity.Application/Auth/OtpService.cs:129-134` | Catch the unique violation and reload the user |
| BE-67 | Low | Identity | OTP per-phone limits are read-then-insert, so parallel requests can exceed the resend gap and the 10-minute limit | `OtpService.cs:184-197` | Use an advisory lock per phone, or a conditional insert |
| BE-68 | Low | Identity | Permissions live in the JWT, so a removed role or a disabled staff member keeps access for up to 15 minutes | `SharedKernel/FreshOrbit.SharedKernel.Web/Auth/AuthenticationExtensions.cs:56-63` | Accept and document it, or check the staff `revision` on each admin request |
| BE-69 | Low | Identity | Different messages for disabled and deleted accounts allow account-state discovery | `OtpService.cs:137,144` | Use one message |
| BE-70 | Low | Customers | `POST /me/addresses/{id}/default` takes no If-Match, while replacing an address requires one | `Modules/Customers/Customers.Infrastructure/Endpoints/CustomersEndpoints.cs:73` | Require If-Match, or log a decision |
| BE-71 | Low | Customers | Account deletion anonymises the Identity user inside the Customers command (one save, so it is atomic, but it breaks the rule 5 pattern) | `Modules/Customers/Customers.Application/Profiles/ProfileService.cs:115-116` | Move it to a `CustomerDeleted` handler, or record the exception |
| BE-72 | Low | Customers | Rule 12: the deletion conflict message joins a list of reasons and has no field key | `ProfileService.cs:91-93` | Return field-keyed errors like the refusal branch (`:82-87`) |
| BE-73 | Low | Customers | The `/admin/customers` group has no base permission (each route adds its own) | `CustomersEndpoints.cs:119` | Add `RequirePermission(customers.view)` on the group |
| BE-74 | Low | Settings | The order-policy and number-series lists are unpaged (small, bounded tables) | `Modules/Settings/Settings.Infrastructure/Endpoints/SettingsEndpoints.cs:46,58` | Document them as bounded |
| BE-75 | Low | Catalog | Product image upload has no `RequestSizeLimit` (complaint photos do) and no `Upload` rate-limit policy; the size is checked only after reading | `Modules/Catalog/Catalog.Infrastructure/Endpoints/CatalogEndpoints.cs:266-279` | Add `RequestSizeLimitAttribute(ImageLimits.MaxBytes + 64 KB)` and `RateLimitPolicies.Upload` |
| BE-76 | Low | Cart / Catalog | `QuantityRules` is duplicated (next-steps §8) | `Modules/Cart/Cart.Domain/Carts/ShoppingCart.cs:17`; `Modules/Catalog/Catalog.Domain/Products/VariantRules.cs` | Move it to SharedKernel next to `LinePricing` |
| BE-77 | Low | Inventory | No Inventory-side safety job for ACTIVE reservations past `expires_at`; it relies entirely on Ordering's unpaid job | `Modules/Inventory/Inventory.Infrastructure/Jobs/InventoryRecurringJobs.cs:12-15` | Add a reservation-expiry sweep as a safety net (rule 6 still applies) |
| BE-78 | Low | Inventory | Batch expiry does one load and one save per stock item in a loop (documented one-transaction-per-item design) | `Modules/Inventory/Inventory.Application/Jobs/InventoryJobs.cs:37-48` | Record the exception to rule 5 in `docs/decisions/log.md` |
| BE-79 | Low | Audit | `actorName` is always null (next-steps §5) | `Modules/Audit/Audit.Domain/Entries/AuditEntry.cs:24,58`; no producer sets it | Fill it from the staff `full_name` claim |
| BE-80 | Low | SharedKernel (paging) | `page` has no upper bound; `(Page - 1) * PageSize` can overflow and make EF throw (500); deep OFFSET pages are slow | `SharedKernel/FreshOrbit.SharedKernel.Web/Paging/PagedRequest.cs:11,21` | Cap `page` (for example at 10 000) |
| BE-81 | Low | Hosting | Recurring jobs that were removed or renamed are never deleted from Hangfire, so they then fail with "not registered" | `Hosts/FreshOrbit.Hosting/BackgroundJobs/RecurringJobScheduler.cs:19-32`; `RecurringJobRunner.cs:11-12` | Remove recurring jobs not in the registry at start-up |
| BE-82 | Low | Hosting | Recurring jobs have no `DisableConcurrentExecution`; a slow run can overlap the next (the minute jobs) | `RecurringJobScheduler.cs:27-31` | Add Hangfire's attribute on `RecurringJobRunner.RunAsync`, or an advisory lock per job |
| BE-83 | Low | Api host | Empty `ForwardedHeaders:KnownNetworks` and `Cors:AllowedOrigins` are accepted in Production. Every client then shares Caddy's IP for every per-IP limit. Compose sets both values, but the backend does not refuse (rule 7) | `Hosts/FreshOrbit.Api/ForwardedHeadersSetup.cs:52-55,71-73`; `Program.cs:25-26`; `infra/compose/docker-compose.prod.yml:155-160` | Refuse empty values outside Development/Testing/Demo |
| BE-84 | Low | Api host | The `Upload` rate-limit rule is missing from `appsettings.json` (the code default applies silently) | `Hosts/FreshOrbit.Api/appsettings.json:18-27`; `RateLimitingSetup.cs:31` | Add it to the file |
| BE-85 | Low | Migrations | `--create-admin --password` takes the password on the command line (visible in shell history and `ps`) | `Hosts/FreshOrbit.Migrations/MigrationRunner.cs:67-71` | Read it from an environment variable or stdin |
| BE-86 | Low | Hosting | The Data Protection key ring is not encrypted and not backed up (E2E-3 follow-up) | `Hosts/FreshOrbit.Hosting/DataProtectionSetup.cs` | Review when a feature first uses Data Protection |
| BE-87 | Low | Migrations | Migrations drop columns and tables in place (no expand/contract rule). Fine before launch; risky once live with a rolling deploy | `Hosts/FreshOrbit.Migrations/Persistence/Migrations/20261027090000_ClientGaps.cs:40-45`; `20261024130000_ApiGaps.cs:142-175` | Adopt expand/contract in `docs/database/README.md` §5 before launch |
| BE-88 | Low | Tests | No test that a slot past cutoff is refused at place order (only SLOT_FULL and COD_LIMIT_EXCEEDED blockers are tested) | `backend/tests/Modules/Ordering.Tests/Integration/CheckoutTests.cs` | Add a cutoff test |
| BE-89 | Low | Tests | No test for a missing or invalid cancel-policy configuration (BE-23), or for refunds across several complaints on one order (BE-28) | `backend/tests/Modules/Ordering.Tests` | Add them with the fixes |

No TODO, FIXME or HACK comments exist in `backend/src` or `backend/tests`.

## Missing features and details

| ID | Priority | Module | What is missing | Who needs it | Suggested approach |
|---|---|---|---|---|---|
| BF-1 | Must | Ordering | Invoice PDF: `pdfUrl` is always null and `SetPdf` has no caller | web, app, admin, finance | Render job on `InvoiceIssued` (free library, for example QuestPDF Community or a SkiaSharp layout); store in MinIO; signed URL |
| BF-2 | Must | Platform | Admin list and replay of dead letters and failed outbox rows | admin, ops | `GET /admin/system/dead-letters`, `POST .../{id}/replay` (resets attempts), audited |
| BF-3 | Must | Settings / Ordering | Launch-readiness check: GSTIN, legal name, SMS, SMTP, DLT ids; checkout refused until complete | ops, admin | `GET /admin/settings/readiness` and readiness health check (BE-1) |
| BF-4 | Must | Notifications | FCM push adapter (only the logging sender exists) | app | `IPushSender` for FCM HTTP v1 behind config; refused when not configured |
| BF-5 | Must | Notifications | Production email path (refused when not configured) | web, app, ops | Require SMTP with a free relay; mask addresses in logs |
| BF-6 | Must | Platform | Retention purge jobs (platform tables, OTP, refresh tokens, login failures, carts) | ops | One daily job per module, retention as configuration (BE-11 to BE-13) |
| BF-7 | Should | Platform | OpenTelemetry traces and metrics | ops | OTLP exporter (free collector) plus meters for outbox, notifications, jobs and checkout |
| BF-8 | Should | Hosting | Worker health and heartbeat | ops | `platform.worker_heartbeat` row plus an Api readiness check |
| BF-9 | Should | Payments | Refund path for collected COD (goodwill after delivery, BE-5) | admin | Open `REFUND_METHOD_REQUIRED`; staff chooses CASH or UPI and marks it paid |
| BF-10 | Should | Ordering | Overdue order sweep (slot ended, not packed) | admin, ops | Job opening an issue and notifying staff; optional auto-cancel by policy (BE-24) |
| BF-11 | Should | Inventory / Ordering | Return to stock or write off after a return or packed cancel | admin | `POST /admin/orders/{id}/stock-disposition` creating RETURN or WASTE movements |
| BF-12 | Should | Ordering | IGST and bill-of-supply document types | finance, admin | A document type per invoice, decided from the CA's answer (BE-30, BE-31) |
| BF-13 | Should | Identity | Break-glass admin reset | ops | `--reset-admin` in the Migrations host (BE-33) |
| BF-14 | Should | Identity | Sync new permissions into the system roles | admin | Add-only grant for `is_system` roles in the reference loader (BE-32) |
| BF-15 | Should | Identity | Admin refresh token in an httpOnly cookie (D40 follow-up) | admin | `Set-Cookie` on `/admin/auth/login` and refresh, SameSite=Strict |
| BF-16 | Should | Delivery | Rider-facing endpoints (my shipments, deliver, attempt) without the full `delivery.manage` permission | rider app / admin | `/rider/*` group with a `RIDER` role scoped to its own shipments |
| BF-17 | Should | Payments | Online payment gateway adapter (only CASH, UPI_ON_DELIVERY and FAKE exist; COD launch is decided) | web, app | Razorpay adapter behind `IPaymentProvider`, with a timestamped signature check |
| BF-18 | Should | Audit | `actorName` on entries | admin | From the staff claim (BE-79) |
| BF-19 | Should | Platform | Alert on dead letters, reconciliation issues and failed SMS | ops | Daily email digest to the support address, or an admin dashboard badge |
| BF-20 | Nice | Ordering | `sort` on `GET /orders` (the mobile app sends `-placedAt`) | app | Accept `placedAt` and `-placedAt`, or document the fixed order |
| BF-21 | Nice | Platform | Order and invoice snapshot purge after 72 months (CA to confirm the start) | ops | Yearly job, once the CA confirms |
| BF-22 | Nice | Catalog | Adopt `defaultVariant` on the mobile card ADD (the API is ready; this is the app side) | app | Mobile change only (`docs/design/mobile.md` §8) |
