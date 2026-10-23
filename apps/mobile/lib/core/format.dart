// Display formatting only (docs/design/README.md §7). Nothing here computes a business value: every number
// comes from the API and is turned into text. Integer arithmetic only, so large paise values stay exact.

import 'package:intl/intl.dart';

import '../api/models/common.dart';

/// How to show paise in [formatRupees].
enum PaiseDisplay {
  /// Show `.50` only when the amount has paise.
  auto,

  /// Always show two decimals.
  always,

  /// Never show paise (rounds nothing: paise are simply not shown).
  never,
}

/// India uses a fixed UTC+05:30 offset with no daylight saving. Delivery windows and order times are shown in
/// IST; slot dates (`YYYY-MM-DD`) are shown as sent, without a zone shift.
const Duration istOffset = Duration(hours: 5, minutes: 30);

/// 124950 → ₹1,249.50 · 12499900 → ₹1,24,999 · -2500 → −₹25.
String formatRupees(int paise, {PaiseDisplay paiseDisplay = PaiseDisplay.auto}) {
  final negative = paise < 0;
  final abs = negative ? -paise : paise;
  final rupees = abs ~/ 100;
  final rest = abs % 100;
  final showPaise = switch (paiseDisplay) {
    PaiseDisplay.always => true,
    PaiseDisplay.never => false,
    PaiseDisplay.auto => rest != 0,
  };
  final text = '₹${groupIndian(rupees)}${showPaise ? '.${rest.toString().padLeft(2, '0')}' : ''}';
  return negative ? '−$text' : text;
}

/// Indian digit grouping: last three digits, then groups of two (1,24,99,999).
String groupIndian(int value) {
  final digits = value.abs().toString();
  if (digits.length <= 3) return value < 0 ? '-$digits' : digits;
  final last3 = digits.substring(digits.length - 3);
  var head = digits.substring(0, digits.length - 3);
  final groups = <String>[];
  while (head.length > 2) {
    groups.insert(0, head.substring(head.length - 2));
    head = head.substring(0, head.length - 2);
  }
  if (head.isNotEmpty) groups.insert(0, head);
  final grouped = '${groups.join(',')},$last3';
  return value < 0 ? '-$grouped' : grouped;
}

/// 500 → 500 g · 1000 → 1 kg · 1250 → 1.25 kg · 1500 → 1.5 kg.
String formatWeight(int grams) {
  if (grams.abs() < 1000) return '$grams g';
  final kg = grams ~/ 1000;
  final rest = (grams.abs() % 1000);
  if (rest == 0) return '$kg kg';
  // Gram precision, trailing zeros removed: 1250 → 1.25 kg, 1255 → 1.255 kg.
  var decimals = rest.toString().padLeft(3, '0');
  while (decimals.endsWith('0')) {
    decimals = decimals.substring(0, decimals.length - 1);
  }
  return '$kg.$decimals kg';
}

/// (450, 550) → 450–550 g · (900, 1100) → 900 g – 1.1 kg.
String formatWeightRange(int minGrams, int maxGrams) {
  if (minGrams < 1000 && maxGrams < 1000) return '$minGrams–$maxGrams g';
  return '${formatWeight(minGrams)} – ${formatWeight(maxGrams)}';
}

/// GRAM → weight · PIECE → "6 pieces" · PACK → "1 pack".
String formatQuantity(Quantity q) => switch (q.unit) {
  'GRAM' => formatWeight(q.value),
  'PIECE' => q.value == 1 ? '1 piece' : '${q.value} pieces',
  'PACK' => q.value == 1 ? '1 pack' : '${q.value} packs',
  _ => '${q.value} ${q.unit.toLowerCase()}',
};

/// Price unit suffix: KG → "/kg", PIECE → "/piece", PACK → "/pack".
String formatPerUnit(String unit) => switch (unit) {
  'KG' => '/kg',
  'GRAM' => '/g',
  'PIECE' => '/piece',
  'PACK' => '/pack',
  _ => '/${unit.toLowerCase()}',
};

