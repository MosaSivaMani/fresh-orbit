# Payments module

Owner: E5. Schema `payments`. Endpoints: `docs/api/README.md` §4.9, §4.16, §4.17 (`cash-collected`), §7.
Error codes: `docs/api/README.md` §2 (Payments, Common). Order states: `docs/api/README.md` §6.
Uses `.Public` of: Ordering (order facts), Delivery (shipment, rider), Identity (staff names) — see Q1.

## 1. Aggregates and boundaries

| Aggregate root | Tables | Children | Row version |
|---|---|---|---|
| Payment | `payments` | `refunds` (of this payment) | `payments.xmin`; every refund change also updates `payments.refunded_paise` / `refund_pending_paise` (S1) so the root row version moves |
| WebhookEvent | `webhook_events` | — | none (insert-once; unique `(provider, provider_event_id)` is the guard) |
| CashSettlement | `cash_settlements` | — | `cash_settlements.xmin` |
| ReconciliationIssue | `reconciliation_issues` (new, S6) | — | `xmin` |

| Boundary rule |
|---|
| Payments writes only `payments.*`. Order status, `amount_due_paise`, invoices belong to Ordering; Payments changes them only by raising events Ordering handles. |
| One payment row = one attempt with one provider (one provider payment id once known). An order can have many attempts; at most one is `CAPTURED`/`COLLECTED` (unique index). |
| A refund always belongs to the order's single successful payment. No payment `CAPTURED`/`COLLECTED` → no refund (`NO_CAPTURED_PAYMENT`). |
| Refund approve / mark-paid quote the refund's `version`; the command loads the whole Payment aggregate (payment + its refunds) and saves once. |
| Provider HTTP calls never run inside a database transaction (initiate: call, then commit; refund: Worker between two commits). |
| Money is `long` paise; the server never accepts an amount from the customer client. |

### Public API (`Payments.Public`)

| Member | Returns | Used by |
|---|---|---|
| `GetOrderPaymentSummaryAsync(orderId)` | method, latest attempt status, paidPaise (captured or collected), refundedPaise, pendingRefundPaise, refundablePaise, nextAction | Ordering (order detail `amountPaidPaise`, `amountRefundedPaise`, `payment`; complaint decide pre-check) |
| `GetOrderPaymentSummariesAsync(orderIds)` | same, batched | Ordering (order lists), Customers (lifetime value) |

## 2. States and transitions

### Payment (`payments.status`)

| From | To | Trigger | Guard / rule |
|---|---|---|---|
| — | PENDING | `OrderPlaced` (COD, UPI_ON_DELIVERY) | PAY-R3 |
| — | PENDING | InitiateOnlinePayment | PAY-R5 |
| PENDING | AUTHORIZED | webhook `authorized` | PAY-R7 |
| PENDING, AUTHORIZED | CAPTURED | webhook `captured`, reconciliation | amount matches (PAY-R9) |
| FAILED, CANCELLED | CAPTURED | late webhook `captured` for the same provider payment id | money is real; PAY-R10 follows |
| PENDING, AUTHORIZED | FAILED | webhook `failed` | PAY-R7 |
| PENDING (COD, UPI_ON_DELIVERY) | COLLECTED | RecordCashCollection | PAY-R4 |
| PENDING, AUTHORIZED | CANCELLED | `OrderCancelled`, order → `PAYMENT_FAILED` / `RETURNED`, newer attempt superseded | PAY-R6, PAY-R11 |
| CAPTURED, COLLECTED | — | terminal; refunds are tracked on `refunds`, never by changing payment status | |

### Refund (`refunds.status`)

| From | To | Trigger | Guard |
|---|---|---|---|
| — | REQUESTED | RequestRefund (staff, complaint decision) | PAY-R12, PAY-R13 |
| — | APPROVED | system refund (cancellation, shortfall, paid-after-close), method ORIGINAL | PAY-R15 |
| REQUESTED | APPROVED | ApproveRefund | approver ≠ requester (PAY-R14) |
| REQUESTED | FAILED | RejectRefund (`failure_reason` = `REJECTED: <reason>`) | PAY-R14 |
| APPROVED (ORIGINAL) | PROCESSING | Worker ProcessRefund, before provider call | PAY-R16 |
| PROCESSING | SUCCEEDED | provider says processed (sync result, webhook `refund.processed`, reconciliation) | |
| PROCESSING | FAILED | provider permanent failure / webhook `refund.failed` | PAY-R16 |
| APPROVED (UPI, BANK_TRANSFER, CASH) | SUCCEEDED | MarkRefundPaid with `reference` | PAY-R17 |
| SUCCEEDED, FAILED | — | terminal | |

### WebhookEvent (`webhook_events.status`, new column S4)

