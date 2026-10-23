// TEMPORARY: replace with fresh_api generated models after Phase 3
//
// docs/api/README.md §4.8: GET /orders, /orders/{id}, /orders/{id}/tracking, /orders/{id}/complaints.
// The list item shape is not written out in docs/api; [OrderSummary] reads the subset of the detail shape a
// list can carry (id, orderNumber, status, placedAt, slot, totals) and tolerates missing optional parts.

import 'common.dart';
import 'json.dart';
import 'locations.dart';

class OrderAmounts {
  const OrderAmounts({
    required this.itemsPaise,
    required this.preparationPaise,
    required this.discountPaise,
    required this.deliveryFeePaise,
    required this.taxPaise,
    required this.totalPaise,
  });

  factory OrderAmounts.fromJson(Json j) => OrderAmounts(
        itemsPaise: optInt(j, 'itemsPaise') ?? 0,
        preparationPaise: optInt(j, 'preparationPaise') ?? 0,
        discountPaise: optInt(j, 'discountPaise') ?? 0,
        deliveryFeePaise: optInt(j, 'deliveryFeePaise') ?? 0,
        taxPaise: optInt(j, 'taxPaise') ?? 0,
        totalPaise: reqInt(j, 'totalPaise'),
      );

  final int itemsPaise;
  final int preparationPaise;
  final int discountPaise;
  final int deliveryFeePaise;
  final int taxPaise;
  final int totalPaise;
}

OrderAmounts? _optAmounts(Json j, String field) {
  final v = asJsonOrNull(j[field], field);
  return v == null ? null : OrderAmounts.fromJson(v);
}

class OrderSummary {
  const OrderSummary({
    required this.id,
    required this.orderNumber,
    required this.status,
    required this.placedAt,
    this.slot,
    this.totalPaise,
    this.itemCount,
  });

  factory OrderSummary.fromJson(Json j) {
    final slot = asJsonOrNull(j['slot'], 'slot');
    final finalAmounts = _optAmounts(j, 'final');
    final estimate = _optAmounts(j, 'estimate');
    return OrderSummary(
      id: reqString(j, 'id'),
      orderNumber: reqString(j, 'orderNumber'),
      status: reqString(j, 'status'),
      placedAt: reqInstant(j, 'placedAt'),
      slot: slot == null ? null : SlotWindow.fromJson(slot),
      totalPaise: optInt(j, 'totalPaise') ?? finalAmounts?.totalPaise ?? estimate?.totalPaise,
      itemCount: optInt(j, 'itemCount'),
    );
  }

  final String id;
  final String orderNumber;
  final String status;
  final DateTime placedAt;
  final SlotWindow? slot;

  /// The server's current total for the order (final once packed, else estimate).
  final int? totalPaise;
  final int? itemCount;
}

class OrderAddress {
  const OrderAddress({
    this.recipientName,
    this.phone,
    this.line1,
    this.line2,
    this.landmark,
    this.locality,
    this.cityName,
    this.pincode,
  });

  factory OrderAddress.fromJson(Json j) => OrderAddress(
        recipientName: optString(j, 'recipientName'),
        phone: optString(j, 'phone'),
        line1: optString(j, 'line1'),
        line2: optString(j, 'line2'),
        landmark: optString(j, 'landmark'),
        locality: optString(j, 'locality'),
        cityName: optString(j, 'cityName'),
        pincode: optString(j, 'pincode'),
      );

  final String? recipientName;
  final String? phone;
  final String? line1;
  final String? line2;
  final String? landmark;
  final String? locality;
  final String? cityName;
  final String? pincode;

  String get oneLine =>
      [line1, line2, landmark, locality, cityName, pincode].whereType<String>().where((s) => s.isNotEmpty).join(', ');
}

class OrderItem {
  const OrderItem({
    required this.id,
    required this.productName,
    this.variantName,
    this.preparationName,
    required this.orderedQuantity,
    this.maxChargeableQuantity,
    this.packedQuantity,
    this.chargedQuantity,
    required this.estimatedTotalPaise,
    this.finalTotalPaise,
    required this.status,
  });

  factory OrderItem.fromJson(Json j) => OrderItem(
        id: reqString(j, 'id'),
        productName: reqString(j, 'productName'),
        variantName: optString(j, 'variantName'),
        preparationName: optString(j, 'preparationName'),
        orderedQuantity: reqQuantity(j, 'orderedQuantity'),
        maxChargeableQuantity: optQuantity(j, 'maxChargeableQuantity'),
        packedQuantity: optQuantity(j, 'packedQuantity'),
        chargedQuantity: optQuantity(j, 'chargedQuantity'),
        estimatedTotalPaise: reqInt(j, 'estimatedTotalPaise'),
        finalTotalPaise: optInt(j, 'finalTotalPaise'),
        status: optString(j, 'status') ?? 'ACTIVE',
      );

  final String id;
  final String productName;
  final String? variantName;
  final String? preparationName;
  final Quantity orderedQuantity;
  final Quantity? maxChargeableQuantity;
  final Quantity? packedQuantity;
  final Quantity? chargedQuantity;
  final int estimatedTotalPaise;
  final int? finalTotalPaise;
  final String status;
}

class AllowedActions {
  const AllowedActions({
    required this.cancel,
    required this.raiseComplaint,
    required this.downloadInvoice,
    required this.reorder,
  });

  factory AllowedActions.fromJson(Json j) => AllowedActions(
        cancel: optBool(j, 'cancel') ?? false,
        raiseComplaint: optBool(j, 'raiseComplaint') ?? false,
        downloadInvoice: optBool(j, 'downloadInvoice') ?? false,
        reorder: optBool(j, 'reorder') ?? false,
      );

