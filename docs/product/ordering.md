# Ordering module specification

Owner: E4. Schema `ordering`. Sources: `docs/database/README.md` (ordering, §4), `docs/api/README.md` §1.8, §2 Ordering,
§4.8, §4.15, §4.17, §5, §6, §7; `docs/decisions/log.md` D6–D17 (win over older text). Related: `docs/product/cart.md`,
`docs/product/promotions.md`, `docs/product/inventory.md`, `docs/product/delivery.md`, `docs/product/payments.md`.

| Cross-module write rule (D6) | Value |
|---|---|
| Same transaction through another module's `.Public` | Checkout (reserve stock, book slot, redeem coupon, order number, cart converted) and substitution accept (reserve new / release old) only |
| Everything else | Ordering commits its own rows + outbox; Inventory, Delivery, Promotions, Payments react in their handlers (seconds of lag accepted) |
| Invoice number | `Settings.NextNumber(INVOICE)` row lock inside Ordering's own `ShipmentDelivered` handler transaction (designed for the caller's transaction) |

## 1. Aggregates and boundaries

| Aggregate | Root / table | Children / table | Decides |
|---|---|---|---|
| Order | `Order` / `ordering.orders` (xmin) | `OrderItem` / `order_items`; `OrderStatusHistory` / `order_status_history`; `OrderAdjustment` / `order_adjustments`; `Substitution` / `substitutions` (under an item) | Placement snapshot, status machine, cancellation, weigh-and-pack final amounts, short supply, substitutions, adjustments, amount due / refund due |
| Complaint | `Complaint` / `ordering.complaints` (xmin) | `ComplaintAttachment` / `complaint_attachments` | Window, one open per line, decision, resolution |
| Invoice | `Invoice` / `ordering.invoices` | `InvoiceLine` / `invoice_lines` | Frozen GST invoice from final amounts; never edited |

| Collaborator (.Public) | Used for | In which transaction |
|---|---|---|
| Cart | `GetCheckoutCart`, `Quote`, `MarkConverted`, `AddLines` (reorder) | Checkout (MarkConverted); reorder |
| Customers | Customer status, address by id | Read only |
| Locations | Pincode → service area, location, city; closures | Read only |
| Catalog | Proposed substitution variant, names, images | Read only |
| Pricing | Substitution price | Read only |
| Inventory | `Reserve` (checkout, substitution accept), `Release` (substitution accept), `GrossQuantity.From`, `GetOrderStock` (admin detail) | Checkout, substitution accept; reads |
| Delivery | `BookSlot`; `GetShipmentForOrder` (tracking, delivery code) | Checkout; reads |
| Promotions | `Redeem` | Checkout |
| Settings | Order policy, `NextNumber(ORDER\|INVOICE)` (row-locked), business profile | Checkout, invoice handler |
| Payments | Enabled methods, `GetOrderPaymentSummary(ies)` (paid, refunded, refundable) | Reads |

| `IOrderingApi` (Ordering.Public) | Use |
|---|---|
| `HasQualifyingOrder(customerId)` | First-order coupons (Cart, checkout): any order not in `CANCELLED`/`PAYMENT_FAILED` |
| `GetOrderPaymentFacts(orderId)` | Payments online initiate (payments.md Q1): customerId, status, paymentMethod, orderNumber, amountDuePaise, paymentDueAt |
| `GetOrderStatus(orderId)` | Payments cash collection check |

## 2. States

### Order (matches API §6 exactly)

| From | To | Who / trigger | Endpoint or event |
|---|---|---|---|
| — | `CONFIRMED` | Customer, COD / UPI_ON_DELIVERY | `POST /orders` |
| — | `PENDING_PAYMENT` | Customer, ONLINE | `POST /orders` |
| `PENDING_PAYMENT` | `CONFIRMED` | Provider | `PaymentCaptured` (webhook `payment.captured`) |
| `PENDING_PAYMENT` | `PAYMENT_FAILED` | Provider / System | `PaymentFailed` (webhook `payment.failed`, ORD-R44); `ExpireUnpaidOrder` job (D8) |
| `PENDING_PAYMENT` | `CANCELLED` | Customer | `POST /orders/{id}/cancel` |
| `CONFIRMED` | `PREPARING` | Staff `orders.pack` | `POST /admin/orders/{id}/start-preparing` |
| `CONFIRMED` | `CANCELLED` | Customer (policy) / Staff `orders.cancel` | `POST /orders/{id}/cancel`, `POST /admin/orders/{id}/cancel` |
| `PREPARING` | `PACKED` | Staff `orders.pack` | `POST /admin/orders/{id}/pack` |
| `PREPARING` | `CANCELLED` | Customer (if policy allows) / Staff | same cancel endpoints |
| `PACKED` | `OUT_FOR_DELIVERY` | Staff `delivery.manage` | `POST /admin/delivery/shipments/{id}/dispatch` → `ShipmentDispatched` |
| `PACKED` | `CANCELLED` | Staff `orders.cancel` | `POST /admin/orders/{id}/cancel` (stock consumed → WASTE/RETURN by inventory staff) |
| `OUT_FOR_DELIVERY` | `DELIVERED` | Staff `delivery.manage` | `POST /admin/delivery/shipments/{id}/deliver` → `ShipmentDelivered` (invoice issued with the transition) |
| `OUT_FOR_DELIVERY` | `DELIVERY_FAILED` | Staff `delivery.manage` | `POST /admin/delivery/shipments/{id}/attempts` → `DeliveryAttemptFailed` |
| `DELIVERY_FAILED` | `OUT_FOR_DELIVERY` | Staff `delivery.manage` | `POST /admin/delivery/shipments/{id}/reattempt` → `ShipmentReattempted` |
| `DELIVERY_FAILED` | `RETURNED` | Staff `delivery.manage` | `POST /admin/delivery/shipments/{id}/return` → `ShipmentReturned` |

Any other transition → 409 `INVALID_STATUS_TRANSITION`. Terminal: `DELIVERED`, `CANCELLED`, `PAYMENT_FAILED`, `RETURNED`.
Status order for policy comparison: `PENDING_PAYMENT < CONFIRMED < PREPARING < PACKED < OUT_FOR_DELIVERY`.

