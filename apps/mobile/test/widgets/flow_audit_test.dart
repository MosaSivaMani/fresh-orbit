// Fixes from the mobile flow audit (docs/research/mobile-flow-audit-2026-10.md): sign-in returns to where the
// customer was (D263), checkout edits an address in place (D264), the inbox follows the session (D268), account
// deletion refusals are shown (D267), the help page shows the store's legal details (D269), and a 429 says how
// long to wait.

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/api_error.dart';
import 'package:fresh_orbit_app/api/models/account.dart';
import 'package:fresh_orbit_app/api/models/catalog.dart';
import 'package:fresh_orbit_app/features/account/account_screens.dart';
import 'package:fresh_orbit_app/features/account/help_screen.dart';
import 'package:fresh_orbit_app/features/auth/login_screen.dart';
import 'package:fresh_orbit_app/features/checkout/checkout_screen.dart';
import 'package:fresh_orbit_app/state/account_state.dart';
import 'package:go_router/go_router.dart';

import '../support/fakes.dart';
import '../support/fixtures.dart';
import '../support/harness.dart';

/// A router with a home page, two pages and a stand-in login page whose button calls [leaveLogin].
GoRouter _router() => GoRouter(
  routes: [
    GoRoute(path: '/home', builder: (_, _) => const Scaffold(body: Text('page:/home'))),
    GoRoute(path: '/a', builder: (_, _) => const Scaffold(body: Text('page:/a'))),
    GoRoute(path: '/b', builder: (_, _) => const Scaffold(body: Text('page:/b'))),
    GoRoute(
      path: '/login',
      builder: (context, state) => Scaffold(
        body: TextButton(
          key: const Key('signedIn'),
          onPressed: () => leaveLogin(context, state.uri.queryParameters['from']),
          child: const Text('done'),
        ),
      ),
    ),
  ],
  initialLocation: '/a',
);

Future<GoRouter> _pumpRouter(WidgetTester tester) async {
  final router = _router();
  await tester.pumpWidget(MaterialApp.router(routerConfig: router));
  await tester.pumpAndSettle();
  return router;
}

/// The page the customer sees. A pushed page does not change the router's base URI (go_router keeps the URI of
/// the last `go`), so the test reads the page on top of the navigator; the pages below it are offstage.
String _location(WidgetTester tester) {
  final pages = tester.widgetList<Text>(find.textContaining('page:')).map((t) => t.data!).toList();
  expect(pages, hasLength(1), reason: 'exactly one page is on top');
  return pages.single.substring('page:'.length);
}

Profile _profile() => Profile.fromJson({
  'customerId': 'cus1',
  'phone': '+919876543210',
  'fullName': 'Sample Customer',
  'email': null,
  'marketingOptIn': false,
  'status': 'ACTIVE',
  'version': '7',
});

