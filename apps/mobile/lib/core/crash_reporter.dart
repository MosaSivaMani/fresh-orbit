// Crash and error reporting behind an interface (D309, gap audit ME-7, MA-5; rule 13: free first, adapter, fake).
//
// - [NoopCrashReporter]: nothing (tests).
// - [LogCrashReporter]: the default. Writes the error to the device log only; nothing leaves the phone.
// - [SentryStoreCrashReporter]: sends the error to a Sentry-compatible server named by
//   `--dart-define=CRASH_REPORT_DSN=https://<key>@<host>/<project>`. The intended server is a self-hosted GlitchTip
//   (MIT licence, speaks the Sentry protocol) or self-hosted Sentry; hosted Sentry's free plan also accepts it. No SDK
//   dependency: one HTTP POST to the Sentry "store" endpoint. Off when the DSN is not set (never guessed, rule 7).
//
// Personal data never leaves the phone: [scrubPersonalData] removes phone numbers, one-time codes, tokens and
// e-mail addresses from every message and stack before it is written or sent.

import 'dart:async';
import 'dart:developer' as developer;
import 'dart:math';
import 'dart:ui' as ui;

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

/// `--dart-define=CRASH_REPORT_DSN=https://<public key>@<host>/<project id>`; empty = do not send reports.
const String crashReportDsnDefine = String.fromEnvironment('CRASH_REPORT_DSN');

/// `--dart-define=APP_ENVIRONMENT=Production` (the same names as the API's environments); tags the reports.
const String appEnvironmentDefine = String.fromEnvironment('APP_ENVIRONMENT', defaultValue: 'Development');

abstract class CrashReporter {
  /// Records [error]. Never throws: a failing reporter must not crash the app a second time.
  Future<void> report(Object error, StackTrace? stack, {required bool fatal, String? reason});
}

class NoopCrashReporter implements CrashReporter {
  const NoopCrashReporter();

  @override
  Future<void> report(Object error, StackTrace? stack, {required bool fatal, String? reason}) async {}
}

/// Writes scrubbed errors to the device log (`adb logcat`, Xcode console).
class LogCrashReporter implements CrashReporter {
  const LogCrashReporter();

  @override
  Future<void> report(Object error, StackTrace? stack, {required bool fatal, String? reason}) async {
    developer.log(
      scrubPersonalData('${reason == null ? '' : '$reason: '}$error'),
      name: 'fresh_orbit.crash',
      level: fatal ? 1200 : 1000,
      stackTrace: stack == null ? null : StackTrace.fromString(scrubPersonalData(stack.toString())),
    );
  }
}

/// A parsed Sentry DSN, or null when [dsn] is empty or not one.
class SentryDsn {
  const SentryDsn({required this.storeUri, required this.publicKey});

  final Uri storeUri;
  final String publicKey;

  static SentryDsn? tryParse(String dsn) {
    final uri = Uri.tryParse(dsn.trim());
    if (uri == null || uri.scheme != 'https' || uri.host.isEmpty || uri.userInfo.isEmpty) return null;
    final segments = uri.pathSegments.where((s) => s.isNotEmpty).toList();
    if (segments.isEmpty) return null;
    final project = segments.last;
    final prefix = segments.sublist(0, segments.length - 1);
    final store = uri.replace(userInfo: '', pathSegments: [...prefix, 'api', project, 'store', '']);
    return SentryDsn(storeUri: store, publicKey: uri.userInfo.split(':').first);
  }
}

/// Sends errors to a Sentry-compatible server (GlitchTip). Also logs them like [LogCrashReporter].
class SentryStoreCrashReporter implements CrashReporter {
  SentryStoreCrashReporter(this.dsn, {Dio? dio, this.release = 'fresh-orbit-app', this.environment = appEnvironmentDefine})
    : _dio = dio ?? Dio(BaseOptions(connectTimeout: const Duration(seconds: 5), sendTimeout: const Duration(seconds: 5)));

  final SentryDsn dsn;
  final String release;
  final String environment;
  final Dio _dio;
  final _random = Random.secure();