### Order item

| From | To | Trigger |
|---|---|---|
| — | `ACTIVE` | Placement; accepted substitution (new line) |
| `ACTIVE` | `SHORT_SUPPLIED` | Pack with `packedQuantity = 0`; substitution rejected or expired |
| `ACTIVE` | `SUBSTITUTED` | Substitution accepted (original line) |
| `ACTIVE` | `CANCELLED` | Order cancelled / payment failed |
| others | — | Terminal |

### Substitution

| From | To | Trigger |
|---|---|---|
| — | `PROPOSED` | Staff proposes, preference `CALL_ME` (expires after `substitutionMinutes`, default 15) |
| — | `ACCEPTED` | Staff proposes, preference `AUTO_SIMILAR` and ORD-R33 guard passes |
| `PROPOSED` | `ACCEPTED` | Customer accept; staff `record-response` |
| `PROPOSED` | `REJECTED` | Customer reject; staff `record-response` |
| `PROPOSED` | `EXPIRED` | `expires_at` passed (job); order cancelled / payment failed |

### Complaint

| From | To | Trigger |
|---|---|---|
| — | `OPEN` | `POST /orders/{id}/complaints` |
| `OPEN` | `APPROVED` / `REJECTED` | `POST /admin/complaints/{id}/decide` |
| `APPROVED` | `RESOLVED` | `POST /admin/complaints/{id}/resolve` |
| `REJECTED`, `RESOLVED` | — | Terminal |

### Invoice

Created once at `DELIVERED`; no states; never edited or deleted.

## 3. Business rules

### Checkout (preview and place)

| Key | Rule |
|---|---|
| ORD-R1 | Customer JWT required; guest token only → 401 `LOGIN_REQUIRED`; customer not `ACTIVE` → 403 `CUSTOMER_DELETED`. |
| ORD-R2 | `POST /orders` requires `Idempotency-Key` (400 `IDEMPOTENCY_KEY_REQUIRED`); middleware scope (route, customer), 24 h: replay same body → original response + `Idempotent-Replayed: true`; different body → 422 `IDEMPOTENCY_KEY_REUSED`; running → 409 `IDEMPOTENCY_IN_PROGRESS`. |
| ORD-R3 | Second guard: Read looks up an order by `(customer_id, idempotency_key)`; found → 201 with that order, no writes. A unique violation on that index at save → same. |
| ORD-R4 | Lines come only from the server cart (`ACTIVE`, ≥ 1 line, else 422 `CART_EMPTY`); body carries no lines or prices. |
| ORD-R5 | Address must be the customer's active address (404); pincode must resolve to an active service area (422 `ADDRESS_NOT_SERVICEABLE`); its fulfillment location must equal the cart's, else 409 `CART_LOCATION_CHANGED` (client calls `PUT /cart/location` first). Fee and min order come from the address's service area. |
| ORD-R6 | Re-price via `ICartApi.Quote` at `now` (CART-R1..R12). Any line issue → 422 with that code, field `lines[i]`. |
| ORD-R7 | `subtotal − discount >= service area min_order_paise`, else 422 `MIN_ORDER_NOT_MET`. |
| ORD-R8 | Payment method enabled for the city (Payments.Public), else 422 `PAYMENT_METHOD_NOT_ALLOWED`. |
| ORD-R9 | COD or UPI_ON_DELIVERY and `maxTotalPaise > codMaxOrderPaise` (order policy, default 500000 = ₹5,000, D17) → 422 `COD_LIMIT_EXCEEDED` (Q3). |
| ORD-R10 | `expectedTotalPaise != estimatedTotalPaise` → 409 `PRICE_CHANGED`, member `preview` = full preview; checked after Decide, before any write. The refusal is stored for the key, so confirming the new price is a new user action with a new `Idempotency-Key`. |
| ORD-R11 | Transaction, in this order (database §4): **re-read prices** (R6) → **reserve stock** `Inventory.Reserve` (gross per `GrossQuantity.From`, per order item; `OUT_OF_STOCK`, `STOCK_ITEM_MISSING`) → **book slot** `Delivery.BookSlot` (`SLOT_FULL`, `SLOT_CUTOFF_PASSED`, `SLOT_NOT_AVAILABLE`, `LOCATION_CLOSED`) → **use coupon** `Promotions.Redeem` (`COUPON_*`) → **order + items + history** → **order number** `Settings.NextNumber(ORDER)` → cart `MarkConverted` → **outbox** (`OrderPlaced`). One `SaveChangesAsync`. The **pending payment** row is created by Payments' `OrderPlaced` handler (payments.md PAY-R3) from `amountDuePaise` in the event, not inside this transaction (Q1). |
| ORD-R12 | Aggregates (StockItem, DeliverySlot, Coupon, Cart) check availability and are saved with their xmin; `.Public` writes only track changes on the shared DbContext, never save. Number series row lock is taken last before save, inside the explicit transaction. DB checks `booked_count <= capacity`, `used_count <= total_limit` are the last line (`reserved <= on_hand` dropped by D7; `Reserve` refuses over-commit). Deadlock `40P01` = lost race. |
| ORD-R13 | `ConcurrentSaveException` → roll back, new unit of work, repeat from Read; up to 3 retries, then 409 `CONCURRENT_SAVE`. Rule refusals are not retried. A rolled-back attempt consumes no order number. |
| ORD-R14 | Initial status: COD / UPI_ON_DELIVERY → `CONFIRMED` (`confirmed_at = placed_at`, reservation `expires_at` null); ONLINE → `PENDING_PAYMENT`, `payment_due_at` and reservation `expires_at` = `now + reservationMinutes` (policy, default 15, D17). |
| ORD-R15 | Preview runs the same Read + Decide with no writes; business problems are returned as `blockers` (never 422); `expectedTotalPaise = estimatedTotalPaise`; `paymentMethods[]` with `reasonCode`. |
| ORD-R16 | Order header snapshot: address fields, city, location, service area, slot id + start/end, substitution preference, payment method, coupon code, `items/preparation/discount/delivery_fee/tax/total_paise` (estimate), notes (≤ 500 chars). |
| ORD-R17 | Item snapshot: product id/name, variant id/name, sku, image key, hsn, preparation code/name, sell unit, unit price, preparation charge rate + basis, tax rate, yield, ordered quantity, estimated total, max chargeable quantity, allocated discount. Later catalog/price changes never alter it. |
| ORD-R18 | `max_chargeable_quantity`: weight-variable variants floor(`ordered × (10000 + maxOverweightBp) / 10000`), policy at placement; others = ordered. |
| ORD-R19 | Line numbers 1..n in cart line order; substitution lines take the next number. |

