# Inventory module specification

Owner: E3. Schema `inventory`. Uses `Catalog.Public`, `Locations.Public`. Source docs: `CLAUDE.md`,
`docs/architecture/README.md`, `docs/database/README.md` (§3 inventory, §4), `docs/api/README.md` (§2 Inventory, §4.5, §4.14, §5).
Items marked **(Qn)** depend on an open question in §7; the spec is written as if the proposed answer were accepted.

Units: quantity is `int` in the variant's stock unit (`GRAM` or `PIECE`); money `long` paise; temperature `numeric(4,1)` °C.

## 1. Aggregates

| Aggregate | Root (table) | Children (table) | Decides | Row version |
|---|---|---|---|---|
| StockItem | `stock_items` | `batches` (ACTIVE loaded; others read-only), `stock_movements` (insert-only), `stock_reservations` (ACTIVE loaded) | Availability, reservation, consumption, release, FEFO picking, movement cost, on hand / reserved summary, batch state, low-stock edge | `stock_items.xmin` |
| ColdChainLog | `cold_chain_logs` | — | Out-of-range flag | none (insert-only) |

| Boundary rule |
|---|
| One StockItem = one (fulfillment location × variant). A batch belongs to exactly one stock item (same location and variant). |
| Every StockItem command modifies the root row (`on_hand`, `reserved` or `low_stock_threshold`); a child-only change marks the root modified so `xmin` always moves. |
| Movements are never updated or deleted. Batches and reservations change only through the StockItem. |
| Repository loads: `GetForReservation` (root only, children added), `GetWithActiveChildren` (root + ACTIVE batches + ACTIVE reservations), `GetManyByIds` (one query for many roots). |
| Other modules never touch these tables; they call `Inventory.Public` (checkout reservation, reads) or raise events. |

### Invariants

| Key | Invariant |
|---|---|
| INV-I1 | `on_hand = Σ remaining_quantity` of the item's ACTIVE batches (batchless pairs net to 0, INV-R14). |
| INV-I2 | `reserved = Σ quantity` of the item's ACTIVE reservations. |
| INV-I3 | `on_hand >= 0`, `reserved >= 0`; `0 <= remaining_quantity` (no upper bound: ADJUSTMENT_IN can raise it). |
| INV-I4 | A new reservation or a discretionary decrease (ADJUSTMENT_OUT) never makes `reserved > on_hand`. Physical-fact movements (SALE at pack, WASTE, discard, expiry, COUNT_CORRECTION) may (Q1). |
| INV-I5 | `on_hand` changes only together with a movement row of the same signed quantity, in the same save. |

## 2. States and transitions

### Batch (`batches.status`)

| From | To | Trigger |
|---|---|---|
| — | ACTIVE | Receive stock |
| ACTIVE | DEPLETED | `remaining_quantity` reaches 0 (SALE, WASTE, ADJUSTMENT_OUT, negative COUNT_CORRECTION) |
| DEPLETED | ACTIVE | ADJUSTMENT_IN or positive COUNT_CORRECTION naming this batch, only if `best_before_at > now` |
| ACTIVE | EXPIRED | Batch expiry job (`best_before_at <= now`); remaining written off as WASTE `EXPIRED` |
| ACTIVE | DISCARDED | Discard endpoint; remaining written off as WASTE with given reason |
| EXPIRED, DISCARDED | — | Terminal. Any command naming them → 422 `BATCH_NOT_ACTIVE` |

### Reservation (`stock_reservations.status`)

| From | To | Trigger |
|---|---|---|
| — | ACTIVE | Checkout (`Inventory.Public.Reserve`), `expires_at` = order's payment deadline for ONLINE, `null` otherwise |
| ACTIVE | ACTIVE (`expires_at` cleared) | `OrderConfirmed` |
| ACTIVE | CONSUMED | `OrderPacked`, line packed > 0 |
| ACTIVE | RELEASED | `OrderCancelled`; `OrderPacked` line `SHORT_SUPPLIED` / packed 0; substitution replaces the line (Q5) |
| ACTIVE | EXPIRED | `OrderPaymentFailed` (Q2) |
| CONSUMED, RELEASED, EXPIRED | — | Terminal |

Stock items and cold-chain logs have no status.

## 3. Business rules

### Validation

