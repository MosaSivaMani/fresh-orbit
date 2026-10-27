// TEMPORARY: replace with fresh_api generated models after Phase 3
//
// docs/api/README.md §4.3 GET /serviceability and §4.5 GET /delivery-slots.

import 'json.dart';

class Serviceability {
  const Serviceability({
    required this.pincode,
    required this.serviceable,
    this.reasonCode,
    this.cityName,
    this.cityStateCode,
    this.cityStateName,
    this.locationId,
    this.locationName,
    this.serviceAreaName,
    this.deliveryFeePaise,
    this.freeDeliveryAbovePaise,
    this.minOrderPaise,
    this.nextSlot,
  });

  factory Serviceability.fromJson(Json j) {
    final city = asJsonOrNull(j['city'], 'city');
    final location = asJsonOrNull(j['fulfillmentLocation'], 'fulfillmentLocation');
    final area = asJsonOrNull(j['serviceArea'], 'serviceArea');
    final slot = asJsonOrNull(j['nextSlot'], 'nextSlot');
    return Serviceability(
      pincode: reqString(j, 'pincode'),
      serviceable: reqBool(j, 'serviceable'),
      reasonCode: optString(j, 'reasonCode'),
      cityName: city == null ? null : reqString(city, 'name'),
      cityStateCode: city == null ? null : optString(city, 'stateCode'),
      cityStateName: city == null ? null : optString(city, 'stateName'),
      locationId: location == null ? null : optString(location, 'id'),
      locationName: location == null ? null : reqString(location, 'name'),
      serviceAreaName: area == null ? null : optString(area, 'name'),
      deliveryFeePaise: area == null ? null : optInt(area, 'deliveryFeePaise'),
      freeDeliveryAbovePaise: area == null ? null : optInt(area, 'freeDeliveryAbovePaise'),
      minOrderPaise: area == null ? null : optInt(area, 'minOrderPaise'),
      nextSlot: slot == null ? null : SlotWindow.fromJson(slot),
    );
  }

  final String pincode;
  final bool serviceable;
  final String? reasonCode;
  final String? cityName;

  /// The city's state (newer API; null when the server does not send it). Prefills the address form.
  final String? cityStateCode;
  final String? cityStateName;

  /// The store that serves the pincode; a back-in-stock alert names it.
  final String? locationId;
  final String? locationName;
  final String? serviceAreaName;
  final int? deliveryFeePaise;
  final int? freeDeliveryAbovePaise;
  final int? minOrderPaise;
  final SlotWindow? nextSlot;
}

/// `{ startAt, endAt }` (plus `id` where the API sends it).
class SlotWindow {
  const SlotWindow({this.id, required this.startAt, required this.endAt});

  factory SlotWindow.fromJson(Json j) =>
      SlotWindow(id: optString(j, 'id'), startAt: reqInstant(j, 'startAt'), endAt: reqInstant(j, 'endAt'));

  final String? id;
  final DateTime startAt;
  final DateTime endAt;
}

/// One delivery slot: status AVAILABLE, FULL, CUTOFF_PASSED or CLOSED.
class DeliverySlot {
  const DeliverySlot({
    required this.id,
    required this.slotDate,
    required this.startAt,
    required this.endAt,
    required this.cutoffAt,
    required this.status,
  });

  factory DeliverySlot.fromJson(Json j) => DeliverySlot(
    id: reqString(j, 'id'),
    slotDate: reqString(j, 'slotDate'),
    startAt: reqInstant(j, 'startAt'),
    endAt: reqInstant(j, 'endAt'),
    cutoffAt: reqInstant(j, 'cutoffAt'),
    status: reqString(j, 'status'),
  );

  final String id;

  /// `YYYY-MM-DD` in the city's time zone (used for day tabs, no zone shift).
  final String slotDate;
  final DateTime startAt;
  final DateTime endAt;
  final DateTime cutoffAt;
  final String status;

  bool get isBookable => status == 'AVAILABLE';
}

class DeliverySlots {
  const DeliverySlots({required this.fulfillmentLocationId, required this.items});

  factory DeliverySlots.fromJson(Json j) => DeliverySlots(
    fulfillmentLocationId: optString(j, 'fulfillmentLocationId'),
    items: reqList(j, 'items', DeliverySlot.fromJson),
  );

  final String? fulfillmentLocationId;
  final List<DeliverySlot> items;
}