### Status, reads, access

| Key | Rule |
|---|---|
| ORD-R20 | Every transition goes through `Order.ChangeStatus(to, actor, reason)`: validates §2, sets the status timestamp, appends `order_status_history`, raises `OrderStatusChanged`. |
| ORD-R21 | Staff limited to their locations: other location → 403 `LOCATION_ACCESS_DENIED`; lists filtered. Every admin write audited. |
| ORD-R22 | Another customer's order → 404. |
| ORD-R23 | `allowedActions`: `cancel` per R38; `raiseComplaint` = `DELIVERED` and within the complaint window (R54); `downloadInvoice` = invoice exists; `reorder` = always. |
| ORD-R24 | `amountPaidPaise`, `amountRefundedPaise`, `payment` come from Payments.Public; `final` null until `PACKED`; `deliveryCode` only while `OUT_FOR_DELIVERY`. |

### Weigh and pack

| Key | Rule |
|---|---|
| ORD-R25 | `POST /admin/orders/{id}/pack` only from `PREPARING` with `If-Match`. Any `PROPOSED` substitution → 409 `INVALID_STATUS_TRANSITION`. |
| ORD-R26 | Every `ACTIVE` line exactly once, unit = line unit, `packedQuantity >= 0` → else 422 `PACKED_QUANTITY_INVALID` keyed `lines[i]`. |
| ORD-R27 | `charged = min(packed, max_chargeable_quantity)`; `capped = packed > max_chargeable_quantity`. |
| ORD-R28 | Final line = shared line formula (CART-R3..R6) at `charged` with the item snapshot (price, prep rate + basis, tax rate). Stored split per line (D16): `charged_quantity`, `final_item_paise`, `final_preparation_paise`, `final_tax_paise`, `final_total_paise`; order stores the same split for `final`. |
| ORD-R29 | `packed = 0` → item `SHORT_SUPPLIED`, final 0, adjustment `SHORT_SUPPLY` = −estimated. Non-weight-variable `0 < packed < ordered` → `ACTIVE`, adjustment `SHORT_SUPPLY` = final − estimated. Weight-variable, final ≠ estimated → adjustment `WEIGHT` = final − estimated (signed). |
| ORD-R30 | Discount stays as redeemed; if final eligible subtotal < discount, discount = final eligible subtotal. Delivery fee unchanged; min order not re-checked. Discount re-allocated over final lines (PROMO-R14). |
| ORD-R31 | `final_total = Σ final lines − discount + delivery fee`. COD / UPI_ON_DELIVERY: `amount_due = final_total`, refund due 0. ONLINE: final total capped at the captured amount (D15): charged = `min(final_total, paid)`, `amount_due = 0`, `refundDue = paid − charged` split into `SHORT_SUPPLY` (Σ estimated of lines short-supplied at pack or by substitution) and `WEIGHT_SHORTFALL` (rest). |
| ORD-R32 | Pack commits only Ordering rows (`packed_at`, items, adjustments, totals) + `OrderPacked` (D6). Inventory's handler consumes reservations and posts SALE movements FEFO by gross of `packedQuantity` (above the reservation or book stock is recorded as a physical fact, D7); `SHORT_SUPPLIED` lines are released there. Delivery's handler sets `packed_at`, `cold_box_used`. Pack never refuses for stock. |

### Substitutions

| Key | Rule |
|---|---|
| ORD-R33 | Propose: order `PREPARING`, line `ACTIVE`, no `PROPOSED` on the line, preference ≠ `NONE` (else 422, gap code `SUBSTITUTION_NOT_ALLOWED`); proposed variant + preparation sellable and priced at the order's location, quantity valid for that variant. `price_difference_paise` = proposed estimated line − original estimated line. |
| ORD-R34 | `CALL_ME`: `PROPOSED`, `expires_at = now + substitutionMinutes` (policy, default 15, D17). `AUTO_SIMILAR`: accepted immediately when proposed max line total ≤ original max line total; otherwise handled as `CALL_ME` (Q6). |
| ORD-R35 | Accept (customer or `record-response`; `AUTO_SIMILAR` at proposal): status `PROPOSED` (else 409 `SUBSTITUTION_NOT_PENDING`), `now < expires_at` (else 422 `SUBSTITUTION_EXPIRED`). One transaction (D6 exception): original `SUBSTITUTED` + `Inventory.Release(originalItemId)`; new `ACTIVE` line with fresh snapshot (current price) + `Inventory.Reserve(newItemId)` (`OUT_OF_STOCK` refuses the accept); adjustment `SUBSTITUTION` = price difference; `ConcurrentSaveException` retried as R13. |
| ORD-R36 | Reject or expire: line `SHORT_SUPPLIED`, adjustment `SHORT_SUPPLY` = −estimated; Ordering rows only. The reservation is released by Inventory on `OrderPacked` (line `SHORT_SUPPLIED`) or on `OrderCancelled`/`OrderPaymentFailed`. |
| ORD-R37 | The same answer repeated → 200 with current state; the opposite answer → 409 `SUBSTITUTION_NOT_PENDING`. |

### Cancellation