| Key | Rule | Refusal |
|---|---|---|
| INV-R1 | Stock item: variant exists and is active (`Catalog.Public`), location exists (`Locations.Public`); one per (location, variant). | 422 `VALIDATION_FAILED`; 409 `DUPLICATE` (`errors.variantId`) |
| INV-R2 | Commands for a (location, variant) without a stock item are refused, never auto-created. | 422 `STOCK_ITEM_MISSING` |
| INV-R3 | Quantity `unit` must equal the variant's stock unit (`GRAM` for sell unit KG; `PIECE` for PIECE; PACK see Q4); value > 0 (counts: ≥ 0). | 422 `VALIDATION_FAILED` |
| INV-R4 | Receipt: `batchCode` 1–40 chars unique per location; `totalCostPaise >= 0`; `bestBeforeAt > now`; `caughtOrHarvestedOn <= today` (location date); `receiptTemperatureC` −40.0…40.0. `receivedAt` = server clock. | 409 `BATCH_CODE_TAKEN`; 422 `VALIDATION_FAILED` |
| INV-R5 | Waste reason ∈ `SPOILED`, `TEMPERATURE_BREACH`, `DAMAGED`, `EXPIRED`, `QC_REJECTED`. Adjustment reason ∈ `DATA_ENTRY_ERROR`, `FOUND`, `LOST`, `THEFT`, `TRANSFER`, `OTHER` (Q7). `OTHER` needs `notes`. | 422 `REASON_CODE_INVALID` |
| INV-R6 | Explicit batch must belong to the stock item and be ACTIVE (DEPLETED allowed only for an increase, §2) and hold the requested quantity for a decrease. | 422 `BATCH_NOT_ACTIVE`; 422 `VALIDATION_FAILED` (`errors.quantity`) |
| INV-R7 | ADJUSTMENT_OUT may not make `on_hand < 0` or `on_hand < reserved`. | 422 `ADJUSTMENT_BELOW_ZERO` |
| INV-R8 | Positive ADJUSTMENT_IN / COUNT_CORRECTION goes into `batchId` if given, else the newest ACTIVE batch (max `received_at`); no ACTIVE batch and no `batchId` → refused. | 422 `VALIDATION_FAILED` (`errors.batchId`) |
| INV-R9 | `lowStockThreshold >= 0`, in stock unit. | 422 `VALIDATION_FAILED` |
| INV-R10 | Staff location scope applies to every admin command and list (`LOCATION_ACCESS_DENIED` / silent filter). | 403 |

### Calculation

| Key | Rule |
|---|---|
| INV-R11 | **Gross quantity.** Stock unit GRAM: `gross = ceil(net × 10000 / yield_bp)` with integer math `(net × 10000 + yield_bp − 1) / yield_bp` on `long`. PIECE: `gross = net` (yield ignored). 500 g at 6500 bp → 770 g. `yield_bp` 1…10000, else `VALIDATION_FAILED`. Exposed as `Inventory.Public.GrossQuantity.From(...)` so Cart/Ordering never re-implement it. |
| INV-R12 | **Available** = `on_hand − reserved`, shown as `max(0, …)`. **Max net orderable** (GRAM) = `floor(available × yield_bp / 10000)` (exact inverse of INV-R11); caller rounds down to the variant's `order_step`. |
| INV-R13 | **Availability status** (customer): no stock item or no price → `NOT_SOLD_HERE`; `gross(min_order) > available` → `OUT_OF_STOCK`; `threshold > 0 and available <= threshold` → `LOW_STOCK`; else `IN_STOCK`. |
| INV-R14 | **FEFO picking** (SALE, WASTE without batch, ADJUSTMENT_OUT without batch, negative COUNT_CORRECTION): ACTIVE batches ordered by `best_before_at`, then `received_at`, then `id`; take `min(remaining, need)` from each; one movement per batch touched; a batch reaching 0 → DEPLETED. If batches cannot cover a SALE (system short of physical): write `COUNT_CORRECTION +gap` (batchless, `reason_code PACK_SHORTFALL`, cost 0) and `SALE −gap` (batchless, cost 0), raise `StockDiscrepancyFound` (Q1). |
| INV-R15 | **Movement cost.** `cost_paise = round_half_away_from_zero(quantity × batch.total_cost_paise / batch.received_quantity)` in `decimal`, same sign as `quantity`. When a movement leaves the batch at `remaining = 0`, its cost is `−(Σ cost_paise of all earlier movements of that batch, RECEIPT included)` so a drained batch's movements sum to 0 exactly (one grouped query in Read). Batchless movements cost 0. Example: batch 10 000 g for 845 000 p, WASTE 333 g → −28 139 p. |
| INV-R16 | Receipt `costPerUnitPaise` in responses = `round(total_cost_paise / received_quantity)`; display only, never stored or used for cost. |
| INV-R17 | **Low-stock edge.** After any change, if `threshold > 0` and available moved from `> threshold` to `<= threshold` → `LowStockReached`; from `> 0` to `<= 0` → `OutOfStockReached`; from `<= threshold` to `> threshold` → `StockReplenished`. No event when the level does not cross. |
| INV-R18 | **Cold chain.** `is_out_of_range` computed by server from the storage unit's range (Q6: `CHILLER` −1.0…4.0, `FREEZER` ≤ −18.0, `RECEIPT` ≤ 4.0). Receipt temperature > 4.0 accepted, writes a `RECEIPT` log with `is_out_of_range = true`, response `temperatureWarning: true`. `recordedAt` optional, `now − 24 h <= recordedAt <= now`, default now. |
| INV-R19 | Reservation `quantity` = gross (INV-R11) of the order line's net ordered quantity; one ACTIVE reservation per `order_item_id`. Two lines of the same variant (different preparations) reserve on the same stock item separately. |
| INV-R20 | Consumption at pack: reservation CONSUMED (`reserved −= reservation.quantity`); SALE gross = INV-R11 of `packedQuantity` (can exceed the reservation; overweight comes from free stock, then INV-I4 over-commit is allowed and raises `StockOverCommitted`). |
| INV-R21 | Movement references: SALE → (`ORDER_ITEM`, order item id); RECEIPT, discard, expiry → (`BATCH`, batch id); manual waste/adjust/count → (`MANUAL`, idempotency or request id). `recorded_by` = staff id, `null` for system. |