  @override
  Future<void> report(Object error, StackTrace? stack, {required bool fatal, String? reason}) async {
    await const LogCrashReporter().report(error, stack, fatal: fatal, reason: reason);
    try {
      await _dio.postUri<Object?>(
        dsn.storeUri,
        data: sentryEvent(error, stack, fatal: fatal, reason: reason, eventId: _eventId()),
        options: Options(
          headers: {
            'X-Sentry-Auth': 'Sentry sentry_version=7, sentry_key=${dsn.publicKey}, sentry_client=fresh-orbit-app/1',
          },
        ),
      );
    } catch (_) {
      // Offline or the server is down: the error is in the device log; never crash while reporting a crash.
    }
  }

  /// The event body (public for tests): scrubbed message and stack, no user, no device identifiers.
  Map<String, Object?> sentryEvent(
    Object error,
    StackTrace? stack, {
    required bool fatal,
    String? reason,
    required String eventId,
  }) => {
    'event_id': eventId,
    'timestamp': DateTime.now().toUtc().toIso8601String(),
    'platform': 'other',
    'level': fatal ? 'fatal' : 'error',
    'logger': 'fresh_orbit.crash',
    'release': release,
    'environment': environment,
    'tags': {'os': defaultTargetPlatform.name},
    'message': {'formatted': scrubPersonalData('${reason == null ? '' : '$reason: '}${error.runtimeType}: $error')},
    'extra': {if (stack != null) 'stack': scrubPersonalData(stack.toString())},
  };

  String _eventId() => List.generate(32, (_) => _random.nextInt(16).toRadixString(16)).join();
}

/// The reporter for this build: [SentryStoreCrashReporter] when `CRASH_REPORT_DSN` is a valid DSN, else
/// [LogCrashReporter].
CrashReporter crashReporterFromDefines([String dsn = crashReportDsnDefine]) {
  final parsed = SentryDsn.tryParse(dsn);
  return parsed == null ? const LogCrashReporter() : SentryStoreCrashReporter(parsed);
}

final _bearer = RegExp(r'Bearer\s+[A-Za-z0-9\-._~+/]+=*', caseSensitive: false);
final _jwt = RegExp(r'eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]*');
final _secretField = RegExp(
  r'''("?(?:accessToken|refreshToken|cartToken|otp|phone|pushToken)"?\s*[:=]\s*)("[^"]*"|[^\s,}&]+)''',
  caseSensitive: false,
);
// A one-time code sent as `"code": "123456"` (error codes such as "OTP_EXPIRED" are kept: they help debugging).
final _otpCode = RegExp(r'''("?code"?\s*[:=]\s*"?)\d{4,8}''', caseSensitive: false);
final _phone = RegExp(r'(?<![\d.:])(?:\+?91[\s-]?)?0?[6-9]\d{4}[\s-]?\d{5}(?![\d.])');
final _email = RegExp(r'[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}');

/// Removes personal data and secrets from [text] (phone numbers, tokens, one-time codes, e-mail addresses).
String scrubPersonalData(String text) => text
    .replaceAll(_bearer, 'Bearer [removed]')
    .replaceAll(_jwt, '[token removed]')
    .replaceAllMapped(_secretField, (m) => '${m[1]}[removed]')
    .replaceAllMapped(_otpCode, (m) => '${m[1]}[removed]')
    .replaceAll(_phone, '[phone removed]')
    .replaceAll(_email, '[e-mail removed]');

/// Sends every uncaught Flutter framework and platform error to [reporter] (ME-6, MA-5). Framework errors are
/// still printed as before; an uncaught asynchronous error is reported and marked handled so the app keeps running.
void installCrashHandlers(CrashReporter reporter) {
  final previous = FlutterError.onError;
  FlutterError.onError = (details) {
    if (previous != null) {
      previous(details);
    } else {
      FlutterError.presentError(details);
    }
    unawaited(reporter.report(details.exception, details.stack, fatal: false, reason: details.context?.toString()));
  };
  ui.PlatformDispatcher.instance.onError =(error, stack) {
    unawaited(reporter.report(error, stack, fatal: true));
    return true;
  };
}