| Key | Rule |
|---|---|
| ORD-R38 | Customer: `PENDING_PAYMENT` always; `CONFIRMED`/`PREPARING` only when status ≤ `cancelAllowedUntilStatus` (default `CONFIRMED`; allowed values `PENDING_PAYMENT`, `CONFIRMED`, `PREPARING`). `PACKED` and later (`OUT_FOR_DELIVERY`, `DELIVERY_FAILED`, `DELIVERED`, `RETURNED`) → 422 `ORDER_NOT_CANCELLABLE`; `CANCELLED`/`PAYMENT_FAILED` → 409 `INVALID_STATUS_TRANSITION`. Needs `Idempotency-Key`. |
| ORD-R39 | Staff (`orders.cancel`, `If-Match`): `CONFIRMED`, `PREPARING`, `PACKED`. Beyond `PACKED` → 422 `ORDER_NOT_CANCELLABLE`; `PENDING_PAYMENT` → 409 `INVALID_STATUS_TRANSITION` (machine: customer only, Q5). |
| ORD-R40 | Reason codes: customer `CHANGED_MIND`, `WRONG_ADDRESS`, `SLOT_ISSUE`, `OTHER`; staff `OUT_OF_STOCK`, `CUSTOMER_REQUEST`, `ADDRESS_ISSUE`, `QUALITY_ISSUE`, `OTHER`. Stored in `cancel_reason_code` and history. |
| ORD-R41 | Cancel commits Ordering rows only (D6): status, `ACTIVE` items `CANCELLED`, `PROPOSED` substitutions `EXPIRED`, history, outbox `OrderCancelled` + `OrderStatusChanged`. Handlers: Inventory releases `ACTIVE` reservations (none after pack); Delivery cancels the shipment and releases the slot (or raises `ShipmentRecallRequired` if already dispatched); Promotions reverses the redemption; Payments cancels the pending payment and refunds captured amounts (`CANCELLATION`). |
| ORD-R42 | `PACKED` cancel sets `stockConsumed = true` in the event (inventory staff record RETURN/WASTE). Customer/staff cancel is refused once the order is `OUT_FOR_DELIVERY` (the `ShipmentDispatched` handler has run); a cancel committed before that handler wins and the late dispatch event is ignored (ORD-R46). |

### Payment and delivery events

| Key | Rule |
|---|---|
| ORD-R43 | `PaymentCaptured`: `PENDING_PAYMENT` → `CONFIRMED`, raises `OrderConfirmed` (Inventory clears reservation `expires_at`). Already `CONFIRMED` or later by the same payment → no-op. Order terminal (`PAYMENT_FAILED`, `CANCELLED`) or already paid by another attempt → Ordering raises `PaymentReceivedForClosedOrder` (Payments refunds, PAY-R10). |
| ORD-R44 | `PaymentFailed`: only when `PENDING_PAYMENT`, `hasOtherActiveAttempt = false` and `now >= payment_due_at` → `PAYMENT_FAILED` with ORD-R45 effects; otherwise no-op (customer may retry until due). |
| ORD-R45 | Expiry (D8; `PAYMENT_FAILED`, actor SYSTEM, reason `PAYMENT_TIMEOUT`): items `CANCELLED`, `PROPOSED` substitutions `EXPIRED`, history, outbox `OrderPaymentFailed` + `OrderStatusChanged`. Handlers: Inventory reservations → `EXPIRED`; Delivery shipment `CANCELLED` + slot released; Promotions redemption `REVERSED`; Payments cancels open attempts. |
| ORD-R46 | Delivery events move status per §2 and raise `OrderDispatched` / `OrderDelivered` / `OrderReturned` (and `OrderStatusChanged`; attempt failure and reattempt raise only `OrderStatusChanged`). Already in the target status → no-op. Invalid transition (e.g. dispatch after cancel) → no status change, audit entry `ORDER_EVENT_REJECTED`, warning log, handler completes (no dead-letter loop) (Q9). |
| ORD-R47 | Every handler is idempotent: `platform.inbox` per (event, handler) plus the status checks above. |

### Invoice

| Key | Rule |
|---|---|
| ORD-R48 | Issued in the `ShipmentDelivered` handler, same transaction as `OUT_FOR_DELIVERY` → `DELIVERED`: lock `number_series (INVOICE, period)` → invoice + lines → status → history → outbox `InvoiceIssued`, `OrderDelivered`. |
| ORD-R49 | Lines: one per item with `charged > 0` (incl. substitution lines), description "product – variant – preparation", hsn, charged quantity + unit, unit price, `total` = final line − allocated discount, `tax` contained at snapshot rate, `taxable = total − tax`; plus a delivery-fee line when fee > 0. |
| ORD-R50 | Invoice `total_paise` = Σ lines = order `final_total_paise`; mismatch throws (handler retried, then dead letter). |
| ORD-R51 | Number `INV/26-27/000001` (D17; ≤ 16 chars for GST): prefix `INV/`, period = Indian FY `yy-yy` in the business time zone, 6-digit counter; Settings owns the series. Gapless: number taken only in the transaction that inserts the invoice; rollback returns it; row lock serializes concurrent deliveries; invoices never deleted. |
| ORD-R52 | `seller_gstin` from Settings business profile; missing → `CONFIGURATION_MISSING` (handler fails and retries). |
| ORD-R53 | PDF rendered by a follow-on handler of `InvoiceIssued` (sets `pdf_storage_key`); `GET …/invoice` returns JSON + signed `pdfUrl` (10 min); before `DELIVERED` → 404 `INVOICE_NOT_ISSUED`. |

### Complaints, goodwill, reorder, pick list

| Key | Rule |
|---|---|
| ORD-R54 | Raise only on `DELIVERED` (else 409 `INVALID_STATUS_TRANSITION`) and `now <= delivered_at + complaintWindowHours` (policy, default 24, D17; else 422 `COMPLAINT_WINDOW_CLOSED`). `Idempotency-Key` required. |
| ORD-R55 | `orderItemId` required except kind `LATE` (order level); must belong to the order. One `OPEN`/`APPROVED` complaint per line (per order for `LATE`) → 409 `COMPLAINT_ALREADY_OPEN`. |
| ORD-R56 | ≤ 4 images, JPEG/PNG/WebP, ≤ 5 MB (`IMAGE_INVALID`); uploaded before commit, deleted if the commit fails. Description 10–1000 chars. |
| ORD-R57 | Decide (`OPEN` only, `If-Match`): `APPROVED` + `REFUND` needs `1 <= refundAmountPaise <=` line final total (order final total for `LATE`); `REJECTED` → resolution `NONE`. Raises `ComplaintDecided`; Payments creates a `COMPLAINT` refund request (idempotency key = complaint id). `REPLACEMENT` is recorded only (Q11). |
| ORD-R58 | Goodwill (`refunds.request`, `If-Match`): negative amount, allowed `PACKED`, `OUT_FOR_DELIVERY`, `DELIVERED`; Σ goodwill ≤ final total. Before collection (COD) reduces `amount_due`; otherwise `OrderAdjusted` → Payments refund request (Q12). |
| ORD-R59 | Reorder adds the order's lines (variant, preparation, ordered quantity) via `ICartApi.AddLines`; unsellable lines skipped and named. |
| ORD-R60 | Pick list: `CONFIRMED`/`PREPARING` orders of the slot, `ACTIVE` lines, Σ gross `ceil(net × 10000 / yield)` per variant × preparation, one query. |