  static const none = AllowedActions(cancel: false, raiseComplaint: false, downloadInvoice: false, reorder: false);

  final bool cancel;
  final bool raiseComplaint;
  final bool downloadInvoice;
  final bool reorder;
}

class OrderPayment {
  const OrderPayment({required this.method, required this.status});

  factory OrderPayment.fromJson(Json j) =>
      OrderPayment(method: reqString(j, 'method'), status: optString(j, 'status') ?? 'PENDING');

  final String method;
  final String status;
}

class OrderDetail {
  const OrderDetail({
    required this.id,
    required this.orderNumber,
    this.version,
    required this.status,
    required this.paymentMethod,
    required this.placedAt,
    this.slot,
    this.address,
    required this.items,
    required this.estimate,
    this.finalAmounts,
    required this.amountDuePaise,
    required this.amountPaidPaise,
    required this.amountRefundedPaise,
    this.couponCode,
    this.payment,
    required this.allowedActions,
  });

  factory OrderDetail.fromJson(Json j) {
    final slot = asJsonOrNull(j['slot'], 'slot');
    final address = asJsonOrNull(j['address'], 'address');
    final payment = asJsonOrNull(j['payment'], 'payment');
    final actions = asJsonOrNull(j['allowedActions'], 'allowedActions');
    return OrderDetail(
      id: reqString(j, 'id'),
      orderNumber: reqString(j, 'orderNumber'),
      version: optString(j, 'version'),
      status: reqString(j, 'status'),
      paymentMethod: reqString(j, 'paymentMethod'),
      placedAt: reqInstant(j, 'placedAt'),
      slot: slot == null ? null : SlotWindow.fromJson(slot),
      address: address == null ? null : OrderAddress.fromJson(address),
      items: listOrEmpty(j, 'items', OrderItem.fromJson),
      estimate: OrderAmounts.fromJson(asJson(j['estimate'], 'estimate')),
      finalAmounts: _optAmounts(j, 'final'),
      amountDuePaise: optInt(j, 'amountDuePaise') ?? 0,
      amountPaidPaise: optInt(j, 'amountPaidPaise') ?? 0,
      amountRefundedPaise: optInt(j, 'amountRefundedPaise') ?? 0,
      couponCode: optString(j, 'couponCode'),
      payment: payment == null ? null : OrderPayment.fromJson(payment),
      allowedActions: actions == null ? AllowedActions.none : AllowedActions.fromJson(actions),
    );
  }

  final String id;
  final String orderNumber;
  final String? version;
  final String status;
  final String paymentMethod;
  final DateTime placedAt;
  final SlotWindow? slot;
  final OrderAddress? address;
  final List<OrderItem> items;
  final OrderAmounts estimate;

  /// Null until PACKED.
  final OrderAmounts? finalAmounts;
  final int amountDuePaise;
  final int amountPaidPaise;
  final int amountRefundedPaise;
  final String? couponCode;
  final OrderPayment? payment;
  final AllowedActions allowedActions;
}

class TimelineEntry {
  const TimelineEntry({required this.status, required this.at});

  factory TimelineEntry.fromJson(Json j) => TimelineEntry(status: reqString(j, 'status'), at: reqInstant(j, 'at'));

  final String status;
  final DateTime at;
}

class ShipmentInfo {
  const ShipmentInfo({required this.status, this.riderName, this.riderPhone, this.dispatchedAt});

  factory ShipmentInfo.fromJson(Json j) => ShipmentInfo(
        status: reqString(j, 'status'),
        riderName: optString(j, 'riderName'),
        riderPhone: optString(j, 'riderPhone'),
        dispatchedAt: optInstant(j, 'dispatchedAt'),
      );

  final String status;
  final String? riderName;
  final String? riderPhone;
  final DateTime? dispatchedAt;
}

class OrderTracking {
  const OrderTracking({
    required this.orderNumber,
    required this.status,
    required this.timeline,
    this.slot,
    this.shipment,
    this.deliveryCode,
    this.amountDuePaise,
  });

  factory OrderTracking.fromJson(Json j) {
    final slot = asJsonOrNull(j['slot'], 'slot');
    final shipment = asJsonOrNull(j['shipment'], 'shipment');
    return OrderTracking(
      orderNumber: reqString(j, 'orderNumber'),
      status: reqString(j, 'status'),
      timeline: listOrEmpty(j, 'timeline', TimelineEntry.fromJson),
      slot: slot == null ? null : SlotWindow.fromJson(slot),
      shipment: shipment == null ? null : ShipmentInfo.fromJson(shipment),
      deliveryCode: optString(j, 'deliveryCode'),
      amountDuePaise: optInt(j, 'amountDuePaise'),
    );
  }

  final String orderNumber;
  final String status;
  final List<TimelineEntry> timeline;
  final SlotWindow? slot;
  final ShipmentInfo? shipment;

  /// Sent only while OUT_FOR_DELIVERY.
  final String? deliveryCode;
  final int? amountDuePaise;
}

class Complaint {
  const Complaint({required this.id, this.orderItemId, required this.kind, required this.status, this.description});

  factory Complaint.fromJson(Json j) => Complaint(
        id: reqString(j, 'id'),
        orderItemId: optString(j, 'orderItemId'),
        kind: reqString(j, 'kind'),
        status: reqString(j, 'status'),
        description: optString(j, 'description'),
      );

  final String id;
  final String? orderItemId;

  /// MISSING, DAMAGED, QUALITY, WRONG_ITEM, LATE.
  final String kind;

  /// OPEN, APPROVED, REJECTED, RESOLVED.
  final String status;
  final String? description;
}