/// 500 → 5% · 1250 → 12.5% · 7000 → 70%.
String formatPercentFromBp(int bp) {
  final whole = bp ~/ 100;
  final rest = bp.abs() % 100;
  if (rest == 0) return '$whole%';
  var decimals = rest.toString().padLeft(2, '0');
  if (decimals.endsWith('0')) decimals = decimals.substring(0, 1);
  return '$whole.$decimals%';
}

/// Yield note shown next to a preparation: 7000 → "You receive about 70% of the whole weight after cleaning."
String formatYieldNote(int yieldBp) => yieldBp >= 10000
    ? 'Sold as is, no cleaning loss.'
    : 'About ${formatPercentFromBp(yieldBp)} of the whole weight remains after cleaning; you pay for net weight only.';

DateTime toIst(DateTime instant) => instant.toUtc().add(istOffset);

final DateFormat _date = DateFormat('d MMM y', 'en_US');
final DateFormat _time = DateFormat('h:mm a', 'en_US');
final DateFormat _dayShort = DateFormat('EEE, d MMM', 'en_US');

/// UTC instant → "23 Oct 2026" (IST).
String formatDate(DateTime instant) => _date.format(toIst(instant));

/// UTC instant → "11:00 am" (IST).
String formatTime(DateTime instant) => _time.format(toIst(instant)).toLowerCase();

/// UTC instant → "23 Oct 2026, 11:00 am" (IST).
String formatDateTime(DateTime instant) => '${formatDate(instant)}, ${formatTime(instant)}';

/// UTC instant → "Fri, 23 Oct" (IST).
String formatDayShort(DateTime instant) => _dayShort.format(toIst(instant));

/// Two UTC instants → "2:00 pm – 4:00 pm" (IST).
String formatTimeRange(DateTime start, DateTime end) => '${formatTime(start)} – ${formatTime(end)}';

/// Slot window with its day: "Fri, 23 Oct · 2:00 pm – 4:00 pm".
String formatSlotWindow(DateTime start, DateTime end) => '${formatDayShort(start)} · ${formatTimeRange(start, end)}';

/// "2026-10-23" → "Fri, 23 Oct" (calendar date as sent, no zone shift).
String formatCalendarDate(String yyyyMmDd) {
  final parts = yyyyMmDd.split('-');
  if (parts.length != 3) return yyyyMmDd;
  final d = DateTime.utc(int.parse(parts[0]), int.parse(parts[1]), int.parse(parts[2]));
  return _dayShort.format(d);
}

/// OUT_FOR_DELIVERY → "Out for delivery" (fallback label for codes without UI copy).
String humanizeCode(String code) {
  if (code.isEmpty) return code;
  final lower = code.replaceAll('_', ' ').toLowerCase();
  return lower[0].toUpperCase() + lower.substring(1);
}

/// "+919876543210" → "+91 98765 43210" (display only).
String formatPhone(String e164) {
  if (e164.startsWith('+91') && e164.length == 13) {
    return '+91 ${e164.substring(3, 8)} ${e164.substring(8)}';
  }
  return e164;
}

/// Input check for an Indian mobile number typed without the country code. Returns E.164 or null.
/// This is form validation, not a business rule: the server validates again.
String? toIndianE164(String input) {
  final digits = input.replaceAll(RegExp(r'[^0-9]'), '');
  final local = digits.length == 12 && digits.startsWith('91') ? digits.substring(2) : digits;
  if (local.length != 10 || !RegExp(r'^[6-9]').hasMatch(local)) return null;
  return '+91$local';
}

/// Six-digit pincode input check (form validation only; serviceability comes from the server).
bool isPincodeShape(String input) => RegExp(r'^[1-9][0-9]{5}$').hasMatch(input.trim());
