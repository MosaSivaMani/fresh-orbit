// Push in the app (D349–D352): registering the device token with the server, asking for permission at the right
// moment, and opening the right screen when a notification is tapped. Works with any [PushService]; with the
// no-op service (no Firebase values) every step is skipped.
//
// - The token is registered (`PUT /me/device-tokens`) after a sign-in, at launch when already signed in, when the
//   provider issues a new token, and right after the customer allows notifications, but only while permission is
//   granted. Sign-out removes it (`DELETE /me/device-tokens/{token}`, SessionController.signOut).
// - Permission is asked once, on the order-placed screen after the first order (the customer has just made a
//   reason to want updates), and again only from Settings › "Notifications on this phone" (D352).
// - A tap opens `/orders/{orderId}` for an order message (D351); the open waits until the launch screen has
//   routed, so the splash never replaces it.

import 'dart:async';

import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/push_service.dart';
import 'core_providers.dart';
import 'session.dart';

final pushCoordinatorProvider = Provider<PushCoordinator>((ref) {
  final coordinator = PushCoordinator(ref);
  ref.onDispose(coordinator.dispose);
  return coordinator;
});

/// The phone's notification permission, for Settings. Invalidate after asking.
final pushPermissionProvider = FutureProvider<PushPermission>((ref) => ref.read(pushServiceProvider).permission());

/// Pages a tapped notification never opens over: the launch screens still decide where the app starts.
const _launchPages = {'/splash', '/onboarding'};

class PushCoordinator {
  PushCoordinator(this._ref);

  final Ref _ref;
  final List<StreamSubscription<Object?>> _subscriptions = [];
  String? _pendingRoute;
  GoRouter? _router;

  PushService get _push => _ref.read(pushServiceProvider);

  /// Start push for [router]: listen for taps and new tokens, open the tap that launched the app, and register the
  /// token when already signed in. Returns the function that stops listening.
  VoidCallback attach(GoRouter router) {
    _router = router;
    router.routerDelegate.addListener(_openPending);
    unawaited(_start());
    return () => router.routerDelegate.removeListener(_openPending);
  }

  Future<void> _start() async {
    final push = _push;
    if (!push.available) return;
    await push.initialize();
    _subscriptions
      ..add(push.tokenRefreshes.listen((token) => unawaited(_register(token))))
      ..add(push.opened.listen(open));
    final initial = await push.takeInitialOpen();
    if (initial != null) open(initial);
    if (_ref.read(sessionProvider).signedIn) await registerIfPermitted();
  }

  /// Opens the screen a tapped notification names; a notification that names none just brings the app forward.
  void open(PushOpen tap) {
    final route = tap.route;
    if (route == null) return;
    _pendingRoute = route;
    _openPending();
  }

  void _openPending() {
    final router = _router;
    final route = _pendingRoute;
    if (router == null || route == null || router.routerDelegate.currentConfiguration.isEmpty) return;
    if (_launchPages.contains(router.state.uri.path)) return;
    _pendingRoute = null;
    // A signed-out customer goes through the sign-in and comes back (router redirect with `from`).
    WidgetsBinding.instance.addPostFrameCallback((_) => router.go(route));
    WidgetsBinding.instance.ensureVisualUpdate();
  }

  /// Registers this device's token while signed in and permitted. Failures are quiet: push is not needed to order,
  /// and the next launch, sign-in or token refresh tries again.
  Future<void> registerIfPermitted() async {
    final push = _push;
    if (!push.available || !_ref.read(sessionProvider).signedIn) return;
    if (await push.permission() != PushPermission.granted) return;
    final token = await push.token();
    if (token != null) await _register(token);
  }

  Future<void> _register(String token) async {
    if (!_ref.read(sessionProvider).signedIn) return;
    try {
      await _ref.read(accountRepositoryProvider).registerDeviceToken(pushToken: token, platform: devicePlatform());
    } catch (_) {
      // Retried on the next launch, sign-in or token refresh.
    }
  }

  /// Removes this device's token from the account (sign-out). Never throws.
  Future<void> unregister() async {
    final push = _push;
    if (!push.available) return;
    try {
      final token = await push.token();
      if (token != null) await _ref.read(accountRepositoryProvider).unregisterDeviceToken(token);
    } catch (_) {
      // Signing out works offline; a token left behind is removed by the server when the provider reports it
      // unregistered, or moves to the next account that signs in on this phone.
    }
  }

  /// After an order is placed (D352): the first time only, ask for permission, then register the token.
  Future<void> askAfterOrder() async {
    final push = _push;
    final prefs = _ref.read(appPrefsProvider);
    if (!push.available || prefs.pushPermissionAsked) return;
    await prefs.setPushPermissionAsked();
    if (await push.permission() != PushPermission.granted) await push.requestPermission();
    await registerIfPermitted();
  }

  /// Settings › "Notifications on this phone": ask now, register when allowed, and return the answer.
  Future<PushPermission> askFromSettings() async {
    final push = _push;
    if (!push.available) return PushPermission.denied;
    await _ref.read(appPrefsProvider).setPushPermissionAsked();
    final answer = await push.requestPermission();
    await registerIfPermitted();
    _ref.invalidate(pushPermissionProvider);
    return answer;
  }

  void dispose() {
    for (final s in _subscriptions) {
      unawaited(s.cancel());
    }
    _subscriptions.clear();
  }
}
