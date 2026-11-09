// Firebase Cloud Messaging behind [PushService] (D349, D350). The only file that imports Firebase. Firebase is
// started from the build's --dart-define values (push_config.dart), so no google-services.json and no Google
// Services Gradle plugin are needed. When Firebase fails to start (wrong values, no Google Play services), push is
// off for this run: one line in the log, never a crash.
//
// Notification messages (the server sends `notification` + `data`, FcmPushSender) are shown by the system while the
// app is in the background or closed; a tap arrives through [opened] or [takeInitialOpen]. While the app is open
// Android shows nothing; the screens refresh on their own (pull to refresh, reload on resume).
//
// Tokens: firebase_messaging 17 deprecates getToken/onTokenRefresh in favour of register()/onRegistered, which give a
// Firebase Installation ID (FID) as a "direct-send target". The server's FcmPushSender sends to FCM registration
// tokens (`message.token`, HTTP v1), so the app keeps the registration-token calls until the server can send to an
// FID (follow-up; D349).

import 'dart:async';

import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';

import 'push_config.dart';
import 'push_service.dart';

class FcmPushService implements PushService {
  FcmPushService(this._values, this._log);

  final FirebaseValues _values;
  final void Function(String message) _log;
  final _opened = StreamController<PushOpen>.broadcast();
  final _refreshes = StreamController<String>.broadcast();
  Future<void>? _starting;
  bool _started = false;
  bool _failed = false;
  RemoteMessage? _initial;

  @override
  bool get available => !_failed;

  @override
  Future<void> initialize() => _starting ??= _start();

  Future<void> _start() async {
    try {
      await Firebase.initializeApp(
        options: FirebaseOptions(
          apiKey: _values.apiKey,
          appId: _values.appId,
          messagingSenderId: _values.messagingSenderId,
          projectId: _values.projectId,
        ),
      );
      final messaging = FirebaseMessaging.instance;
      FirebaseMessaging.onMessageOpenedApp.listen((m) => _opened.add(_open(m)));
      // Registration tokens, not FIDs (note at the top).
      // ignore: deprecated_member_use
      messaging.onTokenRefresh.listen(_refreshes.add);
      _initial = await messaging.getInitialMessage();
      _started = true;
    } catch (e) {
      _failed = true;
      _log('Push notifications are off: Firebase did not start: $e');
    }
  }

  static PushOpen _open(RemoteMessage message) =>
      PushOpen({for (final e in message.data.entries) e.key: '${e.value}'});

  static PushPermission _map(AuthorizationStatus status) => switch (status) {
    AuthorizationStatus.authorized || AuthorizationStatus.provisional => PushPermission.granted,
    AuthorizationStatus.notDetermined => PushPermission.notAsked,
    _ => PushPermission.denied,
  };

  @override
  Future<PushPermission> permission() async {
    await initialize();
    if (!_started) return PushPermission.denied;
    try {
      return _map((await FirebaseMessaging.instance.getNotificationSettings()).authorizationStatus);
    } catch (_) {
      return PushPermission.denied;
    }
  }

  @override
  Future<PushPermission> requestPermission() async {
    await initialize();
    if (!_started) return PushPermission.denied;
    try {
      return _map((await FirebaseMessaging.instance.requestPermission()).authorizationStatus);
    } catch (_) {
      return PushPermission.denied;
    }
  }

  @override
  Future<String?> token() async {
    await initialize();
    if (!_started) return null;
    try {
      // Registration tokens, not FIDs (note at the top).
      // ignore: deprecated_member_use
      return await FirebaseMessaging.instance.getToken();
    } catch (_) {
      // No network or no Google Play services: tried again on the next sign-in, launch or token refresh.
      return null;
    }
  }

  @override
  Stream<String> get tokenRefreshes => _refreshes.stream;

  @override
  Stream<PushOpen> get opened => _opened.stream;

  @override
  Future<PushOpen?> takeInitialOpen() async {
    await initialize();
    final initial = _initial;
    _initial = null;
    return initial == null ? null : _open(initial);
  }
}
