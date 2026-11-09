// Push in the app (D349–D352), with a fake push service: no Firebase in tests. Covers the build's Firebase values
// (push off when missing, never a crash), the route a tapped notification opens, token registration after
// sign-in, on refresh and after permission, removal on sign-out, and when permission is asked.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/core/push_config.dart';
import 'package:fresh_orbit_app/core/push_service.dart';
import 'package:fresh_orbit_app/features/account/settings_screen.dart';
import 'package:fresh_orbit_app/features/checkout/order_success_screen.dart';
import 'package:fresh_orbit_app/state/core_providers.dart';
import 'package:fresh_orbit_app/state/push_state.dart';
import 'package:fresh_orbit_app/state/session.dart';
import 'package:fresh_orbit_app/theme/app_theme.dart';
import 'package:go_router/go_router.dart';

import '../support/fakes.dart';
import '../support/fixtures.dart';
import '../support/harness.dart';

const _orderId = '0192b5a0-7c1e-7d3a-9f10-2c4e5a6b7c8d';

const _complete = FirebaseValues(apiKey: 'key', appId: '1:2:android:3', messagingSenderId: '2', projectId: 'p');

Future<ProviderContainer> _container(TestDeps deps, PushService push) async {
  await deps.load();
  return ProviderContainer.test(
    overrides: [...deps.overrides, pushServiceProvider.overrideWithValue(push)],
    retry: (_, _) => null,
  );
}