### Concurrency

| Key | Rule |
|---|---|
| INV-R22 | StockItem saved with `xmin`. Commands with `If-Match` (threshold, discard, adjustments, counts): stale quote → 412 `CONCURRENCY_CONFLICT`; lost race after a matching quote → 409 `CONCURRENT_SAVE`, no retry. |
| INV-R23 | Commands without `If-Match` (receipt, waste: `Idempotency-Key` only; cold chain; stock item create): on `ConcurrentSaveException` clear the change tracker and retry from Read, max 3, then 409 `CONCURRENT_SAVE`. |
| INV-R24 | Checkout: `Inventory.Public.Reserve` only tracks changes on the caller's DbContext, never saves; Ordering's single `SaveChangesAsync` writes all StockItems with their `xmin`; lost race → Ordering retries the whole checkout from Read (max 3). `Reserve` is side-effect free so a retry re-reads. DB check is the last line. Deadlock `40P01` is treated as a lost race. |
| INV-R25 | Event handlers: lost race → handler throws, outbox retries it (inbox row is written in the same transaction, so success happens once); after max attempts → `platform.dead_letters`. |
| INV-R26 | `StockAvailableAgain(stockItemId, fulfillmentLocationId, variantId, available)` (Inventory.Public/Events) is raised in the same save whenever a stock item's available quantity goes from ≤ 0 to > 0 (receipt, adjustment, count, released or expired reservation). Customers consumes it for back-in-stock alerts (CUS-R10). |

### Events raised

| Event | Payload | Raised by |
|---|---|---|
| `StockReceived` | stockItemId, fulfillmentLocationId, variantId, batchId, batchCode, quantity, costPaise, bestBeforeAt, temperatureWarning | Receive |
| `WasteRecorded` | stockItemId, fulfillmentLocationId, variantId, reasonCode, lines[{batchId, quantity, costPaise}], totalQuantity, totalCostPaise | Waste, discard |
| `BatchExpired` | batchId, stockItemId, fulfillmentLocationId, variantId, writtenOffQuantity, costPaise | Expiry job |
| `StockAdjusted` | stockItemId, kind, reasonCode, quantity, costPaise | Adjustment, count |
| `LowStockReached` / `OutOfStockReached` / `StockReplenished` | stockItemId, fulfillmentLocationId, variantId, available, threshold | INV-R17 (any command or handler) |
| `StockOverCommitted` | stockItemId, fulfillmentLocationId, variantId, onHand, reserved, shortfall | INV-I4 / INV-R20 |
| `StockDiscrepancyFound` | stockItemId, orderItemId, gapQuantity | INV-R14 |
| `ColdChainBreachRecorded` | coldChainLogId, fulfillmentLocationId, storageUnit, temperatureC, recordedAt | Cold-chain entry or receipt out of range |
| `LowStockDigestReady` | fulfillmentLocationId, items[{stockItemId, variantId, available, threshold}] | Low-stock job |

