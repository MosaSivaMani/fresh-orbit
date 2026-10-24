# Delivery module specification

Owner: E3. Schema `delivery`. Uses `Locations.Public` (locations, city time zone, closures). Source docs: `CLAUDE.md`,
`docs/architecture/README.md`, `docs/database/README.md` (§3 delivery, §4), `docs/api/README.md` (§2 Delivery and
Ordering slot codes, §4.5, §4.8 tracking, §4.17, §6). Items marked **(Qn)** depend on an open question in §7; the spec
is written as if the proposed answer were accepted.

Times: stored UTC; template times and `slot_date` are in the location's city time zone. `day_of_week` is ISO (1 = Monday … 7 = Sunday).

## 1. Aggregates

| Aggregate | Root (table) | Children | Decides | Row version |
|---|---|---|---|---|
| SlotTemplate | `slot_templates` | — | Weekly pattern: day, start, end, capacity, cutoff; active flag | `xmin` |
| DeliverySlot | `delivery_slots` | — | Bookability (open, cutoff, capacity, location), booked count, manual capacity / close | `xmin` |
| Rider | `riders` | — | Active, location | `xmin` |
| Shipment | `shipments` | `delivery_attempts` (insert-only) | Shipment state machine, rider assignment, packed/dispatch facts, delivery code check, attempt numbering, reattempt limit, cash record | `xmin` |

| Boundary rule |
|---|
| A slot is not a child of its template: it is booked concurrently at checkout and must lock alone. Template changes reach slots through the module's own handler (DEL-R12). |
| A shipment refers to a slot and a rider by id only; it never changes them. Slot booking is changed only through DeliverySlot. |
| Delivery never writes order, payment or invoice rows; order status follows from Delivery events handled by Ordering (Q1). |
| Other modules call `Delivery.Public`: `BookSlot` / `GetSlot` (checkout), `GetSlots` / `GetNextSlot` (serviceability), `GetShipmentForOrder` (tracking), `HasActiveSlotTemplate` (city readiness). |

### Invariants

| Key | Invariant |
|---|---|
| DEL-I1 | `0 <= booked_count <= capacity` (DB check). |
| DEL-I2 | `start_at < end_at`, `cutoff_at <= start_at`; unique (location, `start_at`). |
| DEL-I3 | One shipment per order; attempts numbered 1, 2, … without gaps per shipment. |
| DEL-I4 | A shipment in `OUT_FOR_DELIVERY` has a rider and `packed_at` (A2). |

## 2. States and transitions

### Delivery slot (`delivery_slots.status`, stored)

| From | To | Trigger |
|---|---|---|
| — | OPEN | Slot generation (DEL-R9) |
| OPEN | CLOSED | `PATCH /admin/delivery/slots/{id}` (`closed_reason MANUAL`); `LocationClosureAdded` (`CLOSURE`); template deactivated and no booking (`TEMPLATE_INACTIVE`) |
| CLOSED | OPEN | PATCH (refused while a closure exists for the date: `SLOT_NOT_AVAILABLE`); `LocationClosureRemoved` reopens only `CLOSURE`-closed slots |

Displayed status (customer, `GET /delivery-slots`, checkout preview), computed, never stored:

| Order | Condition | Display |
|---|---|---|
| 1 | `status = CLOSED` | `CLOSED` |
| 2 | `now >= cutoff_at` | `CUTOFF_PASSED` |
| 3 | `booked_count >= capacity` | `FULL` |
| 4 | otherwise | `AVAILABLE` |

### Slot template

`is_active` true ↔ false via `PUT /admin/delivery/slot-templates/{id}`. No other states.

### Rider

`is_active` true ↔ false via `PUT /admin/delivery/riders/{riderId}`; deactivation refused while the rider has ASSIGNED / OUT_FOR_DELIVERY / FAILED shipments (A6).

### Shipment (`shipments.status`)

