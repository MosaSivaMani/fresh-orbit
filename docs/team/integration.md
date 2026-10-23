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

## From E1 (Identity, Customers, Settings, Audit, Notifications)

| Item | Action |
|---|---|
| Cross-module events blocked by architecture tests | D30: move consumed events to `.Public/Events`; update tests |
| Cart handles `CustomerSignedIn` (guest cart merge) | adapter after D30 |
| Notifications on order/payment/delivery events, `CustomerDeleted` drops device tokens | adapters after D30 |
| HTTP 401/403/429 per error | D31 shared exceptions; remove Identity/Customers/Payments/Reporting/Inventory/Delivery filters |
| Module "Uses": Identity, Customers, Settings, Notifications → Audit; Customers → Locations; Notifications → Settings, Customers | update architecture doc and project refs |
| Host: `refresh` rate-limit policy; Development SMTP `localhost:1025` (Mailpit) | Api/Worker appsettings |
| Migrations host `--create-admin` → `IFirstAdminService.CreateFirstAdminAsync(new CreateFirstAdminCommand(email, fullName, password))` after reference data | Migrations host |
| Ordering implements `ICustomerDeletionCheck` (`ACTIVE_ORDERS_EXIST`) | E4 / manager |
| Wishlist priced products; address `isServiceable` | after Catalog/Locations Public lookups exist |

## From E2 (Locations, Catalog, Pricing, storage)

| Item | Action |
|---|---|
| Api host: `app.UseFreshOrbitMedia();` after `app.UseFreshOrbitWeb();` | host |
| `search` rate-limit policy for `/products` and `/search/suggest` | host + attach in Catalog |
| `QuantityRules` from Catalog.Domain to SharedKernel so Cart/Ordering share it | decide with E4's LinePricing |
| Catalog `DatabaseObjectsSql` must run after Inventory tables exist (storefront view) | migration ordering |
| `/home` banners from Promotions; `isWishlisted` from Customers | after E4 merge / Customers wishlist API |
| Architecture tests updated for D30 (Domain → own Public) | ✅ done at E2 merge |
