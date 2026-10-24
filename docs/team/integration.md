# Phase 3 integration checklist

Status after the `integration/phase3` branch (2026-10-24): ✅ done, ⏳ open.

Work the manager does after all Phase 2 branches are merged. Source: engineers' reports.

## Event adapters (IEventHandler<T> → module service, no SaveChanges in the adapter)

| Consumer | Event | Calls | Mapping | Status |
|---|---|---|---|---|
| Inventory | OrderConfirmed | `ConfirmReservationsAsync(OrderId)` | | ✅ |
| Inventory | OrderPaymentFailed | `ExpireReservationsAsync(OrderId)` | | ✅ |
| Inventory | OrderCancelled | `ReleaseReservationsAsync(OrderId)` | | ✅ |
| Inventory | OrderPacked | `ConsumeForPackedOrderAsync(PackedOrder)` | OrderId, FulfillmentLocationId, PackedAt, Lines → PackedLine(OrderItemId, VariantId, YieldBp, PackedQuantity, Status ACTIVE/SHORT_SUPPLIED/SUBSTITUTED) | ✅ |
| Delivery | OrderPlaced | `OnOrderPlacedAsync(OrderId, FulfillmentLocationId, DeliverySlotId, PlacedAt)` | | ✅ |
| Delivery | OrderCancelled | `OnOrderCancelledAsync(OrderId, FulfillmentLocationId, DeliverySlotId, CancelledAt)` | | ✅ |
| Delivery | OrderPaymentFailed | `OnOrderPaymentFailedAsync(OrderId, FulfillmentLocationId, DeliverySlotId, failedAt)` | | ✅ |
| Delivery | OrderPacked | `OnOrderPackedAsync(OrderId, PackedAt, ColdBoxUsed)` | throws if shipment missing → inbox retry | ✅ |
| Delivery | LocationClosureAdded / Removed | `OnClosureAddedAsync` / `OnClosureRemovedAsync(LocationId, ClosedOn)` | | ✅ |
| Delivery | Location activated | `OnLocationActivatedAsync(id)` | | ✅ |
| Payments | OrderPlaced | `OnOrderPlacedAsync(OrderPlacedInput)` | OrderId, CustomerId, FulfillmentLocationId, PaymentMethod code, AmountDuePaise | ✅ |
| Payments | OrderPacked | `OnOrderPackedAsync(OrderPackedInput)` | OrderId, PaymentMethod, AmountDuePaise, RefundDuePaise, RefundDueByReason | ✅ |
| Payments | OrderAdjusted | `OnOrderAdjustedAsync(OrderAdjustedInput)` | OrderId, AdjustmentId, AmountDuePaise, RefundDuePaise, RequestedBy | ✅ |
| Payments | OrderCancelled / OrderPaymentFailed / OrderReturned | `OnOrderCancelledAsync` / `OnOrderPaymentFailedAsync` / `OnOrderReturnedAsync(orderId)` | | ✅ |
| Payments | ShipmentDelivered | `OnShipmentDeliveredAsync(ShipmentDeliveredInput)` | OrderId, ShipmentId, RiderId, DeliveredAt, CashCollectedPaise?, CashMode?, UpiReference? | ✅ |
| Payments | PaymentReceivedForClosedOrder | `OnPaymentReceivedForClosedOrderAsync(orderId, paymentId)` | | ✅ |
| Payments | ComplaintDecided | `OnComplaintDecidedAsync(ComplaintDecidedInput)` | OrderId, ComplaintId, Resolution, RefundAmountPaise?, RefundMethod?, DecidedBy | ✅ |

Superseded by D30: the events live in the owner's `.Public/Events`, so adapters reference only `.Public`. All adapters are in the consumer's `.Application/Handlers` and are checked by `CompositionTests`.

## Shared and host changes

| Item | From | Status |
|---|---|---|
| Register Catalog and Locations public APIs (all Public APIs) in hosts | E3 | ✅ |
| `ILocationsApi.GetClosuresAsync` for Delivery's calendar | E3 | ✅ |
| Max delivery attempts from `order_policies` via an adapter (Delivery may not reference Settings) | E3 | ✅ |
| Shared Forbidden / Unauthorized / ServiceUnavailable exceptions → 401/403/503 in the exception handler; remove module problem filters | E3, E5 | ✅ |
| Replace Payments `UnavailableShipmentFacts` with adapter over Delivery.Public (`GetShipmentAsync`, `GetRiderAsync`) | E5 | ✅ |
| Identity reference data includes `payments.manage` | E5 | ✅ present |
| Cross-schema foreign keys (orders, locations) in the initial migration | E3 | ✅ 26 deferred FKs to `ordering.orders`, `locations.fulfillment_locations`, `locations.cities` (`CrossSchemaForeignKeys`) |
| Initial migration runs all `DatabaseObjectsSql` after tables (Reporting views need real Ordering/Inventory/Catalog columns) | E5 | ✅ the Migrations host runs every module's statements after `MigrateAsync` (not embedded in the migration) |
| SALE movements use `reference_type = 'ORDER_ITEM'` (Reporting assumption) — verify | E5 | ✅ verified in the smoke run |

## From E1 (Identity, Customers, Settings, Audit, Notifications)