## 4. Events

Final payload list (D9). Other modules conform to these names and fields. Common envelope on every event: `eventId`,
`occurredAt`, `correlationId`. Every order event also carries `orderId`, `orderNumber`, `customerId`, `fulfillmentLocationId`.
Quantities are in the variant stock unit with `stockUnit` (`GRAM`/`PIECE`; PACK variants use PIECE, D10).

### Raised (outbox, same transaction as the change)

| Event | Payload (beyond the common order fields) | Consumers (what they use) |
|---|---|---|
| `OrderPlaced` | `cartId`, `cityId`, `serviceAreaId`, `deliverySlotId`, `slotStartAt`, `slotEndAt`, `status` (`CONFIRMED`/`PENDING_PAYMENT`), `paymentMethod`, `paymentDueAt?`, `amountDuePaise` (= estimated total), `estimatedTotalPaise`, `maxTotalPaise`, `discountPaise`, `deliveryFeePaise`, `couponCode?`, `lines[{ orderItemId, variantId, preparationCode, stockUnit, yieldBp, orderedQuantity, grossQuantity }]`, `placedAt` | Delivery (orderId, fulfillmentLocationId, deliverySlotId, placedAt → shipment PENDING); Payments (customerId, fulfillmentLocationId, paymentMethod, amountDuePaise → PENDING COD/UPI payment, PAY-R3); Notifications; Ordering (schedule expiry when ONLINE). Inventory: not consumed (reserved in checkout) |
| `OrderConfirmed` | `paymentId`, `confirmedAt` (raised only for `PENDING_PAYMENT` → `CONFIRMED`) | Inventory (clear reservation `expires_at`); Notifications |
| `OrderPaymentFailed` | `deliverySlotId`, `reason` (`PAYMENT_TIMEOUT`/`PAYMENT_FAILED`), `couponCode?`, `at` | Inventory (reservations → EXPIRED); Delivery (shipment CANCELLED, slot released); Promotions (reverse redemption); Payments (cancel open attempts); Notifications |
| `OrderCancelled` | `previousStatus`, `deliverySlotId`, `actorType`, `actorId?`, `reasonCode`, `paymentMethod`, `stockConsumed`, `couponCode?`, `cancelledAt` | Inventory (release ACTIVE reservations); Delivery (cancel shipment + slot, or `ShipmentRecallRequired`); Promotions (reverse); Payments (cancel / CANCELLATION refund, PAY-H4); Notifications |
| `OrderPacked` | `packedAt`, `coldBoxUsed`, `paymentMethod`, `finalTotalPaise`, `amountDuePaise`, `refundDuePaise`, `refundDueByReason { WEIGHT_SHORTFALL, SHORT_SUPPLY }`, `lines[{ orderItemId, variantId, stockUnit, yieldBp, orderedQuantity, packedQuantity, chargedQuantity, finalTotalPaise, status }]` (all lines incl. `SHORT_SUPPLIED`/`SUBSTITUTED`) | Inventory (consume / release per line, SALE FEFO); Delivery (packed_at, cold box); Payments (COD amount due PAY-R8, ONLINE refunds per reason PAY-H2); Notifications |
| `OrderDispatched` | `shipmentId`, `riderId`, `dispatchedAt` | Notifications |
| `OrderDelivered` | `shipmentId`, `deliveredAt`, `finalTotalPaise`, `amountDuePaise`, `invoiceId`, `invoiceNumber` | Notifications; Payments (optional, PAY-H6 uses `ShipmentDelivered`) |
| `OrderReturned` | `shipmentId`, `paymentMethod`, `returnedAt` | Payments (RETURNED + paid → refund REQUESTED, PAY-H5); Notifications |
| `OrderStatusChanged` | `from`, `to`, `actorType`, `actorId?`, `reasonCode?`, `changedAt` (every transition) | Notifications; Payments PAY-H5 may use `OrderPaymentFailed`/`OrderReturned` instead |
| `OrderAdjusted` | `adjustmentId`, `kind` (`GOODWILL`/`SUBSTITUTION`), `amountPaise`, `amountDuePaise`, `refundDuePaise`, `reason`, `requestedBy`, `at` | Payments (PAY-H3); Notifications |
| `PaymentReceivedForClosedOrder` | `paymentId`, `orderStatus`, `amountPaise` | Payments (CANCELLATION refund, PAY-R10) |
| `InvoiceIssued` | `invoiceId`, `invoiceNumber`, `issuedAt`, `taxablePaise`, `taxPaise`, `totalPaise` | Ordering (render PDF); Notifications (email) |
| `SubstitutionProposed` | `substitutionId`, `orderItemId`, `originalName`, `proposedName`, `proposedQuantity`, `stockUnit`, `priceDifferencePaise`, `expiresAt` | Notifications (push/SMS); Ordering (schedule expiry) |
| `SubstitutionResolved` | `substitutionId`, `orderItemId`, `status`, `actorType`, `newOrderItemId?`, `at` | Notifications (stock already moved in the accept transaction, D6) |
| `ComplaintRaised` | `complaintId`, `orderItemId?`, `kind`, `at` | Notifications (staff alert) |
| `ComplaintDecided` | `complaintId`, `orderItemId?`, `status`, `resolution`, `refundAmountPaise?`, `refundMethod?`, `decidedBy`, `at` | Payments (COMPLAINT refund REQUESTED, PAY-H8); Notifications |