| From | To | Trigger | Order status effect (Ordering handler) |
|---|---|---|---|
| — | PENDING | `OrderPlaced` handler | — |
| PENDING | ASSIGNED | Assign rider | — |
| ASSIGNED | ASSIGNED | Reassign rider | — |
| ASSIGNED | OUT_FOR_DELIVERY | Dispatch (needs `packed_at`) | PACKED → OUT_FOR_DELIVERY |
| OUT_FOR_DELIVERY | DELIVERED | Deliver (code ok) | → DELIVERED + invoice |
| OUT_FOR_DELIVERY | FAILED | Failed attempt | → DELIVERY_FAILED |
| FAILED | FAILED | Reassign rider | — |
| FAILED | OUT_FOR_DELIVERY | Reattempt (DEL-R16) | DELIVERY_FAILED → OUT_FOR_DELIVERY |
| FAILED | RETURNED | Return | → RETURNED |
| PENDING, ASSIGNED | CANCELLED | `OrderCancelled` / `OrderPaymentFailed` handler (A1) | — |
| DELIVERED, RETURNED, CANCELLED | — | Terminal; any command → 409 `INVALID_STATUS_TRANSITION` | |

### Delivery attempt (`delivery_attempts.outcome`, immutable)

| Outcome | Written by |
|---|---|
| `DELIVERED` | Deliver |
| `CUSTOMER_UNAVAILABLE`, `ADDRESS_NOT_FOUND`, `REFUSED`, `OTHER` | Failed attempt (`OTHER` needs `notes`) |

## 3. Business rules

### Validation

| Key | Rule | Refusal |
|---|---|---|
| DEL-R1 | Template: location exists; `dayOfWeek` 1–7; `startTime < endTime`, same day, `HH:mm` on a 15-minute boundary; `capacity` 1–500; `cutoffMinutes` 0–2880. | 422 `VALIDATION_FAILED` |
| DEL-R2 | Active templates of one location and day must not overlap (`[start, end)`). | 409 `SLOT_TEMPLATE_OVERLAP` |
| DEL-R3 | Slot PATCH: `capacity >= booked_count`; `capacity` 0–500 (0 = no new bookings). | 422 `SLOT_CAPACITY_BELOW_BOOKED` / `VALIDATION_FAILED` |
| DEL-R4 | Rider: `fullName` 1–120, phone E.164 unique among active riders, location exists. | 422 `VALIDATION_FAILED`; 409 `DUPLICATE` (`errors.phone`) |
| DEL-R5 | Assign: rider active and at the shipment's location (slot's location); shipment PENDING, ASSIGNED or FAILED. | 422 `RIDER_NOT_AVAILABLE`; 409 `INVALID_STATUS_TRANSITION` |
| DEL-R6 | Dispatch: shipment ASSIGNED, `packed_at` set, rider still active; `dispatchTemperatureC` −40.0…40.0 required. | 409 `SHIPMENT_NOT_DISPATCHABLE`; 422 `RIDER_NOT_AVAILABLE` / `VALIDATION_FAILED` |
| DEL-R7 | Deliver: shipment OUT_FOR_DELIVERY; `deliveryCode` 4 digits matches (constant-time); after 5 wrong codes on a shipment, refused until reattempt (A4). `cashCollectedPaise >= 0`, only once per shipment. | 422 `DELIVERY_CODE_INVALID` / `DELIVERY_CODE_LOCKED` (A4); 409 `CASH_ALREADY_COLLECTED` |
| DEL-R8 | Staff location scope on every admin command and list. | 403 `LOCATION_ACCESS_DENIED` |

### Calculation

