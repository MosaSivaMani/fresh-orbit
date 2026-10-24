# Audit module

Owner: E1. Schema `audit`. Uses no other module's `.Public`. Endpoint: `GET /api/v1/admin/audit` (`audit.view`).

## 1. Model

| Entity | Table | Notes |
|---|---|---|
| AuditEntry | `entries` (`id bigint` identity) | append-only: a database trigger refuses UPDATE and DELETE |

### Public API (`Audit.Public`)

| Member | Behaviour | Used by |
|---|---|---|
| `IAuditApi.Record(AuditRecord)` | Adds an entry for the current actor to the shared context; saved by the caller's `SaveChangesAsync` (same transaction as the change). Never saves. | every module's admin writes |

## 2. Rules

| Key | Rule |
|---|---|
| AUD-R1 | `action` UPPER_SNAKE (e.g. `PRICE_CHANGED`), `entityType` UPPER_SNAKE or PascalCase name ≤ 60, `entityId` ≤ 100. Invalid input throws (programming error, 500). |
| AUD-R2 | Actor from `ICurrentActor`: `actor_type` CUSTOMER/STAFF/SYSTEM/ANONYMOUS, `actor_id` = user id; `ip` from the request (null in the Worker); `correlation_id` = current trace id. `occurred_at` from `IClock`. |
| AUD-R3 | `before` / `after` serialized with the shared JSON options to `jsonb`; null allowed. |
| AUD-R4 | Rows are never updated or deleted (trigger `entries_append_only`). |
| AUD-R5 | List filters: `entityType`, `entityId`, `actorId`, `action`, `fromDate`, `toDate` (inclusive dates, UTC); newest first; paging per API §1.5. |
| AUD-R6 | **Every admin write is audited (SEC-7).** During an `/api/v1/admin` POST, PUT, PATCH or DELETE by an authenticated STAFF actor, `AdminWriteAuditInterceptor` (an EF `SaveChangesInterceptor` registered by `AuditModule` and picked up by `AddFreshOrbitPersistence`) adds one entry per added, modified or deleted entity to the **same** `SaveChanges`, so the change and its audit commit or roll back together. If the save fails, the added entries are detached. In memory only, no database call. Nothing is recorded outside an HTTP request (Worker, Migrations), for customers or anonymous callers, or for exempt endpoints (§3). |
| AUD-R7 | **Generic entry shape.** `entityType` = UPPER_SNAKE of the CLR type (`StaffMember` → `STAFF_MEMBER`); `entityId` = primary key (composite keys joined with `:`). `action` = the endpoint's named action (§3) or `<ENTITY>_CREATED`, `_UPDATED` or `_DELETED`. `before`/`after`: every property for a create (after) or delete (before); only the changed properties for an update (both). Bookkeeping columns (`created_*`, `modified_*`, `xmin`) and the `audit` and `platform` schemas are skipped; an update that changes only bookkeeping records nothing. |
| AUD-R8 | **Redaction.** A property whose name contains `Hash`, `Password`, `Secret`, `Token`, `Otp`, `Salt`, `ApiKey`, `PrivateKey`, `SigningKey`, `Cvv` or `Payload` is stored as `"[REDACTED]"` (the fact that it changed is kept). `byte[]` values are stored as their length. |
| AUD-R9 | **Explicit records cover their entity only, never doubled.** An explicit `IAuditApi.Record` (type T, id I) in the save covers matching changed entities, and the interceptor skips those. Every other changed entity in that save still gets a generic entry under the endpoint's action. *Type match*: the entity's UPPER_SNAKE class name C equals T or T ends with `_C` (`NOTIFICATION_TEMPLATE` matches class `Template`). *Id match*: the entity of that class whose primary key equals I (case-insensitive) is covered. If none has that key, because the record uses a natural key or a singleton id (`default`, `code/period`, `code/channel`, a city id), every changed entity of that class in the save is covered. Explicit callers today and what they cover: Identity `STAFF_MEMBER` by PK (so the `USER` password change of a reset, and `STAFF_ROLE` rows, get generic entries); Customers `CUSTOMER` by PK; Settings `BUSINESS_PROFILE`, `ORDER_POLICY`, `NUMBER_SERIES` and Notifications `NOTIFICATION_TEMPLATE` by natural key (the whole class in that save). |
| AUD-R10 | **No bypass.** Set-based writes (`ExecuteUpdate`, `ExecuteDelete`, raw SQL) are not seen by the interceptor; module code may use them only in the files listed by `AuditBypassTests` (none runs in an admin request). |

## 3. Admin-write policy (`AdminAuditPolicy`)

Endpoint metadata wins: `.WithAuditAction("REFUND_APPROVED")` names the action and `.ExemptFromAudit("reason")` exempts
the endpoint (both in `SharedKernel.Web/Auditing`). Until module owners move them onto their own endpoints, the Audit
module keeps two central tables keyed by `METHOD route-pattern`:

- **Named actions** for the SEC-7 high-value writes: staff and roles (`STAFF_CREATED`, `STAFF_UPDATED`,
  `STAFF_PASSWORD_RESET`, `STAFF_PASSWORD_CHANGED`, `ROLE_CREATED/UPDATED/DELETED`), customer block/unblock, refunds
  (`REFUND_REQUESTED/APPROVED/REJECTED/MARKED_PAID`), cash settlements (`CASH_SETTLEMENT_RECORDED/SETTLED/DISPUTED`),
  `PAYMENT_ISSUE_RESOLVED`, `WEBHOOK_EVENT_REPROCESSED`, price, charge and tax schedules (`PRICE_SCHEDULED`,
  `PRICE_ENDED`, `PREPARATION_CHARGE_SCHEDULED`, `PRICES_BULK_IMPORTED`, `TAX_RULE_SCHEDULED`), settings (`*_CHANGED`),
  city activation (`CITY_ACTIVATED/PAUSED/RETIRED`), staff order cancel, goodwill and complaint decisions.
- **Exemptions** (only these are allowed): `POST /api/v1/admin/auth/login` (anonymous caller; failed logins are
  counted by Identity) and `POST /api/v1/admin/auth/logout` (revokes the caller's own sessions).

`AdminWriteCoverageTests` enumerates every mapped admin POST/PUT/PATCH/DELETE of the real Api host and asserts that
each is audited or exempt with a reason, that the exemptions are only the two above, that every audited one requires an
authenticated caller, and that every named-action key still matches a mapped route.