Consumers expected: Notifications (alerts to `inventory.view` staff), Audit, Reporting reads tables directly.

### Events consumed

| Event (from) | Payload needed | Handler (idempotent) |
|---|---|---|
| `OrderPlaced` (Ordering) | — | Not consumed: reservation happens inside checkout (INV-R24). |
| `OrderConfirmed` (Ordering, PENDING_PAYMENT → CONFIRMED) | orderId | Clear `expires_at` on the order's ACTIVE reservations. None ACTIVE or already null → no-op. |
| `OrderPaymentFailed` (Ordering) | orderId | ACTIVE reservations of the order → EXPIRED, `reserved −=`. None ACTIVE → no-op. |
| `OrderCancelled` (Ordering) | orderId, previousStatus | ACTIVE reservations → RELEASED. Already PACKED → nothing to release (stock left as SALE; staff records WASTE / adjustment). No ACTIVE → no-op. |
| `OrderPacked` (Ordering) | orderId, fulfillmentLocationId, packedAt, lines[{orderItemId, variantId, stockUnit, yieldBp, packedQuantity, status}] | Per line: ACTIVE reservation → CONSUMED and SALE movements FEFO by gross of `packedQuantity` (INV-R14, R15, R20); line `SHORT_SUPPLIED` or packed 0 → RELEASED, no SALE. Guard: a line whose reservation is not ACTIVE **and** that already has a SALE with reference (`ORDER_ITEM`, id) is skipped. All stock items loaded in one query; one save. |
| `OrderDelivered` (Ordering) | — | Not consumed: stock left at pack. |
| `SubstitutionAccepted` (Ordering) | see Q5 | See Q5. |

## 4. Commands

| Command | Endpoint (auth) | Read | Decide | Commit | Follow-on |
|---|---|---|---|---|---|
| Create stock item | `POST /api/v1/admin/inventory/stock-items` (inventory.adjust) | Variant (Catalog.Public), location (Locations.Public), existing item | INV-R1, R9, R10 | Insert root (`on_hand 0, reserved 0`) | Audit |
| Set threshold | `PUT /api/v1/admin/inventory/stock/{stockItemId}/threshold` (inventory.adjust, IM) | Root | INV-R9, R22 | Update threshold | INV-R17 event if level crosses |
| Receive stock | `POST /api/v1/admin/inventory/receipts` (inventory.receive, IK) | Stock item by (location, variant), batch code exists?, variant stock unit | INV-R2–R4, R18 | Batch ACTIVE + RECEIPT movement (cost = total cost) + `on_hand +=` + cold-chain log if > 4.0 | `StockReceived`, `ColdChainBreachRecorded`?, `StockReplenished`? |
| Discard batch | `POST /api/v1/admin/inventory/batches/{batchId}/discard` (inventory.adjust, IM on stock item) | Stock item with active children | INV-R5, R6, R15 | WASTE (all remaining) + batch DISCARDED + `on_hand −=` | `WasteRecorded`, R17 events, `StockOverCommitted`? |
| Record waste | `POST /api/v1/admin/inventory/waste` (inventory.adjust, IK) | Stock item with active children | INV-R5, R6, R14, R15 | WASTE movement(s) + batch remaining/DEPLETED + `on_hand −=` | `WasteRecorded`, R17, `StockOverCommitted`? |
| Adjust | `POST /api/v1/admin/inventory/adjustments` (inventory.adjust, IM on stock item) | Stock item with active children | INV-R5–R8, R14, R15 | ADJUSTMENT_IN/OUT movement(s), batches, `on_hand` | `StockAdjusted`, R17 |
| Count | `POST /api/v1/admin/inventory/counts` (inventory.adjust, IM on stock item) | Stock item with active children | diff = counted − on_hand; 0 → no movement (200, unchanged version); < 0 FEFO; > 0 INV-R8 | COUNT_CORRECTION movement(s), batches, `on_hand` | `StockAdjusted`, R17, `StockOverCommitted`? |
| Record temperature | `POST /api/v1/admin/inventory/cold-chain` (inventory.adjust) | Location | INV-R18 | Insert log | `ColdChainBreachRecorded` if out of range |
| Reserve for checkout | none: `Inventory.Public.Reserve(orderId, locationId, lines[{orderItemId, variantId, stockUnit, netQuantity, yieldBp}], expiresAt?)` | All stock items of the lines in one query | INV-R2, R11, R19; all-or-nothing; refusals per line `OUT_OF_STOCK` / `STOCK_ITEM_MISSING` with `lineId` | Tracks reservations + `reserved +=`; caller saves | R17 events raised in caller's save |
| Check availability | none: `Inventory.Public.GetAvailability(locationId, variantIds)` | Stock items | INV-R12, R13 | — | — |
| Readiness | none: `Inventory.Public.HasStockItem(locationId)` | — | — | — | — (city activation `STOCK_ITEM_MISSING`) |
| Reservations for order | none: `Inventory.Public.GetOrderStock(orderId)` | Reservations + SALE movements by order | — | — | — (admin order detail: gross reserved, batches) |
| Customer availability | `GET /api/v1/availability?variantId=&preparation=` (public, LOC) | Stock items, yield (Catalog.Public), price presence (Pricing via caller — see API gap A1) | INV-R12, R13 | — | — |