| Key | Rule |
|---|---|
| DEL-R9 | **Slot generation.** For each ACTIVE fulfillment location, for each local date `today … today + 6` (7 dates, matches `GET /delivery-slots`): skip the date if a `location_closures` row exists; for each active template with `day_of_week` = ISO day of the date: `start_at = (date + start_time)` in the city time zone → UTC; `end_at` likewise; `cutoff_at = start_at − cutoff_minutes`; `capacity` from template; `slot_template_id` set (A3). Skip when `cutoff_at <= now` or a slot with (location, `start_at`) already exists. Read all existing slots for the horizon in one query; insert only missing ones; unique (location, `start_at`) is the backstop (conflict = skip). |
| DEL-R10 | **Booking** (`Delivery.Public.BookSlot(slotId, fulfillmentLocationId)` inside checkout): slot exists, `status = OPEN`, same location, else `SLOT_NOT_AVAILABLE`; `now >= cutoff_at` → `SLOT_CUTOFF_PASSED`; `booked_count >= capacity` → `SLOT_FULL`; else `booked_count += 1`. Checks in that order. Tracks only; caller saves. |
| DEL-R11 | **Release** (handler): `booked_count = max(0, booked_count − 1)`, whatever the time or status (freed capacity before cutoff is bookable again). |
| DEL-R12 | **Template change** (own handler of `SlotTemplateChanged`): future slots (`start_at > now`) of the template: `booked_count = 0` → times, cutoff, capacity rewritten from the template (if new `start_at` collides with another slot → close this one `TEMPLATE_INACTIVE` and let generation create the new one); `booked_count > 0` → times and cutoff kept, `capacity = max(new capacity, booked_count)`. Deactivated template → unbooked future slots CLOSED (`TEMPLATE_INACTIVE`), booked ones keep OPEN with `capacity = booked_count`. Then run DEL-R9 for the location. Manual slot capacity on an unbooked slot is overwritten. |
| DEL-R13 | **Closure added** (`LocationClosureAdded{locationId, closedOn}`): OPEN slots of that local date → CLOSED (`CLOSURE`); if any has `booked_count > 0` raise `SlotClosedWithBookings`. **Closure removed**: `CLOSURE`-closed slots of that date with `start_at > now` → OPEN; DEL-R9 fills missing ones. |
| DEL-R14 | **Delivery code.** Generated at dispatch (4 digits, cryptographic RNG), kept across reattempts; shown to the customer only while OUT_FOR_DELIVERY (via `GetShipmentForOrder`); storage per A4. |
| DEL-R15 | **Attempt numbering.** `attempt_no = max(attempt_no) + 1` read with the shipment; unique (shipment, attempt_no) is the backstop. |
| DEL-R16 | **Reattempt.** Allowed only from FAILED, while attempts so far `< max_delivery_attempts` (default 3, A11, Q3), and `now` is on the slot's local date (perishable; Q3). Rider may be changed before (assign). Code failure counter reset. Otherwise only Return. |
| DEL-R17 | **Dispatch temperature** > 4.0 °C is accepted, shipment flagged, `ShipmentDispatched.temperatureWarning = true`. |
| DEL-R18 | **Cash.** Delivery records what the rider reports (`cash_collected_paise`, mode, reference, at) and never compares it with the amount due (Delivery cannot read orders); Payments compares and flags. |

### Concurrency

| Key | Rule |
|---|---|
| DEL-R19 | Commands with `If-Match` (template PUT, slot PATCH, rider PUT, shipment assign / dispatch / deliver / attempts / reattempt / return): stale → 412 `CONCURRENCY_CONFLICT`; lost race → 409 `CONCURRENT_SAVE`; no retry. |
| DEL-R20 | Commands without `If-Match` (template POST, rider POST, `cash-collected` with IK): retry from Read on `ConcurrentSaveException`, max 3, then 409 `CONCURRENT_SAVE`. |
| DEL-R21 | Checkout booking: DeliverySlot saved with `xmin` in Ordering's single save; lost race → Ordering retries the checkout from Read (max 3); `BookSlot` is side-effect free. DB check `booked_count <= capacity` is the last line. |
| DEL-R22 | Template overlap (DEL-R2) is checked by reading siblings; a concurrent create can slip through → DB `EXCLUDE` constraint (A5) is the backstop, mapped to 409 `SLOT_TEMPLATE_OVERLAP`. |
| DEL-R23 | Handlers: lost race → throw, outbox retries; inbox row in the same transaction; dead letter after max attempts. |

### Events raised

| Event | Payload | Consumers |
|---|---|---|
| `SlotTemplateChanged` | slotTemplateId, fulfillmentLocationId, isActive | Delivery (DEL-R12) |
| `SlotClosedWithBookings` | deliverySlotId, fulfillmentLocationId, startAt, bookedCount, reason | Notifications (ops alert) |
| `RiderAssigned` | shipmentId, orderId, riderId, previousRiderId | Notifications, Audit |
| `ShipmentDispatched` | shipmentId, orderId, riderId, dispatchedAt, dispatchTemperatureC, temperatureWarning, coldBoxUsed | Ordering (→ OUT_FOR_DELIVERY), Notifications |
| `ShipmentDelivered` | shipmentId, orderId, riderId, deliveredAt, cashCollectedPaise?, cashMode?, upiReference? | Ordering (→ DELIVERED + invoice), Payments (COLLECTED) |
| `DeliveryAttemptFailed` | shipmentId, orderId, attemptNo, outcome, notes, attemptedAt | Ordering (→ DELIVERY_FAILED), Notifications |
| `ShipmentReattempted` | shipmentId, orderId, riderId, attemptNo | Ordering (→ OUT_FOR_DELIVERY) |
| `ShipmentReturned` | shipmentId, orderId, returnedAt | Ordering (→ RETURNED) |
| `CashCollected` | shipmentId, orderId, riderId, amountPaise, mode, upiReference, collectedAt | Payments |
| `ShipmentRecallRequired` | shipmentId, orderId, riderId | Notifications (order cancelled after dispatch) |

