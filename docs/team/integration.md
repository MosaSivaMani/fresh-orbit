# Phase 3 integration checklist

Work the manager does after all Phase 2 branches are merged. Source: engineers' reports.

## Event adapters (IEventHandler<T> → module service, no SaveChanges in the adapter)

| Consumer | Event | Calls | Mapping |
|---|---|---|---|
| Inventory | OrderConfirmed | `ConfirmReservationsAsync(OrderId)` | |
| Inventory | OrderPaymentFailed | `ExpireReservationsAsync(OrderId)` | |
| Inventory | OrderCancelled | `ReleaseReservationsAsync(OrderId)` | |
| Inventory | OrderPacked | `ConsumeForPackedOrderAsync(PackedOrder)` | OrderId, FulfillmentLocationId, PackedAt, Lines → PackedLine(OrderItemId, VariantId, YieldBp, PackedQuantity, Status ACTIVE/SHORT_SUPPLIED/SUBSTITUTED) |
| Delivery | OrderPlaced | `OnOrderPlacedAsync(OrderId, FulfillmentLocationId, DeliverySlotId, PlacedAt)` | |
| Delivery | OrderCancelled | `OnOrderCancelledAsync(OrderId, FulfillmentLocationId, DeliverySlotId, CancelledAt)` | |
| Delivery | OrderPaymentFailed | `OnOrderPaymentFailedAsync(OrderId, FulfillmentLocationId, DeliverySlotId, failedAt)` | |
| Delivery | OrderPacked | `OnOrderPackedAsync(OrderId, PackedAt, ColdBoxUsed)` | throws if shipment missing → inbox retry |
| Delivery | LocationClosureAdded / Removed | `OnClosureAddedAsync` / `OnClosureRemovedAsync(LocationId, ClosedOn)` | |
| Delivery | Location activated | `OnLocationActivatedAsync(id)` | |
| Payments | OrderPlaced | `OnOrderPlacedAsync(OrderPlacedInput)` | OrderId, CustomerId, FulfillmentLocationId, PaymentMethod code, AmountDuePaise |
| Payments | OrderPacked | `OnOrderPackedAsync(OrderPackedInput)` | OrderId, PaymentMethod, AmountDuePaise, RefundDuePaise, RefundDueByReason |
| Payments | OrderAdjusted | `OnOrderAdjustedAsync(OrderAdjustedInput)` | OrderId, AdjustmentId, AmountDuePaise, RefundDuePaise, RequestedBy |
| Payments | OrderCancelled / OrderPaymentFailed / OrderReturned | `OnOrderCancelledAsync` / `OnOrderPaymentFailedAsync` / `OnOrderReturnedAsync(orderId)` | |
| Payments | ShipmentDelivered | `OnShipmentDeliveredAsync(ShipmentDeliveredInput)` | OrderId, ShipmentId, RiderId, DeliveredAt, CashCollectedPaise?, CashMode?, UpiReference? |
| Payments | PaymentReceivedForClosedOrder | `OnPaymentReceivedForClosedOrderAsync(orderId, paymentId)` | |
| Payments | ComplaintDecided | `OnComplaintDecidedAsync(ComplaintDecidedInput)` | OrderId, ComplaintId, Resolution, RefundAmountPaise?, RefundMethod?, DecidedBy |

Architecture tests must allow these adapters to reference the source module's `.Domain` event namespaces.

## Shared and host changes

| Item | From |
|---|---|
| Register Catalog and Locations public APIs (all Public APIs) in hosts | E3 |
| `ILocationsApi.GetClosuresAsync` for Delivery's calendar | E3 |
| Max delivery attempts from `order_policies` via an adapter (Delivery may not reference Settings) | E3 |
| Shared Forbidden / Unauthorized / ServiceUnavailable exceptions → 401/403/503 in the exception handler; remove module problem filters | E3, E5 |
| Replace Payments `UnavailableShipmentFacts` with adapter over Delivery.Public (`GetShipmentAsync`, `GetRiderAsync`) | E5 |
| Identity reference data includes `payments.manage` | E5 |
| Cross-schema foreign keys (orders, locations) in the initial migration | E3 |
| Initial migration runs all `DatabaseObjectsSql` after tables (Reporting views need real Ordering/Inventory/Catalog columns) | E5 |
| SALE movements use `reference_type = 'ORDER_ITEM'` (Reporting assumption) — verify | E5 |