Reads (no Decide/Commit): `GET /api/v1/admin/inventory/stock`, `/stock/{stockItemId}` (ETag), `/batches`, `/movements`, `/reservations`, `/cold-chain` — all `inventory.view`, location-scoped, standard paging.

## 5. Recurring jobs (Worker, Hangfire)

| Job | Schedule | Work exists when | Does |
|---|---|---|---|
| Batch expiry | Every 15 min | Any `batches` row `status = ACTIVE and best_before_at <= now` (partial index) | Group by stock item; per stock item one transaction: batch → EXPIRED, WASTE `EXPIRED` for remaining (INV-R15), `on_hand −=`; raise `BatchExpired`, R17 events, `StockOverCommitted`?. Conflict → skip, next run retries. |
| Low-stock digest | Hourly | Locations whose local time is 07:00–07:59 **and** that have items with `threshold > 0 and on_hand − reserved <= threshold` | One `LowStockDigestReady` per location. |

Reservation expiry is not an Inventory job (Q2).

## 6. Test scenarios

### Unit (domain, no database)

| # | Scenario | Expect |
|---|---|---|
| U1 | Gross 500 g at 6500 bp | 770 |
| U2 | Gross 1000 g at 10000; 1 g at 3333 | 1000; 4 |
| U3 | Gross for PIECE 2 at 6500 | 2 |
| U4 | Max net orderable: available 770 at 6500 | 500; and gross(500) = 770 ≤ 770 |
| U5 | Reserve 770 with on hand 1000, reserved 300 | `OUT_OF_STOCK`, no change |
| U6 | Reserve two lines, second short | Whole reserve refused, first line not reserved |
| U7 | FEFO 770 from A(300, bb D+1), B(1000, bb D+2) | Movements A −300, B −470; A DEPLETED; on_hand 530 |
| U8 | FEFO tie on best_before → older `received_at` first | Order verified |
| U9 | Waste cost 333 g from 10 000 g / 845 000 p batch | cost −28 139 |
| U10 | Batch fully drained in 3 movements with rounding | Σ cost = −total_cost_paise exactly |
| U11 | Explicit batch DISCARDED / EXPIRED | `BATCH_NOT_ACTIVE` |
| U12 | ADJUSTMENT_OUT leaving on_hand < reserved | `ADJUSTMENT_BELOW_ZERO` |
| U13 | Waste leaving on_hand < reserved (Q1) | Accepted, `StockOverCommitted` |
| U14 | Pack SALE gross > batches total | Batchless COUNT_CORRECTION + SALE pair, `StockDiscrepancyFound`, INV-I1 holds |
| U15 | Low stock: threshold 1000, available 1200 → 900 → 800 → 1100 | `LowStockReached` once, then `StockReplenished` once |
| U16 | Threshold 0 | No low-stock events; `OutOfStockReached` still raised at 0 |
| U17 | Count equal to on_hand | No movement |
| U18 | Positive count with no ACTIVE batch and no batchId | `VALIDATION_FAILED` `errors.batchId` |
| U19 | Cold chain CHILLER 4.1 / FREEZER −17.5 | Out of range both |
| U20 | Invalid reason code | `REASON_CODE_INVALID` |

### Integration (Postgres via Testcontainers)