### Events consumed

| Event (from) | Payload needed | Handler (idempotent) |
|---|---|---|
| `OrderPlaced` (Ordering) | orderId, fulfillmentLocationId, deliverySlotId, placedAt | Create shipment PENDING. Shipment for `order_id` exists → no-op (unique `order_id` backstop). |
| `OrderConfirmed` (Ordering) | — | Not consumed. |
| `OrderPaymentFailed` (Ordering) | orderId, deliverySlotId | Shipment PENDING/ASSIGNED → CANCELLED and release slot (DEL-R11) in one save. Already CANCELLED → no-op. |
| `OrderCancelled` (Ordering) | orderId, deliverySlotId, previousStatus | PENDING/ASSIGNED → CANCELLED + release slot. OUT_FOR_DELIVERY → raise `ShipmentRecallRequired`, no change. CANCELLED → no-op. No shipment yet (event order) → create it CANCELLED and release slot, so a late `OrderPlaced` no-ops. |
| `OrderPacked` (Ordering) | orderId, packedAt, coldBoxUsed | Set `packed_at`, `cold_box_used`. Already set → no-op. Shipment CANCELLED → no-op. |
| `OrderDelivered` (Ordering) | — | Not consumed: Delivery is the source (`ShipmentDelivered`). |
| `LocationClosureAdded` / `LocationClosureRemoved` (Locations) | fulfillmentLocationId, closedOn | DEL-R13; state-based, re-run is a no-op. |
| `FulfillmentLocationActivated` (Locations) | fulfillmentLocationId | Run DEL-R9 for the location. |

## 4. Commands

| Command | Endpoint (auth) | Read | Decide | Commit | Follow-on |
|---|---|---|---|---|---|
| Create template | `POST /api/v1/admin/delivery/slot-templates` (delivery.manage) | Location, active templates same location/day | DEL-R1, R2, R8 | Insert template | `SlotTemplateChanged` → R12/R9 |
| Update template | `PUT /api/v1/admin/delivery/slot-templates/{id}` (delivery.manage, IM) | Template, siblings | DEL-R1, R2 | Update template | `SlotTemplateChanged` → R12 |
| Change slot | `PATCH /api/v1/admin/delivery/slots/{slotId}` (delivery.manage, IM) | Slot, closure for date (Locations.Public) | DEL-R3, §2 slot | Capacity / status / `closed_reason` | `SlotClosedWithBookings` if closed with bookings |
| Create rider | `POST /api/v1/admin/delivery/riders` (delivery.manage) | Location, active riders by phone | DEL-R4 | Insert | Audit |
| Update / deactivate rider | `PUT /api/v1/admin/delivery/riders/{riderId}` (delivery.manage, IM) | Rider, open shipments count | DEL-R4, §2 rider | Update | Audit |
| Assign rider | `POST /api/v1/admin/delivery/shipments/{shipmentId}/assign` (delivery.manage, IM) | Shipment, slot (location), rider | DEL-R5 | Rider id; PENDING → ASSIGNED | `RiderAssigned` |
| Dispatch | `POST /api/v1/admin/delivery/shipments/{shipmentId}/dispatch` (delivery.manage, IM) | Shipment, rider | DEL-R6, R14, R17 | OUT_FOR_DELIVERY, `dispatched_at`, temperature, code | `ShipmentDispatched` |
| Deliver | `POST /api/v1/admin/delivery/shipments/{shipmentId}/deliver` (delivery.manage, IM) | Shipment + attempts | DEL-R7, R15, R18 | DELIVERED, `delivered_at`, attempt DELIVERED, cash fields if given | `ShipmentDelivered` (+ `CashCollected` if cash given) |
| Failed attempt | `POST /api/v1/admin/delivery/shipments/{shipmentId}/attempts` (delivery.manage, IM) | Shipment + attempts | §2 attempt outcomes, R15 | Attempt row; → FAILED | `DeliveryAttemptFailed` |
| Reattempt | `POST /api/v1/admin/delivery/shipments/{shipmentId}/reattempt` (delivery.manage, IM) | Shipment + attempts, slot | DEL-R16 | → OUT_FOR_DELIVERY, code counter reset | `ShipmentReattempted` |
| Return | `POST /api/v1/admin/delivery/shipments/{shipmentId}/return` (delivery.manage, IM) | Shipment | FAILED only | → RETURNED (slot booking kept) | `ShipmentReturned` |
| Record cash | `POST /api/v1/admin/delivery/shipments/{shipmentId}/cash-collected` (delivery.manage, IK) | Shipment | DELIVERED, not yet recorded (`CASH_ALREADY_COLLECTED`), `amountPaise > 0`, `mode` CASH/UPI, `upiReference` required for UPI | Cash fields | `CashCollected` |
| Book slot | none: `Delivery.Public.BookSlot` | Slot | DEL-R10 | Tracks `booked_count + 1`; caller saves | — |
| Slot for preview | none: `Delivery.Public.GetSlot(slotId, locationId)` | Slot | Displayed status (§2) | — | — (preview blockers) |
| Shipment for tracking | none: `Delivery.Public.GetShipmentForOrder(orderId)` | Shipment, rider, attempts | Code only while OUT_FOR_DELIVERY | — | — |
| Readiness / next slot | none: `HasActiveSlotTemplate(locationId)`, `GetNextSlot(locationId)` | Templates / slots | Next = earliest `AVAILABLE` | — | — |

