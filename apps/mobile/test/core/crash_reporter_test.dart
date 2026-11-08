// Crash reporting (D309, gap audit ME-6, ME-7, MA-5): adapter choice, scrubbing, global handlers, keystore failure.

import 'package:flutter/foundation.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/token_store.dart';
import 'package:fresh_orbit_app/core/crash_reporter.dart';

class _RecordingReporter implements CrashReporter {
  final reports = <(Object, bool)>[];

  @override
  Future<void> report(Object error, StackTrace? stack, {required bool fatal, String? reason}) async =>
      reports.add((error, fatal));
}

class _BrokenSecrets implements SecretStore {
  final deleted = <String>[];

  @override
  Future<String?> read(String key) async => throw StateError('keystore key does not match');

  @override
  Future<void> write(String key, String value) async {}

  @override
  Future<void> delete(String key) async => deleted.add(key);
}

void main() {
  test('no DSN means the local log reporter; a DSN means the Sentry-compatible one', () {
    expect(crashReporterFromDefines(''), isA<LogCrashReporter>());
    expect(crashReporterFromDefines('not a dsn'), isA<LogCrashReporter>());
    expect(crashReporterFromDefines('http://key@glitchtip.test/7'), isA<LogCrashReporter>());
    expect(crashReporterFromDefines('https://abc123@glitchtip.test/7'), isA<SentryStoreCrashReporter>());
  });

  test('the DSN gives the store address and the public key', () {
    final dsn = SentryDsn.tryParse('https://abc123@errors.example.test/sub/42')!;
    expect(dsn.publicKey, 'abc123');
    expect(dsn.storeUri.toString(), 'https://errors.example.test/sub/api/42/store/');
  });

  test('personal data and secrets are removed, error codes and stack lines kept', () {
    const text =
        'Phone +91 98765 43210 and 9876543210, Bearer eyJabc.def.ghi, {"refreshToken":"r-1","code":"123456"} '
        'OTP_EXPIRED a@b.in at package:fresh_orbit_app/main.dart:123:45';
    final out = scrubPersonalData(text);
    expect(out, isNot(contains('98765')));
    expect(out, isNot(contains('9876543210')));
    expect(out, isNot(contains('eyJabc')));
    expect(out, isNot(contains('r-1')));
    expect(out, isNot(contains('123456')));
    expect(out, isNot(contains('a@b.in')));
    expect(out, contains('OTP_EXPIRED'));
    expect(out, contains('main.dart:123:45'));
  });

  test('the event sent has no personal data', () {
    final reporter = SentryStoreCrashReporter(SentryDsn.tryParse('https://k@errors.example.test/1')!);
    final event = reporter.sentryEvent(
      StateError('cart for +919876543210 failed'),
      StackTrace.current,
      fatal: true,
      eventId: '0' * 32,
    );
    expect(event['level'], 'fatal');
    expect(event.toString(), isNot(contains('9876543210')));
    expect(event.containsKey('user'), isFalse);
  });

  test('framework errors reach the reporter and are still presented', () {
    final previous = FlutterError.onError;
    final presented = <FlutterErrorDetails>[];
    FlutterError.onError = presented.add;
    addTearDown(() => FlutterError.onError = previous);
    final reporter = _RecordingReporter();

    installCrashHandlers(reporter);
    FlutterError.reportError(FlutterErrorDetails(exception: StateError('boom')));

    expect(presented, hasLength(1));
    expect(reporter.reports.single.$1, isA<StateError>());
    expect(reporter.reports.single.$2, isFalse);
  });

  test('an unreadable keystore starts signed out instead of blocking the launch (ME-6)', () async {
    final secrets = _BrokenSecrets();
    final store = TokenStore(secrets);

    await store.load();

    expect(store.hasSession, isFalse);
    expect(store.guestCartToken, isNull);
    expect(store.loadFailed, isTrue);
    expect(secrets.deleted, containsAll(['fo.refreshToken', 'fo.guestCartToken']));
  });
}
