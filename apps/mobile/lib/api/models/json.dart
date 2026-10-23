// TEMPORARY: replace with fresh_api generated models after Phase 3
//
// Small JSON readers shared by the hand-written models. They fail loudly (FormatException) when a required
// field is missing or has the wrong type, so a contract drift shows up as an error state, never as a guess.

typedef Json = Map<String, dynamic>;

Json asJson(Object? value, String field) {
  if (value is Map<String, dynamic>) return value;
  if (value is Map) return value.cast<String, dynamic>();
  throw FormatException('Expected an object for "$field".');
}

Json? asJsonOrNull(Object? value, String field) => value == null ? null : asJson(value, field);

String reqString(Json j, String field) {
  final v = j[field];
  if (v is String) return v;
  throw FormatException('Expected a string for "$field".');
}

String? optString(Json j, String field) {
  final v = j[field];
  if (v == null) return null;
  if (v is String) return v;
  throw FormatException('Expected a string or null for "$field".');
}

int reqInt(Json j, String field) {
  final v = j[field];
  if (v is int) return v;
  if (v is num && v == v.roundToDouble()) return v.toInt();
  throw FormatException('Expected an integer for "$field".');
}

int? optInt(Json j, String field) => j[field] == null ? null : reqInt(j, field);

bool reqBool(Json j, String field) {
  final v = j[field];
  if (v is bool) return v;
  throw FormatException('Expected a boolean for "$field".');
}

bool? optBool(Json j, String field) => j[field] == null ? null : reqBool(j, field);

/// ISO-8601 instant, kept in UTC.
DateTime reqInstant(Json j, String field) => DateTime.parse(reqString(j, field)).toUtc();

DateTime? optInstant(Json j, String field) => j[field] == null ? null : reqInstant(j, field);

List<T> reqList<T>(Json j, String field, T Function(Json) read) {
  final v = j[field];
  if (v is List) return [for (final e in v) read(asJson(e, field))];
  throw FormatException('Expected a list for "$field".');
}

/// Lists are never omitted by the API (§1.1), but tolerate null as empty for forward compatibility.
List<T> listOrEmpty<T>(Json j, String field, T Function(Json) read) =>
    j[field] == null ? <T>[] : reqList(j, field, read);

List<String> stringList(Json j, String field) {
  final v = j[field];
  if (v == null) return const [];
  if (v is List) return [for (final e in v) e as String];
  throw FormatException('Expected a list of strings for "$field".');
}