Reads: `GET /api/v1/delivery-slots?date=` (public, LOC; today + 6, displayed status), `GET /api/v1/admin/delivery/slot-templates`, `/slots` (with `bookedCount`), `/riders`, `/shipments` — location-scoped, list items carry `version`.

## 5. Recurring jobs (Worker, Hangfire)

| Job | Schedule | Work exists when | Does |
|---|---|---|---|
| Slot generation | Hourly (also enqueued after template change, closure removal, location activation) | For some ACTIVE location the expected set (DEL-R9) minus existing (location, `start_at`) is non-empty — computed from one read of templates, closures and horizon slots | Inserts missing slots, one transaction per location; nothing written otherwise. |
| Stale shipment alert (not implemented in Phase 2) | Every 15 min | Shipments PENDING/ASSIGNED whose slot `start_at <= now` without `packed_at`, or OUT_FOR_DELIVERY with `dispatched_at` older than slot `end_at` + 60 min | Raises one `ShipmentsOverdue{fulfillmentLocationId, shipmentIds}` per location. (Optional; drop if not wanted.) |

## 6. Test scenarios

### Unit (domain)

| # | Scenario | Expect |
|---|---|---|
| U1 | Generate for Mon template 08:00–10:00, cutoff 120, tz Asia/Kolkata, date Mon 2026-10-26 | `start_at` 02:30Z, `end_at` 04:30Z, `cutoff_at` 00:30Z |
| U2 | Date with a closure | No slot |
| U3 | Today's slot whose cutoff passed | Not generated |
| U4 | Existing slot at same `start_at` | Not regenerated |
| U5 | Book: booked 9 / capacity 10 | booked 10 |
| U6 | **Slot full**: booked 10 / 10 | `SLOT_FULL` |
| U7 | **Cutoff passed**: now = `cutoff_at` | `SLOT_CUTOFF_PASSED` (checked before full) |
| U8 | Closed slot / other location | `SLOT_NOT_AVAILABLE` |
| U9 | Release at booked 0 | stays 0 |
| U10 | PATCH capacity below booked | `SLOT_CAPACITY_BELOW_BOOKED` |
| U11 | Template overlap 08:00–10:00 vs 09:30–11:00 same day; adjacent 10:00–12:00 | Overlap refused; adjacent allowed |
| U12 | Template change with booked slot | Times kept, capacity = max(new, booked) |
| U13 | Dispatch without `packed_at` / without rider | `SHIPMENT_NOT_DISPATCHABLE` |
| U14 | Deliver wrong code ×5, then right code | 5 × `DELIVERY_CODE_INVALID`, then `DELIVERY_CODE_LOCKED` |
| U15 | Reattempt after 3 attempts / on the next local day | Refused `REATTEMPT_NOT_ALLOWED` in both cases (A6) |
| U16 | Attempt numbering after deliver following one failure | Attempts 1 (failed), 2 (DELIVERED) |
| U17 | Assign inactive rider / rider of other location | `RIDER_NOT_AVAILABLE` |
| U18 | Displayed status precedence CLOSED > CUTOFF_PASSED > FULL > AVAILABLE | As table |