| # | Scenario | Expect |
|---|---|---|
| I1 | **Concurrent reservations for the last stock**: on hand 1000; two checkouts each needing 770 start together | One succeeds; the other retries from Read and gets `OUT_OF_STOCK`; reserved = 770; no check-constraint error |
| I2 | **Yield-adjusted reservation**: order 500 g curry cut (6500) | Reservation row 770; `reserved` +770; available −770 |
| I3 | **FEFO across two batches** via `OrderPacked` (packed 500 net at 6500) | SALE rows on A and B with costs per INV-R15; reservation CONSUMED; on_hand and batch remaining match INV-I1 |
| I4 | **Waste cost**: `POST /admin/inventory/waste` 333 g | Movement `cost_paise` −28 139; `WasteRecorded` in outbox |
| I5 | **Duplicate event delivery**: `OrderPacked` dispatched twice (and once with inbox row removed) | One set of SALE rows; reservation CONSUMED once; on_hand decreased once |
| I6 | Duplicate `OrderCancelled` | Released once; reserved decreased once |
| I7 | Receipt replay with same `Idempotency-Key` | One batch, one RECEIPT; `Idempotent-Replayed: true` |
| I8 | Receipt with duplicate batch code at location | 409 `BATCH_CODE_TAKEN` |
| I9 | Adjustment with stale `If-Match` / no `If-Match` | 412 `CONCURRENCY_CONFLICT` / 428 `VERSION_REQUIRED` |
| I10 | Two adjustments with the same matching version | One 200, one 409 `CONCURRENT_SAVE` |
| I11 | Expiry job: one batch past best-before with remaining 400 | EXPIRED, WASTE `EXPIRED` −400, `BatchExpired`; second run writes nothing |
| I12 | Expiry job with no expired batches | No transaction opened |
| I13 | `OrderPaymentFailed` | Reservations EXPIRED, reserved restored |
| I14 | Receipt at 5.2 °C | 201, `temperatureWarning: true`, out-of-range cold-chain log |
| I15 | Staff scoped to location A lists stock | Only A; command on B → 403 `LOCATION_ACCESS_DENIED` |
| I16 | `GET /availability` LOW_STOCK / OUT_OF_STOCK / NOT_SOLD_HERE | Status per INV-R13 |

### Variant names on reads (G3)

The admin GET reads (stock list and detail with its batches, batches, movements, reservations) add `productName`,
`variantName` and `sku` from `ICatalogApi.GetVariantsAsync` — one call per page, never per row; movements also
carry the stock item's `variantId`. Names are null when Catalog does not know the variant and in write responses.
Inventory stores no names (Catalog owns them).


## 7. API gaps

| # | Gap | Proposal |
|---|---|---|
| A1 | `GET /availability` owner and `NOT_SOLD_HERE` need price presence (Pricing). Inventory does not use Pricing.Public. | Inventory owns the endpoint; add `Pricing` to Inventory's uses, or move the endpoint to Catalog (E2). |
| A2 | Request bodies not specified for stock-items, threshold, discard, waste, adjustments, counts, cold-chain. | stock-items `{fulfillmentLocationId, variantId, lowStockThreshold}`; threshold `{lowStockThreshold}`; discard `{reasonCode, notes}`; waste `{stockItemId, batchId?, quantity, reasonCode, notes}`; adjustments `{stockItemId, kind, batchId?, quantity, reasonCode, notes}`; counts `{stockItemId, countedQuantity, batchId?, notes}`; cold-chain `{fulfillmentLocationId, storageUnit, temperatureC, recordedAt?}`. |
| A3 | No way to record `RETURN` movements (kind exists in DB). | Allow `kind: RETURN` on `/admin/inventory/adjustments` with explicit `batchId`, or drop `RETURN` from the kind list. |
| A4 | Adjustment reason codes not listed. | INV-R5 list. |
| A5 | `GET /admin/inventory/stock/{stockItemId}` is the only source of the stock item ETag; discard/adjust/count quote it — fine, but `GET /batches` items should carry `stockItemId` and `stockItemVersion`. | Add both fields. |
| A6 | DB: `stock_items` check `reserved <= on_hand` blocks physical-fact movements (Q1). | Drop that check; keep `on_hand >= 0`, `reserved >= 0`. |
| A7 | DB: `stock_movements.batch_id` must be nullable (INV-R14 batchless pair). Reason code `PACK_SHORTFALL`. | Nullable; add reason. |
| A8 | Cold-chain ranges are not data anywhere (rule 7). | Reference file `storage-units.json` (code, minC, maxC) loaded by Migrations; `storage_unit` validated against it. |
| A9 | `docs/database` §4 puts pack and cancel stock changes inside Ordering's transaction; `CLAUDE.md` rule 5 and the brief say handler. | Handler (this spec); update database §4. |

