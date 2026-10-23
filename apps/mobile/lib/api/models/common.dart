// TEMPORARY: replace with fresh_api generated models after Phase 3
//
// Shapes from docs/api/README.md §1.1 (Quantity), §1.5 (lists) and §5.1 (availability, issues).

import 'json.dart';

/// `{ "value": 500, "unit": "GRAM" }`. `unit` is GRAM (sell unit KG), PIECE or PACK.
class Quantity {
  const Quantity(this.value, this.unit);

  factory Quantity.fromJson(Json j) => Quantity(reqInt(j, 'value'), reqString(j, 'unit'));

  final int value;
  final String unit;

  Json toJson() => {'value': value, 'unit': unit};

  /// Same unit, another value. Used by the stepper to *propose* a quantity; the server validates it.
  Quantity withValue(int newValue) => Quantity(newValue, unit);

  @override
  bool operator ==(Object other) => other is Quantity && other.value == value && other.unit == unit;

  @override
  int get hashCode => Object.hash(value, unit);

  @override
  String toString() => '$value $unit';
}

Quantity? optQuantity(Json j, String field) {
  final v = asJsonOrNull(j[field], field);
  return v == null ? null : Quantity.fromJson(v);
}

Quantity reqQuantity(Json j, String field) => Quantity.fromJson(asJson(j[field], field));

/// `{ items, page, pageSize, totalCount }`.
class Paged<T> {
  const Paged({required this.items, required this.page, required this.pageSize, required this.totalCount});

  factory Paged.fromJson(Json j, T Function(Json) read) => Paged(
        items: reqList(j, 'items', read),
        page: reqInt(j, 'page'),
        pageSize: reqInt(j, 'pageSize'),
        totalCount: reqInt(j, 'totalCount'),
      );

  final List<T> items;
  final int page;
  final int pageSize;
  final int totalCount;

  bool get hasMore => page * pageSize < totalCount;
}

/// `{ status, message }`: IN_STOCK, LOW_STOCK, OUT_OF_STOCK, NOT_SOLD_HERE.
class Availability {
  const Availability({required this.status, this.message});

  factory Availability.fromJson(Json j) => Availability(status: reqString(j, 'status'), message: optString(j, 'message'));

  final String status;
  final String? message;

  bool get canAdd => status == 'IN_STOCK' || status == 'LOW_STOCK';
}

Availability? optAvailability(Json j, String field) {
  final v = asJsonOrNull(j[field], field);
  return v == null ? null : Availability.fromJson(v);
}

/// `{ code, message }` (cart line issues) and `{ code, lineId?, message }` (preview blockers).
class CodedMessage {
  const CodedMessage({required this.code, required this.message, this.lineId});

  factory CodedMessage.fromJson(Json j) =>
      CodedMessage(code: reqString(j, 'code'), message: optString(j, 'message') ?? '', lineId: optString(j, 'lineId'));

  final String code;
  final String message;
  final String? lineId;
}
