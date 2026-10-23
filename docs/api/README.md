# REST API specification

Source of truth for every HTTP endpoint of `FreshOrbit.Api`. Follows `CLAUDE.md`, `docs/architecture/README.md`
and `docs/database/README.md`. Swagger is generated from code and must match this file.

## 1. Conventions

### 1.1 Base paths and clients

| Base path | Callers | Auth |
|---|---|---|
| `/api/v1` | Website, mobile app, payment providers (webhooks) | Public or customer JWT |
| `/api/v1/admin` | Admin dashboard | Staff JWT with permission |

| Rule | Value |
|---|---|
| Transport | HTTPS only; JSON (`application/json; charset=utf-8`); uploads `multipart/form-data` |
| JSON casing | camelCase properties; enum values UPPER_SNAKE strings; unknown properties ignored |
| Nulls | Absent optional values are `null`, never omitted in responses |
| IDs | UUIDv7 strings; human numbers (`orderNumber` `FO-260001`, `invoiceNumber`) are display only |
| Money | `long` paise, property suffix `Paise` (`unitPricePaise`) |
| Weight | `int` grams, suffix `Grams` |
| Rates | `int` basis points, suffix `Bp` (500 = 5%) |
| Quantity | Object `{ "value": 500, "unit": "GRAM" }`; `unit` is `GRAM` (sell unit KG), `PIECE` or `PACK` |
| Timestamps | ISO-8601 UTC with `Z` (`2026-10-23T05:30:00Z`); dates `YYYY-MM-DD`; times of day `HH:mm` in the location's time zone |
| Phone | E.164 (`+919876543210`) |
| Correlation | Request `X-Correlation-Id` optional; response always echoes it (generated if absent) |
| Versioning | URL major version; additive changes only within `v1` |

### 1.2 Authentication

| Item | Rule |
|---|---|
| Header | `Authorization: Bearer <accessToken>` |
| Access token | JWT, 15 min; claims `sub` (user id), `typ` (`CUSTOMER`/`STAFF`), `perm` (staff permission codes), `loc` (staff location ids, absent = all) |
| Refresh token | Opaque, rotating; customer 30 days, staff 12 h; each use returns a new pair and revokes the old token |
| Refresh reuse | Presenting an already-rotated token revokes the whole family → 401 `REFRESH_TOKEN_REUSED` |
| Customer login | Phone + OTP (`/auth/otp/request`, `/auth/otp/verify`); account is created on first verify |
| Staff login | Email + password (`/admin/auth/login`) |
| Refresh flow | On 401 `TOKEN_EXPIRED` the client calls `POST /api/v1/auth/refresh` once, then retries; a second 401 → sign-in screen |
| Customer token on `/admin` | 403 `FORBIDDEN` |
| Staff location scope | Staff without all-locations access acting on another location → 403 `LOCATION_ACCESS_DENIED`; lists are filtered silently |

### 1.3 Guest cart

| Item | Rule |
|---|---|
| Header | `X-Cart-Token: <opaque token>` |
| Issued | By the first cart write without a token or login; returned in response header `X-Cart-Token` and body `cartToken` |
| Storage | Server stores only a hash (`carts.guest_token_hash`) |
| Merge | `POST /auth/otp/verify` with `guestCartToken` merges into the customer's active cart (quantities added, capped at variant `maxOrder`); guest cart becomes `CONVERTED` |
| Checkout | Requires customer JWT; guest token alone → 401 `LOGIN_REQUIRED` |

### 1.4 Location context

| Item | Rule |
|---|---|
| Input | Header `X-Pincode: 600041` or query `?pincode=600041` (query wins) |
| Resolution | pincode → active service area → fulfillment location → city; city must be `ACTIVE`, location `ACTIVE` |
| Echo | Response headers `X-Fulfillment-Location: <locationCode>` and `X-Service-Area: <id>` |
| Unknown / inactive pincode | 422 `PINCODE_NOT_SERVICEABLE` (except `/serviceability`, which answers 200) |
| Cart | The cart stores its pincode; cart endpoints use the cart's pincode, the header only on the first add or via `PUT /cart/location` |
| Checkout / orders | Location comes from the delivery address / the order; header ignored |

| Endpoint group | Location context |
|---|---|
| `GET /serviceability` | Required (query) |
| Catalog list, detail, search, home, promotions | Optional: with it, prices and availability are included; without it `price` and `availability` are `null` and `locationRequired: true` |
| `GET /availability`, `GET /delivery-slots` | Required → 422 `LOCATION_REQUIRED` |
| Cart: first add without a cart pincode | Required → 422 `LOCATION_REQUIRED` |
| Auth, account, addresses, orders, payments, notifications | Not used |
| `/admin/*` | Not used; location is an explicit `fulfillmentLocationId` parameter |

### 1.5 Lists, paging, sorting, filtering

| Param | Rule |
|---|---|
| `page` | 1-based, default 1 |
| `pageSize` | default 20, max 100; above max → 422 `VALIDATION_FAILED` |
| `sort` | Comma list, `-` for descending: `sort=-placedAt,orderNumber`; unknown field → 422 |
| `q` | Free-text search where supported |
| Filters | Named query params; repeat for OR within a field (`status=CONFIRMED&status=PREPARING`); ranges `fromDate`/`toDate`, `minPricePaise`/`maxPricePaise` |
| Response | `{ "items": [...], "page": 1, "pageSize": 20, "totalCount": 134 }` |

### 1.6 Errors (Problem Details, RFC 7807)

```json
{
  "type": "https://api.freshorbit.in/problems/validation-failed", "title": "One or more fields are invalid.", "status": 422,
  "detail": "Quantity must be a multiple of 250 grams.", "instance": "/api/v1/cart/items", "code": "VALIDATION_FAILED",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "errors": { "quantity.value": [ { "code": "QUANTITY_STEP_INVALID", "message": "Quantity must be a multiple of 250 grams." } ] }
}
```

| Field | Rule |
|---|---|
| `code` | Always present; UPPER_SNAKE from §2; clients branch on `code`, never on text |
| `errors` | Field-keyed (camelCase JSON path, `items[1].quantity`); each value is a list of `{ code, message }` |
| Extra members | Some codes add members (`missing` for `CITY_NOT_READY`, `retryAfterSeconds`, `preview` for `PRICE_CHANGED`) |
| `message` | One sentence naming the field to fix |

