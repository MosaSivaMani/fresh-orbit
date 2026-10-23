// Push notifications behind an interface (Ruling D28). Today the app ships [NoopPushService]: no Firebase
// project exists yet, so firebase_messaging is NOT a dependency. When the owner creates the Firebase project,
// add an `FcmPushService` implementing this interface (steps in docs/design/mobile.md §FCM) and switch the
// override in main.dart. Nothing else in the app changes.

import 'dart:async';

/// A tap on a notification: the app opens [route] (e.g. `/orders/{orderId}`).
class PushOpen {
  const PushOpen(this.route);
  final String route;
}

abstract class PushService {
  /// Ask for permission and start listening. Safe to call more than once.
  Future<void> initialize();

  /// Current FCM registration token, or null when push is unavailable or not permitted.
  Future<String?> token();

  /// New tokens issued by the provider (re-register with `PUT /me/device-tokens`).
  Stream<String> get tokenRefreshes;

  /// Notification taps that should open a screen.
  Stream<PushOpen> get opened;
}

class NoopPushService implements PushService {
  @override
  Future<void> initialize() async {}

  @override
  Future<String?> token() async => null;

  @override
  Stream<String> get tokenRefreshes => const Stream.empty();

  @override
  Stream<PushOpen> get opened => const Stream.empty();
}