void main() {
  group('Firebase values (D350)', () {
    PushService select(FirebaseValues values, List<String> log, {bool isWeb = false}) => selectPushService(
      values: values,
      isWeb: isWeb,
      firebase: (_) => FakePushService(),
      log: log.add,
    );

    test('without values push is off and the log says so once', () {
      final log = <String>[];
      final push = select(const FirebaseValues(apiKey: '', appId: '', messagingSenderId: '', projectId: ''), log);
      expect(push, isA<NoopPushService>());
      expect(push.available, isFalse);
      expect(log, hasLength(1));
    });

    test('part of the values keeps push off and names the missing ones', () {
      final log = <String>[];
      final push = select(const FirebaseValues(apiKey: 'key', appId: ' ', messagingSenderId: '2', projectId: ''), log);
      expect(push, isA<NoopPushService>());
      expect(log.single, contains('FIREBASE_APP_ID'));
      expect(log.single, contains('FIREBASE_PROJECT_ID'));
      expect(log.single, isNot(contains('FIREBASE_API_KEY')));
    });

    test('every value set starts Firebase, except in the web preview', () {
      final log = <String>[];
      expect(select(_complete, log), isA<FakePushService>());
      expect(log, isEmpty);
      expect(select(_complete, log, isWeb: true), isA<NoopPushService>());
    });

    test('the build without --dart-define values has push off', () {
      expect(FirebaseValues.fromDefines.isEmpty, isTrue);
    });

    test('the no-op service never asks and has no token', () async {
      final push = NoopPushService();
      expect(await push.requestPermission(), PushPermission.denied);
      expect(await push.token(), isNull);
      expect(await push.takeInitialOpen(), isNull);
    });
  });

  group('tapped notification route (D351)', () {
    test('an order message opens the order', () {
      expect(pushRoute({'templateCode': 'ORDER_PACKED', 'orderId': _orderId}), '/orders/$_orderId');
    });

    test('a back-in-stock message opens the product', () {
      expect(pushRoute({'templateCode': 'BACK_IN_STOCK', 'productSlug': 'seer-fish-steaks'}), '/product/seer-fish-steaks');
    });

    test('a malformed id never becomes a path; the inbox opens instead', () {
      expect(pushRoute({'templateCode': 'ORDER_PACKED', 'orderId': '../account'}), '/account/notifications');
      expect(pushRoute({'templateCode': 'BACK_IN_STOCK', 'productSlug': 'a/b'}), '/account/notifications');
    });

    test('data that is not ours opens nothing', () {
      expect(pushRoute(const {}), isNull);
      expect(const PushOpen({'foo': 'bar'}).route, isNull);
    });
  });

  group('token registration (D349)', () {
    test('signed in and permitted: the token is registered for this platform', () async {
      final deps = TestDeps(signedIn: true);
      final push = FakePushService(current: PushPermission.granted);
      final container = await _container(deps, push);

      await container.read(pushCoordinatorProvider).registerIfPermitted();

      expect(deps.account.registeredTokens, ['fcm-token-1@ANDROID']);
    });

    test('not permitted, signed out or push off: nothing is registered', () async {
      final signedIn = TestDeps(signedIn: true);
      final denied = await _container(signedIn, FakePushService());
      await denied.read(pushCoordinatorProvider).registerIfPermitted();
      expect(signedIn.account.registeredTokens, isEmpty);

      final signedOut = TestDeps();
      final guest = await _container(signedOut, FakePushService(current: PushPermission.granted));
      await guest.read(pushCoordinatorProvider).registerIfPermitted();
      expect(signedOut.account.registeredTokens, isEmpty);

      final off = TestDeps(signedIn: true);
      final noop = await _container(off, NoopPushService());
      await noop.read(pushCoordinatorProvider).registerIfPermitted();
      expect(off.account.registeredTokens, isEmpty);
    });

    test('sign-out removes this phone\'s token before the session ends', () async {
      final deps = TestDeps(signedIn: true);
      final container = await _container(deps, FakePushService(current: PushPermission.granted));

      await container.read(sessionProvider.notifier).signOut();

      expect(deps.account.unregisteredTokens, ['fcm-token-1']);
      expect(container.read(sessionProvider).signedIn, isFalse);
    });
  });

  group('permission (D352)', () {
    test('asked once after the first order, then the token is registered', () async {
      final deps = TestDeps(signedIn: true);
      final push = FakePushService();
      final container = await _container(deps, push);
      final coordinator = container.read(pushCoordinatorProvider);

      await coordinator.askAfterOrder();
      await coordinator.askAfterOrder();

      expect(push.permissionRequests, 1);
      expect(deps.prefs.pushPermissionAsked, isTrue);
      expect(deps.account.registeredTokens, ['fcm-token-1@ANDROID']);
    });

    test('a refusal after the order is not asked again; Settings can still ask', () async {
      final deps = TestDeps(signedIn: true);
      final push = FakePushService(answer: PushPermission.denied);
      final container = await _container(deps, push);
      final coordinator = container.read(pushCoordinatorProvider);

      await coordinator.askAfterOrder();
      await coordinator.askAfterOrder();
      expect(push.permissionRequests, 1);
      expect(deps.account.registeredTokens, isEmpty);

      push.answer = PushPermission.granted;
      expect(await coordinator.askFromSettings(), PushPermission.granted);
      expect(push.permissionRequests, 2);
      expect(deps.account.registeredTokens, ['fcm-token-1@ANDROID']);
    });

    test('without push nothing is asked and nothing is remembered', () async {
      final deps = TestDeps(signedIn: true);
      final container = await _container(deps, NoopPushService());

      await container.read(pushCoordinatorProvider).askAfterOrder();

      expect(deps.prefs.pushPermissionAsked, isFalse);
    });

    testWidgets('the order-placed screen asks once', (tester) async {
      final deps = TestDeps(pincode: '600041', signedIn: true);
      deps.orders.onOrder = (_) async => order();
      final push = FakePushService();
      await pumpScreen(
        tester,
        deps,
        const OrderSuccessScreen(orderId: 'o1'),
        otherPaths: ['/orders/o1'],
        overrides: [pushServiceProvider.overrideWithValue(push)],
      );
      await tester.pumpAndSettle();

      expect(push.permissionRequests, 1);
      expect(deps.prefs.pushPermissionAsked, isTrue);
    });

    testWidgets('Settings shows this phone\'s permission and turns it on', (tester) async {
      final deps = TestDeps(signedIn: true);
      final push = FakePushService();
      await pumpScreen(tester, deps, const SettingsScreen(), overrides: [pushServiceProvider.overrideWithValue(push)]);
      await tester.pumpAndSettle();

      expect(find.text('Notifications on this phone'), findsOneWidget);
      expect(find.text('Off'), findsOneWidget);

      await tester.tap(find.byKey(const Key('phonePushTurnOn')));
      await tester.pumpAndSettle();

      expect(push.permissionRequests, 1);
      expect(find.text('On'), findsOneWidget);
      expect(find.byKey(const Key('phonePushTurnOn')), findsNothing);
      expect(deps.account.registeredTokens, ['fcm-token-1@ANDROID']);
    });

    testWidgets('Settings hides the row when the build has no push', (tester) async {
      await pumpScreen(tester, TestDeps(signedIn: true), const SettingsScreen());
      await tester.pumpAndSettle();

      expect(find.text('Notifications on this phone'), findsNothing);
    });
  });

  group('taps and refreshes', () {
    Future<(GoRouter, ProviderContainer)> pumpRouter(
      WidgetTester tester,
      TestDeps deps,
      FakePushService push,
    ) async {
      final container = await _container(deps, push);
      final router = GoRouter(
        initialLocation: '/splash',
        routes: [
          for (final p in ['/splash', '/home', '/account/notifications'])
            GoRoute(path: p, builder: (_, state) => Scaffold(body: Text('route:${state.uri}'))),
          GoRoute(path: '/orders/:id', builder: (_, state) => Scaffold(body: Text('route:${state.uri}'))),
        ],
      );
      final detach = container.read(pushCoordinatorProvider).attach(router);
      addTearDown(detach);
      await tester.pumpWidget(
        UncontrolledProviderScope(
          container: container,
          child: MaterialApp.router(theme: AppTheme.light(), routerConfig: router),
        ),
      );
      await tester.pumpAndSettle();
      return (router, container);
    }

    testWidgets('a tap on an order message opens the order', (tester) async {
      final push = FakePushService(current: PushPermission.granted);
      final (router, _) = await pumpRouter(tester, TestDeps(signedIn: true), push);
      router.go('/home');
      await tester.pumpAndSettle();

      push.tap({'templateCode': 'ORDER_OUT_FOR_DELIVERY', 'orderId': _orderId, 'messageId': 'm1'});
      await tester.pumpAndSettle();

      expect(find.text('route:/orders/$_orderId'), findsOneWidget);
    });

    testWidgets('the tap that launched the app waits for the launch screen, then opens the order', (tester) async {
      final push = FakePushService(
        current: PushPermission.granted,
        initialOpen: const PushOpen({'templateCode': 'ORDER_PACKED', 'orderId': _orderId}),
      );
      final (router, _) = await pumpRouter(tester, TestDeps(signedIn: true), push);

      // Still on the launch screen: the order is not opened over it.
      expect(find.text('route:/splash'), findsOneWidget);

      router.go('/home');
      await tester.pumpAndSettle();

      expect(find.text('route:/orders/$_orderId'), findsOneWidget);
    });

    testWidgets('at launch a signed-in phone registers its token; a new token is registered too', (tester) async {
      final deps = TestDeps(signedIn: true);
      final push = FakePushService(current: PushPermission.granted);
      await pumpRouter(tester, deps, push);

      expect(push.initializeCalls, 1);
      expect(deps.account.registeredTokens, ['fcm-token-1@ANDROID']);

      push.refreshToken('fcm-token-2');
      await tester.pumpAndSettle();

      expect(deps.account.registeredTokens, ['fcm-token-1@ANDROID', 'fcm-token-2@ANDROID']);
    });

    testWidgets('a new token while signed out is not sent', (tester) async {
      final deps = TestDeps();
      final push = FakePushService(current: PushPermission.granted);
      await pumpRouter(tester, deps, push);

      push.refreshToken('fcm-token-2');
      await tester.pumpAndSettle();

      expect(deps.account.registeredTokens, isEmpty);
    });
  });
}