### Integration (Postgres via Testcontainers)

| # | Scenario | Expect |
|---|---|---|
| I1 | **Slot full under concurrency**: capacity 1, two checkouts book together | One booked; other retries and gets `SLOT_FULL`; `booked_count` 1, no check violation |
| I2 | **Cutoff passed** at checkout via `BookSlot` with fake clock past `cutoff_at` | `SLOT_CUTOFF_PASSED`, nothing saved |
| I3 | Generation job twice in a row | Second run inserts nothing, opens no write transaction |
| I4 | Generation with two templates on every weekday (7 × 2), a closure on day 3, fake clock before all cutoffs | 12 slots (6 dates × 2), none on the closure date |
| I5 | `LocationClosureAdded` for a date with a booked slot | Slots CLOSED (`CLOSURE`), `SlotClosedWithBookings` in outbox; removal reopens them |
| I6 | **Duplicate event delivery**: `OrderPlaced` twice | One shipment |
| I7 | `OrderCancelled` twice (and with inbox row removed) | Shipment CANCELLED once; `booked_count` decreased once |
| I8 | `OrderCancelled` before `OrderPlaced` (out of order) | Shipment CANCELLED, slot released once; late `OrderPlaced` no-op |
| I9 | Full happy path: placed → packed → assign → dispatch → deliver with cash | States as §2; `RiderAssigned`, `ShipmentDispatched`, `ShipmentDelivered` (with cash fields) in outbox. `CashCollected` and `cash-collected` belong to Payments (D14) |
| I10 | Failed attempt → reattempt → deliver | Attempts 1, 2; events in order |
| I11 | Dispatch with stale `If-Match` / missing | 412 / 428 |
| I12 | Template PUT with booked future slots | Booked slot times unchanged, unbooked slots rewritten |
| I13 | `GET /delivery-slots` | 7 dates, displayed statuses correct, other locations excluded |
| I14 | Staff scoped to location A assigns shipment of B | 403 `LOCATION_ACCESS_DENIED` |

## 7. API gaps

| # | Gap | Proposal |
|---|---|---|
| A1 | `shipments.status` has no state for an order cancelled before dispatch. | Add `CANCELLED`. |
| A2 | Dispatch needs "order is PACKED"; Delivery cannot read orders. | Add `shipments.packed_at`, set from `OrderPacked`. |
| A3 | Template change and closure removal must find their slots. | Add `delivery_slots.slot_template_id` (nullable) and `closed_reason` `MANUAL`/`CLOSURE`/`TEMPLATE_INACTIVE`. |
| A4 | Tracking shows the plain `deliveryCode`, but only `delivery_code_hash` is stored; a 4-digit hash is brute-forceable anyway. | Store `delivery_code` (char 4) + `delivery_code_failures`; lock after 5 wrong; new code `DELIVERY_CODE_LOCKED` (422). |
| A5 | Template overlap needs a DB backstop. | `EXCLUDE USING gist (fulfillment_location_id WITH =, day_of_week WITH =, int4range(start_min, end_min) WITH &&) WHERE (is_active)` or a generated range column. |
| A6 | Missing error codes. | `RIDER_HAS_ACTIVE_SHIPMENTS` (409), `REATTEMPT_NOT_ALLOWED` (422), `DELIVERY_CODE_LOCKED` (422). |
| A7 | Deliver body has `cashCollectedPaise` but no `mode` / `upiReference` (cash-collected has them). | Add `cashMode` CASH/UPI and `upiReference` to deliver. |
| A8 | `CASH_ALREADY_COLLECTED` must be decided synchronously by Delivery. | Add `shipments.cash_collected_paise`, `cash_mode`, `upi_reference`, `cash_collected_at`. |
| A9 | No single GET for shipment / slot / template / rider; If-Match must come from list `version`. | Add `GET /admin/delivery/shipments/{shipmentId}` (ETag, attempts); others keep list `version`. |
| A10 | `GET /delivery-slots?date=`: single date or whole horizon? | `date` optional; absent = today … today + 6. |
| A11 | Max delivery attempts is not configuration anywhere (rule 7). | `settings.order_policies.max_delivery_attempts` default 3. |
| A12 | Ordering events `OrderPlaced`, `OrderPaymentFailed`, `OrderCancelled`, `OrderPacked` must carry the fields in §3; Locations must raise `LocationClosureAdded/Removed`, `FulfillmentLocationActivated` and expose city time zone and closures in `Locations.Public`. | Agree payloads with E4 / E2. |