| From | To | When |
|---|---|---|
| — | REJECTED | signature invalid (`signature_valid = false`), never processed |
| — | PROCESSED | valid, applied in the same transaction |
| — | IGNORED | valid, event type not handled |
| — | ERROR | valid, not applicable (`error` = `UNKNOWN_PAYMENT`, `AMOUNT_MISMATCH`, `UNKNOWN_REFUND`) |
| ERROR | PROCESSED / ERROR | ReprocessWebhookEvent |
| (processing exception) | no row | transaction rolled back, 500, provider retries |

### CashSettlement (`cash_settlements.status`)

| From | To | Trigger | Guard |
|---|---|---|---|
| — | SETTLED | CreateCashSettlement with `difference_paise = 0` | PAY-R20 |
| — | OPEN | CreateCashSettlement with `difference_paise ≠ 0` | PAY-R20 |
| OPEN, SETTLED | DISPUTED | DisputeCashSettlement (`reason`) | |
| OPEN, DISPUTED | SETTLED | ResolveCashSettlement (API gap G2) with `note` | resolver ≠ creator when `difference_paise ≠ 0` |

### ReconciliationIssue (`reconciliation_issues.status`)

| From | To | Trigger |
|---|---|---|
| — | OPEN | reconciliation job / webhook error / handler detection (PAY-R22) |
| OPEN | AUTO_RESOLVED | a later job run finds the condition gone |
| OPEN | RESOLVED | staff resolve with note (API gap G3) |

## 3. Business rules

