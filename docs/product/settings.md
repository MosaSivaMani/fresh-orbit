# Settings module

Owner: E1. Schema `settings`. Uses no other module's `.Public`. Endpoints: `docs/api/README.md` §4.21
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

## 2. Rules

| Key | Rule |
|---|---|
| SET-R1 | Business profile: `legalName`, `tradeName`, `address`, `supportPhone` (E.164), `supportEmail`, `timeZone` (IANA id) required; `gstin` optional but, when sent, 15 characters with a valid state code, PAN shape and checksum → 422 `GSTIN_INVALID`; `fssaiLicenseNo` 14 digits when sent. First save uses `If-None-Match: *`, later saves `If-Match`. |
| SET-R2 | Order policy columns (database §3 + RD-G3 + D12 + D17): `reservationMinutes` 5–120, `maxOverweightBp` 0–5000, `cancelAllowedUntilStatus` one of PENDING_PAYMENT, CONFIRMED, PREPARING, PACKED, `codMaxOrderPaise` ≥ 0, `enabledPaymentMethods` non-empty subset of COD, UPI_ON_DELIVERY, ONLINE, `substitutionResponseMinutes` 5–120, `complaintWindowHours` 1–168, `maxDeliveryAttempts` 1–5. |
| SET-R3 | `PUT /order-policies/default` updates the default row (If-Match). `PUT /order-policies/{cityId}` creates a city row with `If-None-Match: *` or updates it with `If-Match`. A city without a row uses the default. |
| SET-R4 | Number = `prefix` + `next_value` left-padded to `pad_width` (`FO-260001`, `INV/26-27/000001`); past the pad width it grows, never wraps. Period = Indian financial year (1 April – 31 March) in the business time zone, label `YYYY-YY`. |
| SET-R5 | A missing series row → 422 `CONFIGURATION_MISSING`. Numbers are gapless: drawn only inside the caller's transaction, a rollback returns the number. |
| SET-R6 | Admin `PUT /number-series/{code}/{period}` creates a row (`If-None-Match: *`) or changes `prefix`, `padWidth`, `nextValue` (If-Match). Once a number was issued (`last_issued_at` set) → 409 `NUMBER_SERIES_IN_USE`. Code is ORDER or INVOICE; period matches `^\d{4}-\d{2}$` with consecutive years. |
| SET-R7 | Reference data: `order-policies.json` and `number-series.json` INSERT_IF_MISSING (never overwrite admin edits or issued series). |

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