## 8. Open questions (with proposed answers)

| # | Question | Proposal |
|---|---|---|
| Q1 | API §6 says the invoice is issued "in the same transaction" as deliver; `CLAUDE.md` rule 5 forbids Delivery writing Ordering rows. | Delivery commits the shipment and raises `ShipmentDelivered`; Ordering's handler issues the invoice and sets DELIVERED in its own single transaction (seconds of lag). Same for dispatch, attempts, reattempt, return. Update API §6 wording. |
| Q2 | Riders have no login; who records deliver / attempts / cash? | Dispatcher (`delivery.manage`) from the admin dashboard for now; rider app later. |
| Q3 | Reattempt limit and window. | Max 3 attempts total (A11), reattempt only on the slot's local date; after that Return. |
| Q4 | Cancel after dispatch (order PACKED → CANCELLED races with dispatch). | Delivery raises `ShipmentRecallRequired`; Ordering refuses cancel once its handler has moved the order to OUT_FOR_DELIVERY. |
| Q5 | Does a slot need to fall within the location's `operating_hours`? | No: delivery windows are independent of store hours; not validated. |
| Q6 | Ownership of `GET /delivery-slots` and `Delivery.Public.GetNextSlot` for `/serviceability`. | Delivery (E3) owns both. |

## 9. Phase 2 implementation notes

| Item | Resolution |
|---|---|
| A1–A10 | Implemented as proposed (A5 uses `tsrange(DATE '2000-01-01' + start_time, … + end_time)`, mapped to 409 `SLOT_TEMPLATE_OVERLAP`). |
| A11 | `IDeliveryPolicyProvider` port, default 3 attempts; wire to `order_policies.max_delivery_attempts` at merge. |
| A12 | Ordering/Locations events are consumed through `OrderEventHandlers` / `LocationEventHandlers` services; the `IEventHandler<T>` adapters are added at merge. Closures come through the `ILocationCalendar` port; its Locations.Public adapter waits for `GetClosuresAsync`. |
| Cash | `cash-collected` endpoint and `CashCollected` event are owned by Payments (D14). Delivery records cash given at deliver and raises it inside `ShipmentDelivered` (`cashCollectedPaise`, `cashMode`, `upiReference`). |
| Location scope | Staff outside a location get 403 `LOCATION_ACCESS_DENIED` from a module-local endpoint filter until SharedKernel maps a forbidden exception. |
| Customer slots | `GET /delivery-slots` returns `{ fulfillmentLocationId, items }` (not paged; at most 7 days of slots). |

## 10. Admin gaps G7 and G10 (branch `backend/api-gaps-admin-orders`)

| Rule | Behaviour |
|---|---|
| DEL-G7 | `GET /api/v1/admin/delivery/riders/lookup` is readable with `delivery.manage` **or** `cash.settle` (cash settlement pickers). Rows are `{ id, fulfillmentLocationId, fullName, phoneLast4, isActive }` (no full phone, no version); filters `fulfillmentLocationId`, `isActive`, `q` (name contains); ordered by name; location-scoped (a `fulfillmentLocationId` outside the caller's locations → 403 `LOCATION_ACCESS_DENIED`). The full rider list stays `delivery.manage`. |
| DEL-G10 | `GET /api/v1/admin/delivery/shipments` adds `slotDate` (local date of the shipment's slot, `yyyy-MM-dd`) and `sort`: comma list of `placedAt` (default), `slotStartAt`, `status`, `dispatchedAt`, `-` for descending; `placedAt` then id break ties. Unknown or repeated keys → 422 `VALIDATION_FAILED` on `sort`. |