Reporting reads tables through views; no event consumer.

### Consumed

| Event (module) | Payload used | Handler effect | Idempotency |
|---|---|---|---|
| `PaymentCaptured` (Payments) | `paymentId`, `orderId`, `amountPaise`, `capturedAt` | ORD-R43 | inbox + status check |
| `PaymentFailed` (Payments) | `paymentId`, `orderId`, `failureCode`, `hasOtherActiveAttempt` | ORD-R44 | inbox + status check |
| `ShipmentDispatched` (Delivery) | `shipmentId`, `orderId`, `riderId`, `dispatchedAt` | `PACKED` → `OUT_FOR_DELIVERY`, `OrderDispatched` | inbox + target-status no-op |
| `ShipmentDelivered` (Delivery) | `shipmentId`, `orderId`, `deliveredAt` | → `DELIVERED` + invoice, `OrderDelivered`, `InvoiceIssued` (ORD-R48) | inbox + invoice `unique order_id` |
| `DeliveryAttemptFailed` (Delivery) | `shipmentId`, `orderId`, `attemptNo`, `outcome`, `attemptedAt` | → `DELIVERY_FAILED` | inbox + status |
| `ShipmentReattempted` (Delivery) | `shipmentId`, `orderId` | → `OUT_FOR_DELIVERY` | inbox + status |
| `ShipmentReturned` (Delivery) | `shipmentId`, `orderId`, `returnedAt` | → `RETURNED`, `OrderReturned` | inbox + status |
| `PaymentCollected`, `RefundSucceeded` (Payments) | — | Not consumed: amounts read live from Payments.Public | — |
| `OrderPlaced`, `SubstitutionProposed`, `InvoiceIssued` (own) | ids, due times | Schedule delayed jobs / render PDF | inbox; jobs re-check state |

## 5. Commands

| Command | Endpoint | Read | Decide | Commit | Follow-on |
|---|---|---|---|---|---|
| PreviewCheckout | `POST /checkout/preview` | Customer, address, cart quote, slot, policy, methods | R1, R4–R9, R15 | — | Preview |
| PlaceOrder | `POST /orders` (IK) | Existing order by key; R15 reads; stock items, slot, coupon (tracked) | R1–R14, R16–R19 | R11 one save; retry R13 | `OrderPlaced`, `OrderStatusChanged`; 201 detail |
| ListMyOrders / GetOrder / Tracking | `GET /orders`, `/orders/{id}`, `/orders/{id}/tracking` | Order (+ Payments, Delivery reads) | R22–R24 | — | ETag on detail |
| CancelByCustomer | `POST /orders/{id}/cancel` (IK) | Order, policy | R38, R40 | R41 | `OrderCancelled`, `OrderStatusChanged` |
| AnswerSubstitution | `POST /orders/{id}/substitutions/{sid}/accept` \| `/reject` | Order, substitution, (accept) price + stock | R35–R37 | Order (+ `Inventory.Reserve`/`Release` on accept, D6) | `SubstitutionResolved` |
| RaiseComplaint | `POST /orders/{id}/complaints` (IK, multipart) | Order, open complaints | R54–R56 | Complaint | `ComplaintRaised` |
| ListComplaints | `GET /orders/{id}/complaints` | — | — | — | — |
| GetInvoice | `GET /orders/{id}/invoice`, `GET /admin/orders/{id}/invoice` | Invoice | R53 | — | Signed URL |
| Reorder | `POST /orders/{id}/reorder` | Order items | R59 | Cart (via Cart.Public) | Priced cart |
| AdminList / AdminDetail | `GET /admin/orders`, `/admin/orders/{id}` | Orders; reservations, batches (Inventory reads) | R21 | — | ETag |
| PickList | `GET /admin/orders/pick-list` | Items for slot | R60 | — | — |
| StartPreparing | `POST /admin/orders/{id}/start-preparing` (IM) | Order | §2 | Order | `OrderStatusChanged` |
| Pack | `POST /admin/orders/{id}/pack` (IM) | Order, policy | R25–R31 | Order only (R32) | `OrderPacked`, `OrderStatusChanged` (Inventory, Delivery, Payments react); pack response |
| ProposeSubstitution | `POST /admin/orders/{id}/items/{itemId}/substitutions` (IM) | Order, variant, price, stock | R33–R34 (+R35 if auto) | Order (+ reservation if auto) | `SubstitutionProposed` or `SubstitutionResolved` |
| RecordSubstitutionResponse | `POST /admin/orders/{id}/substitutions/{sid}/record-response` (IM) | As accept/reject | R35–R37, actor STAFF | Order | `SubstitutionResolved` |
| CancelByStaff | `POST /admin/orders/{id}/cancel` (IM) | Order | R39–R40 | R41 | `OrderCancelled`, `OrderStatusChanged` |
| Goodwill | `POST /admin/orders/{id}/goodwill` (IM) | Order, adjustments | R58 | Order adjustment | `OrderAdjusted` |
| AdminComplaints | `GET /admin/complaints`, `/admin/complaints/{id}` | Complaints, attachment URLs | R21 | — | ETag |
| DecideComplaint | `POST /admin/complaints/{id}/decide` (IM) | Complaint, order item | R57 | Complaint | `ComplaintDecided` |
| ResolveComplaint | `POST /admin/complaints/{id}/resolve` (IM) | Complaint | §2 | Complaint | — |
| OnPaymentCaptured / OnPaymentFailed | handlers | Order | R43–R45 | Order | `OrderConfirmed` / `OrderPaymentFailed` / `PaymentReceivedForClosedOrder` |
| OnShipment* | handlers | Order, number series (delivered) | R46–R52 | Order (+ invoice + number series row) | `OrderDispatched` / `OrderDelivered` + `InvoiceIssued` / `OrderReturned` |
| ExpireUnpaidOrder / ExpireSubstitution | jobs (§6) | Order | R45 / R36 | Order only | `OrderPaymentFailed` / `SubstitutionResolved` |

### API gaps

