import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/models/common.dart';
import 'package:fresh_orbit_app/core/format.dart';

void main() {
  group('formatRupees', () {
    test('uses Indian grouping and shows paise only when present', () {
      expect(formatRupees(124950), '₹1,249.50');
      expect(formatRupees(12499900), '₹1,24,999');
      expect(formatRupees(1234567890000), '₹12,34,56,78,900');
      expect(formatRupees(0), '₹0');
      expect(formatRupees(5), '₹0.05');
    });

    test('respects the paise display option', () {
      expect(formatRupees(129900, paiseDisplay: PaiseDisplay.always), '₹1,299.00');
      expect(formatRupees(129950, paiseDisplay: PaiseDisplay.never), '₹1,299');
    });

    test('shows negative amounts with a minus sign', () {
      expect(formatRupees(-10000), '−₹100');
    });
  });

  test('groupIndian', () {
    expect(groupIndian(999), '999');
    expect(groupIndian(1000), '1,000');
    expect(groupIndian(100000), '1,00,000');
    expect(groupIndian(10000000), '1,00,00,000');
  });

  test('formatWeight and ranges', () {
    expect(formatWeight(500), '500 g');
    expect(formatWeight(1000), '1 kg');
    expect(formatWeight(1250), '1.25 kg');
    expect(formatWeight(1500), '1.5 kg');
    expect(formatWeight(1255), '1.255 kg');
    expect(formatWeightRange(450, 550), '450–550 g');
    expect(formatWeightRange(900, 1100), '900 g – 1.1 kg');
  });

  test('formatQuantity by unit', () {
    expect(formatQuantity(const Quantity(750, 'GRAM')), '750 g');
    expect(formatQuantity(const Quantity(1, 'PIECE')), '1 piece');
    expect(formatQuantity(const Quantity(6, 'PIECE')), '6 pieces');
    expect(formatQuantity(const Quantity(2, 'PACK')), '2 packs');
  });

  test('formatPercentFromBp and per-unit suffix', () {
    expect(formatPercentFromBp(500), '5%');
    expect(formatPercentFromBp(1250), '12.5%');
    expect(formatPercentFromBp(7000), '70%');
    expect(formatPercentFromBp(5525), '55.25%');
    expect(formatPerUnit('KG'), '/kg');
    expect(formatPerUnit('PIECE'), '/piece');
  });

  group('dates in IST', () {
    final instant = DateTime.utc(2026, 10, 23, 5, 30); // 11:00 IST

    test('date, time and both', () {
      expect(formatDate(instant), '23 Oct 2026');
      expect(formatTime(instant), '11:00 am');
      expect(formatDateTime(instant), '23 Oct 2026, 11:00 am');
      expect(formatDayShort(instant), 'Fri, 23 Oct');
    });

    test('crosses midnight correctly', () {
      expect(formatDate(DateTime.utc(2026, 10, 23, 19, 0)), '24 Oct 2026'); // 00:30 IST next day
    });

    test('time range and calendar date without zone shift', () {
      expect(
        formatTimeRange(DateTime.utc(2026, 10, 23, 8, 30), DateTime.utc(2026, 10, 23, 10, 30)),
        '2:00 pm – 4:00 pm',
      );
      expect(formatCalendarDate('2026-10-23'), 'Fri, 23 Oct');
    });
  });

  test('humanizeCode, phone and pincode input checks', () {
    expect(humanizeCode('OUT_FOR_DELIVERY'), 'Out for delivery');
    expect(formatPhone('+919876543210'), '+91 98765 43210');
    expect(toIndianE164('98765 43210'), '+919876543210');
    expect(toIndianE164('+91 98765 43210'), '+919876543210');
    expect(toIndianE164('12345'), isNull);
    expect(toIndianE164('5876543210'), isNull);
    expect(isPincodeShape('600041'), isTrue);
    expect(isPincodeShape('060041'), isFalse);
    expect(isPincodeShape('60004'), isFalse);
  });

  group('pasted and autofilled Indian numbers (ME-4)', () {
    test('strip +91, a 12-digit 91 and the trunk 0', () {
      expect(indianMobileDigits('+91 98765 43210'), '9876543210');
      expect(indianMobileDigits('919876543210'), '9876543210');
      expect(indianMobileDigits('09876543210'), '9876543210');
      expect(indianMobileDigits('+91-91234-56789'), '9123456789');
      expect(toIndianE164('09876543210'), '+919876543210');
      expect(toIndianE164('919876543210'), '+919876543210');
    });

    test('a 10-digit number that starts with 91 is kept', () {
      expect(indianMobileDigits('9123456789'), '9123456789');
      expect(toIndianE164('9123456789'), '+919123456789');
    });

    test('the field formatter turns a paste into the right 10 digits', () {
      const formatter = IndianMobileInputFormatter();
      for (final paste in ['+91 98765 43210', '09876543210', '919876543210']) {
        final out = formatter.formatEditUpdate(TextEditingValue.empty, TextEditingValue(text: paste));
        expect(out.text, '9876543210', reason: paste);
        expect(out.selection.baseOffset, 10);
      }
      final typed = formatter.formatEditUpdate(
        const TextEditingValue(text: '9876543210'),
        const TextEditingValue(text: '98765432101'),
      );
      expect(typed.text, '9876543210');
    });
  });
}