| Status | Use |
|---|---|
| 400 | Unreadable request: malformed JSON, wrong type, bad header format, missing `Idempotency-Key` where required |
| 401 | No / invalid / expired token |
| 403 | Authenticated but missing permission or location access |
| 404 | Resource does not exist or is not visible to the caller (another customer's order is 404, not 403) |
| 409 | State conflict: lost race (`CONCURRENT_SAVE`), duplicate, wrong current state, idempotent request in flight |
| 412 | `If-Match` does not match the current version (`CONCURRENCY_CONFLICT`) |
| 422 | Readable request refused by validation or a business rule |
| 428 | `If-Match` missing on an endpoint that requires it (`VERSION_REQUIRED`) |
| 429 | Rate limit hit; `Retry-After` header in seconds |
| 500 / 503 | Unexpected / dependency down (`INTERNAL_ERROR`, `SERVICE_UNAVAILABLE`); never leak exception text |

### 1.7 ETag / If-Match

| Rule | Value |
|---|---|
| Source | Aggregate root row version (Postgres `xmin`) |
| GET single resource | Response header `ETag: "918273"`; body field `version: "918273"` (lists carry `version` per item) |
| Update / state change on an aggregate (`PUT`, `PATCH`, `DELETE`, command `POST` marked If-Match) | `If-Match: "918273"` required |
| Missing | 428 `VERSION_REQUIRED` |
| Stale | 412 `CONCURRENCY_CONFLICT`; client re-reads |
| Lost race after a matching quote | 409 `CONCURRENT_SAVE`; client may retry with the new ETag |
| Success | Response carries the new `ETag` |
| Create-if-absent | `If-None-Match: *` (price set when no current price) |

### 1.8 Idempotency-Key

| Rule | Value |
|---|---|
| Header | `Idempotency-Key: <UUID>` generated by the client per user action, reused on retries |
| Scope | (route template, caller id); stored in `platform.idempotency_keys` for 24 h |
| Replay | Same key + same body hash → original status and body, header `Idempotent-Replayed: true` |
| Same key, different body | 422 `IDEMPOTENCY_KEY_REUSED` |
| First request still running | 409 `IDEMPOTENCY_IN_PROGRESS` (retry after 1 s) |
| Missing where required | 400 `IDEMPOTENCY_KEY_REQUIRED` |
| Errors | 4xx refusals are stored and replayed; 5xx are not stored |

| Requires `Idempotency-Key` |
|---|
| `POST /orders` (place order) |
| `POST /orders/{orderId}/payments` (initiate online payment) |
| `POST /orders/{orderId}/cancel` |
| `POST /orders/{orderId}/complaints` |
| `POST /admin/inventory/receipts`, `/admin/inventory/waste` |
| `POST /admin/orders/{orderId}/refunds` |
| `POST /admin/delivery/shipments/{shipmentId}/cash-collected` |
| `POST /admin/cash-settlements` |

Webhooks use the provider event id instead (§7).

### 1.9 Rate limits

| Policy | Applies to | Limit | Key |
|---|---|---|---|
| `otp-request` | `POST /auth/otp/request` | 3 / 10 min and 10 / day | phone |
| `otp-request-ip` | same | 20 / hour | IP |
| `otp-verify` | `POST /auth/otp/verify` | 5 attempts per challenge (then `OTP_TOO_MANY_ATTEMPTS`), 30 / 10 min | challenge; IP |
| `staff-login` | `POST /admin/auth/login` | 5 / 15 min; account `LOCKED` after 10 consecutive failures | email + IP |
| `refresh` | `POST /auth/refresh` | 30 / min | IP |
| `checkout` | `/checkout/preview`, `POST /orders`, payment initiate | 20 / min (preview), 5 / min (place, pay) | customer |
| `search` | `GET /products`, `/search/*` | 60 / min | IP |
| `public` | other anonymous endpoints | 120 / min | IP |
| `customer` | other customer endpoints | 300 / min | user |
| `admin` | `/admin/*` | 600 / min | user |
| `webhook` | `/payments/webhooks/*` | none (signature-checked) | — |

429 body: `code: RATE_LIMITED`, `retryAfterSeconds`.

## 2. Error-code catalogue

### Common
| Code | HTTP | Meaning |
|---|---|---|
| VALIDATION_FAILED | 422 | One or more fields invalid; see `errors` |
| MALFORMED_REQUEST | 400 | Body or parameter cannot be read |
| UNAUTHENTICATED | 401 | No or invalid token |
| TOKEN_EXPIRED | 401 | Access token expired; refresh |
| LOGIN_REQUIRED | 401 | Customer login needed (checkout with guest cart) |
| FORBIDDEN | 403 | Missing permission |
| LOCATION_ACCESS_DENIED | 403 | Staff not allowed at this fulfillment location |
| NOT_FOUND | 404 | Resource not found or not visible |
| CONCURRENCY_CONFLICT | 412 | `If-Match` stale |
| VERSION_REQUIRED | 428 | `If-Match` missing |
| CONCURRENT_SAVE | 409 | Lost race on save; retry |
| IDEMPOTENCY_KEY_REQUIRED | 400 | Header missing |
| IDEMPOTENCY_KEY_REUSED | 422 | Key used with a different body |
| IDEMPOTENCY_IN_PROGRESS | 409 | Same key still processing |
| RATE_LIMITED | 429 | Too many requests |
| INVALID_STATUS_TRANSITION | 409 | Entity is not in a state that allows this action |
| DUPLICATE | 409 | Unique value already used (`errors` names the field) |
| CONFIGURATION_MISSING | 422 | Required configuration absent (policy, number series, template) |
| INTERNAL_ERROR | 500 | Unexpected failure |
| SERVICE_UNAVAILABLE | 503 | Dependency down |

### Identity
| Code | HTTP | Meaning |
|---|---|---|
| OTP_INVALID | 422 | Code does not match |
| OTP_EXPIRED | 422 | Challenge expired (5 min) or consumed |
| OTP_TOO_MANY_ATTEMPTS | 429 | 5 wrong codes; request a new OTP |
| OTP_RESEND_TOO_SOON | 429 | Resend before `resendAfterSeconds` |
| INVALID_CREDENTIALS | 401 | Staff email/password wrong (same for unknown email) |
| ACCOUNT_LOCKED | 403 | User `LOCKED` |
| ACCOUNT_DISABLED | 403 | User `DISABLED` |
| REFRESH_TOKEN_INVALID | 401 | Unknown, expired or revoked refresh token |
| REFRESH_TOKEN_REUSED | 401 | Rotated token presented again; family revoked |
| PASSWORD_TOO_WEAK | 422 | Under 12 characters or in breached list |
| ROLE_IN_USE | 409 | Role still assigned to staff |
| SYSTEM_ROLE_READ_ONLY | 422 | Default roles cannot be deleted or renamed |
| LAST_ADMIN | 422 | Cannot remove the last active ADMIN |
| ROLE_NOT_FOUND | 422 | A `roleIds` entry does not exist |
| PERMISSION_UNKNOWN | 422 | A role lists a permission code that does not exist |
| ADMIN_ALREADY_EXISTS | 422 | `--create-admin` refused: an active ADMIN exists |
| PURPOSE_NOT_SUPPORTED | 422 | OTP `purpose` other than `LOGIN` (VERIFY_PHONE not offered yet) |

### Customers
| Code | HTTP | Meaning |
|---|---|---|
| ADDRESS_NOT_SERVICEABLE | 422 | Address pincode has no active service area |
| ADDRESS_LIMIT_REACHED | 422 | More than 20 active addresses |
| CUSTOMER_DELETED | 403 | Account deleted |

### Locations
| Code | HTTP | Meaning |
|---|---|---|
| LOCATION_REQUIRED | 422 | Pincode context missing |
| PINCODE_NOT_SERVICEABLE | 422 | Pincode unknown, inactive, or its city/location not active |
| CITY_NOT_READY | 422 | Activation refused; `missing` lists absent configuration |
| CITY_NOT_ACTIVE | 422 | City is `DRAFT`, `PAUSED` or `RETIRED` |
| LOCATION_CLOSED | 422 | Fulfillment location closed on that date |
| PINCODE_ALREADY_ASSIGNED | 409 | Pincode active in another service area |
| CITY_RETIREMENT_BLOCKED | 422 | Retire refused; `blocking` lists what every `ICityRetirementCheck` reports (e.g. open orders) |
| CODE_TAKEN | 409 | City or fulfillment-location code already used |
| CITY_NOT_FOUND, FULFILLMENT_LOCATION_NOT_FOUND, SERVICE_AREA_NOT_FOUND, CLOSURE_NOT_FOUND, PINCODE_NOT_FOUND | 404 | Specific `NOT_FOUND` codes for admin locations |

### Catalog
| Code | HTTP | Meaning |
|---|---|---|
| PRODUCT_NOT_AVAILABLE | 422 | Product/variant not `ACTIVE` or inactive |
| PREPARATION_NOT_AVAILABLE | 422 | Preparation not enabled for the variant |
| QUANTITY_BELOW_MIN | 422 | Below variant `minOrder` |
| QUANTITY_ABOVE_MAX | 422 | Above variant `maxOrder` |
| QUANTITY_STEP_INVALID | 422 | Not a multiple of `orderStep` |
| QUANTITY_UNIT_INVALID | 422 | Unit does not match the variant's sell unit |
| SLUG_TAKEN | 409 | Slug already used |
| SKU_TAKEN | 409 | SKU already used |
| DEFAULT_PREPARATION_REQUIRED | 422 | Variant needs exactly one default preparation |
| YIELD_INVALID | 422 | `yieldBp` outside 1–10000 |
| IMAGE_INVALID | 422 | Not JPEG/PNG/WebP (checked by content, not extension), over 5 MB, or shortest side under 800 px |
| CATEGORY_NOT_FOUND, PRODUCT_NOT_FOUND, VARIANT_NOT_FOUND, IMAGE_NOT_FOUND | 404 | Specific `NOT_FOUND` codes for admin catalog |

### Pricing
| Code | HTTP | Meaning |
|---|---|---|
| PRICE_NOT_AVAILABLE | 422 | No current price for variant at the location: not for sale there |
| PREPARATION_CHARGE_NOT_AVAILABLE | 422 | Prep method enabled but no charge row (0 must be explicit) |
| TAX_RULE_MISSING | 422 | No tax rule for the product's tax category |
| PRICE_PERIOD_OVERLAP | 409 | New period overlaps an existing one |
| PRICE_IN_PAST | 422 | `validFrom` more than 60 s before now (within 60 s it is treated as now); more than 90 days ahead → `VALIDATION_FAILED` |

### Inventory
| Code | HTTP | Meaning |
|---|---|---|
| OUT_OF_STOCK | 422 | Available stock (on hand − reserved) below required gross quantity |
| STOCK_ITEM_MISSING | 422 | No stock item for variant at location |
| BATCH_CODE_TAKEN | 409 | Batch code already used at the location |
| BATCH_NOT_ACTIVE | 422 | Batch depleted, expired or discarded |
| ADJUSTMENT_BELOW_ZERO | 422 | Adjustment would make on hand negative or below reserved |
| DUPLICATE | 409 | Stock item already exists for the location and variant |
| REASON_CODE_INVALID | 422 | Waste/adjust reason not allowed |

### Cart
| Code | HTTP | Meaning |
|---|---|---|
| CART_NOT_FOUND | 404 | Token unknown or cart not `ACTIVE` |
| CART_EMPTY | 422 | Checkout with no lines |
| CART_ITEM_LIMIT | 422 | More than 30 lines |
| CART_LOCATION_CHANGED | 409 | New pincode maps to another location; response lists lines no longer sellable; confirm with `force=true` |

### Promotions
| Code | HTTP | Meaning |
|---|---|---|
| COUPON_INVALID | 422 | Unknown, paused, wrong city, or out of scope for all cart lines |
| COUPON_EXPIRED | 422 | Outside `validFrom`–`validTo` |
| COUPON_NOT_STARTED | 422 | Before `validFrom` |
| COUPON_LIMIT_REACHED | 422 | Total limit or per-customer limit used |
| COUPON_FIRST_ORDER_ONLY | 422 | Customer already has a delivered or active order |
| COUPON_MIN_ORDER_NOT_MET | 422 | Eligible subtotal below `minOrderPaise` |

### Ordering / Checkout
| Code | HTTP | Meaning |
|---|---|---|
| MIN_ORDER_NOT_MET | 422 | Subtotal below service area `minOrderPaise` |
| SLOT_FULL | 422 | Slot `bookedCount = capacity` |
| SLOT_CUTOFF_PASSED | 422 | Now ≥ slot `cutoffAt` |
| SLOT_NOT_AVAILABLE | 422 | Slot closed or for another location |
| PAYMENT_METHOD_NOT_ALLOWED | 422 | Method not enabled for the city / not implemented yet |
| COD_LIMIT_EXCEEDED | 422 | Total above `codMaxOrderPaise` |
| PRICE_CHANGED | 409 | Server total differs from `expectedTotalPaise`; `preview` member holds the new preview |
| ORDER_NOT_CANCELLABLE | 422 | Status beyond `cancelAllowedUntilStatus` (customer) or beyond `PACKED` (staff) |
| PACKED_QUANTITY_INVALID | 422 | Negative, wrong unit, or missing line in pack request |
| SUBSTITUTION_NOT_PENDING | 409 | Substitution not `PROPOSED` (answered or expired) |
| SUBSTITUTION_EXPIRED | 422 | Proposal past `expiresAt` |
| COMPLAINT_WINDOW_CLOSED | 422 | More than 24 h after delivery |
| COMPLAINT_ALREADY_OPEN | 409 | Open complaint exists for the line |
| INVOICE_NOT_ISSUED | 404 | Invoice exists only after `DELIVERED` |

### Payments
| Code | HTTP | Meaning |
|---|---|---|
| PAYMENT_NOT_REQUIRED | 422 | Order is COD / UPI on delivery, or already paid |
| PAYMENT_PROVIDER_UNAVAILABLE | 503 | Adapter disabled or provider down |
| WEBHOOK_SIGNATURE_INVALID | 401 | Signature check failed (event stored with `signatureValid=false`) |
| WEBHOOK_PROVIDER_UNKNOWN | 404 | No adapter for `{provider}` |
| REFUND_EXCEEDS_PAYMENT | 422 | Refunded + requested > paid/collected |
| REFUND_NOT_APPROVABLE | 409 | Refund not `REQUESTED` |
| REFUND_SELF_APPROVAL | 422 | Requester cannot approve own refund |
| NO_CAPTURED_PAYMENT | 422 | Nothing paid or collected to refund |
| CASH_ALREADY_COLLECTED | 409 | Shipment cash already recorded |
| SETTLEMENT_EXISTS | 409 | Settlement for rider + date exists |
| SETTLEMENT_SELF_RESOLUTION | 422 | The staff who recorded a settlement with a difference cannot settle it |
| LOCATION_ACCESS_DENIED | 403 | Payment, refund, shipment, settlement or `fulfillmentLocationId` outside the caller's locations (also Reports) |
| Field codes (in `errors`, with 422 `VALIDATION_FAILED`) | 422 | `CASH_AMOUNT_MISMATCH` (`amountPaise` ≠ amount due), `REFUND_METHOD_NOT_ALLOWED` (`ORIGINAL` for a provider without refunds), `UPI_ID_REQUIRED`, `UPI_REFERENCE_REQUIRED`, `COMPLAINT_REQUIRED`, `REASON_REQUIRED`, `REFERENCE_INVALID` (mark-paid reference 4–64 chars), `AMOUNT_INVALID`, `DATE_IN_FUTURE` |

### Delivery
| Code | HTTP | Meaning |
|---|---|---|
| RIDER_NOT_AVAILABLE | 422 | Rider inactive or at another location |
| DELIVERY_CODE_INVALID | 422 | 4-digit customer delivery code wrong |
| SHIPMENT_NOT_DISPATCHABLE | 409 | Order not `PACKED` or no rider |
| SLOT_TEMPLATE_OVERLAP | 409 | Template overlaps another for the same day |
| SLOT_CAPACITY_BELOW_BOOKED | 422 | New capacity below `bookedCount` |
| SLOT_NOT_AVAILABLE | 422 | Slot missing, closed or at another location (checkout booking; reopening a slot on a closure date) |
| SLOT_CUTOFF_PASSED | 422 | Slot booking cutoff has passed (checked before capacity) |
| SLOT_FULL | 422 | Slot has no capacity left |
| RIDER_HAS_ACTIVE_SHIPMENTS | 409 | Rider cannot be deactivated while ASSIGNED / OUT_FOR_DELIVERY / FAILED shipments exist |
| REATTEMPT_NOT_ALLOWED | 422 | Max delivery attempts reached or the slot's local date has passed; return the shipment |
| DELIVERY_CODE_LOCKED | 422 | 5 wrong delivery codes; record a failed attempt and reattempt (resets the counter) |
| CASH_ALREADY_COLLECTED | 409 | Cash already recorded for the shipment |

### Notifications / Settings
| Code | HTTP | Meaning |
|---|---|---|
| TEMPLATE_PLACEHOLDER_UNKNOWN | 422 | Template uses an unknown `{{placeholder}}` |
| NUMBER_SERIES_IN_USE | 409 | Series period already issued numbers; prefix/next value locked |
| GSTIN_INVALID | 422 | GSTIN format/checksum invalid |
| PREFERENCE_REQUIRED | 422 | `ORDER_UPDATES` SMS cannot be disabled |

## 3. Permissions and roles

| Permission | Allows | ADMIN | STORE_MANAGER | PACKER | DISPATCHER | SUPPORT | FINANCE |
|---|---|---|---|---|---|---|---|
| `catalog.manage` | Categories, products, variants, preparations, images, home promotions banners | ✓ |  |  |  |  |  |
| `pricing.manage` | Variant prices, preparation charges, tax rules | ✓ | ✓ |  |  |  |  |
| `promotions.manage` | Coupons | ✓ |  |  |  |  |  |
| `inventory.view` | Stock, batches, movements, cold-chain logs | ✓ | ✓ | ✓ |  |  | ✓ |
| `inventory.receive` | Receive stock (batches) | ✓ | ✓ |  |  |  |  |
| `inventory.adjust` | Waste, adjustments, counts, batch discard, cold-chain entries | ✓ | ✓ |  |  |  |  |
| `orders.view` | Order lists, detail, history | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `orders.pack` | Start preparing, weigh and pack, propose substitutions | ✓ | ✓ | ✓ |  |  |  |
| `orders.cancel` | Cancel orders | ✓ | ✓ |  |  | ✓ |  |
| `complaints.manage` | Decide complaints | ✓ | ✓ |  |  | ✓ |  |
| `refunds.request` | Create refund requests | ✓ | ✓ |  |  | ✓ |  |
| `refunds.approve` | Approve / reject refunds | ✓ |  |  |  |  | ✓ |
| `payments.view` | Payments, webhook events | ✓ | ✓ |  |  | ✓ | ✓ |
| `cash.settle` | Rider cash settlements | ✓ | ✓ |  |  |  | ✓ |
| `delivery.manage` | Slots, templates, riders, shipments, dispatch, delivery attempts, cash collected | ✓ | ✓ |  | ✓ |  |  |
| `locations.manage` | Cities, fulfillment locations, service areas, pincodes, hours, closures | ✓ |  |  |  |  |  |
| `cities.activate` | Activate / pause / retire a city | ✓ |  |  |  |  |  |
| `customers.view` | Customer list and detail | ✓ | ✓ |  |  | ✓ | ✓ |
| `customers.manage` | Block / unblock, edit customer contact | ✓ |  |  |  | ✓ |  |
| `notifications.manage` | Templates, test sends | ✓ |  |  |  |  |  |
| `reports.view` | Reports | ✓ | ✓ |  |  |  | ✓ |
| `settings.manage` | Business profile, order policies, number series | ✓ |  |  |  |  |  |
| `staff.manage` | Staff, roles, location access | ✓ |  |  |  |  |  |
| `audit.view` | Audit log | ✓ |  |  |  |  | ✓ |

Rules: roles are data (`identity.roles`), default roles are `isSystem` (permissions editable, code not); custom roles allowed;
STORE_MANAGER, PACKER, DISPATCHER are normally location-scoped. Every admin write is audited (`audit.entries`).

## 4. Endpoints

Auth column: `public`, `customer`, `customer|guest` (JWT or `X-Cart-Token`), or a permission code. Notes: `IK` =
`Idempotency-Key` required, `IM` = `If-Match` required, `LOC` = location context (§1.4).

### 4.1 Identity / Auth

| Method | Path | Auth | Notes | Purpose |
|---|---|---|---|---|
| POST | `/api/v1/auth/otp/request` | public | rate `otp-request` | Send OTP to phone |
| POST | `/api/v1/auth/otp/verify` | public | rate `otp-verify` | Verify OTP, issue tokens, merge guest cart |
| POST | `/api/v1/auth/refresh` | public | rate `refresh` | Rotate refresh token (customer and staff) |
| POST | `/api/v1/auth/logout` | customer | | Revoke refresh token (family with `allDevices=true`) |
| POST | `/api/v1/admin/auth/login` | public | rate `staff-login` | Staff login |
| POST | `/api/v1/admin/auth/logout` | staff | | Revoke refresh token |
| GET | `/api/v1/admin/auth/me` | staff | | Staff profile, permissions, locations |
| POST | `/api/v1/admin/auth/password` | staff | | Change own password (`currentPassword`, `newPassword`) |

**POST /api/v1/auth/otp/request** → 202
```json
{ "phone": "+919876543210", "purpose": "LOGIN" }
```
```json
{ "challengeId": "0192b5a0-7c1e-7d3a-9f10-2c4e5a6b7c8d", "expiresAt": "2026-10-23T05:35:00Z", "resendAfterSeconds": 30, "codeLength": 6 }
```
Always 202 for a well-formed phone (no account enumeration).

**POST /api/v1/auth/otp/verify** → 200
```json
{
  "challengeId": "0192b5a0-7c1e-7d3a-9f10-2c4e5a6b7c8d", "phone": "+919876543210", "code": "482913", "deviceLabel": "Android - Pixel 8",
  "guestCartToken": "gct_5Jx9QwL2"
}
```
```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIs...", "accessTokenExpiresAt": "2026-10-23T05:45:00Z", "refreshToken": "rt_9f8e7d6c5b4a39281706f5e4d3c2b1a0",
  "refreshTokenExpiresAt": "2026-11-22T05:30:00Z",
  "user": { "id": "0192b5a1-0000-7aaa-8bbb-111122223333", "phone": "+919876543210", "fullName": null, "isNewCustomer": true },
  "cart": { "id": "0192b5a1-1111-7ccc-8ddd-444455556666", "mergedLineCount": 2 }
}
```
Errors: `OTP_INVALID`, `OTP_EXPIRED`, `OTP_TOO_MANY_ATTEMPTS`, `ACCOUNT_DISABLED`.
Implementation note: `cart` is `null` for now; Cart merges the guest cart asynchronously on the `CustomerSignedIn`
event (which carries the guest token), so the client re-reads the cart after login.

**POST /api/v1/auth/refresh** → 200
```json
{ "refreshToken": "rt_9f8e7d6c5b4a39281706f5e4d3c2b1a0" }
```
```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIs...", "accessTokenExpiresAt": "2026-10-23T06:00:00Z", "refreshToken": "rt_1a2b3c4d5e6f708192a3b4c5d6e7f809",
  "refreshTokenExpiresAt": "2026-11-22T05:45:00Z"
}
```
Errors: `REFRESH_TOKEN_INVALID`, `REFRESH_TOKEN_REUSED`.

### 4.2 Customers, addresses, wishlist

| Method | Path | Auth | Notes | Purpose |
|---|---|---|---|---|
| GET | `/api/v1/me` | customer | ETag | Profile |
| PATCH | `/api/v1/me` | customer | IM | Update `fullName`, `email`, `marketingOptIn` |
| DELETE | `/api/v1/me` | customer | IM | Delete account (status `DELETED`, personal data anonymised; refused with active orders → `INVALID_STATUS_TRANSITION`) |
| GET | `/api/v1/me/addresses` | customer | | Active addresses with `isServiceable` |
| POST | `/api/v1/me/addresses` | customer | | Add address (non-serviceable allowed, flagged) |
| PUT | `/api/v1/me/addresses/{addressId}` | customer | IM | Replace address |
| DELETE | `/api/v1/me/addresses/{addressId}` | customer | | Deactivate address |
| POST | `/api/v1/me/addresses/{addressId}/default` | customer | | Make default |
| GET | `/api/v1/me/wishlist` | customer | LOC optional | Wishlist products (priced when LOC given) |
| PUT | `/api/v1/me/wishlist/{productId}` | customer | | Add (idempotent by nature) |
| DELETE | `/api/v1/me/wishlist/{productId}` | customer | | Remove |

### 4.3 Locations / serviceability (public)

| Method | Path | Auth | Notes | Purpose |
|---|---|---|---|---|
| GET | `/api/v1/serviceability?pincode=` | public | | Is the pincode served; location, fees, next slot |
| GET | `/api/v1/cities` | public | | Active cities (for city picker) |

**GET /api/v1/serviceability?pincode=600041** → 200
```json
{
  "pincode": "600041", "serviceable": true, "reasonCode": null,
  "city": { "id": "0192b400-0001-7000-8000-000000000001", "code": "CHENNAI", "name": "Chennai" },
  "fulfillmentLocation": { "id": "0192b400-0002-7000-8000-000000000002", "code": "CHN-TVM", "name": "Thiruvanmiyur Store", "hasColdStorage": true },
  "serviceArea": {
    "id": "0192b400-0003-7000-8000-000000000003", "name": "Adyar - Thiruvanmiyur", "deliveryFeePaise": 3900, "freeDeliveryAbovePaise": 49900,
    "minOrderPaise": 29900
  },
  "nextSlot": { "id": "0192b5f0-aaaa-7000-8000-000000000010", "startAt": "2026-10-23T08:30:00Z", "endAt": "2026-10-23T10:30:00Z" }
}
```
Not served → 200 `{ "pincode": "600123", "serviceable": false, "reasonCode": "PINCODE_NOT_SERVICEABLE", ... null }`.

### 4.4 Catalog and search (public)

| Method | Path | Auth | Notes | Purpose |
|---|---|---|---|---|
| GET | `/api/v1/home` | public | LOC optional | Banners (`promotions`), featured categories, featured products |
| GET | `/api/v1/categories` | public | | Category tree |
| GET | `/api/v1/categories/{slug}` | public | | Category detail |
| GET | `/api/v1/products` | public | LOC optional, rate `search` | List: `category`, `q`, `sourceType`, `preparation`, `inStock`, `minPricePaise`, `maxPricePaise`, `sort` (`relevance`, `price`, `-price`, `name`, `-newest`) |
| GET | `/api/v1/products/{slug}` | public | LOC optional | Detail with variants, preparations, yield, price, availability |
| GET | `/api/v1/search/suggest?q=` | public | rate `search` | Top 8 name / local-name matches (trigram) |
| GET | `/api/v1/promotions` | public | LOC optional | Active home offers for the city |

Not sellable at the location (no price, no stock item, product inactive) → excluded from lists; detail returns `availability.status: NOT_SOLD_HERE`.

**GET /api/v1/products?category=fish&pageSize=2** (`X-Pincode: 600041`) → 200
```json
{
  "items": [
    {
      "id": "0192b410-0001-7000-8000-000000000101", "slug": "seer-fish-vanjaram", "name": "Seer Fish",
      "localNames": [ "Vanjaram", "Neymeen", "Surmai" ], "sourceType": "WILD", "imageUrl": "https://img.freshorbit.in/products/seer-fish/1-640.webp",
      "fromPrice": { "unitPricePaise": 129900, "compareAtPaise": 149900, "perUnit": "KG" }, "availability": { "status": "IN_STOCK", "message": null },
      "locationRequired": false
    },
    {
      "id": "0192b410-0002-7000-8000-000000000102", "slug": "tiger-prawns", "name": "Tiger Prawns", "localNames": [ "Eral", "Konju" ],
      "sourceType": "WILD", "imageUrl": "https://img.freshorbit.in/products/tiger-prawns/1-640.webp",
      "fromPrice": { "unitPricePaise": 89900, "compareAtPaise": null, "perUnit": "KG" },
      "availability": { "status": "LOW_STOCK", "message": "Only a few kg left today." }, "locationRequired": false
    }
  ],
  "page": 1, "pageSize": 2, "totalCount": 14
}
```
`availability.status`: `IN_STOCK`, `LOW_STOCK` (available ≤ threshold), `OUT_OF_STOCK`, `NOT_SOLD_HERE`, or `null` without LOC.

**GET /api/v1/products/seer-fish-vanjaram** (`X-Pincode: 600041`) → 200
```json
{
  "id": "0192b410-0001-7000-8000-000000000101", "slug": "seer-fish-vanjaram", "name": "Seer Fish", "localNames": [ "Vanjaram", "Neymeen", "Surmai" ],
  "speciesScientificName": "Scomberomorus commerson", "description": "Firm, mildly sweet flesh; ideal for fry and curry.", "sourceType": "WILD",
  "sourceRegion": "Kasimedu, Chennai", "storageInstructions": "Keep at 0-4 °C; cook within 24 hours.", "category": { "slug": "fish", "name": "Fish" },
  "images": [ { "url": "https://img.freshorbit.in/products/seer-fish/1-1280.webp", "altText": "Seer fish steaks", "width": 1280, "height": 960 } ],
  "location": { "fulfillmentLocationCode": "CHN-TVM", "pincode": "600041" },
  "variants": [
    {
      "id": "0192b411-0001-7000-8000-000000000201", "sku": "SEER-KG", "name": "Medium (3-5 kg fish)", "sellUnit": "KG", "isWeightVariable": true,
      "minOrder": { "value": 500, "unit": "GRAM" }, "maxOrder": { "value": 5000, "unit": "GRAM" }, "orderStep": { "value": 250, "unit": "GRAM" },
      "unitPricePaise": 129900, "compareAtPaise": 149900,
      "preparations": [
        { "code": "WHOLE", "name": "Whole, uncleaned", "yieldBp": 10000, "isDefault": false, "chargePaise": 0, "chargeBasis": "FLAT" },
        { "code": "STEAK", "name": "Steaks (1 cm)", "yieldBp": 7000, "isDefault": true, "chargePaise": 4000, "chargeBasis": "PER_KG" },
        { "code": "FILLET", "name": "Boneless fillet", "yieldBp": 5500, "isDefault": false, "chargePaise": 8000, "chargeBasis": "PER_KG" }
      ],
      "availability": { "status": "IN_STOCK", "maxOrderableNow": { "value": 5000, "unit": "GRAM" }, "message": null }
    }
  ],
  "netWeightNote": "You pay for net weight after cleaning. Final weight may vary up to 10% above the ordered weight.", "isWishlisted": false
}
```
`maxOrderableNow` is computed server-side per preparation from gross stock and yield (`?preparation=STEAK`, default preparation otherwise).

### 4.5 Availability and delivery slots (customer)

| Method | Path | Auth | Notes | Purpose |
|---|---|---|---|---|
| GET | `/api/v1/availability?variantId=&preparation=` | public | LOC required | Live availability for one or more variants (repeat `variantId`) |
| GET | `/api/v1/delivery-slots?date=` | public | LOC required | Slots for today + 6 days: `startAt`, `endAt`, `cutoffAt`, `status` (`AVAILABLE`, `FULL`, `CUTOFF_PASSED`, `CLOSED`) |

`GET /delivery-slots` (Delivery, A10): `date` optional (absent = today … today + 6 in the city time zone; a date outside
that range returns no items). Response `{ fulfillmentLocationId, items: [{ id, slotDate, startAt, endAt, cutoffAt, status }] }`
ordered by `startAt`. Missing pincode → 422 `LOCATION_REQUIRED`; not served → 422 `PINCODE_NOT_SERVICEABLE`.

### 4.6 Cart

Cart is priced on every read (§5). Owner = customer JWT, else `X-Cart-Token`.

| Method | Path | Auth | Notes | Purpose |
|---|---|---|---|---|
| GET | `/api/v1/cart` | customer\|guest | | Priced cart; empty cart if none |
| POST | `/api/v1/cart/items` | customer\|guest | LOC on first add | Add line (same variant + preparation → quantities add) |
| PATCH | `/api/v1/cart/items/{itemId}` | customer\|guest | | Change `quantity` or `preparationCode` |
| DELETE | `/api/v1/cart/items/{itemId}` | customer\|guest | | Remove line |
| DELETE | `/api/v1/cart` | customer\|guest | | Clear cart |
| PUT | `/api/v1/cart/location` | customer\|guest | `force` query | Change pincode; `CART_LOCATION_CHANGED` if the location changes and lines drop |
| PUT | `/api/v1/cart/coupon` | customer | | Apply coupon (`{ "code": "FRESH100" }`); validated and returned in totals |
| DELETE | `/api/v1/cart/coupon` | customer | | Remove coupon |

Cart writes return the full priced cart (same shape as GET). No If-Match on cart (last write wins; lines are small).

**POST /api/v1/cart/items** → 200
```json
{ "variantId": "0192b411-0001-7000-8000-000000000201", "preparationCode": "STEAK", "quantity": { "value": 500, "unit": "GRAM" } }
```

**PATCH /api/v1/cart/items/{itemId}** → 200
```json
{ "quantity": { "value": 750, "unit": "GRAM" } }
```

**GET /api/v1/cart** → 200
```json
{
  "id": "0192b5a1-1111-7ccc-8ddd-444455556666", "cartToken": null, "pincode": "600041", "fulfillmentLocationCode": "CHN-TVM",
  "lines": [
    {
      "id": "0192b5a2-0001-7000-8000-000000000301", "productId": "0192b410-0001-7000-8000-000000000101", "productSlug": "seer-fish-vanjaram",
      "productName": "Seer Fish (Vanjaram)", "variantId": "0192b411-0001-7000-8000-000000000201", "variantName": "Medium (3-5 kg fish)",
      "sellUnit": "KG", "imageUrl": "https://img.freshorbit.in/products/seer-fish/1-320.webp",
      "preparation": { "code": "STEAK", "name": "Steaks (1 cm)", "yieldBp": 7000 }, "quantity": { "value": 500, "unit": "GRAM" },
      "estimatedNetQuantity": { "value": 500, "unit": "GRAM" }, "maxChargeableQuantity": { "value": 550, "unit": "GRAM" }, "unitPricePaise": 129900,
      "compareAtPaise": 149900, "priceUnit": "KG", "itemPaise": 64950, "preparationChargePaise": 2000, "preparationChargeBasis": "PER_KG",
      "taxRateBp": 0, "taxPaise": 0, "estimatedLineTotalPaise": 66950, "maxLineTotalPaise": 73645,
      "availability": { "status": "IN_STOCK", "message": null }, "isSellable": true, "issues": []
    },
    {
      "id": "0192b5a2-0002-7000-8000-000000000302", "productId": "0192b410-0002-7000-8000-000000000102", "productSlug": "tiger-prawns",
      "productName": "Tiger Prawns", "variantId": "0192b411-0002-7000-8000-000000000202", "variantName": "Large (20-25 count/kg)", "sellUnit": "KG",
      "imageUrl": "https://img.freshorbit.in/products/tiger-prawns/1-320.webp",
      "preparation": { "code": "CLEANED", "name": "Peeled and deveined", "yieldBp": 6000 }, "quantity": { "value": 500, "unit": "GRAM" },
      "estimatedNetQuantity": { "value": 500, "unit": "GRAM" }, "maxChargeableQuantity": { "value": 550, "unit": "GRAM" }, "unitPricePaise": 89900,
      "compareAtPaise": null, "priceUnit": "KG", "itemPaise": 44950, "preparationChargePaise": 3000, "preparationChargeBasis": "PER_KG",
      "taxRateBp": 0, "taxPaise": 0, "estimatedLineTotalPaise": 47950, "maxLineTotalPaise": 52745,
      "availability": { "status": "LOW_STOCK", "message": "Only 1.5 kg left today." }, "isSellable": true, "issues": []
    },
    {
      "id": "0192b5a2-0003-7000-8000-000000000303", "productId": "0192b410-0003-7000-8000-000000000103", "productSlug": "mud-crab",
      "productName": "Mud Crab (Nandu)", "variantId": "0192b411-0003-7000-8000-000000000203", "variantName": "Live, approx. 600 g each",
      "sellUnit": "PIECE", "imageUrl": "https://img.freshorbit.in/products/mud-crab/1-320.webp",
      "preparation": { "code": "WHOLE", "name": "Whole, live", "yieldBp": 10000 }, "quantity": { "value": 2, "unit": "PIECE" },
      "estimatedNetQuantity": { "value": 2, "unit": "PIECE" }, "maxChargeableQuantity": { "value": 2, "unit": "PIECE" }, "unitPricePaise": 65000,
      "compareAtPaise": null, "priceUnit": "PIECE", "itemPaise": 130000, "preparationChargePaise": 0, "preparationChargeBasis": "FLAT", "taxRateBp": 0,
      "taxPaise": 0, "estimatedLineTotalPaise": 130000, "maxLineTotalPaise": 130000, "availability": { "status": "IN_STOCK", "message": null },
      "isSellable": true, "issues": []
    }
  ],
  "coupon": { "code": "FRESH100", "status": "APPLIED", "discountPaise": 10000, "message": null },
  "totals": {
    "itemsPaise": 239900, "preparationPaise": 5000, "subtotalPaise": 244900, "discountPaise": 10000, "deliveryFeePaise": 0, "deliveryFeeWaived": true,
    "taxPaise": 0, "estimatedTotalPaise": 234900, "maxTotalPaise": 246390, "minOrderPaise": 29900, "minOrderShortfallPaise": 0,
    "freeDeliveryShortfallPaise": 0
  },
  "canCheckout": true, "checkoutBlockers": [],
  "messages": [ "Weight-based items are charged on actual net weight, up to 10% over the ordered weight." ]
}
```

### 4.7 Promotions / coupons (customer)

| Method | Path | Auth | Notes | Purpose |
|---|---|---|---|---|
| GET | `/api/v1/coupons` | customer | LOC optional | Coupons visible to the customer (code, description, min order, validTo) |
| PUT/DELETE | `/api/v1/cart/coupon` | customer | | See §4.6 |

### 4.8 Checkout and orders (customer)

| Method | Path | Auth | Notes | Purpose |
|---|---|---|---|---|
| POST | `/api/v1/checkout/preview` | customer | rate `checkout` | Price the cart for an address, slot, payment method; no writes |
| POST | `/api/v1/orders` | customer | IK, rate `checkout` | Place order (one transaction, §database 4) |
| GET | `/api/v1/orders` | customer | | My orders: `status`, `sort=-placedAt` |
| GET | `/api/v1/orders/{orderId}` | customer | ETag | Order detail |
| GET | `/api/v1/orders/{orderId}/tracking` | customer | | Timeline, shipment, delivery code |
| POST | `/api/v1/orders/{orderId}/cancel` | customer | IK | Cancel (`reasonCode`: `CHANGED_MIND`, `WRONG_ADDRESS`, `SLOT_ISSUE`, `OTHER`) |
| POST | `/api/v1/orders/{orderId}/substitutions/{substitutionId}/accept` | customer | | Accept proposed substitution |
| POST | `/api/v1/orders/{orderId}/substitutions/{substitutionId}/reject` | customer | | Reject (line short-supplied) |
| POST | `/api/v1/orders/{orderId}/complaints` | customer | IK, multipart | Raise complaint (`orderItemId`, `kind`, `description`, up to 4 images) |
| GET | `/api/v1/orders/{orderId}/complaints` | customer | | Complaints and their status |
| GET | `/api/v1/orders/{orderId}/invoice` | customer | | Invoice JSON + `pdfUrl` (signed, 10 min); 404 `INVOICE_NOT_ISSUED` before delivery |
| POST | `/api/v1/orders/{orderId}/reorder` | customer | | Add the order's sellable lines to the cart; returns priced cart |

**POST /api/v1/checkout/preview** → 200
```json
{
  "addressId": "0192b5b0-0001-7000-8000-000000000401", "deliverySlotId": "0192b5f0-aaaa-7000-8000-000000000010", "paymentMethod": "COD",
  "substitutionPreference": "CALL_ME"
}
```
```json
{
  "cartId": "0192b5a1-1111-7ccc-8ddd-444455556666",
  "address": {
    "id": "0192b5b0-0001-7000-8000-000000000401", "label": "HOME", "line1": "12, 3rd Cross Street", "locality": "Thiruvanmiyur", "pincode": "600041",
    "isServiceable": true
  },
  "fulfillmentLocationCode": "CHN-TVM",
  "slot": {
    "id": "0192b5f0-aaaa-7000-8000-000000000010", "startAt": "2026-10-23T08:30:00Z", "endAt": "2026-10-23T10:30:00Z",
    "cutoffAt": "2026-10-23T06:30:00Z", "status": "AVAILABLE"
  },
  "lines": [ "… same line shape as cart …" ], "coupon": { "code": "FRESH100", "status": "APPLIED", "discountPaise": 10000, "message": null },
  "totals": {
    "itemsPaise": 239900, "preparationPaise": 5000, "subtotalPaise": 244900, "discountPaise": 10000, "deliveryFeePaise": 0, "deliveryFeeWaived": true,
    "taxPaise": 0, "estimatedTotalPaise": 234900, "maxTotalPaise": 246390
  },
  "paymentMethods": [
    { "method": "COD", "enabled": true, "reasonCode": null }, { "method": "UPI_ON_DELIVERY", "enabled": true, "reasonCode": null },
    { "method": "ONLINE", "enabled": false, "reasonCode": "PAYMENT_METHOD_NOT_ALLOWED" }
  ],
  "canPlaceOrder": true, "blockers": [], "expectedTotalPaise": 234900
}
```
`blockers` is a list of `{ code, lineId?, message }` using §2 codes (`OUT_OF_STOCK`, `SLOT_FULL`, `MIN_ORDER_NOT_MET`, …). Preview never returns 422 for these; it reports them.

**POST /api/v1/orders** (`Idempotency-Key: 6f1d2c3b-8a9e-4f70-b1c2-d3e4f5a6b7c8`) → 201, `Location: /api/v1/orders/{id}`
```json
{
  "addressId": "0192b5b0-0001-7000-8000-000000000401", "deliverySlotId": "0192b5f0-aaaa-7000-8000-000000000010", "paymentMethod": "COD",
  "substitutionPreference": "CALL_ME", "customerNotes": "Please call before arriving.", "expectedTotalPaise": 234900
}
```
Response: order detail (shape below) with `status: CONFIRMED` (COD / UPI_ON_DELIVERY) or `PENDING_PAYMENT` + `payment.nextAction` (ONLINE).

| Place-order rule | Value |
|---|---|
| Source of lines | Server-side cart; body carries no lines or prices |
| `expectedTotalPaise` | Must equal the recomputed estimated total, else 409 `PRICE_CHANGED` with `preview` |
| Refusals | 422 `CART_EMPTY`, `OUT_OF_STOCK`, `PRICE_NOT_AVAILABLE`, `SLOT_FULL`, `SLOT_CUTOFF_PASSED`, `COUPON_*`, `MIN_ORDER_NOT_MET`, `COD_LIMIT_EXCEEDED`, `ADDRESS_NOT_SERVICEABLE`, `PAYMENT_METHOD_NOT_ALLOWED` |
| Race | Retried internally up to 3 times; then 409 `CONCURRENT_SAVE` |
| Effects | Stock reserved `ceil(net × 10000 / yieldBp)` g, slot booked, coupon redeemed, cart `CONVERTED`, reservation expiry `reservationMinutes` for `PENDING_PAYMENT` |

**GET /api/v1/orders/{orderId}** → 200 (after packing, COD)
```json
{
  "id": "0192b600-0001-7000-8000-000000000501", "orderNumber": "FO-260001", "version": "918273", "status": "PACKED", "paymentMethod": "COD",
  "placedAt": "2026-10-23T05:41:12Z", "slot": { "startAt": "2026-10-23T08:30:00Z", "endAt": "2026-10-23T10:30:00Z" },
  "address": {
    "recipientName": "Priya Raman", "phone": "+919876543210", "line1": "12, 3rd Cross Street", "line2": null, "landmark": "Near RTO",
    "locality": "Thiruvanmiyur", "cityName": "Chennai", "stateCode": "TN", "pincode": "600041"
  },
  "substitutionPreference": "CALL_ME",
  "items": [
    {
      "id": "0192b600-0101-7000-8000-000000000601", "lineNo": 1, "productName": "Seer Fish (Vanjaram)", "variantName": "Medium (3-5 kg fish)",
      "preparationName": "Steaks (1 cm)", "orderedQuantity": { "value": 500, "unit": "GRAM" },
      "maxChargeableQuantity": { "value": 550, "unit": "GRAM" }, "packedQuantity": { "value": 540, "unit": "GRAM" },
      "chargedQuantity": { "value": 540, "unit": "GRAM" }, "unitPricePaise": 129900, "preparationChargePaise": 2160, "estimatedTotalPaise": 66950,
      "finalTotalPaise": 72306, "status": "ACTIVE"
    },
    {
      "id": "0192b600-0102-7000-8000-000000000602", "lineNo": 2, "productName": "Tiger Prawns", "variantName": "Large (20-25 count/kg)",
      "preparationName": "Peeled and deveined", "orderedQuantity": { "value": 500, "unit": "GRAM" },
      "maxChargeableQuantity": { "value": 550, "unit": "GRAM" }, "packedQuantity": { "value": 580, "unit": "GRAM" },
      "chargedQuantity": { "value": 550, "unit": "GRAM" }, "unitPricePaise": 89900, "preparationChargePaise": 3300, "estimatedTotalPaise": 47950,
      "finalTotalPaise": 52745, "status": "ACTIVE"
    },
    {
      "id": "0192b600-0103-7000-8000-000000000603", "lineNo": 3, "productName": "Mud Crab (Nandu)", "variantName": "Live, approx. 600 g each",
      "preparationName": "Whole, live", "orderedQuantity": { "value": 2, "unit": "PIECE" }, "maxChargeableQuantity": { "value": 2, "unit": "PIECE" },
      "packedQuantity": { "value": 2, "unit": "PIECE" }, "chargedQuantity": { "value": 2, "unit": "PIECE" }, "unitPricePaise": 65000,
      "preparationChargePaise": 0, "estimatedTotalPaise": 130000, "finalTotalPaise": 130000, "status": "ACTIVE"
    }
  ],
  "estimate": { "itemsPaise": 239900, "preparationPaise": 5000, "discountPaise": 10000, "deliveryFeePaise": 0, "taxPaise": 0, "totalPaise": 234900 },
  "final": { "itemsPaise": 249591, "preparationPaise": 5460, "discountPaise": 10000, "deliveryFeePaise": 0, "taxPaise": 0, "totalPaise": 245051 },
  "amountDuePaise": 245051, "amountPaidPaise": 0, "amountRefundedPaise": 0, "couponCode": "FRESH100", "substitutions": [],
  "payment": { "method": "COD", "status": "PENDING", "nextAction": null }, "invoice": null,
  "allowedActions": { "cancel": false, "raiseComplaint": false, "downloadInvoice": false, "reorder": true }
}
```
`final` is `null` until `PACKED`. `allowedActions` is server-computed; clients show buttons from it.

**GET /api/v1/orders/{orderId}/tracking** → 200
```json
{
  "orderNumber": "FO-260001", "status": "OUT_FOR_DELIVERY",
  "timeline": [
    { "status": "CONFIRMED", "at": "2026-10-23T05:41:12Z" }, { "status": "PREPARING", "at": "2026-10-23T07:02:40Z" },
    { "status": "PACKED", "at": "2026-10-23T07:48:05Z" }, { "status": "OUT_FOR_DELIVERY", "at": "2026-10-23T08:20:31Z" }
  ],
  "slot": { "startAt": "2026-10-23T08:30:00Z", "endAt": "2026-10-23T10:30:00Z" },
  "shipment": {
    "status": "OUT_FOR_DELIVERY", "riderName": "Karthik S", "riderPhone": "+919812345678", "dispatchedAt": "2026-10-23T08:20:31Z", "coldBoxUsed": true,
    "attempts": []
  },
  "deliveryCode": "4821", "amountDuePaise": 245051
}
```
`deliveryCode` shown only while `OUT_FOR_DELIVERY`; rider enters it on delivery.

### 4.9 Payments and refunds (customer)

| Method | Path | Auth | Notes | Purpose |
|---|---|---|---|---|
| POST | `/api/v1/orders/{orderId}/payments` | customer | IK, rate `checkout` | Initiate online payment; returns provider checkout params (`paymentId`, `provider`, `providerOrderId`, `amountPaise`, `keyId`, `expiresAt`); 201 new attempt, 200 when a `PENDING` attempt for the same amount is still valid for > 2 min (reused); 422 `PAYMENT_NOT_REQUIRED` for COD / already paid; 409 `INVALID_STATUS_TRANSITION` unless the order is `PENDING_PAYMENT`; 404 `ORDER_NOT_FOUND` for another customer's order |
| GET | `/api/v1/orders/{orderId}/payments` | customer | | Array of attempts (`id`, `method`, `provider`, `status` `PENDING`/`AUTHORIZED`/`CAPTURED`/`COLLECTED`/`FAILED`/`CANCELLED`, `amountPaise`, `providerOrderId`, `expiresAt`, `capturedAt`, `collectedAt`) |
| GET | `/api/v1/orders/{orderId}/refunds` | customer | | Array of refunds (`id`, `amountPaise`, `reasonCode`, `method`, `status`, `requestedAt`, `succeededAt`); no staff fields |
| POST | `/api/v1/payments/webhooks/{provider}` | public (signature) | §7 | Provider callback |
| POST | `/api/v1/dev/payments/fake/{providerOrderId}/simulate` | public, Development/Testing only | | Fake gateway: `{ "outcome": "CAPTURED"/"AUTHORIZED"/"FAILED", "amountPaise"? }` → sends a signed webhook through the normal path |

Client return from the gateway never changes status; the client polls `GET /orders/{orderId}` (status comes from the webhook).
An attempt expires `Payments:OnlineAttemptMinutes` (default 15) after creation until Ordering exposes the order's payment deadline.

### 4.10 Notifications (customer)

| Method | Path | Auth | Notes | Purpose |
|---|---|---|---|---|
| GET | `/api/v1/me/notifications` | customer | | In-app messages, `unreadOnly` filter |
| POST | `/api/v1/me/notifications/{messageId}/read` | customer | | Mark read |
| POST | `/api/v1/me/notifications/read-all` | customer | | Mark all read |
| PUT | `/api/v1/me/device-tokens` | customer | | Register a push token `{ pushToken, provider: "FCM", platform: ANDROID/IOS/WEB }` (Ruling D28); re-registering moves the token to the caller |
| DELETE | `/api/v1/me/device-tokens/{token}` | customer | | Unregister |
| GET | `/api/v1/me/notification-preferences` | customer | | Preferences per category × channel |
| PUT | `/api/v1/me/notification-preferences` | customer | | Replace preferences (`ORDER_UPDATES` SMS cannot be disabled) |

### 4.11 Admin catalog

| Method | Path | Auth | Notes | Purpose |
|---|---|---|---|---|
| GET | `/api/v1/admin/categories` | catalog.manage | | Category tree incl. inactive |
| GET | `/api/v1/admin/categories/{id}` | catalog.manage | ETag | Category detail |
| POST | `/api/v1/admin/categories` | catalog.manage | | Create |
| PUT | `/api/v1/admin/categories/{id}` | catalog.manage | IM | Update / activate / deactivate |
| GET | `/api/v1/admin/products` | catalog.manage | | List: `q`, `categoryId`, `status` |
| POST | `/api/v1/admin/products` | catalog.manage | | Create (`DRAFT`) |
| GET | `/api/v1/admin/products/{id}` | catalog.manage | ETag | Detail with variants, preparations, images |
| PUT | `/api/v1/admin/products/{id}` | catalog.manage | IM | Update product fields |
| POST | `/api/v1/admin/products/{id}/publish` | catalog.manage | IM | `DRAFT` → `ACTIVE` (needs ≥1 active variant with default preparation and image) |
| POST | `/api/v1/admin/products/{id}/archive` | catalog.manage | IM | → `ARCHIVED` |
| POST | `/api/v1/admin/products/{id}/restore` | catalog.manage | IM | `ARCHIVED` → `DRAFT` |
| POST | `/api/v1/admin/products/{id}/variants` | catalog.manage | IM (product) | Add variant |
| PUT | `/api/v1/admin/products/{id}/variants/{variantId}` | catalog.manage | IM (product) | Update variant, preparations with `yieldBp` and `isDefault` |
| POST | `/api/v1/admin/products/{id}/images` | catalog.manage | IM (product), multipart `file` | Upload image (D21: local disk via `IFileStorage`, served at `/media/{key}`); stores 320/640/1280 px WebP + original under `products/{productId}/{imageId}/` |
| PUT | `/api/v1/admin/products/{id}/images/order` | catalog.manage | IM | Reorder images |
| DELETE | `/api/v1/admin/products/{id}/images/{imageId}` | catalog.manage | IM | Remove image |
| GET | `/api/v1/admin/preparation-methods` | catalog.manage | | Reference list |
| GET/POST/PUT | `/api/v1/admin/promotions[/{id}]` | catalog.manage | IM on PUT | Home banners (Promotions module; `/home` returns `banners: []` until it ships) |

### 4.12 Admin pricing

| Method | Path | Auth | Notes | Purpose |
|---|---|---|---|---|
| GET | `/api/v1/admin/pricing/prices` | pricing.manage | | Current + future prices: `fulfillmentLocationId` (required), `variantId`, `q`, `missingOnly=true` |
| GET | `/api/v1/admin/pricing/locations/{locationId}/variants/{variantId}/price` | pricing.manage | ETag | Current price and history |
| PUT | `/api/v1/admin/pricing/locations/{locationId}/variants/{variantId}/price` | pricing.manage | IM, or `If-None-Match: *` when none | Set price: closes current period at `validFrom`, opens new one |
| DELETE | `/api/v1/admin/pricing/locations/{locationId}/variants/{variantId}/price` | pricing.manage | IM | End the current period now (stop selling) |
| GET/PUT | `/api/v1/admin/pricing/locations/{locationId}/variants/{variantId}/preparations/{code}/charge` | pricing.manage | IM / If-None-Match | Preparation charge, same period rules |
| GET | `/api/v1/admin/pricing/tax-rules` | pricing.manage | | Tax rules by category |
| POST | `/api/v1/admin/pricing/tax-rules` | pricing.manage | | New period for a tax category (`taxCategoryCode`, `rateBp` 0–2800, `validFrom`, `replaceFuture`); no `If-Match`: tax rules are global and periods do not overlap |
| POST | `/api/v1/admin/pricing/prices/bulk` | pricing.manage | IK | CSV/JSON bulk set; all-or-nothing, returns per-row errors; `?dryRun=true` validates only; max 5000 rows. JSON `{ validFrom?, replaceFuture?, rows: [{ kind: PRICE\|PREP_CHARGE, locationCode, sku, unitPricePaise, compareAtPaise, preparationCode, chargePaise, chargeBasis, validFrom? }] }` or `text/csv` with the same column names |

**PUT …/locations/{locationId}/variants/{variantId}/price** (`If-Match: "55102"`) → 200
```json
{ "unitPricePaise": 124900, "compareAtPaise": 149900, "validFrom": "2026-10-24T00:30:00Z", "reason": "Morning auction rate dropped" }
```
```json
{
  "fulfillmentLocationId": "0192b400-0002-7000-8000-000000000002", "variantId": "0192b411-0001-7000-8000-000000000201", "version": "55187",
  "current": {
    "id": "0192b700-0001-7000-8000-000000000701", "unitPricePaise": 129900, "compareAtPaise": 149900, "validFrom": "2026-10-20T00:30:00Z",
    "validTo": "2026-10-24T00:30:00Z"
  },
  "next": {
    "id": "0192b700-0002-7000-8000-000000000702", "unitPricePaise": 124900, "compareAtPaise": 149900, "validFrom": "2026-10-24T00:30:00Z",
    "validTo": null
  }
}
```
`validFrom` omitted = now. Errors: `PRICE_IN_PAST`, `PRICE_PERIOD_OVERLAP` (a future period exists; send `replaceFuture: true`), `CONCURRENCY_CONFLICT`, `LOCATION_ACCESS_DENIED`. Sending the values already current (or already scheduled at the same `validFrom`) is a no-op 200.

### 4.13 Admin promotions

| Method | Path | Auth | Notes | Purpose |
|---|---|---|---|---|
| GET | `/api/v1/admin/coupons` | promotions.manage | | List: `status`, `cityId`, `q` |
| POST | `/api/v1/admin/coupons` | promotions.manage | | Create |
| GET | `/api/v1/admin/coupons/{id}` | promotions.manage | ETag | Detail with scopes, `usedCount` |
| PUT | `/api/v1/admin/coupons/{id}` | promotions.manage | IM | Update (code immutable once used) |
| POST | `/api/v1/admin/coupons/{id}/pause` \| `/resume` | promotions.manage | IM | Status change |
| GET | `/api/v1/admin/coupons/{id}/redemptions` | promotions.manage | | Redemptions |

### 4.14 Admin inventory

| Method | Path | Auth | Notes | Purpose |
|---|---|---|---|---|
| GET | `/api/v1/admin/inventory/stock` | inventory.view | | Stock items: `fulfillmentLocationId`, `q`, `lowStockOnly`; returns `onHand`, `reserved`, `available` with unit |
| GET | `/api/v1/admin/inventory/stock/{stockItemId}` | inventory.view | ETag | Detail with active batches |
| PUT | `/api/v1/admin/inventory/stock/{stockItemId}/threshold` | inventory.adjust | IM | Low-stock threshold |
| POST | `/api/v1/admin/inventory/stock-items` | inventory.adjust | | Create stock item (location × variant) |
| POST | `/api/v1/admin/inventory/receipts` | inventory.receive | IK | Receive stock: batch + RECEIPT movement |
| GET | `/api/v1/admin/inventory/batches` | inventory.view | | `fulfillmentLocationId`, `variantId`, `status`, `expiringBefore` |
| POST | `/api/v1/admin/inventory/batches/{batchId}/discard` | inventory.adjust | IM (stock item) | Discard remaining (WASTE movement) |
| POST | `/api/v1/admin/inventory/waste` | inventory.adjust | IK | Record waste (FEFO or explicit batch; reason SPOILED/TEMPERATURE_BREACH/DAMAGED/EXPIRED/QC_REJECTED) |
| POST | `/api/v1/admin/inventory/adjustments` | inventory.adjust | IM (stock item) | ADJUSTMENT_IN/OUT with reason |
| POST | `/api/v1/admin/inventory/counts` | inventory.adjust | IM (stock item) | Physical count → COUNT_CORRECTION |
| GET | `/api/v1/admin/inventory/movements` | inventory.view | | Movement ledger: `stockItemId`, `kind`, `fromDate`, `toDate` |
| GET | `/api/v1/admin/inventory/reservations` | inventory.view | | Active reservations |
| GET | `/api/v1/admin/inventory/cold-chain` | inventory.view | | Logs: `fulfillmentLocationId`, `outOfRangeOnly` |
| POST | `/api/v1/admin/inventory/cold-chain` | inventory.adjust | | Record temperature |

Notes (E3, implemented):
- `GET /availability` (§4.5) is served by Catalog (D3), not Inventory. `GET /stock` has no `q` filter yet (Catalog names are not in the inventory schema); filter by `fulfillmentLocationId`, `variantId`, `lowStockOnly`.
- Quantities in request bodies are `{ "value": int, "unit": "GRAM" | "PIECE" }` and must match the stock item's unit.
- Bodies: stock-items `{fulfillmentLocationId, variantId, lowStockThreshold}`; threshold `{lowStockThreshold}`; discard `{reasonCode, notes}`; waste `{stockItemId, batchId?, quantity, reasonCode, notes}`; adjustments `{stockItemId, kind: ADJUSTMENT_IN|ADJUSTMENT_OUT|RETURN, batchId?, quantity, reasonCode, notes}` (`RETURN` needs `batchId`); counts `{stockItemId, countedQuantity, batchId?, notes}`; cold-chain `{fulfillmentLocationId, storageUnit, temperatureC, recordedAt?}`.
- Adjustment reasons: `DATA_ENTRY_ERROR`, `FOUND`, `LOST`, `THEFT`, `TRANSFER`, `OTHER` (notes required for `OTHER`).
- `GET /batches` items carry `stockItemId` and `stockItemVersion` (the If-Match value for discard/adjust/count) and also accept a `stockItemId` filter. `GET /movements` also accepts `fulfillmentLocationId`; `GET /reservations` accepts `fulfillmentLocationId`, `orderId`, `status`.
- Storage units and their ranges are reference data (`data/reference/inventory/storage-units.json`); the receipt check uses the `RECEIPT` unit.

**POST /api/v1/admin/inventory/receipts** (`Idempotency-Key: 2b7e…`) → 201
```json
{
  "fulfillmentLocationId": "0192b400-0002-7000-8000-000000000002", "variantId": "0192b411-0001-7000-8000-000000000201", "batchCode": "SEER-261023-01",
  "supplierName": "Murugan Fisheries", "sourceLandingCenter": "Kasimedu Fishing Harbour", "caughtOrHarvestedOn": "2026-10-23",
  "receivedQuantity": { "value": 24500, "unit": "GRAM" }, "totalCostPaise": 2131500, "bestBeforeAt": "2026-10-25T00:30:00Z",
  "receiptTemperatureC": 1.5
}
```
```json
{
  "batch": {
    "id": "0192b710-0001-7000-8000-000000000801", "batchCode": "SEER-261023-01", "status": "ACTIVE",
    "receivedQuantity": { "value": 24500, "unit": "GRAM" }, "remainingQuantity": { "value": 24500, "unit": "GRAM" }, "totalCostPaise": 2131500,
    "bestBeforeAt": "2026-10-25T00:30:00Z"
  },
  "movement": { "id": "0192b710-0002-7000-8000-000000000802", "kind": "RECEIPT", "quantity": { "value": 24500, "unit": "GRAM" }, "costPaise": 2131500 },
  "stockItem": {
    "id": "0192b720-0001-7000-8000-000000000901", "version": "77213", "onHand": { "value": 31200, "unit": "GRAM" },
    "reserved": { "value": 1430, "unit": "GRAM" }, "available": { "value": 29770, "unit": "GRAM" }
  }
}
```
Receipt temperature above 4.0 °C is accepted but also writes an out-of-range cold-chain log and flags `temperatureWarning: true`.

### 4.15 Admin orders

| Method | Path | Auth | Notes | Purpose |
|---|---|---|---|---|
| GET | `/api/v1/admin/orders` | orders.view | | List: `fulfillmentLocationId`, `status`, `slotDate`, `slotId`, `paymentMethod`, `q` (number/phone) |
| GET | `/api/v1/admin/orders/{orderId}` | orders.view | ETag | Detail incl. gross reserved quantities, batches, history, adjustments |
| GET | `/api/v1/admin/orders/pick-list?fulfillmentLocationId=&slotId=` | orders.pack | | Aggregated gross quantities per variant × preparation for a slot |
| POST | `/api/v1/admin/orders/{orderId}/start-preparing` | orders.pack | IM | `CONFIRMED` → `PREPARING` |
| POST | `/api/v1/admin/orders/{orderId}/pack` | orders.pack | IM | Weigh and pack all lines → `PACKED` |
| POST | `/api/v1/admin/orders/{orderId}/items/{itemId}/substitutions` | orders.pack | IM | Propose substitution (customer has 15 min; `AUTO_SIMILAR` auto-accepts) |
| POST | `/api/v1/admin/orders/{orderId}/substitutions/{substitutionId}/record-response` | orders.pack | IM | Record answer taken by phone (`CALL_ME`) |
| POST | `/api/v1/admin/orders/{orderId}/cancel` | orders.cancel | IM | Cancel with `reasonCode` (`OUT_OF_STOCK`, `CUSTOMER_REQUEST`, `ADDRESS_ISSUE`, `QUALITY_ISSUE`, `OTHER`) |
| POST | `/api/v1/admin/orders/{orderId}/goodwill` | refunds.request | IM | Goodwill adjustment (negative amount) |
| GET | `/api/v1/admin/orders/{orderId}/invoice` | orders.view | | Invoice + PDF URL |
| GET | `/api/v1/admin/complaints` | complaints.manage | | List: `status`, `kind`, `fulfillmentLocationId` |
| GET | `/api/v1/admin/complaints/{complaintId}` | complaints.manage | ETag | Detail with attachment URLs |
| POST | `/api/v1/admin/complaints/{complaintId}/decide` | complaints.manage | IM | `APPROVED`/`REJECTED`, `resolution` `REFUND`/`REPLACEMENT`/`NONE`, `refundAmountPaise` (creates refund request) |
| POST | `/api/v1/admin/complaints/{complaintId}/resolve` | complaints.manage | IM | → `RESOLVED` |

**POST /api/v1/admin/orders/{orderId}/pack** (`If-Match: "918201"`) → 200
```json
{
  "lines": [
    { "orderItemId": "0192b600-0101-7000-8000-000000000601", "packedQuantity": { "value": 540, "unit": "GRAM" } },
    { "orderItemId": "0192b600-0102-7000-8000-000000000602", "packedQuantity": { "value": 580, "unit": "GRAM" } },
    { "orderItemId": "0192b600-0103-7000-8000-000000000603", "packedQuantity": { "value": 2, "unit": "PIECE" } }
  ],
  "coldBoxUsed": true
}
```
```json
{
  "orderId": "0192b600-0001-7000-8000-000000000501", "status": "PACKED", "version": "918273",
  "lines": [
    {
      "orderItemId": "0192b600-0101-7000-8000-000000000601", "packed": { "value": 540, "unit": "GRAM" }, "charged": { "value": 540, "unit": "GRAM" },
      "capped": false, "finalTotalPaise": 72306, "adjustmentPaise": 5356, "status": "ACTIVE"
    },
    {
      "orderItemId": "0192b600-0102-7000-8000-000000000602", "packed": { "value": 580, "unit": "GRAM" }, "charged": { "value": 550, "unit": "GRAM" },
      "capped": true, "finalTotalPaise": 52745, "adjustmentPaise": 4795, "status": "ACTIVE"
    },
    {
      "orderItemId": "0192b600-0103-7000-8000-000000000603", "packed": { "value": 2, "unit": "PIECE" }, "charged": { "value": 2, "unit": "PIECE" },
      "capped": false, "finalTotalPaise": 130000, "adjustmentPaise": 0, "status": "ACTIVE"
    }
  ],
  "finalTotalPaise": 245051, "amountDuePaise": 245051, "refundDuePaise": 0
}
```
| Pack rule | Value |
|---|---|
| Lines | Every `ACTIVE` line must appear once; `packedQuantity` 0 → `SHORT_SUPPLIED` |
| Charge | `charged = min(packed, maxChargeable)`; price and preparation charge use `charged` |
| Shortfall | COD/UPI on delivery: `amountDuePaise` reduced; prepaid: `refundDuePaise` > 0, Payments handler creates `WEIGHT_SHORTFALL` / `SHORT_SUPPLY` refund |
| Stock | Reservation consumed; SALE movements FEFO by gross `ceil(packed × 10000 / yieldBp)` |
| Pending substitution | 409 `INVALID_STATUS_TRANSITION` until answered or expired |

Implementation notes (Phase 2, Cart / Promotions / Ordering):
- Every endpoint in §4.6, §4.7, §4.8 (except the Payments-owned `/payments` and `/refunds` rows) §4.13 coupons and §4.15 is implemented. `GET /api/v1/promotions` (public banners, LOC optional) and `GET/POST/PUT /api/v1/admin/promotions[/{id}]` are implemented in Promotions.
- `GET .../invoice` returns the frozen invoice with `pdfUrl: null`: PDF rendering is deferred to the `InvoiceIssued` consumer. Invoice before delivery → 404 `INVOICE_NOT_ISSUED`.
- Complaints are created from JSON (`orderItemId`, `kind`, `description`); attachment upload is not implemented yet.
- Staff order access outside the staff member's locations → 403 `LOCATION_ACCESS_DENIED`; lists are filtered to the staff member's locations. Customer access to another customer's order → 404 `NOT_FOUND` (not visible).
- `PRICE_CHANGED` (409) is stored by the idempotency record with its `preview`; confirming the new total needs a new `Idempotency-Key`.
- Unpaid ONLINE orders and PROPOSED substitutions are expired by minute sweeps (`ordering.expire-unpaid-orders`, `ordering.expire-substitutions`) instead of per-order delayed jobs.
- The prepaid cap (charge never above the paid amount) is applied as a cap waiver inside the final discount, allocated over the lines, so invoice lines add up to the charged total.
- Not written to the audit log yet (Audit is not in Ordering's Public API uses); rejected out-of-order Delivery events are logged as warnings.

### 4.16 Admin payments and refunds

| Method | Path | Auth | Notes | Purpose |
|---|---|---|---|---|
| GET | `/api/v1/admin/payments` | payments.view | | List: `status`, `method`, `provider`, `fromDate`, `toDate`, `fulfillmentLocationId`, `page`, `pageSize` (location-scoped) |
| GET | `/api/v1/admin/payments/{paymentId}` | payments.view | | One payment with `summary` (`paidPaise`, `refundedPaise`, `pendingRefundPaise`, `refundablePaise`); ETag |
| GET | `/api/v1/admin/orders/{orderId}/payments` | payments.view | | `{ orderId, payments[], refunds[] }` |
| GET | `/api/v1/admin/webhook-events` | payments.view | all-locations staff only | Received events: `provider`, `signatureValid`, `status` `RECEIVED`/`PROCESSED`/`IGNORED`/`ERROR`/`REJECTED`, `fromDate`, `toDate` |
| POST | `/api/v1/admin/webhook-events/{id}/reprocess` | payments.manage (D15) | | Retry a valid event in `ERROR` |
| POST | `/api/v1/admin/orders/{orderId}/refunds` | refunds.request | IK | Create refund request (`REQUESTED`); ETag = payment version |
| GET | `/api/v1/admin/refunds` | payments.view | | List: `status`, `reasonCode`, `fromDate`, `toDate`, `fulfillmentLocationId` |
| GET | `/api/v1/admin/refunds/{refundId}` | payments.view | | One refund; ETag = version of its payment (refunds change through the payment row) |
| POST | `/api/v1/admin/refunds/{refundId}/approve` | refunds.approve | IM | `REQUESTED` → `APPROVED`; Worker calls provider → `PROCESSING` → `SUCCEEDED`/`FAILED` |
| POST | `/api/v1/admin/refunds/{refundId}/reject` | refunds.approve | IM | → `FAILED` with `reason` |
| POST | `/api/v1/admin/refunds/{refundId}/mark-paid` | refunds.approve | IM | Manual UPI/bank/cash refund done (`reference`) → `SUCCEEDED` |
| GET | `/api/v1/admin/cash-settlements` | cash.settle | | List: `riderId`, `date`, `status`, `fromDate`, `toDate`, `fulfillmentLocationId` |
| GET | `/api/v1/admin/cash-settlements/{id}` | cash.settle | | One settlement; ETag |
| POST | `/api/v1/admin/cash-settlements` | cash.settle | IK | Settle rider cash for a date (`riderId`, `date` local, `receivedPaise`, `note`); server computes expected and difference; difference 0 → `SETTLED`, else `OPEN` + `CASH_DIFFERENCE` issue; future date 422 `DATE_IN_FUTURE` |
| POST | `/api/v1/admin/cash-settlements/{id}/dispute` | cash.settle | IM | → `DISPUTED` (`reason`) |
| POST | `/api/v1/admin/cash-settlements/{id}/settle` | cash.settle | IM | `OPEN`/`DISPUTED` → `SETTLED` (`note`); with a difference another staff than the recorder (`SETTLEMENT_SELF_RESOLUTION`) |
| GET | `/api/v1/admin/payment-issues` | payments.view | | Reconciliation issues: `kind`, `status` (`OPEN`/`AUTO_RESOLVED`/`RESOLVED`), location-scoped (issues without a location only for all-locations staff) |
| POST | `/api/v1/admin/payment-issues/{id}/resolve` | payments.manage | IM | `OPEN` → `RESOLVED` (`note`) |

**POST /api/v1/admin/orders/{orderId}/refunds** (`Idempotency-Key: 9c0d…`) → 201
```json
{
  "amountPaise": 20000, "reasonCode": "COMPLAINT", "method": "UPI", "complaintId": "0192b800-0001-7000-8000-000000000a01",
  "upiId": "priya.raman@okicici", "notes": "Two prawns spoiled on arrival; photo verified."
}
```
```json
{
  "id": "0192b810-0001-7000-8000-000000000b01", "orderId": "0192b600-0001-7000-8000-000000000501", "paymentId": "0192b650-0001-7000-8000-000000000c01",
  "amountPaise": 20000, "reasonCode": "COMPLAINT", "method": "UPI", "status": "REQUESTED", "requestedBy": "Anitha (Support)",
  "requestedAt": "2026-10-23T11:05:00Z", "version": "1201",
  "paymentSummary": { "paidPaise": 245051, "refundedPaise": 0, "pendingRefundPaise": 20000, "refundablePaise": 225051 }
}
```
Rules: `amountPaise` ≤ paid/collected − refunded − pending → else 422 `REFUND_EXCEEDS_PAYMENT`; `method: ORIGINAL` only for online payments; approver ≠ requester (`REFUND_SELF_APPROVAL`).

### 4.17 Admin delivery

| Method | Path | Auth | Notes | Purpose |
|---|---|---|---|---|
| GET | `/api/v1/admin/delivery/slot-templates` | delivery.manage | | By `fulfillmentLocationId` |
| POST | `/api/v1/admin/delivery/slot-templates` | delivery.manage | | Create (`dayOfWeek`, `startTime`, `endTime`, `capacity`, `cutoffMinutes`) |
| PUT | `/api/v1/admin/delivery/slot-templates/{id}` | delivery.manage | IM | Update (future slots regenerate; booked kept) |
| GET | `/api/v1/admin/delivery/slots` | delivery.manage | | Slots with `bookedCount`: `fulfillmentLocationId`, `fromDate`, `toDate` |
| PATCH | `/api/v1/admin/delivery/slots/{slotId}` | delivery.manage | IM | Change capacity, `CLOSED`/`OPEN` |
| GET/POST | `/api/v1/admin/delivery/riders` | delivery.manage | | Riders |
| PUT | `/api/v1/admin/delivery/riders/{riderId}` | delivery.manage | IM | Update / deactivate |
| GET | `/api/v1/admin/delivery/shipments` | delivery.manage | | `fulfillmentLocationId`, `slotId`, `status`, `riderId` |
| POST | `/api/v1/admin/delivery/shipments/{shipmentId}/assign` | delivery.manage | IM | Assign rider |
| POST | `/api/v1/admin/delivery/shipments/{shipmentId}/dispatch` | delivery.manage | IM | Order `PACKED` → `OUT_FOR_DELIVERY` (`dispatchTemperatureC`) |
| POST | `/api/v1/admin/delivery/shipments/{shipmentId}/deliver` | delivery.manage | IM | `deliveryCode`, optional `cashCollectedPaise` → `DELIVERED` + invoice |
| POST | `/api/v1/admin/delivery/shipments/{shipmentId}/attempts` | delivery.manage | IM | Failed attempt (`outcome`, `notes`) → `DELIVERY_FAILED` |
| POST | `/api/v1/admin/delivery/shipments/{shipmentId}/reattempt` | delivery.manage | IM | `DELIVERY_FAILED` → `OUT_FOR_DELIVERY` |
| POST | `/api/v1/admin/delivery/shipments/{shipmentId}/return` | delivery.manage | IM | `DELIVERY_FAILED` → `RETURNED` |
| POST | `/api/v1/admin/delivery/shipments/{shipmentId}/cash-collected` | delivery.manage | IK | Record COD / UPI-on-delivery collection (`amountPaise`, `mode` `CASH`/`UPI`, `upiReference`) when not given at deliver. Implemented by Payments (D14): order must be `OUT_FOR_DELIVERY`/`DELIVERED` (else 409), `amountPaise` must equal the amount due (422 `CASH_AMOUNT_MISMATCH`), second record 409 `CASH_ALREADY_COLLECTED`; returns the payment |
| GET | `/api/v1/admin/delivery/shipments/{shipmentId}` | delivery.manage | ETag | Shipment detail with attempts (A9); source of the shipment `If-Match` |

Delivery request bodies (Phase 2, D13). Every list item and command response carries `version`; commands return the
updated resource and its `ETag`. Staff outside the resource's location → 403 `LOCATION_ACCESS_DENIED`; lists are filtered.

| Endpoint | Body | Response |
|---|---|---|
| POST `slot-templates` | `{ fulfillmentLocationId, dayOfWeek (1=Mon…7), startTime "HH:mm", endTime "HH:mm", capacity 1–500, cutoffMinutes 0–2880 }` (15-minute boundaries) | 201 template |
| PUT `slot-templates/{id}` | `{ dayOfWeek, startTime, endTime, capacity, cutoffMinutes, isActive }` | 200 template; future slots follow via `SlotTemplateChanged` (seconds of lag) |
| GET `slots` | query `fulfillmentLocationId`, `fromDate`, `toDate`, `page`, `pageSize` | items `{ id, fulfillmentLocationId, slotTemplateId, slotDate, startAt, endAt, cutoffAt, capacity, bookedCount, status OPEN/CLOSED, closedReason MANUAL/CLOSURE/TEMPLATE_INACTIVE, displayStatus, version }` |
| PATCH `slots/{slotId}` | `{ capacity?: 0–500, status?: "OPEN" \| "CLOSED" }` (at least one) | 200 slot; `SLOT_CAPACITY_BELOW_BOOKED`, `SLOT_NOT_AVAILABLE` (open on a closure date) |
| POST `riders` | `{ fulfillmentLocationId, fullName 1–120, phone E.164 }` | 201 rider; 409 `DUPLICATE` (`errors.phone`) |
| PUT `riders/{riderId}` | `{ fullName, phone, isActive }` | 200 rider; 409 `RIDER_HAS_ACTIVE_SHIPMENTS` |
| GET `shipments` | query `fulfillmentLocationId`, `slotId`, `riderId`, `status` (repeatable), `page`, `pageSize` | items as shipment detail |
| POST `shipments/{id}/assign` | `{ riderId }` | 200 shipment; 422 `RIDER_NOT_AVAILABLE`; 409 `INVALID_STATUS_TRANSITION` |
| POST `shipments/{id}/dispatch` | `{ dispatchTemperatureC: -40.0…40.0 }` | 200 shipment (`temperatureWarning` above 4.0 °C); 409 `SHIPMENT_NOT_DISPATCHABLE` |
| POST `shipments/{id}/deliver` | `{ deliveryCode "1234", cashCollectedPaise?, cashMode? CASH/UPI (required with cash), upiReference? (required for UPI) }` (A7) | 200 shipment; 422 `DELIVERY_CODE_INVALID` (failure counted and saved), `DELIVERY_CODE_LOCKED`; 409 `CASH_ALREADY_COLLECTED` |
| POST `shipments/{id}/attempts` | `{ outcome CUSTOMER_UNAVAILABLE/ADDRESS_NOT_FOUND/REFUSED/OTHER, notes? (required for OTHER, ≤ 500) }` | 200 shipment (FAILED) |
| POST `shipments/{id}/reattempt` | — | 200 shipment; 422 `REATTEMPT_NOT_ALLOWED` |
| POST `shipments/{id}/return` | — | 200 shipment (RETURNED) |

Shipment detail: `{ id, orderId, fulfillmentLocationId, deliverySlotId, riderId, status, placedAt, packedAt, coldBoxUsed,
dispatchTemperatureC, temperatureWarning, dispatchedAt, deliveredAt, returnedAt, cancelledAt, deliveryCodeFailures,
deliveryCodeLocked, cashCollectedPaise, cashMode, upiReference, cashCollectedAt, attempts[{ attemptNo, outcome, notes,
attemptedAt }], version }`. The delivery code is never shown to staff; the customer sees it through order tracking while
`OUT_FOR_DELIVERY`.

### 4.18 Admin cities, locations, pincodes, schedules

| Method | Path | Auth | Notes | Purpose |
|---|---|---|---|---|
| GET | `/api/v1/admin/cities` | locations.manage | | All cities with status |
| POST | `/api/v1/admin/cities` | locations.manage | | Create (`DRAFT`) |
| GET | `/api/v1/admin/cities/{cityId}` | locations.manage | ETag | Detail + readiness checklist |
| PUT | `/api/v1/admin/cities/{cityId}` | locations.manage | IM | Update name, time zone |
| GET | `/api/v1/admin/cities/{cityId}/readiness` | locations.manage | | Same checks as activation, no write |
| POST | `/api/v1/admin/cities/{cityId}/activate` | cities.activate | IM | `DRAFT`/`PAUSED` → `ACTIVE` |
| POST | `/api/v1/admin/cities/{cityId}/pause` | cities.activate | IM | → `PAUSED` (no new orders; existing continue) |
| POST | `/api/v1/admin/cities/{cityId}/retire` | cities.activate | IM | → `RETIRED` (refused with open orders) |
| GET/POST | `/api/v1/admin/fulfillment-locations` | locations.manage | | List (`cityId`) / create |
| GET/PUT | `/api/v1/admin/fulfillment-locations/{id}` | locations.manage | ETag / IM | Detail / update incl. `status` |
| PUT | `/api/v1/admin/fulfillment-locations/{id}/operating-hours` | locations.manage | IM | Replace 7-day hours |
| GET/POST | `/api/v1/admin/fulfillment-locations/{id}/closures` | locations.manage | | Closures (slots on that date close) |
| DELETE | `/api/v1/admin/fulfillment-locations/{id}/closures/{closureId}` | locations.manage | | Remove future closure |
| GET/POST | `/api/v1/admin/service-areas` | locations.manage | | List / create (fees, min order) |
| PUT | `/api/v1/admin/service-areas/{id}` | locations.manage | IM | Update fees, `isActive` |
| GET | `/api/v1/admin/service-areas/{id}/pincodes` | locations.manage | | Pincodes |
| POST | `/api/v1/admin/service-areas/{id}/pincodes` | locations.manage | IM (service area) | Add pincodes (`["600041","600020"]`) |
| DELETE | `/api/v1/admin/service-areas/{id}/pincodes/{pincode}` | locations.manage | IM (service area) | Deactivate pincode |

**POST /api/v1/admin/cities/{cityId}/activate** (`If-Match: "3310"`) → 200
```json
{
  "id": "0192b400-0001-7000-8000-000000000001", "code": "CHENNAI", "status": "ACTIVE", "activatedAt": "2026-10-23T04:00:00Z",
  "activatedBy": "Mosa (Admin)", "version": "3342"
}
```
Refused → 422
```json
{
  "type": "https://api.freshorbit.in/problems/city-not-ready", "title": "City cannot be activated.", "status": 422,
  "detail": "Coimbatore is missing required configuration.", "code": "CITY_NOT_READY",
  "missing": [
    { "code": "SLOT_TEMPLATE_MISSING", "fulfillmentLocationId": "0192b401-0002-7000-8000-000000000012", "message": "No active slot template." },
    { "code": "PRICE_MISSING", "fulfillmentLocationId": "0192b401-0002-7000-8000-000000000012", "message": "No variant has a current price." },
    { "code": "STOCK_ITEM_MISSING", "fulfillmentLocationId": "0192b401-0002-7000-8000-000000000012", "message": "No stock item exists." }
  ],
  "errors": {}
}
```
`missing[].code`: `FULFILLMENT_LOCATION_MISSING`, `SERVICE_AREA_MISSING`, `PINCODE_MISSING`, `SLOT_TEMPLATE_MISSING`, `PRICE_MISSING`, `STOCK_ITEM_MISSING`.

### 4.19 Admin customers

| Method | Path | Auth | Notes | Purpose |
|---|---|---|---|---|
| GET | `/api/v1/admin/customers` | customers.view | | List: `q` (phone, name), `status`, `cityId` |
| GET | `/api/v1/admin/customers/{customerId}` | customers.view | ETag | Profile, addresses, order counts, lifetime value |
| GET | `/api/v1/admin/customers/{customerId}/orders` | customers.view + orders.view | | Orders |
| POST | `/api/v1/admin/customers/{customerId}/block` \| `/unblock` | customers.manage | IM | User `DISABLED` / `ACTIVE` (revokes refresh tokens) |

### 4.20 Reports

All `GET`, permission `reports.view`, params `fulfillmentLocationId`, `cityId`, `fromDate`, `toDate`; `format=csv` returns `text/csv`. Data refreshes every 15 min (`asOf` in response).

| Path | Purpose |
|---|---|
| `/api/v1/admin/reports/daily-sales` | Orders, gross, discounts, delivery fees, net revenue per day |
| `/api/v1/admin/reports/product-performance` | Quantity and revenue per variant |
| `/api/v1/admin/reports/contribution-margin` | Revenue − SALE cost − refunds |
| `/api/v1/admin/reports/waste` | Waste quantity and cost by reason |
| `/api/v1/admin/reports/repeat-customers` | Repeat rate, cohorts |
| `/api/v1/admin/reports/dashboard` | Today's tiles: orders by status, slot fill, low stock, open complaints |

Report-specific filters: product-performance `variantId`, `categoryId`, `preparationCode`; waste `reasonCode`, `variantId`; repeat-customers `cohortMonths` (1–12, default 6). Also `page`, `pageSize` (≤ 100), `sort` (a column, `-` for descending; default `-businessDate`).

Response (daily-sales, product-performance, contribution-margin, waste; repeat-customers uses the same envelope with cohort rows):
```json
{
  "asOf": "2026-10-23T12:15:03Z", "fromDate": "2026-10-23", "toDate": "2026-10-23",
  "filters": { "fromDate": "2026-10-23", "toDate": "2026-10-23", "fulfillmentLocationId": "0192b000-0001-7000-8000-000000000101" },
  "totals": { "ordersPlaced": 4, "ordersDelivered": 2, "grossPaise": 315306, "netRevenuePaise": 305306, "avgOrderValuePaise": 152653 },
  "items": [ { "businessDate": "2026-10-23", "cityId": "…", "fulfillmentLocationId": "…", "ordersPlaced": 4, "netRevenuePaise": 305306 } ],
  "page": 1, "pageSize": 20, "totalCount": 1
}
```
`asOf` = oldest refresh of the views used (null before the first refresh); `totals` cover all matching rows. Defaults `toDate` = today at the locations in scope, `fromDate` = `toDate − 29`; range > 366 days or `fromDate > toDate` → 422 `VALIDATION_FAILED`. Scoped staff see their locations only; `fulfillmentLocationId` outside access → 403 `LOCATION_ACCESS_DENIED`; `cityId` narrows to that city's locations; inconsistent `cityId` + `fulfillmentLocationId` → 422.
`format=csv`: UTF-8 with BOM, `Content-Disposition: attachment; filename="<view>_<from>_<to>.csv"`, snake_case header, money as `_paise` plus `_inr` (`1234.50`), text starting `= + - @` prefixed with `'`, last row `TOTAL`, header `X-Report-As-Of`; > 100 000 rows → 422 on `toDate`; dashboard has no CSV (422). All report responses `Cache-Control: private, max-age=60`.
Dashboard: `{ asOf, date, filters, ordersByStatus[{status,count}], slots[{slotId, fulfillmentLocationId, startAt, endAt, bookedCount, capacity, fillBp}], lowStock[{stockItemId, variantId, sku, variantName, available, lowStockThreshold}], openComplaints, refundsRequested, refundsRequestedPaise, openPaymentIssues }`.

### 4.21 Settings, staff, roles, audit, notifications

| Method | Path | Auth | Notes | Purpose |
|---|---|---|---|---|
| GET | `/api/v1/admin/staff` | staff.manage | | Staff list |
| POST | `/api/v1/admin/staff` | staff.manage | | Create (email, name, roles, locations, temporary password) |
| GET | `/api/v1/admin/staff/{staffId}` | staff.manage | ETag | Detail |
| PUT | `/api/v1/admin/staff/{staffId}` | staff.manage | IM | Name, roles, `allLocations`, `locationIds`, `isActive` |
| POST | `/api/v1/admin/staff/{staffId}/reset-password` | staff.manage | IM | New temporary password; revokes sessions |
| GET | `/api/v1/admin/roles` | staff.manage | | Roles with permissions |
| POST | `/api/v1/admin/roles` | staff.manage | | Custom role |
| PUT | `/api/v1/admin/roles/{roleId}` | staff.manage | IM | Name, permissions |
| DELETE | `/api/v1/admin/roles/{roleId}` | staff.manage | IM | Delete custom role (`ROLE_IN_USE`) |
| GET | `/api/v1/admin/permissions` | staff.manage | | Permission catalogue |
| GET/PUT | `/api/v1/admin/settings/business-profile` | settings.manage | ETag / IM (If-None-Match: * to create) | Legal name, GSTIN, FSSAI, address, support contacts, `timeZone` (IANA) |
| GET | `/api/v1/admin/settings/order-policies` | settings.manage | | Default + per-city policies |
| PUT | `/api/v1/admin/settings/order-policies/{cityId\|default}` | settings.manage | IM / If-None-Match | `reservationMinutes`, `maxOverweightBp`, `cancelAllowedUntilStatus`, `codMaxOrderPaise`, `enabledPaymentMethods`, `substitutionResponseMinutes`, `complaintWindowHours`, `maxDeliveryAttempts` |
| GET | `/api/v1/admin/settings/number-series` | settings.manage | | Series per code × period |
| PUT | `/api/v1/admin/settings/number-series/{code}/{period}` | settings.manage | IM / If-None-Match | `prefix`, `padWidth`, `nextValue` (`NUMBER_SERIES_IN_USE` once issued) |
| GET | `/api/v1/admin/audit` | audit.view | | `entityType`, `entityId`, `actorId`, `action`, `fromDate`, `toDate` |
| GET | `/api/v1/admin/notification-templates` | notifications.manage | | Templates |
| PUT | `/api/v1/admin/notification-templates/{code}/{channel}` | notifications.manage | IM | `subject`, `body`, `isActive` (`TEMPLATE_PLACEHOLDER_UNKNOWN`) |
| POST | `/api/v1/admin/notification-templates/{code}/{channel}/test` | notifications.manage | | Send test to own contact |

## 5. Cart and checkout pricing contract

The client never computes, rounds, sums or compares business values. It renders the fields below; any value it
needs that is not here is a missing API field, not a client calculation.

### 5.1 Per line (cart, preview, order)

| Field | Meaning | Rule |
|---|---|---|
| `quantity` | Ordered net quantity | GRAM for KG variants, PIECE, PACK |
| `estimatedNetQuantity` | Net quantity the customer receives (estimate) | = `quantity` (net-weight model) |
| `maxChargeableQuantity` | Cap on charged quantity | weight-variable: `quantity × (10000 + maxOverweightBp) / 10000` floored; otherwise `quantity` |
| `unitPricePaise` / `priceUnit` | Current location price per KG / PIECE / PACK | from `variant_prices` at the fulfillment location |
| `compareAtPaise` | Strike-through price | display only |
| `itemPaise` | `quantity × unitPrice` (per kg ÷ 1000 for grams) | round half up to paise per line |
| `preparationChargePaise` / `preparationChargeBasis` | Prep charge for the line | PER_KG × net kg, PER_ITEM × count, FLAT once |
| `taxRateBp`, `taxPaise` | GST on the line | from `tax_rules` for the product's tax category; prices are tax-inclusive, `taxPaise` is the contained tax |
| `estimatedLineTotalPaise` | `itemPaise + preparationChargePaise` | what the customer pays if packed exactly |
| `maxLineTotalPaise` | Line total at `maxChargeableQuantity` | the most this line can cost |
| `availability.status` / `message` | IN_STOCK, LOW_STOCK, OUT_OF_STOCK, NOT_SOLD_HERE | gross need `ceil(net × 10000 / yieldBp)` vs available |
| `isSellable`, `issues[]` | Line blocks checkout | `issues` = `{ code, message }` with §2 codes (`OUT_OF_STOCK`, `PRICE_NOT_AVAILABLE`, `PRODUCT_NOT_AVAILABLE`, `QUANTITY_ABOVE_MAX`) |

### 5.2 Totals

| Field | Rule |
|---|---|
| `itemsPaise` | Σ `itemPaise` of sellable lines |
| `preparationPaise` | Σ `preparationChargePaise` |
| `subtotalPaise` | `itemsPaise + preparationPaise` |
| `discountPaise` | Coupon discount on eligible lines; PERCENT capped by `maxDiscountPaise`; FREE_DELIVERY → 0 here and fee waived |
| `deliveryFeePaise`, `deliveryFeeWaived` | Service area fee; 0 when `subtotal − discount ≥ freeDeliveryAbovePaise` or FREE_DELIVERY coupon |
| `taxPaise` | Σ line tax + tax on delivery fee (contained, informational) |
| `estimatedTotalPaise` | `subtotalPaise − discountPaise + deliveryFeePaise` |
| `maxTotalPaise` | Same with `maxLineTotalPaise` |
| `minOrderPaise`, `minOrderShortfallPaise` | Service area minimum and gap (on `subtotal − discount`) |
| `freeDeliveryShortfallPaise` | Amount to add for free delivery; `null` if none configured |
| `canCheckout` / `checkoutBlockers` | Server decision; blockers use §2 codes |
| `messages[]` | Display strings (net-weight note, low stock, coupon hints) |

### 5.3 Rules

| Rule |
|---|
| Prices are re-read on every cart read, preview and order placement; nothing is cached on the client or stored in the cart. |
| The order stores a snapshot (prices, yield, tax rate, max chargeable) at placement; later price changes do not affect it. |
| Final amounts are computed only at weigh-and-pack; invoice is issued at delivery from final amounts. |
| A coupon that becomes invalid is reported in `coupon.status` (`APPLIED`, `NOT_APPLICABLE`, `INVALID`) with `code`; it does not throw on cart reads. |
| Place order recomputes everything and compares with `expectedTotalPaise` (§4.8). |

## 6. Order status machine

| From | To | Who / trigger | Endpoint or event |
|---|---|---|---|
| — | `CONFIRMED` | Customer, COD / UPI_ON_DELIVERY | `POST /orders` |
| — | `PENDING_PAYMENT` | Customer, ONLINE | `POST /orders` |
| `PENDING_PAYMENT` | `CONFIRMED` | Provider | webhook `payment.captured` |
| `PENDING_PAYMENT` | `PAYMENT_FAILED` | Provider / System | webhook `payment.failed`; reservation-expiry job |
| `PENDING_PAYMENT` | `CANCELLED` | Customer | `POST /orders/{id}/cancel` |
| `CONFIRMED` | `PREPARING` | Staff `orders.pack` | `POST /admin/orders/{id}/start-preparing` |
| `CONFIRMED` | `CANCELLED` | Customer (policy) / Staff `orders.cancel` | `POST /orders/{id}/cancel`, `POST /admin/orders/{id}/cancel` |
| `PREPARING` | `PACKED` | Staff `orders.pack` | `POST /admin/orders/{id}/pack` |
| `PREPARING` | `CANCELLED` | Customer (if policy allows) / Staff | same cancel endpoints |
| `PACKED` | `OUT_FOR_DELIVERY` | Staff `delivery.manage` | `POST /admin/delivery/shipments/{id}/dispatch` |
| `PACKED` | `CANCELLED` | Staff `orders.cancel` | `POST /admin/orders/{id}/cancel` (stock already consumed → WASTE/RETURN by inventory staff) |
| `OUT_FOR_DELIVERY` | `DELIVERED` | Staff `delivery.manage` | `POST /admin/delivery/shipments/{id}/deliver` (invoice issued in same transaction) |
| `OUT_FOR_DELIVERY` | `DELIVERY_FAILED` | Staff `delivery.manage` | `POST /admin/delivery/shipments/{id}/attempts` |
| `DELIVERY_FAILED` | `OUT_FOR_DELIVERY` | Staff `delivery.manage` | `POST /admin/delivery/shipments/{id}/reattempt` |
| `DELIVERY_FAILED` | `RETURNED` | Staff `delivery.manage` | `POST /admin/delivery/shipments/{id}/return` |

| Rule |
|---|
| Any other transition → 409 `INVALID_STATUS_TRANSITION`. Terminal: `DELIVERED`, `CANCELLED`, `PAYMENT_FAILED`, `RETURNED`. |
| Customer cancel allowed while status ≤ `order_policies.cancelAllowedUntilStatus` (default `CONFIRMED`), else 422 `ORDER_NOT_CANCELLABLE`. |
| Cancel releases reservations, slot booking, coupon; prepaid → `CANCELLATION` refund via Payments handler. |
| Every transition writes `order_status_history` (actor type/id, reason) and an outbox event `OrderStatusChanged` (notifications). |
| Complaints (`POST /orders/{id}/complaints`) are allowed only on `DELIVERED`, within 24 h. |

## 7. Payment webhooks

| Item | Rule |
|---|---|
| Path | `POST /api/v1/payments/webhooks/{provider}`; `{provider}` lowercase adapter key: `fake` (dev/tests), `razorpay` (later), others by adapter |
| COD / UPI on delivery | No webhook; collection recorded by `deliver` / `cash-collected` |
| Auth | None; provider signature only. Not behind JWT, CSRF or rate limit |
| Body | Read raw bytes before parsing; signature computed over raw body |
| Signature | Adapter-specific: `fake` → `X-Fake-Signature` HMAC-SHA256(secret, body) hex; `razorpay` → `X-Razorpay-Signature` HMAC-SHA256(webhook secret, body) hex; constant-time compare |
| Invalid signature | Event stored with `signatureValid=false`, not processed → 401 `WEBHOOK_SIGNATURE_INVALID` |
| Event id | Adapter extracts `providerEventId` (`fake`: `id`; `razorpay`: `X-Razorpay-Event-Id` header) |
| Idempotency | Insert `payments.webhook_events (provider, provider_event_id)`; unique violation = duplicate → 200 `{ "received": true, "duplicate": true }`, nothing re-applied |
| Processing | Same transaction: event row → payment status → order status → outbox (database §4) |
| Unknown / irrelevant event type | Stored, marked processed → 200 |
| Order already terminal (e.g. `PAYMENT_FAILED` after expiry, then `captured`) | 200; payment `CAPTURED`, outbox `PaymentReceivedForClosedOrder` → automatic `CANCELLATION` refund |
| Amount mismatch | 200; payment stays `PENDING`, event `error` set, alert to `payments.view` staff |
| Processing failure (bug, DB down) | 500 so the provider retries; event row rolled back |
| Status mapping | `captured` → `CAPTURED`, order `CONFIRMED`; `authorized` → `AUTHORIZED`; `failed` → `FAILED`, order `PAYMENT_FAILED` only if no other active attempt and reservation expired |
| Refund events | `refund.processed` / `refund.failed` → refund `SUCCEEDED` / `FAILED` by `provider_refund_id` |
| Unique event id | Enforced for valid signatures only (`signature_valid = true`), so a forged request with a real event id cannot block the genuine event |
| Same state, new event id | Stored and processed; the payment is already in that state, so no second domain event is raised |
| Stored headers | Only the signature and event-id headers are kept with the payload |
| Missed webhooks | Hourly reconciliation asks the provider about attempts 15 min–72 h old and applies captures (event id `recon:{providerPaymentId}:CAPTURED`) |

**POST /api/v1/payments/webhooks/fake** (`X-Fake-Signature: 5d41402abc4b2a76b9719d911017c592…`)
```json
{
  "id": "evt_01JB7Q2ZK9", "type": "payment.captured", "createdAt": "2026-10-23T05:43:02Z",
  "data": { "providerOrderId": "fake_order_8812", "providerPaymentId": "fake_pay_55731", "amountPaise": 234900, "currency": "INR", "method": "UPI" }
}
```
→ 200
```json
{ "received": true, "duplicate": false }
```
Same `id` again → 200 `{ "received": true, "duplicate": true }`.