void main() {
  group('leaveLogin (D263)', () {
    testWidgets('a login pushed by a screen pops back to that screen', (tester) async {
      final router = await _pumpRouter(tester);
      unawaited(router.push('/login'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('signedIn')));
      await tester.pumpAndSettle();

      expect(_location(tester), '/a');
    });

    testWidgets('a redirect login replaces itself with the page that needed sign-in; Back keeps the page below', (
      tester,
    ) async {
      final router = await _pumpRouter(tester);
      unawaited(router.push('/login?from=/b'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('signedIn')));
      await tester.pumpAndSettle();
      expect(_location(tester), '/b');

      router.pop();
      await tester.pumpAndSettle();
      expect(_location(tester), '/a');
    });

    testWidgets('a login opened alone goes to the page it was asked for', (tester) async {
      final router = await _pumpRouter(tester);
      router.go('/login?from=/b');
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('signedIn')));
      await tester.pumpAndSettle();

      expect(_location(tester), '/b');
    });

    testWidgets('a login opened alone without a target goes home', (tester) async {
      final router = await _pumpRouter(tester);
      router.go('/login');
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('signedIn')));
      await tester.pumpAndSettle();

      expect(_location(tester), '/home');
    });
  });

  testWidgets('checkout edits an address in place over the checkout (D264)', (tester) async {
    final deps = TestDeps(pincode: '600041', signedIn: true);
    deps.account.addressList = [address()];
    deps.location.onSlots = (_) async => slots();
    deps.orders.onPreview = (_) async => preview();
    await pumpScreen(tester, deps, const CheckoutScreen(), otherPaths: ['/addresses/a1', '/addresses/new']);
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const ValueKey('editAddress-a1')));
    await tester.pumpAndSettle();

    expect(find.text('route:/addresses/a1'), findsOneWidget);
  });

  testWidgets('checkout adds an address with the full-screen form', (tester) async {
    final deps = TestDeps(pincode: '600041', signedIn: true);
    deps.account.addressList = [address()];
    deps.location.onSlots = (_) async => slots();
    deps.orders.onPreview = (_) async => preview();
    await pumpScreen(tester, deps, const CheckoutScreen(), otherPaths: ['/addresses/a1', '/addresses/new']);
    await tester.pumpAndSettle();

    await tester.ensureVisible(find.byKey(const Key('addAddress')));
    await tester.tap(find.byKey(const Key('addAddress')));
    await tester.pumpAndSettle();

    expect(find.text('route:/addresses/new'), findsOneWidget);
  });

  for (final signedIn in [false, true]) {
    test('the inbox is read only for a signed-in customer (signed in: $signedIn, D268)', () async {
      final deps = TestDeps(pincode: '600041', signedIn: signedIn);
      await deps.load();
      final container = deps.container();
      addTearDown(container.dispose);
      container.listen(inboxProvider, (_, _) {});

      final inbox = await container.read(inboxProvider.future);

      expect(inbox.items, isEmpty);
      expect(deps.account.notificationReads, signedIn ? 1 : 0);
    });
  }

  testWidgets('a refused account deletion (422 ACCOUNT_HAS_OPEN_MATTERS) is shown (D267)', (tester) async {
    final deps = TestDeps(pincode: '600041', signedIn: true);
    deps.account.me = _profile();
    deps.account.failDelete = ApiError.fromProblem(422, {
      'code': 'ACCOUNT_HAS_OPEN_MATTERS',
      'errors': {
        'account': [
          {'code': 'OPEN_COMPLAINT', 'message': 'Close your open complaint first.'},
        ],
      },
    });
    await pumpScreen(tester, deps, const ProfileScreen());
    await tester.pumpAndSettle();

    await tester.ensureVisible(find.byKey(const Key('deleteAccount')));
    await tester.tap(find.byKey(const Key('deleteAccount')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('confirmDelete')));
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('profileError')), findsOneWidget);
    expect(find.text('route:/home'), findsNothing);
  });

  testWidgets('help shows the legal details the owner configured (D269)', (tester) async {
    final deps = TestDeps(pincode: '600041');
    deps.catalog.profile = const BusinessProfile(
      legalName: 'Sample Legal Name',
      supportPhone: '+919999900101',
      address: 'Sample address',
      gstin: '37AAAAA0000A1Z1',
    );
    await pumpScreen(tester, deps, const HelpScreen());
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('helpStoreDetails')), findsOneWidget);
    expect(find.text('37AAAAA0000A1Z1'), findsOneWidget);
  });

  testWidgets('help leaves the legal section out when nothing is configured', (tester) async {
    final deps = TestDeps(pincode: '600041');
    await pumpScreen(tester, deps, const HelpScreen());
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('helpStoreDetails')), findsNothing);
  });

  test('a 429 without a sentence carries the wait from Retry-After, not the bare title', () {
    final e = ApiError.fromProblem(429, {
      'code': 'RATE_LIMITED',
      'title': 'Too Many Requests',
      'retryAfterSeconds': 30,
    });

    expect(e.code, 'RATE_LIMITED');
    expect(e.message, isNot('Too Many Requests'));
    expect(e.message, contains('30'));
  });
}