| # | Gap | Proposal |
|---|---|---|
| G1 | §4.17/§6 say the invoice is issued "in the same transaction" as `deliver`, but Delivery owns `deliver` and Ordering owns invoices | Delivery commits shipment + `ShipmentDelivered`; Ordering's handler does DELIVERED + invoice in one transaction (database §4 "Issue invoice"); `deliver` response has no invoice |
| G2 | No code for proposing a substitution when preference is `NONE` or the line/order state is wrong | Add 422 `SUBSTITUTION_NOT_ALLOWED` |
| G3 | Goodwill request body and allowed statuses missing (reason `GOODWILL` added by D15) | Body `{ amountPaise (<0), reason, orderItemId? }`; ORD-R58 |
| G4 | `RETURNED` prepaid orders: no refund reason or rule | Add reason `RETURN`; Payments refunds paid amount on `OrderStatusChanged` to `RETURNED` (Q10) |
| G5 | Place-order refusals omit `CART_LOCATION_CHANGED`, `LOCATION_CLOSED`, `CUSTOMER_DELETED`, `CONFIGURATION_MISSING`, `TAX_RULE_MISSING` | Add to §4.8 list |
| G6 | Tracking `deliveryCode` vs `delivery_code_hash` | Resolved by D12 (plain code); read via `Delivery.GetShipmentForOrder` |
| G7 | "Payment method enabled for the city" has no configuration table | Payments.Public `GetEnabledMethods(cityId)` from adapter registration + `order_policies` flag (E5/E1) |
| G8 | Substitution answer repeat behaviour undefined | ORD-R37 |
| G9 | Complaint `refundAmountPaise` bounds; COD complaint refund method | ORD-R57; method chosen by Payments (`UPI`/`BANK_TRANSFER`/`CASH`) |
| G10 | Database columns needed: `orders.payment_due_at` (+ partial index where `PENDING_PAYMENT`), `orders.final_items_paise`, `final_preparation_paise`, `final_discount_paise`, `final_tax_paise`; `order_items.charged_quantity`, `final_item_paise`, `final_preparation_paise`, `final_tax_paise`, `discount_paise`, `is_weight_variable`, `substituted_from_item_id`; `substitutions.proposed_preparation_code`; `settings.order_policies.substitution_minutes`, `complaint_window_hours` (D17, E1); `invoice_lines.unit`, CGST/SGST split; `complaints.order_item_id` nullable (`LATE`); `order_items.preparation_charge_paise` = rate per basis | Manager adds before the migration |
| G11 | API §6/§7 and database §4 wording for pack, cancel, expiry, payment callback, deliver | Resolved by D6; per D13 I update my sections of `docs/api/README.md` in the Phase 2 branch (G1–G10 too) |

## 6. Jobs

All jobs: primary trigger is a Hangfire delayed job scheduled at the due time by a handler of our own event (runs only when there is work); a safety sweep catches lost schedules with a cheap `EXISTS` on a partial index and returns immediately when empty. Each order is processed as its own command (own transaction, xmin); a lost race re-reads and skips if no longer due.

| Job | Trigger | Sweep (EXISTS) | Action |
|---|---|---|---|
| `ExpireUnpaidOrder(orderId)` | Scheduled at `payment_due_at` from `OrderPlaced` (ONLINE) | Every 5 min: `orders where status = PENDING_PAYMENT and payment_due_at <= now` | ORD-R45; skip when status changed (captured first) |
| `ExpireSubstitution(substitutionId)` | Scheduled at `expires_at` from `SubstitutionProposed` | Every 2 min: `substitutions where status = PROPOSED and expires_at <= now` | ORD-R36 with status `EXPIRED`; skip when answered |
| `RenderInvoicePdf(invoiceId)` | `InvoiceIssued` handler | Hourly: `invoices where pdf_storage_key is null` | Render, upload, set key |
| `AbandonCarts` | Cart module | see `cart.md` | — |

## 7. Test scenarios

### Unit (domain)

| # | Scenario | Expect |
|---|---|---|
| U1 | Every allowed transition in §2 | succeeds, history + event |
| U2 | Every other from/to pair | `INVALID_STATUS_TRANSITION` |
| U3 | Pack example API §4.15 (540 g, 580 g capped at 550, 2 pcs) | finals 72306 / 52745 / 130000, total 245051, adjustments 5356 / 4795 / 0 |
| U4 | Overweight cap: weight-variable 500 g, 1000 bp, packed 600 | charged 550, `capped = true` |
| U5 | Non-weight-variable packed above ordered | charged = ordered |
| U6 | Packed 0 | `SHORT_SUPPLIED`, adjustment −estimated |
| U7 | Prepaid, packed short | `refundDueByReason` split SHORT_SUPPLY / WEIGHT_SHORTFALL, Σ = `refundDuePaise` = paid − final |
| U8 | Prepaid, overweight | charged = paid, refund 0 (D15) |
| U9 | Discount above final eligible subtotal after short supply | discount reduced to subtotal |
| U10 | Customer cancel by status × policy value | allowed / `ORDER_NOT_CANCELLABLE` / `INVALID_STATUS_TRANSITION` per R38 |
| U11 | Staff cancel `PENDING_PAYMENT`, `PACKED`, `OUT_FOR_DELIVERY` | 409 / ok / 422 |
| U12 | Substitution accept at `expires_at` | `SUBSTITUTION_EXPIRED` |
| U13 | `AUTO_SIMILAR` within / above guard | accepted / proposed |
| U14 | Pack with `PROPOSED` substitution, missing line, duplicate line, wrong unit, negative | 409 / 422 `PACKED_QUANTITY_INVALID` |
| U15 | Complaint at 24 h + 1 s; second open on a line | `COMPLAINT_WINDOW_CLOSED`; `COMPLAINT_ALREADY_OPEN` |
| U16 | Invoice lines sum = final total incl. delivery line and allocated discount | equal |
| U17 | Max chargeable floor: 333 g at 1000 bp | 366 |

### Integration (Postgres, Testcontainers)

