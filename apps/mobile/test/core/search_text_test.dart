import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/core/search_text.dart';

void main() {
  test('accepts 2 to 60 characters after trimming with a letter or digit (CAT-R12)', () {
    expect(isSearchable('seer'), isTrue);
    expect(isSearchable('  ab  '), isTrue);
    expect(isSearchable('42'), isTrue);
    expect(isSearchable('வஞ்சிரம்'), isTrue, reason: 'local names in any script');
    expect(isSearchable('a' * 60), isTrue);
  });

  test('refuses what the server refuses with VALIDATION_FAILED on q', () {
    expect(isSearchable(''), isFalse);
    expect(isSearchable('a'), isFalse);
    expect(isSearchable('  a  '), isFalse);
    expect(isSearchable('a' * 61), isFalse);
    expect(isSearchable('%%'), isFalse);
    expect(isSearchable('- -'), isFalse);
  });
}
