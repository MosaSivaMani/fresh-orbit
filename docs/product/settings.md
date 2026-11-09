# Settings module

Owner: E1. Schema `settings`. Uses `Audit.Public`, `Delivery.Public` (implements `IDeliveryAttemptPolicy`), and, for the store readiness report only, `Locations.Public` and `Notifications.Public` (D358). Endpoints: `docs/api/README.md` §4.21
(business profile, order policies, number series). Error codes: `docs/api/README.md` §2 (Notifications / Settings, Common).

## 1. Aggregates

| Aggregate root | Table | Row version |
|---|---|---|
| BusinessProfile | `business_profile` (single row) | `xmin` |
| OrderPolicy | `order_policies` (one default row with `city_id` null + optional row per city) | `xmin` |
| NumberSeries | `number_series` (one row per code × period; surrogate `id`, unique `(code, period)`) | `xmin` |

### Public API (`Settings.Public`)

| Member | Returns | Used by |
|---|---|---|
| `GetOrderPolicyAsync(cityId?)` | city row, else the default row; none → 422 `CONFIGURATION_MISSING` | Cart, Ordering, Payments, Delivery |
| `NextNumberAsync(code, period)` | next number; locks the row `FOR UPDATE` in the caller's transaction and increments it (tracked, saved by the caller) | Ordering |
| `NextNumberAsync(code, at)` | same, period derived from `at` in the business time zone | Ordering |
| `GetFinancialPeriodAsync(at)` | `2026-27` label | Ordering, Reporting |
| `GetBusinessProfileAsync()` | profile; none → 422 `CONFIGURATION_MISSING` | Ordering (invoice), Notifications |
| `GetStoreReadinessAsync()` | `StoreReadiness { missingProfileFields, isReady }` (SET-R9); never throws | Ordering (checkout), Api readiness check |

## 2. Rules

| Key | Rule |
|---|---|
| SET-R1 | Business profile: `legalName`, `tradeName`, `address`, `supportPhone` (E.164), `supportEmail`, `timeZone` (IANA id) required; `gstin` optional but, when sent, 15 characters with a valid state code, PAN shape and checksum → 422 `GSTIN_INVALID`; `fssaiLicenseNo` 14 digits when sent. First save uses `If-None-Match: *`, later saves `If-Match`. |
| SET-R2 | Order policy columns (database §3 + RD-G3 + D12 + D17): `reservationMinutes` 5–120, `maxOverweightBp` 0–5000 (default 0, D347; a non-zero allowance stays configurable but the customer promise and policy texts assume 0), `cancelAllowedUntilStatus` one of PENDING_PAYMENT, CONFIRMED, PREPARING, PACKED, `codMaxOrderPaise` ≥ 0, `enabledPaymentMethods` non-empty subset of COD, UPI_ON_DELIVERY, ONLINE, `substitutionResponseMinutes` 5–120, `complaintWindowHours` 1–168, `maxDeliveryAttempts` 1–5. |
| SET-R3 | `PUT /order-policies/default` updates the default row (If-Match). `PUT /order-policies/{cityId}` creates a city row with `If-None-Match: *` or updates it with `If-Match`. A city without a row uses the default. |
| SET-R4 | Number = `prefix` + `next_value` left-padded to `pad_width` (`FO-260001`, `INV/26-27/000001`); past the pad width it grows, never wraps. Period = Indian financial year (1 April – 31 March) in the business time zone, label `YYYY-YY`. |
| SET-R5 | A missing series row → 422 `CONFIGURATION_MISSING`. Numbers are gapless: drawn only inside the caller's transaction, a rollback returns the number. |
| SET-R6 | Admin `PUT /number-series/{code}/{period}` creates a row (`If-None-Match: *`) or changes `prefix`, `padWidth`, `nextValue` (If-Match). Once a number was issued (`last_issued_at` set) → 409 `NUMBER_SERIES_IN_USE`. Code is ORDER or INVOICE; period matches `^\d{4}-\d{2}$` with consecutive years. |
| SET-R7 | Reference data: `order-policies.json` and `number-series.json` INSERT_IF_MISSING (never overwrite admin edits or issued series). |
| SET-R8 | `GET /api/v1/business-profile` (anonymous, public rate limit) returns the legal footer and contact block: legal and trade name, GSTIN, FSSAI licence, address, support phone, e-mail and hours, time zone. Not configured yet → 404 `BUSINESS_PROFILE_NOT_CONFIGURED` (never a guessed default). `supportHours` (free text, ≤ 200, e.g. `7 am – 9 pm, all days`) is optional and saved with the admin `PUT /admin/settings/business-profile`. |
| SET-R9 | **Store readiness (D295, BE-1).** An invoice needs the seller's `legalName` and `gstin` and the business `timeZone` (INVOICE number period). `InvoicingReadiness` lists the missing ones (`businessProfile` when no profile was saved). While any is missing, checkout answers 422 `STORE_NOT_READY` (ORD-R0) and the Api's `GET /health/ready` answers 503; the GSTIN stays optional on save (SET-R1) so a profile can be entered in steps, but no order is taken until it is complete. |
| SET-R10 | **Store readiness report (D358, D359, audit BF-3).** `GET /admin/settings/store-readiness` (`settings.manage`, read-only) lists what blocks real trading: the business profile's `legalName`, `address`, `gstin`, `timeZone` (invoicing readiness of SET-R9 plus the address the invoice prints, D342); each fulfillment location's FSSAI licence (`ILocationsApi.GetAllFulfillmentLocationsAsync`, any status); and the SMS, e-mail and push provider in force (`INotificationProviders`). Each item is `OK` or `MISSING`; a channel is OK only with a real provider (`DLT`, `SMTP`, `FCM`), so `DISABLED` and `DEVELOPMENT` are MISSING. Only provider codes are returned, never a key, host or sender id. `isReady` is true only when every item is OK and at least one location exists. Checkout still checks only SET-R9 (ORD-R0). Admin page: Settings › Store readiness (`/settings/store-readiness`), each MISSING row linking to the page that fixes it (business profile, the location, or the server settings named in `runbooks/security-configuration.md`). |

## 3. Events

| Event | Payload | Consumers |
|---|---|---|
| `BusinessProfileChanged` | changed fields | Audit (when allowed) |
| `OrderPolicyChanged` | policyId, cityId? | Audit |
| `NumberSeriesChanged` | code, period | Audit |

## 4. Commands

| Command | Endpoint | Writes |
|---|---|---|
| SaveBusinessProfile | `PUT /admin/settings/business-profile` | BusinessProfile |
| SaveOrderPolicy | `PUT /admin/settings/order-policies/{cityId|default}` | OrderPolicy |
| SaveNumberSeries | `PUT /admin/settings/number-series/{code}/{period}` | NumberSeries |
