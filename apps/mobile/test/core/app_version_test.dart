// L-80 (D376): the store build takes its version name from pubspec.yaml and its build number from the release
// workflow (`--build-number`), so the name must be a release version (1.0.0 or later) with a numeric build suffix.

import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

void main() {
  test('the version name is a release version with a build number', () {
    final pubspec = File('pubspec.yaml').readAsLinesSync();
    final line = pubspec.firstWhere((l) => l.startsWith('version:'));
    final match = RegExp(r'^version: (\d+)\.(\d+)\.(\d+)\+(\d+)$').firstMatch(line.trim());

    expect(match, isNotNull, reason: line);
    expect(int.parse(match!.group(1)!), greaterThanOrEqualTo(1));
  });
}
