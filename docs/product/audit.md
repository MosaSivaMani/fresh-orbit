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