| # | Scenario | Expect |
|---|---|---|
| I1 | Concurrent checkouts for the last stock (10 parallel, stock for 1) | exactly 1 order; others 422 `OUT_OF_STOCK`; `reserved <= on_hand`; no 500 |
| I2 | Concurrent checkouts for the last slot capacity | 1 booked; others `SLOT_FULL` |
| I3 | Coupon `total_limit = 1`, two customers in parallel | one redemption; `used_count = 1` |
| I4 | Duplicate `Idempotency-Key`, same body, sequential and parallel | one order; replay 201 with `Idempotent-Replayed: true` or 409 `IDEMPOTENCY_IN_PROGRESS`; never two orders |
| I5 | Same key, different body | 422 `IDEMPOTENCY_KEY_REUSED` |
| I6 | Idempotency row purged, same key retried | existing order returned via `(customer_id, idempotency_key)` |
| I7 | Price changed between preview and place | 409 `PRICE_CHANGED` with `preview.expectedTotalPaise` = new total; no reservation, slot, redemption, number used |
| I8 | Forced `ConcurrentSaveException` on first two attempts | third succeeds; one order number consumed |
| I9 | Forced conflict on all 4 attempts | 409 `CONCURRENT_SAVE`; nothing persisted |
| I10 | Checkout effects | reservation gross `ceil(net × 10000 / yield)`, slot +1, redemption APPLIED, order number, cart CONVERTED, outbox `OrderPlaced` (with `amountDuePaise`, lines with `stockUnit`, `yieldBp`) in one commit |
| I11 | Overweight cap end to end | `final_total_paise` uses cap; `OrderPacked` line has `packedQuantity` 580 and `chargedQuantity` 550 (Inventory posts SALE for 580 gross) |
| I12 | Short supply on prepaid order | `OrderPacked.refundDueByReason.SHORT_SUPPLY` = estimated of the line, line `status SHORT_SUPPLIED`, `refundDuePaise` = Σ reasons; outbox row written in the pack commit; replaying the pack request (same `If-Match`) adds no second event |
| I13 | Cancel after `PACKED` by customer | 422 `ORDER_NOT_CANCELLABLE`; staff cancel works, `OrderCancelled { previousStatus: PACKED, stockConsumed: true }`; cancel after `OUT_FOR_DELIVERY` 422 |
| I14 | Cancel commits only Ordering rows + `OrderCancelled` (with `deliverySlotId`, `previousStatus`, `couponCode`); after dispatching outbox with real handlers, `reserved`, `booked_count`, `used_count` restored once even if dispatched twice |
| I15 | Invoice numbering under concurrency: 20 `ShipmentDelivered` in parallel, one forced to roll back | 19 invoices numbered 1..19 with no gap or duplicate; retried one gets 20 |
| I16 | `ShipmentDelivered` delivered twice | one invoice, one transition |
| I17 | `PaymentCaptured` after expiry job | order stays `PAYMENT_FAILED`; `PaymentReceivedForClosedOrder` raised once |
| I18 | Expiry job vs capture race | exactly one wins; the other no-ops |
| I19 | Expiry sweep with nothing due | no writes, no events |
| I20 | Substitution expiry job | `EXPIRED`, line `SHORT_SUPPLIED`, adjustment SHORT_SUPPLY; later `OrderPacked` lists the line as `SHORT_SUPPLIED` |
| I24 | Substitution accept for the last stock of the new variant, two orders in parallel | one accept succeeds; other 422 `OUT_OF_STOCK`; original reservation released only for the winner |
| I21 | Complaint with 5 images / 6 MB image | `IMAGE_INVALID`; no storage objects left |
| I22 | Staff from another location | 403 `LOCATION_ACCESS_DENIED`; list excludes the order |
| I23 | Stale `If-Match` on pack | 412 `CONCURRENCY_CONFLICT` |

## 8. Open questions

| # | Question | Proposed answer |
|---|---|---|
| Q1 | Database §4 lists "pending payment" inside the checkout transaction; payments.md PAY-R3 creates it on `OrderPlaced` | Follow PAY-R3 (D6 spirit): checkout does not write `payments.*`; `OrderPlaced.amountDuePaise` carries the amount |
| Q2 | Prepaid: final above paid | Resolved by D15: capped at captured amount; also applies to a dearer substitution (confirm) |
| Q3 | COD limit compares `maxTotalPaise` or `estimatedTotalPaise`? Applies to UPI_ON_DELIVERY? | `maxTotalPaise`; yes, both on-delivery methods |
| Q4 | `PaymentFailed` before `payment_due_at` | No status change; expiry decides |
| Q5 | Staff cancel of `PENDING_PAYMENT` | Not allowed (machine); the expiry job handles it |
| Q6 | `AUTO_SIMILAR` guard | Auto-accept only when proposed max line total ≤ original max line total |
| Q7 | Substitutions in `CONFIRMED` too? | No, `PREPARING` only |
| Q8 | Number series lock in the invoice handler is a Settings row written in Ordering's transaction | Keep (designed as caller's-transaction lock, needed for gapless numbers); confirm it is within D6 |
| Q9 | Delivery event for an order already cancelled | Ignore, audit `ORDER_EVENT_REJECTED`, alert dispatcher |
| Q10 | `RETURNED` prepaid order: refund? coupon? | Full refund (reason `RETURN`); coupon stays used |
| Q11 | Complaint `REPLACEMENT` fulfilment | Recorded only in Phase 2; staff place a manual replacement later |
| Q12 | Goodwill allowed statuses and effect | `PACKED`..`DELIVERED`; reduces COD due before collection, else refund request |
| Q13 | Order number format/period (`FO-260001`) | Settings owns period (2-digit year) and padding; Ordering calls `NextNumber(ORDER, now)`. Invoice format fixed by D17 |
| Q14 | "Retry up to 3" = 3 attempts or 3 retries? | 3 retries after the first attempt (4 total) |
| Q15 | 0% GST seafood: tax invoice or bill of supply? CGST/SGST split | CA to confirm; Phase 2 stores rate + tax and splits CGST/SGST half each when rendering |
| Q16 | Credit notes for post-delivery refunds | Out of Phase 2; refunds recorded in Payments only |
| Q17 | PDF library (free/open-source) | QuestPDF Community licence or HTML template → PDF via a free renderer; manager to pick |
| Q18 | PACK variants (D10: stock unit PIECE) yield | Yield ignored for PIECE (INV-R11); gross = pack count |