| Item | Action | Status |
|---|---|---|
| Cross-module events blocked by architecture tests | D30: move consumed events to `.Public/Events`; update tests | ✅ |
| Cart handles `CustomerSignedIn` (guest cart merge) | adapter after D30 | ✅ |
| Notifications on order/payment/delivery events, `CustomerDeleted` drops device tokens | adapters after D30 | ✅ |
| HTTP 401/403/429 per error | D31 shared exceptions; remove Identity/Customers/Payments/Reporting/Inventory/Delivery filters | ✅ |
| Module "Uses": Identity, Customers, Settings, Notifications → Audit; Customers → Locations; Notifications → Settings, Customers | update architecture doc and project refs | ✅ |
| Host: `refresh` rate-limit policy; Development SMTP `localhost:1025` (Mailpit) | Api/Worker appsettings | ✅ |
| Migrations host `--create-admin` → `IFirstAdminService.CreateFirstAdminAsync(new CreateFirstAdminCommand(email, fullName, password))` after reference data | Migrations host | ✅ |
| Ordering implements `ICustomerDeletionCheck` (`ACTIVE_ORDERS_EXIST`) | E4 / manager | ✅ |
| Wishlist priced products; address `isServiceable` | after Catalog/Locations Public lookups exist | ✅ address `isServiceable` from Locations; ⏳ wishlist prices not done |

## From E2 (Locations, Catalog, Pricing, storage)

| Item | Action | Status |
|---|---|---|
| Api host: `app.UseFreshOrbitMedia();` after `app.UseFreshOrbitWeb();` | host | ✅ |
| `search` rate-limit policy for `/products` and `/search/suggest` | host + attach in Catalog | ✅ |
| `QuantityRules` from Catalog.Domain to SharedKernel so Cart/Ordering share it | decide with E4's LinePricing | ⏳ not done (Catalog and Cart keep their own `QuantityRules`) |
| Catalog `DatabaseObjectsSql` must run after Inventory tables exist (storefront view) | migration ordering | ✅ runs after all tables (Migrations host) |
| `/home` banners from Promotions; `isWishlisted` from Customers | after E4 merge / Customers wishlist API | ⏳ not done |
| Architecture tests updated for D30 (Domain → own Public) | ✅ done at E2 merge | ✅ |

## From E4 (Cart, Promotions, Ordering, LinePricing)

| Item | Action | Status |
|---|---|---|
| Architecture tests: Ordering.Domain → Ordering.Public allowed (D30) | confirm the generic rule from the E2 merge covers it | ✅ |
| `OrderingCustomerDeletionCheck` (Ordering.Infrastructure/Public/OrderingApi.cs) | add `: ICustomerDeletionCheck`; register with `TryAddEnumerable(ServiceDescriptor.Scoped<ICustomerDeletionCheck, OrderingCustomerDeletionCheck>())` | ✅ |
| Promotions adapters | `OrderCancelled` → `RedemptionReversalService.ReverseForOrderAsync(e.OrderId, ReversalReason.OrderCancelled)`; `OrderPaymentFailed` → same with `ReversalReason.PaymentFailed` | ✅ |
| Ordering adapters (`OrderEventService`) | PaymentCaptured → `OnPaymentCapturedAsync(orderId, paymentId, amountPaise, capturedAt)`; PaymentFailed → `OnPaymentFailedAsync(orderId, paymentId, failureCode, hasOtherActiveAttempt)`; ShipmentDispatched → `OnShipmentDispatchedAsync(orderId, shipmentId, riderId, dispatchedAt)`; ShipmentDelivered → `OnShipmentDeliveredAsync(orderId, shipmentId, deliveredAt)`; DeliveryAttemptFailed → `OnDeliveryAttemptFailedAsync(orderId, attemptNo, outcome, attemptedAt)`; ShipmentReattempted → `OnShipmentReattemptedAsync(orderId)`; ShipmentReturned → `OnShipmentReturnedAsync(orderId, shipmentId, returnedAt)` | ✅ |
| Cart adapter | Identity `CustomerSignedIn` → `ICartApi.MergeGuestCartAsync(guestToken, customerId)` | ✅ |
| Notifications staff-lock adapter (D33) | Identity `StaffAccountLocked` → `NotifyStaffAccountLocked` queues `STAFF_ACCOUNT_LOCKED` for `staff.manage` holders; listed in `CompositionTests.Adapters` | ✅ (`security/round-2-identity`) |
| Replace E4 stand-ins with real Public APIs | `IPromotionsCatalog` (Catalog category lineage/existence), `ICityDirectory` (Locations city lookup), `IDeliveryFeeTaxRate` (Pricing tax rate for DELIVERY_SERVICE), `IOrderStockReleaser` (Inventory release of one line's stock) and low-stock threshold, `IShipmentTracking` (Delivery shipment by order), `IPaymentMethodAvailability` (Payments enabled methods / order policy); Settings `substitutionMinutes` and `complaintWindowHours` from order policy instead of `OrderingOptions` defaults | ✅ |
| `Ordering.Tests` `DbNumberSeriesSettings` stand-in | no longer implements `ISettingsApi` (`NextNumberAsync(string, DateTimeOffset, ct)`, `GetFinancialPeriodAsync`); use the real Settings implementation (locks the series row `FOR UPDATE` in the caller's transaction) | ✅ |
