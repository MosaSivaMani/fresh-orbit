// TEMPORARY: replace with fresh_api generated models after Phase 3
//
// docs/api/README.md §4.8: POST /checkout/preview and POST /orders (Idempotency-Key, expectedTotalPaise,
// 409 PRICE_CHANGED carrying a `preview` member).

import 'cart.dart';
import 'common.dart';
import 'json.dart';

/// What the customer chose at checkout. The body carries no lines or prices (§4.8 place-order rules).
class CheckoutSelection {
  const CheckoutSelection({
    required this.addressId,
    required this.deliverySlotId,
    required this.paymentMethod,
    this.substitutionPreference = 'CALL_ME',
  });

  final String addressId;
  final String deliverySlotId;

  /// COD, UPI_ON_DELIVERY (ONLINE is disabled until KYC, D26).
  final String paymentMethod;

  /// CALL_ME or AUTO_SIMILAR.
  final String substitutionPreference;

  Json toJson() => {
    'addressId': addressId,
    'deliverySlotId': deliverySlotId,
    'paymentMethod': paymentMethod,
    'substitutionPreference': substitutionPreference,
  };
}

class PlaceOrderRequest {
  const PlaceOrderRequest({required this.selection, required this.expectedTotalPaise, this.customerNotes});

  final CheckoutSelection selection;

  /// Taken verbatim from the last preview's `expectedTotalPaise`; never computed here.
  final int expectedTotalPaise;
  final String? customerNotes;

  Json toJson() => {...selection.toJson(), 'customerNotes': customerNotes, 'expectedTotalPaise': expectedTotalPaise};
}

class PreviewAddress {
  const PreviewAddress({required this.id, this.label, this.line1, this.locality, this.pincode, this.isServiceable});

  factory PreviewAddress.fromJson(Json j) => PreviewAddress(
    id: reqString(j, 'id'),
    label: optString(j, 'label'),
    line1: optString(j, 'line1'),
    locality: optString(j, 'locality'),
    pincode: optString(j, 'pincode'),
    isServiceable: optBool(j, 'isServiceable'),
  );

  final String id;
  final String? label;
  final String? line1;
  final String? locality;
  final String? pincode;
  final bool? isServiceable;
}

class PreviewSlot {
  const PreviewSlot({required this.id, required this.startAt, required this.endAt, this.cutoffAt, this.status});

  factory PreviewSlot.fromJson(Json j) => PreviewSlot(
    id: reqString(j, 'id'),
    startAt: reqInstant(j, 'startAt'),
    endAt: reqInstant(j, 'endAt'),
    cutoffAt: optInstant(j, 'cutoffAt'),
    status: optString(j, 'status'),
  );

  final String id;
  final DateTime startAt;
  final DateTime endAt;
  final DateTime? cutoffAt;
  final String? status;
}

class PaymentMethodOption {
  const PaymentMethodOption({required this.method, required this.enabled, this.reasonCode});

  factory PaymentMethodOption.fromJson(Json j) => PaymentMethodOption(
    method: reqString(j, 'method'),
    enabled: reqBool(j, 'enabled'),
    reasonCode: optString(j, 'reasonCode'),
  );

  final String method;
  final bool enabled;
  final String? reasonCode;
}

class CheckoutPreview {
  const CheckoutPreview({
    this.cartId,
    this.address,
    this.slot,
    required this.lines,
    this.coupon,
    required this.totals,
    required this.paymentMethods,
    required this.canPlaceOrder,
    required this.blockers,
    required this.expectedTotalPaise,
  });

  factory CheckoutPreview.fromJson(Json j) {
    final address = asJsonOrNull(j['address'], 'address');
    final slot = asJsonOrNull(j['slot'], 'slot');
    return CheckoutPreview(
      cartId: optString(j, 'cartId'),
      address: address == null ? null : PreviewAddress.fromJson(address),
      slot: slot == null ? null : PreviewSlot.fromJson(slot),
      lines: listOrEmpty(j, 'lines', CartLine.fromJson),
      coupon: optCoupon(j),
      totals: CartTotals.fromJson(asJson(j['totals'], 'totals')),
      paymentMethods: listOrEmpty(j, 'paymentMethods', PaymentMethodOption.fromJson),
      canPlaceOrder: reqBool(j, 'canPlaceOrder'),
      blockers: listOrEmpty(j, 'blockers', CodedMessage.fromJson),
      expectedTotalPaise: reqInt(j, 'expectedTotalPaise'),
    );
  }

  final String? cartId;
  final PreviewAddress? address;
  final PreviewSlot? slot;
  final List<CartLine> lines;
  final CartCoupon? coupon;
  final CartTotals totals;
  final List<PaymentMethodOption> paymentMethods;
  final bool canPlaceOrder;
  final List<CodedMessage> blockers;
  final int expectedTotalPaise;
}
