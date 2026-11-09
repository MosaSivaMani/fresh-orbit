// Push notifications behind an interface (Ruling D28, D349). This file has no Firebase import: the app's screens,
// state and tests only see [PushService]. The Firebase Cloud Messaging implementation is in fcm_push_service.dart
// and is chosen in main.dart only when the build carries the Firebase values (D350); without them the app runs
// with [NoopPushService] and push is simply off.

import 'dart:async';

/// The phone's answer to "may this app show notifications?".
enum PushPermission {
  /// Allowed (Android 12 and older allow by default; iOS provisional counts too).
  granted,

  /// Refused, or not asked yet on a platform that cannot tell the two apart (Android 13+ reports "denied" until asked).
  denied,

  /// Not asked yet (iOS).
  notAsked,
}

/// A tap on a notification: the data the server sent with it (`templateCode`, `messageId`, and `orderId` or
/// `productSlug` when the message is about one, D351).
class PushOpen {
  const PushOpen(this.data);

  final Map<String, String> data;

  /// The screen to open, or null when the data names none ([pushRoute]).
  String? get route => pushRoute(data);
}

final _uuid = RegExp(r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$');
final _slug = RegExp(r'^[a-z0-9]+(?:-[a-z0-9]+)*$');

/// The app route for a notification's data (D351): an order message opens the order (`/orders/{orderId}`), a
/// back-in-stock message opens the product (`/product/{slug}`), any other message from the server opens the
/// notification inbox. Values are checked before they become a path, so a malformed payload never builds an
/// unexpected route; data that is not ours opens nothing.
String? pushRoute(Map<String, String> data) {
  final orderId = data['orderId'];
  if (orderId != null && _uuid.hasMatch(orderId)) return '/orders/$orderId';
  final slug = data['productSlug'];
  if (slug != null && _slug.hasMatch(slug)) return '/product/$slug';
  if ((data['templateCode'] ?? '').isNotEmpty) return '/account/notifications';
  return null;
}

abstract class PushService {
  /// False when this build has no push (no Firebase values, or Firebase failed to start): no permission is asked
  /// and Settings hides the "Notifications on this phone" row.
  bool get available;

  /// Start the provider and listen for taps. Never asks for permission. Safe to call more than once.
  Future<void> initialize();

  /// The current permission, without asking.
  Future<PushPermission> permission();

  /// Show the system's permission question (Android 13+ POST_NOTIFICATIONS, iOS alert); returns the answer.
  Future<PushPermission> requestPermission();

  /// The device's registration token, or null when push is unavailable.
  Future<String?> token();

  /// New tokens issued by the provider (re-registered with `PUT /me/device-tokens`).
  Stream<String> get tokenRefreshes;

  /// Notification taps while the app is running or in the background.
  Stream<PushOpen> get opened;

  /// The tap that launched the app from closed, once; null afterwards.
  Future<PushOpen?> takeInitialOpen();
}

/// Push off: builds without Firebase values, the web preview and tests.
class NoopPushService implements PushService {
  @override
  bool get available => false;

  @override
  Future<void> initialize() async {}

  @override
  Future<PushPermission> permission() async => PushPermission.denied;

  @override
  Future<PushPermission> requestPermission() async => PushPermission.denied;

  @override
  Future<String?> token() async => null;

  @override
  Stream<String> get tokenRefreshes => const Stream.empty();

  @override
  Stream<PushOpen> get opened => const Stream.empty();

  @override
  Future<PushOpen?> takeInitialOpen() async => null;
}