## 8. Open questions (with proposed answers)

| # | Question | Proposal |
|---|---|---|
| Q1 | Overweight packing, waste, expiry or a count can physically leave less stock than is reserved; DB check `reserved <= on_hand` would reject a true fact. | Drop the DB check (A6). New reservations and ADJUSTMENT_OUT still refuse; physical facts are recorded and raise `StockOverCommitted` so staff short-supply or substitute. |
| Q2 | Who owns reservation expiry for unpaid ONLINE orders? | Ordering job (order status is the authority, avoids releasing stock for a just-captured payment); Inventory reacts to `OrderPaymentFailed`. |
| Q3 | Ordering event names and payloads. | E4 publishes `OrderPlaced`, `OrderConfirmed`, `OrderPaymentFailed`, `OrderCancelled{previousStatus}`, `OrderPacked{lines[..., stockUnit, yieldBp, packedQuantity, status], coldBoxUsed}`, `OrderDelivered` with the fields in §3. |
| Q4 | PACK variants: stock unit is GRAM or PIECE, but quantity is a pack count. | PACK variants use stock unit `PIECE` (1 pack = 1 unit, yield ignored); a GRAM-stocked PACK variant is refused at variant setup (E2). |
| Q5 | Substitution accept needs stock for the new variant; a handler cannot refuse. | Extend the checkout exception: Ordering calls `Inventory.Public.Reserve` / `Release` in the substitution-accept transaction. |
| Q6 | Cold-chain ranges per storage unit. | CHILLER −1.0…4.0, FREEZER ≤ −18.0, RECEIPT ≤ 4.0 as reference data (A8). |
| Q7 | Adjustment reason list. | `DATA_ENTRY_ERROR`, `FOUND`, `LOST`, `THEFT`, `TRANSFER`, `OTHER` (notes required). |
| Q8 | Movement `cost_paise` sign. | Same sign as `quantity`; Reporting (E5) negates SALE cost for margin. |

## 9. Implementation status (E3)

Implemented with the proposed answers, as ruled in the decision log (D3, D6, D7, D10, D16, D22, D23).

| Item | Resolution |
|---|---|
| A1 | `GET /availability` is Catalog's (D3); Inventory exposes `IInventoryApi.GetAvailabilityAsync` with `LowStockThreshold`. |
| A2, A3, A4, A5 | As proposed; see the notes under docs/api §4.14. `RETURN` is an adjustment kind and needs `batchId`. |
| A6, A7, Q1 | No `reserved <= on_hand` check; nullable `batch_id`; `PACK_SHORTFALL` pair; `StockOverCommitted` is raised when a physical fact makes the shortfall larger. |
| A8, Q6 | `storage_units` table loaded from `data/reference/inventory/storage-units.json`; cold-chain `storage_unit` has an FK to it. A missing `RECEIPT` range refuses a receipt with `CONFIGURATION_MISSING`. |
| A9 | Pack consumption, release and expiry run in event handlers (`OrderStockHandlers`). |
| Cost | `batches.remaining_cost_paise` holds the cost still on the shelf. A movement that drains a batch takes the exact remainder (instead of summing past movements). |
| Unit | `stock_items.stock_unit` stores GRAM or PIECE so commands can check the request unit without calling Catalog. |
| Low-stock | `StockReplenished` is raised when available rises back above the threshold (with threshold 0: back above zero). Digest: one `low_stock_digests` row per location and local day, built at 07:xx local time. |
| Reservation | `ReserveAsync` is all or nothing; `ReserveStockResult.Refusals` gives the code per order item (`OUT_OF_STOCK` or `STOCK_ITEM_MISSING`). |

**Freshness for the storefront (CAT-R25).** `InventoryFreshnessSource` implements Catalog's `IVariantFreshnessSource`: per variant at a location, the batch the next order is packed from (ACTIVE, `remaining_quantity > 0`, `best_before_at` after now; earliest best-before, then earliest received — the FEFO picking order), returning its `caught_or_harvested_on` (nullable) and `received_at`. Read-only, one query.