| Key | Rule |
|---|---|
| PAY-R1 | **Provider adapter `IPaymentProvider`** (port in `Payments.Application`, adapters in `.Infrastructure`, selected by `Key`): `string Key`; `ProviderCapabilities Capabilities` (`OnlineCheckout`, `Webhooks`, `ProviderRefunds`, `CollectedOnDelivery`); `Task<ProviderOrder> CreateOrderAsync(CreateProviderOrder{paymentId (receipt), orderNumber, amountPaise, currency INR, customerPhone, expiresAt})` → providerOrderId, checkout params (`keyId`, …), expiresAt; `bool VerifySignature(IReadOnlyDictionary<string,string> headers, ReadOnlySpan<byte> rawBody)` (constant-time compare); `ProviderWebhook ParseWebhook(headers, rawBody)` → providerEventId, kind (`PAYMENT_AUTHORIZED`/`PAYMENT_CAPTURED`/`PAYMENT_FAILED`/`REFUND_PROCESSED`/`REFUND_FAILED`/`OTHER`), providerOrderId, providerPaymentId, providerRefundId, amountPaise, currency, failureCode, failureMessage, instrument; `Task<ProviderPaymentState> GetPaymentAsync(providerOrderId)` → attempts with status and amount; `Task<ProviderRefundResult> CreateRefundAsync(providerPaymentId, amountPaise, idempotencyKey, notes)` → providerRefundId, status `PROCESSED`/`PENDING`/`FAILED`(permanent); `Task<ProviderRefundState> GetRefundAsync(providerRefundId)`. Transport errors throw `PaymentProviderUnavailableException` (→ 503 `PAYMENT_PROVIDER_UNAVAILABLE` in requests, retry in jobs). |
| PAY-R2 | **Adapters.** `CASH` (COD): `CollectedOnDelivery` only; every other method throws `NotSupportedException` (callers check capabilities first). `UPI_ON_DELIVERY` (rider QR, reference typed by staff): same as CASH (provider code is S2). `FAKE`: all capabilities; deterministic ids `fake_order_*`, `fake_pay_*`, `fake_rfnd_*`; HMAC-SHA256 hex over raw body with configured secret in `X-Fake-Signature`; event id = body `id`; outcome scripted by tests (and a Development-only simulate endpoint, G9); refused at startup in Production. `RAZORPAY` (later): all capabilities, `X-Razorpay-Signature`, event id from `X-Razorpay-Event-Id`. An adapter is enabled by config `Payments:Providers:<Key>:Enabled`; ONLINE uses `Payments:OnlineProvider`; missing config → initiate 503 `PAYMENT_PROVIDER_UNAVAILABLE`, never a silent fallback. |
| PAY-R3 | **Payment on `OrderPlaced`.** COD → one PENDING payment, provider `CASH`; UPI_ON_DELIVERY → provider `UPI_ON_DELIVERY`; `amount_paise` = order `amountDuePaise` (estimated total); copies `fulfillment_location_id`, `customer_id` (S1). ONLINE → nothing (attempt created by initiate, PAY-R5). Idempotent: skip when the order already has a delivery-collected payment row. |
| PAY-R4 | **COD / UPI-on-delivery collection.** Allowed when the order is `OUT_FOR_DELIVERY` or `DELIVERED` and payment PENDING; `amountPaise` must equal current `amount_paise` (= amount due after weigh-and-pack and adjustments, PAY-R8) else 422 `VALIDATION_FAILED` field `amountPaise` code `CASH_AMOUNT_MISMATCH` (G7); `mode` `CASH`/`UPI`; `UPI` requires `upiReference`; already COLLECTED → 409 `CASH_ALREADY_COLLECTED` (also same-key replays via `Idempotency-Key`). Sets `collected_by_rider_id` (shipment's rider), `collected_at`, `collection_mode`, `collection_reference`. Via the deliver path (event, PAY-H6) a different amount is recorded as given and an issue `COLLECTION_DIFFERENCE` is opened. |
| PAY-R5 | **Online initiate.** Customer owns the order (else 404); method ONLINE and nothing paid (else 422 `PAYMENT_NOT_REQUIRED`); order `PENDING_PAYMENT` (else 409 `INVALID_STATUS_TRANSITION`); reservation not expired. Reuse: an existing PENDING attempt with same amount and `expires_at` > now + 2 min → return it (200). Else: new `paymentId`, call `CreateOrderAsync` (receipt = paymentId, expiresAt = reservation expiry), then commit PENDING row with `provider_order_id`, `expires_at`; older PENDING attempts of the order stay PENDING until expiry (a late capture on them is PAY-R10). Response 201: `paymentId`, `provider`, `providerOrderId`, `amountPaise`, `keyId`, `expiresAt`. Client return never changes status. |
| PAY-R6 | Order cancelled / `PAYMENT_FAILED` / `RETURNED` → every PENDING/AUTHORIZED attempt → CANCELLED. A provider authorization on a cancelled attempt is left to auto-void by the provider; a later capture follows PAY-R10. |
| PAY-R7 | **Webhook.** Raw body read before parsing. Unknown `{provider}` → 404 `WEBHOOK_PROVIDER_UNKNOWN`. Invalid signature → insert event `REJECTED` in its own transaction (no unique constraint on rejected rows, S4), 401 `WEBHOOK_SIGNATURE_INVALID`. Valid → Read event exists? payment by `provider_payment_id`, else by `provider_order_id`; refund by `provider_refund_id` → Decide → Commit event row + payment/refund change + outbox in one `SaveChangesAsync`. Unique violation on `(provider, provider_event_id)` (duplicate or concurrent delivery) → 200 `{received:true, duplicate:true}`, nothing re-applied. Same state already applied (e.g. CAPTURED again with another event id) → event PROCESSED, no change, no event raised. |
| PAY-R8 | **Amount due follows the order.** `OrderPacked` / `OrderAdjusted` with `amountDuePaise` → PENDING COD/UPI payment `amount_paise` = `amountDuePaise`. Payment already COLLECTED → no change; open issue `COLLECTION_DIFFERENCE` if different. |
| PAY-R9 | **Amount mismatch.** Captured amount ≠ attempt `amount_paise` or currency ≠ INR → payment stays PENDING, event `ERROR` with `AMOUNT_MISMATCH`, issue `AMOUNT_MISMATCH` (OPEN), `PaymentAlertRaised` to `payments.view` staff; 200 to provider. Never confirms the order. Resolution: staff refunds the received amount via G6. |
| PAY-R10 | **Payment after the order closed.** `PaymentCaptured` reaches Ordering; if the order is terminal (`PAYMENT_FAILED`, `CANCELLED`) or already paid by another attempt, Ordering raises `PaymentReceivedForClosedOrder` → Payments creates a CANCELLATION refund, method ORIGINAL, amount = captured, auto-approved (PAY-R15). For "already paid by another attempt" the second CAPTURED row needs S3. |
| PAY-R11 | `PaymentFailed` carries `hasOtherActiveAttempt` (another PENDING/AUTHORIZED attempt with unexpired `expires_at`); Ordering moves the order to `PAYMENT_FAILED` only when false and the reservation has expired; otherwise the expiry job does it. |
| PAY-R12 | **Refund amount.** `0 < amountPaise ≤ refundable`, refundable = paid − Σ refunds in (REQUESTED, APPROVED, PROCESSING, SUCCEEDED); else 422 `REFUND_EXCEEDS_PAYMENT`. Checked in the Payment aggregate; the save bumps `payments.xmin` (S1), so a concurrent refund loses with `ConcurrentSaveException`, is retried from Read (max 3), and then fails PAY-R12. |
| PAY-R13 | **Refund method.** `ORIGINAL` only when the payment's provider has `ProviderRefunds` (online); COD/UPI-on-delivery refunds use `UPI` (needs `upiId`), `BANK_TRANSFER` or `CASH`; else 422 `VALIDATION_FAILED` field `method` code `REFUND_METHOD_NOT_ALLOWED`. `reasonCode` `COMPLAINT` requires `complaintId` of the same order. Staff-requested reasons: `COMPLAINT`, `GOODWILL` (S5), `CANCELLATION`; system reasons add `WEIGHT_SHORTFALL`, `SHORT_SUPPLY`. |
| PAY-R14 | **Separation of duties.** Approver / rejecter user id ≠ `requested_by` → else 422 `REFUND_SELF_APPROVAL`. Approve/reject only from REQUESTED → else 409 `REFUND_NOT_APPROVABLE`. |
| PAY-R15 | **System refunds** (CANCELLATION from `OrderCancelled`, WEIGHT_SHORTFALL/SHORT_SUPPLY from `OrderPacked`, CANCELLATION from `PaymentReceivedForClosedOrder`) are created APPROVED, `requested_by` = `approved_by` = SYSTEM, method ORIGINAL, amount computed by the server; they raise `RefundApproved`. Amount capped at refundable (never fails the handler; a capped amount opens issue `REFUND_CAPPED`). |
| PAY-R16 | **Refund processing (Worker).** `RefundApproved` handler: tx1 APPROVED → PROCESSING, `attempt_count + 1`; call `CreateRefundAsync` with `idempotency_key` = `refund:{refundId}` (fixed at creation, unique); tx2 store `provider_refund_id`; `PROCESSED` → SUCCEEDED; `PENDING` → stay PROCESSING (webhook / reconciliation finishes); permanent failure → FAILED with `failure_reason`. Transient error → throw, Hangfire retries (5 attempts, exponential); re-entry on PROCESSING without `provider_refund_id` calls again with the same key. Adapter guarantees one provider refund per key (provider idempotency header, or look up by key in notes before creating). After the last retry → stays PROCESSING, issue `REFUND_STUCK`. |
| PAY-R17 | **Manual refund.** `mark-paid` only for method UPI/BANK_TRANSFER/CASH in APPROVED; requires `reference` (UTR / receipt no., 4–64 chars); sets SUCCEEDED, `manual_reference`, `succeeded_at`, `succeeded_by`. ORIGINAL → 409 `INVALID_STATUS_TRANSITION`. |
| PAY-R18 | A FAILED refund frees its amount (PAY-R12); staff may request a new refund with another method. Payments are never deleted or edited after terminal; refunds never deleted. |
| PAY-R19 | **Cash settlement scope.** One per rider per settlement date (location-local date of `collected_at`); `SETTLEMENT_EXISTS` 409 on duplicate (unique index). Expected = Σ `amount_paise` of payments COLLECTED with `collection_mode = CASH`, `collected_by_rider_id = rider`, local date = date, `cash_settlement_id` null. UPI collections are excluded. Date in the future or rider at a location the staff cannot access → 422 / 403 `LOCATION_ACCESS_DENIED`. |
| PAY-R20 | **Cash settlement create.** Server computes `expected_paise`, `difference_paise = received − expected`; status SETTLED if 0 else OPEN; sets `cash_settlement_id` on the included payments in the same save; issue `CASH_DIFFERENCE` when ≠ 0. A collection recorded later for an already-settled date is not added; it is flagged `UNSETTLED_CASH` and included in the next settlement only via G2 re-open (Q6). |
| PAY-R21 | **Reconciliation job** (Worker, hourly, single instance via `DisableConcurrentExecution`): (a) ONLINE attempts PENDING/AUTHORIZED with `created_at` between 15 min and 72 h ago → `GetPaymentAsync`; captured at provider → apply as webhook with synthetic event id `recon:{providerPaymentId}:CAPTURED` (issue `MISSED_WEBHOOK`, auto-resolved); amount differs → PAY-R9. (b) refunds PROCESSING > 30 min → `GetRefundAsync`, apply result. (c) COD/UPI payments still PENDING > 2 h after `order_delivered_at` (S1) → `DELIVERED_NOT_COLLECTED`. (d) CASH collections with `cash_settlement_id` null and local date < today → `UNSETTLED_CASH`. (e) OPEN/DISPUTED settlements older than 3 days → `SETTLEMENT_AGED`. (f) webhook events `ERROR` unresolved. One read per check, provider calls (HTTP only) in a loop capped at 200 per run, one commit. Runs only when candidate rows exist. |
| PAY-R22 | **Issues.** `reconciliation_issues(kind, subject_type, subject_id, order_id, details jsonb, status, detected_at, resolved_at, resolved_by, note)`; unique `(kind, subject_id)` where status = OPEN (no duplicates). New OPEN issue → `PaymentAlertRaised` (Notifications sends `STAFF_PAYMENT_ALERT` to staff with `payments.view` at the location). |
| PAY-R23 | **Location scoping.** Admin lists filter by `fulfillment_location_id` on payments/refunds/settlements (S1, S5, S7) against the staff `loc` claim; detail of another location → 403 `LOCATION_ACCESS_DENIED`. Webhook-event list is unscoped and needs `payments.view` + all-locations access. |
| PAY-R24 | **Audit.** Every admin write (refund request/approve/reject/mark-paid, cash collected, settlement create/dispute/resolve, reprocess) writes an audit entry via the platform audit hook. Customer UPI ids are stored but masked in lists (`pri***@okicici`). |

## 4. Events

### Consumed (each handler runs once via `platform.inbox`, plus the natural guard shown)

| Key | Event (module) | Payload used | Action | Natural guard |
|---|---|---|---|---|
| PAY-H1 | `OrderPlaced` (Ordering) | orderId, customerId, fulfillmentLocationId, paymentMethod, amountDuePaise | PAY-R3 | partial unique `(order_id)` where provider in (CASH, UPI_ON_DELIVERY) |
| PAY-H2 | `OrderPacked` (Ordering) | orderId, amountDuePaise, refundDuePaise, refundDueByReason {WEIGHT_SHORTFALL, SHORT_SUPPLY} (G10) | COD/UPI: PAY-R8. ONLINE with refundDue > 0: one system refund per reason (PAY-R15) | `refunds.source_reference` unique = `OrderPacked:{orderId}:{reason}` |
| PAY-H3 | `OrderAdjusted` (Ordering; goodwill / substitution after pack, G10) | orderId, amountDuePaise, refundDuePaise | PAY-R8; ONLINE refundDue → GOODWILL system refund needs approval (REQUESTED, requester = staff in event) | `source_reference` = `OrderAdjusted:{adjustmentId}` |
| PAY-H4 | `OrderCancelled` (Ordering) | orderId, reasonCode, actor | PAY-R6; paid ONLINE → CANCELLATION refund of refundable (PAY-R15) | `source_reference` = `OrderCancelled:{orderId}` |
| PAY-H5 | `OrderStatusChanged` (Ordering) to `PAYMENT_FAILED` / `RETURNED` | orderId, toStatus | PAY-R6; `RETURNED` + paid ONLINE → refund REQUESTED (needs approval, Q4) | state check |
| PAY-H6 | `ShipmentDelivered` (Delivery) / `OrderDelivered` (Ordering) | orderId, shipmentId, riderId, deliveredAt, cashCollected {amountPaise, mode, reference}? | set `order_delivered_at`; record collection if given (PAY-R4 deliver path) | payment status check |
| PAY-H7 | `PaymentReceivedForClosedOrder` (Ordering) | orderId, paymentId | PAY-R10 refund | `source_reference` = `ClosedOrder:{paymentId}` |
| PAY-H8 | `ComplaintDecided` (Ordering) with resolution REFUND | orderId, complaintId, refundAmountPaise, decidedBy | refund REQUESTED, reason COMPLAINT, method from the event (default ORIGINAL if online else needs staff choice, Q5), requester = decidedBy | `source_reference` = `Complaint:{complaintId}` |
| PAY-H9 | `RefundApproved` (Payments, own) | refundId | PAY-R16 (Worker, only method ORIGINAL) | refund status check |

### Raised (outbox, same save)

| Event | Payload | Consumers |
|---|---|---|
| `PaymentInitiated` | paymentId, orderId, provider, amountPaise | Audit |
| `PaymentAuthorized` | paymentId, orderId | Audit |
| `PaymentCaptured` | paymentId, orderId, provider, amountPaise, capturedAt | Ordering (`PENDING_PAYMENT` → `CONFIRMED`, or PAY-R10), Notifications (`PAYMENT_RECEIVED`) |
| `PaymentFailed` | paymentId, orderId, failureCode, hasOtherActiveAttempt | Ordering (PAY-R11), Notifications (`PAYMENT_FAILED`) |
| `PaymentCollected` | paymentId, orderId, amountPaise, mode, riderId | Ordering (amount paid), Delivery (shipment cash flag) |
| `PaymentCancelled` | paymentId, orderId | Audit |
| `RefundRequested` | refundId, orderId, amountPaise, reasonCode | Notifications (staff with `refunds.approve`) |
| `RefundApproved` | refundId, orderId, method | Payments Worker (PAY-H9), Notifications (`REFUND_INITIATED`) |
| `RefundSucceeded` | refundId, orderId, amountPaise, method | Ordering (amount refunded), Notifications (`REFUND_SUCCEEDED`) |
| `RefundFailed` | refundId, orderId, failureReason | Notifications (staff alert), Audit |
| `CashSettlementRecorded` | settlementId, riderId, date, differencePaise, status | Notifications (`STAFF_CASH_DIFFERENCE` when ≠ 0), Audit |
| `PaymentAlertRaised` | issueId, kind, orderId, details | Notifications (`STAFF_PAYMENT_ALERT`) |

## 5. Commands (Read → Decide → Commit → Follow-on)

| Command | Endpoint | Read | Decide | Commit | Follow-on |
|---|---|---|---|---|---|
| CreateDeliveryPayment | handler PAY-H1 | existing rows for order | PAY-R3 | insert PENDING | — |
| InitiateOnlinePayment | `POST /api/v1/orders/{orderId}/payments` (IK, rate `checkout`) | order facts (Ordering.Public), attempts | PAY-R5 | (provider `CreateOrderAsync` first) insert PENDING attempt | `PaymentInitiated` |
| ReceiveWebhook | `POST /api/v1/payments/webhooks/{provider}` | adapter, event exists?, payment/refund by provider ids | PAY-R7, R9 | event row + payment/refund status | `PaymentCaptured` / `PaymentFailed` / `PaymentAuthorized` / `RefundSucceeded` / `RefundFailed` / `PaymentAlertRaised` |
| ReprocessWebhookEvent | `POST /api/v1/admin/webhook-events/{id}/reprocess` (`settings.manage`, Q7) | event (`ERROR`, signature valid) else 409 `INVALID_STATUS_TRANSITION` | PAY-R7 decide again | event status + payment change | as ReceiveWebhook |
| RecordCashCollection | `POST /api/v1/admin/delivery/shipments/{shipmentId}/cash-collected` (IK, `delivery.manage`; mapped by Payments, Q2); handler PAY-H6 | shipment (Delivery.Public), order status (Ordering.Public), payment | PAY-R4 | payment COLLECTED | `PaymentCollected` |
| UpdateAmountDue | handlers PAY-H2, PAY-H3 | payment(s) of order | PAY-R8, R15 | amount / system refunds | `RefundApproved` |
| CancelOpenAttempts | handlers PAY-H4, PAY-H5 | attempts, paid payment | PAY-R6, R15 | CANCELLED rows, refund | `PaymentCancelled`, `RefundApproved` |
| RequestRefund | `POST /api/v1/admin/orders/{orderId}/refunds` (IK, `refunds.request`); handlers PAY-H7, PAY-H8 | Payment aggregate of the order (paid payment + refunds), complaint ref | PAY-R12, R13 | refund REQUESTED (or APPROVED, system) + payment summary columns | `RefundRequested` / `RefundApproved` |
| ApproveRefund | `POST /api/v1/admin/refunds/{refundId}/approve` (IM, `refunds.approve`) | Payment aggregate | PAY-R14 | APPROVED, `approved_by`, `approved_at` | `RefundApproved` |
| RejectRefund | `POST /api/v1/admin/refunds/{refundId}/reject` (IM, `refunds.approve`) | Payment aggregate | PAY-R14; `reason` required | FAILED, `failure_reason` | `RefundFailed` |
| ProcessRefund | Worker (PAY-H9) | refund | PAY-R16 | tx1 PROCESSING; (provider call); tx2 result | `RefundSucceeded` / `RefundFailed` |
| MarkRefundPaid | `POST /api/v1/admin/refunds/{refundId}/mark-paid` (IM, `refunds.approve`) | Payment aggregate | PAY-R17 | SUCCEEDED | `RefundSucceeded` |
| CreateCashSettlement | `POST /api/v1/admin/cash-settlements` (IK, `cash.settle`) | rider (Delivery.Public), unsettled CASH collections, existing settlement | PAY-R19, R20 | settlement + `cash_settlement_id` on payments | `CashSettlementRecorded` |
| DisputeCashSettlement | `POST /api/v1/admin/cash-settlements/{id}/dispute` (IM, `cash.settle`) | settlement | OPEN/SETTLED; `reason` required | DISPUTED | `CashSettlementRecorded` |
| ResolveCashSettlement | G2 `POST /api/v1/admin/cash-settlements/{id}/settle` (IM, `cash.settle`) | settlement | OPEN/DISPUTED; `note`; resolver ≠ creator if difference ≠ 0 | SETTLED, `settled_by` | `CashSettlementRecorded` |
| ReconcilePayments | Worker hourly | candidates PAY-R21 | PAY-R21 | status changes + issues | as webhook + `PaymentAlertRaised` |
| ResolveIssue | G3 `POST /api/v1/admin/payment-issues/{id}/resolve` | issue | OPEN | RESOLVED | — |
| Queries | `GET /orders/{id}/payments`, `GET /orders/{id}/refunds` (customer, own order else 404); `GET /admin/payments`, `/admin/orders/{id}/payments`, `/admin/webhook-events`, `/admin/refunds`, `/admin/cash-settlements` | one SQL each, location-scoped (PAY-R23) | | | |

### API gaps

| # | Gap | Proposal |
|---|---|---|
| G1 | No `GET /admin/refunds/{id}`, `GET /admin/payments/{id}`, `GET /admin/cash-settlements/{id}` (ETag source for IM commands) | Add, `payments.view` / `cash.settle`, ETag |
| G2 | No way to close an OPEN/DISPUTED settlement | `POST /admin/cash-settlements/{id}/settle` (IM, `cash.settle`, `note`) |
| G3 | No reconciliation issue list/resolve | `GET /admin/payment-issues` (`payments.view`; `kind`, `status`), `POST /admin/payment-issues/{id}/resolve` (IM, `refunds.approve`) |
| G4 | Refund reason has no `GOODWILL`; `/admin/orders/{id}/goodwill` on a prepaid order needs a refund | Add `GOODWILL` to `refunds.reason_code` and API enum |
| G5 | `/admin/payments`, `/admin/refunds`, `/admin/cash-settlements` lack `fulfillmentLocationId` filter | Add the filter |
| G6 | Amount-mismatch money has no resolution action | `POST /admin/payment-issues/{id}/refund-received` (IK, `refunds.request`) creates a refund on the mismatched attempt (needs S3) |
| G7 | No error for wrong COD amount | `VALIDATION_FAILED` field code `CASH_AMOUNT_MISMATCH` on `amountPaise` |
| G8 | Initiate reuse response | 200 with same body when an attempt is reused, 201 when created |
| G9 | Dev/test cannot trigger Fake outcomes | `POST /api/v1/dev/payments/fake/{providerOrderId}/simulate` (`CAPTURED`/`FAILED`/`AMOUNT_MISMATCH`), mapped only in Development |
| G10 | `OrderPacked` / `OrderAdjusted` payload must carry `refundDueByReason` and `amountDuePaise` | Agree with E4 |
| G11 | `POST /orders` ONLINE response `payment.nextAction` has no shape | `{ "type": "INITIATE_PAYMENT", "href": "/api/v1/orders/{id}/payments" }` (Ordering computes from `GetOrderPaymentSummaryAsync`) |

### Schema gaps (`docs/database/README.md` changes to request)

| # | Change |
|---|---|
| S1 | `payments` + `fulfillment_location_id`, `customer_id`, `currency`, `expires_at`, `captured_at`, `collection_mode` CASH/UPI, `collection_reference`, `cash_settlement_id`, `order_delivered_at`, `refunded_paise`, `refund_pending_paise` |
| S2 | `payments.provider` check adds `UPI_ON_DELIVERY` |
| S3 | Replace unique `(order_id) where status in (CAPTURED, COLLECTED)` by `... and is_excess = false`; add `is_excess bool` (second capture on a paid order) |
| S4 | `webhook_events` + `status` RECEIVED/PROCESSED/IGNORED/ERROR/REJECTED, `headers jsonb` (signature/event-id headers only); unique `(provider, provider_event_id)` only `where signature_valid` |
| S5 | `refunds` + `fulfillment_location_id`, `complaint_id`, `source_reference` (unique), `payee_upi_id`, `notes`, `failure_reason`, `manual_reference`, `approved_at`, `succeeded_at`, `succeeded_by`, `attempt_count`; reason adds `GOODWILL` |
| S6 | New `payments.reconciliation_issues` (PAY-R22) |
| S7 | `cash_settlements` + `fulfillment_location_id`, `collections_count`, `note`, `dispute_reason`, `created_by` used as creator for the resolve rule |

## 6. Test scenarios

### Unit (domain)

| # | Scenario | Expect |
|---|---|---|
| U1 | Payment transitions table (§2), every non-listed pair | allowed / `INVALID_STATUS_TRANSITION` |
| U2 | Paid 245051, refunds SUCCEEDED 20000 + REQUESTED 5000 + FAILED 9000; request 220052 | `REFUND_EXCEEDS_PAYMENT`; 220051 ok |
| U3 | Approve by requester | `REFUND_SELF_APPROVAL` |
| U4 | Approve APPROVED refund | `REFUND_NOT_APPROVABLE` |
| U5 | ORIGINAL on CASH payment; UPI without `upiId` | `VALIDATION_FAILED` (`method` / `upiId`) |
| U6 | Mark-paid on ORIGINAL | `INVALID_STATUS_TRANSITION` |
| U7 | Settlement expected 10000, received 9500 / 10000 | OPEN difference −500 / SETTLED 0 |
| U8 | Fake signature: correct HMAC, one byte changed, wrong length | true / false / false (constant-time path) |

### Integration (Postgres, Testcontainers, Fake provider, Worker handlers run in-test)

| # | Scenario | Expect |
|---|---|---|
| I1 | **Duplicate webhook**: `payment.captured` `evt_1` twice, then twice concurrently | first 200 `duplicate:false`, rest `duplicate:true`; one event row; one `PaymentCaptured` in outbox; order `CONFIRMED` once |
| I2 | **Invalid signature** | 401 `WEBHOOK_SIGNATURE_INVALID`; row `REJECTED`, `signature_valid=false`; payment unchanged; later valid event with the same id is processed (S4) |
| I3 | **Two concurrent refunds** 15000 + 15000 on paid 20000 (different keys, parallel) | one 201, other 422 `REFUND_EXCEEDS_PAYMENT` (after internal retry); refundable 5000; never both |
| I4 | **COD with weight adjustment**: placed 234900 → payment PENDING 234900; `OrderPacked` amountDue 245051 → 245051; cash-collected 234900 | 422 `CASH_AMOUNT_MISMATCH`; with 245051 → COLLECTED, `PaymentCollected`; repeat new key → 409 `CASH_ALREADY_COLLECTED`; same key → replay |
| I5 | **Refund after cancel**: ONLINE captured 66950, order cancelled at CONFIRMED | refund APPROVED CANCELLATION 66950 ORIGINAL; Worker → PROCESSING → SUCCEEDED (`fake_rfnd_*`); handler re-delivered → no second refund |
| I6 | Prepaid shortfall: captured 66950, `OrderPacked` refundDue WEIGHT_SHORTFALL 6695 | one APPROVED refund 6695; re-delivery no duplicate |
| I7 | Capture after `PAYMENT_FAILED` (expiry) | payment CAPTURED; Ordering raises `PaymentReceivedForClosedOrder`; CANCELLATION refund full amount |
| I8 | Amount mismatch webhook (70000 vs 66950) | 200; payment PENDING; event `ERROR AMOUNT_MISMATCH`; issue OPEN; `PaymentAlertRaised` |
| I9 | **Reconciliation mismatch**: attempt PENDING 20 min, Fake says captured 66950 | job → CAPTURED via `recon:` event, issue `MISSED_WEBHOOK` AUTO_RESOLVED; real webhook later → duplicate state, no second `PaymentCaptured` |
| I10 | Reconciliation: Fake says captured 60000 for a 66950 attempt; refund PROCESSING 40 min, Fake processed; COD delivered 3 h not collected | `AMOUNT_MISMATCH` OPEN; refund SUCCEEDED; `DELIVERED_NOT_COLLECTED` OPEN; second run creates no duplicate issues |
| I11 | Provider refund transient failure ×2 then success | one provider refund (same key), SUCCEEDED, `attempt_count` 3 |
| I12 | Cash settlement: rider 3 CASH (1000, 2000, 3000) + 1 UPI (5000) on date; received 5800 | expected 6000, difference −200, OPEN; same rider+date again → 409 `SETTLEMENT_EXISTS` |
| I13 | Staff scoped to location A lists refunds; opens refund of location B | only A rows; 403 `LOCATION_ACCESS_DENIED` |
| I14 | Initiate on COD order; twice on ONLINE within expiry | 422 `PAYMENT_NOT_REQUIRED`; second returns same `providerOrderId` (200) |
| I15 | Approve with stale `If-Match` / missing | 412 / 428 |

## 7. Open questions (with proposed answers)

| # | Question | Proposed answer |
|---|---|---|
| Q1 | Architecture lists Payments as using no `.Public`, but initiate needs order facts, cash collection needs shipment/rider, lists need staff names. | Allow Payments → Ordering.Public, Delivery.Public, Identity.Public (no project cycle: `.Public` projects reference nothing). |
| Q2 | `cash-collected` lives under `/admin/delivery/…` but records a Payments fact with Payments error codes. | Payments maps that route; Delivery's `deliver` passes `cashCollectedPaise` in `ShipmentDelivered` (PAY-H6). Delivery must validate the amount against `amountDuePaise` before raising. |
| Q3 | Database §4 and API §7 put webhook → order status in one transaction; CLAUDE.md rule 5 forbids changing another module's rows in our command. | Follow rule 5: Payments commits event + payment + outbox; Ordering confirms on `PaymentCaptured` (seconds later; clients poll). `PaymentReceivedForClosedOrder` is raised by Ordering, not Payments. Update database §4 and API §7. |
| Q4 | Prepaid order `RETURNED` (delivery failed): refund automatically? | Create REQUESTED CANCELLATION refund (human approves) — failure may be the customer's fault; policy decided by the business. |
| Q5 | Prepaid overweight: final total > captured amount (up to +10%). | Ordering caps a prepaid order's final total at the captured amount (business absorbs overweight); no second online collection at launch. Needs E4 agreement. |
| Q6 | Cash collected after that date was settled. | Flag `UNSETTLED_CASH`; include in the rider's next-day settlement by allowing expected to cover all unsettled CASH collections with local date ≤ settlement date (change PAY-R19 wording once agreed). |
| Q7 | Reprocess uses `settings.manage`; resolving issues needs a permission. | New permission `payments.manage` (ADMIN, FINANCE) for reprocess and issue resolution; until then keep `settings.manage` as in docs/api. |
| Q8 | Refund approval with a single admin early on (requester ≠ approver). | Keep the rule; launch with at least one FINANCE user besides ADMIN. No bypass flag. |
| Q9 | COD/UPI cancellation before delivery after `OrderPacked` — any payment action? | Payment → CANCELLED; nothing collected, no refund. |
| Q10 | Store bank account details for BANK_TRANSFER refunds? | No; staff enter the UTR at mark-paid; bank details stay outside the system. UPI id stored, masked in lists. |
| Q11 | Which online gateway and does it support partial captures/auth-only? | Out of scope for launch (ONLINE disabled in `order_policies`, see reference-data.md); adapter contract above is enough for Razorpay later. |
