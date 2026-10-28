// "Tell us about you" (D290): after a sign-in whose account has no name, the details step opens over the page the
// sign-in lands on; saving the name (PATCH /me with the profile's version) returns there. A customer with a name
// never sees it. Server field errors show at the field. Back leaves the step without trapping.

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/api_error.dart';
import 'package:fresh_orbit_app/api/models/account.dart';
import 'package:fresh_orbit_app/api/models/auth.dart';
import 'package:fresh_orbit_app/features/auth/login_screen.dart';
import 'package:fresh_orbit_app/features/auth/profile_details_screen.dart';
import 'package:fresh_orbit_app/state/first_run.dart';
import 'package:fresh_orbit_app/state/session.dart';
import 'package:fresh_orbit_app/theme/app_theme.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../support/harness.dart';

VerifyOtpResult _verified({String? fullName}) => VerifyOtpResult.fromJson({
  'accessToken': 'a1',
  'accessTokenExpiresAt': '2026-10-29T10:15:00Z',
  'refreshToken': 'r1',
  'refreshTokenExpiresAt': '2026-11-28T10:00:00Z',
  'user': {'id': 'u1', 'phone': '+919876543210', 'fullName': fullName, 'isNewCustomer': fullName == null},
});

Profile _profile({String? fullName}) => Profile.fromJson({
  'customerId': 'cus1',
  'phone': '+919876543210',
  'fullName': fullName,
  'email': null,
  'marketingOptIn': false,
  'status': 'ACTIVE',
  'version': '7',
});

Widget _marker(GoRouterState state) => Scaffold(body: Text('route:${state.uri}'));

/// The real sign-in and details screens in a router whose other pages are markers, with the details step
/// attached as the app's router attaches it.
Future<(TestDeps, GoRouter, ProviderContainer)> _pump(
  WidgetTester tester, {
  required String initialLocation,
  String? fullName,
}) async {
  tester.view.devicePixelRatio = 3;
  tester.view.physicalSize = const Size(400 * 3, 2400 * 3);
  addTearDown(tester.view.reset);
  final deps = TestDeps();
  await deps.load();
  deps.auth.onRequest = (_) async => OtpChallenge(
    challengeId: 'c1',
    expiresAt: DateTime.utc(2026, 10, 29, 10, 5),
    resendAfterSeconds: 30,
    codeLength: 6,
  );
  deps.auth.onVerify = (_, _) async => _verified(fullName: fullName);
  deps.account.me = _profile(fullName: fullName);
  final container = deps.container();
  final router = GoRouter(
    initialLocation: initialLocation,
    routes: [
      for (final p in ['/home', '/cart', '/checkout', '/location']) GoRoute(path: p, builder: (_, s) => _marker(s)),
      GoRoute(
        path: '/login',
        builder: (_, state) => LoginScreen(
          from: state.uri.queryParameters['from'],
          welcome: state.uri.queryParameters['welcome'] == '1',
        ),
      ),
      GoRoute(path: aboutYouPath, builder: (_, _) => const ProfileDetailsScreen()),
    ],
  );
  addTearDown(
    askAboutYouAfterSignIn(
      router,
      isDue: () => container.read(aboutYouDueProvider),
      onAsked: () => container.read(aboutYouDueProvider.notifier).set(false),
    ),
  );
  await tester.pumpWidget(
    UncontrolledProviderScope(
      container: container,
      child: MaterialApp.router(theme: AppTheme.light(), routerConfig: router),
    ),
  );
  await tester.pumpAndSettle();
  return (deps, router, container);
}

Future<void> _signIn(WidgetTester tester) async {
  await tester.enterText(find.byKey(const Key('phoneField')), '9876543210');
  await tester.tap(find.text('Send code'));
  await tester.pumpAndSettle();
  await tester.enterText(find.byKey(const Key('otpField')), '123456');
  await tester.tap(find.text('Verify'));
  await tester.pumpAndSettle();
}

final _nameField = find.byKey(const Key('aboutYouName'));
final _emailField = find.byKey(const Key('aboutYouEmail'));

void main() {
  testWidgets('a new account is asked for its details after the code, then goes where sign-in was going', (
    tester,
  ) async {
    final (deps, _, container) = await _pump(tester, initialLocation: '/login?welcome=1');
    deps.account.addressList = [];

    await _signIn(tester);

    expect(find.byType(ProfileDetailsScreen), findsOneWidget);
    expect(_nameField, findsOneWidget);
    expect(container.read(aboutYouDueProvider), isFalse);

    await tester.enterText(_nameField, '  Asha Rao  ');
    await tester.enterText(_emailField, 'asha@example.com');
    await tester.tap(find.text('Continue'));
    await tester.pumpAndSettle();

    expect(deps.account.profileUpdates, hasLength(1));
    final sent = deps.account.profileUpdates.single;
    expect(sent.fullName, 'Asha Rao');
    expect(sent.email, 'asha@example.com');
    expect(sent.marketingOptIn, isNull);
    expect(sent.version, '7');
    // The first-run destination (D278: no saved address → the pincode screen).
    expect(find.byType(ProfileDetailsScreen), findsNothing);
    expect(find.text('route:/location'), findsOneWidget);
  });

  testWidgets('a customer with a name never sees the step', (tester) async {
    final (deps, _, _) = await _pump(tester, initialLocation: '/login?welcome=1', fullName: 'Asha Rao');
    deps.account.addressList = [];

    await _signIn(tester);

    expect(find.byType(ProfileDetailsScreen), findsNothing);
    expect(find.text('route:/location'), findsOneWidget);
    expect(deps.account.profileUpdates, isEmpty);
  });

  testWidgets('the name is required and an empty e-mail is not sent', (tester) async {
    final (deps, _, _) = await _pump(tester, initialLocation: '/login?welcome=1');
    deps.account.addressList = [];
    await _signIn(tester);

    await tester.enterText(_nameField, '   ');
    await tester.tap(find.text('Continue'));
    await tester.pumpAndSettle();
    expect(deps.account.profileUpdates, isEmpty);
    expect(find.byType(ProfileDetailsScreen), findsOneWidget);

    await tester.enterText(_nameField, 'Asha');
    await tester.tap(find.text('Continue'));
    await tester.pumpAndSettle();
    expect(deps.account.profileUpdates.single.email, isNull);
    expect(find.text('route:/location'), findsOneWidget);
  });

  testWidgets("the server's field error shows at its field and the step stays", (tester) async {
    final (deps, _, _) = await _pump(tester, initialLocation: '/login?welcome=1');
    deps.account.addressList = [];
    await _signIn(tester);
    const message = 'Server sentence for the email field.';
    deps.account.failUpdateProfile = const ApiError(
      status: 422,
      code: 'VALIDATION_FAILED',
      errors: {
        'email': [FieldError(code: 'EMAIL_INVALID', message: message)],
      },
    );

    await tester.enterText(_nameField, 'Asha');
    await tester.enterText(_emailField, 'not-an-email');
    await tester.tap(find.text('Continue'));
    await tester.pumpAndSettle();

    expect(find.byType(ProfileDetailsScreen), findsOneWidget);
    final emailBox = find.ancestor(of: _emailField, matching: find.byType(FreshTextField));
    expect(find.descendant(of: emailBox, matching: find.text(message)), findsOneWidget);
    expect(find.byKey(const Key('aboutYouError')), findsNothing);
  });

  testWidgets('sign-in from checkout asks the details over checkout and returns there', (tester) async {
    final (deps, router, _) = await _pump(tester, initialLocation: '/cart');
    unawaited(router.push('/login?from=/checkout'));
    await tester.pumpAndSettle();

    await _signIn(tester);
    expect(find.byType(ProfileDetailsScreen), findsOneWidget);

    await tester.enterText(_nameField, 'Asha Rao');
    await tester.tap(find.text('Continue'));
    await tester.pumpAndSettle();

    expect(deps.account.profileUpdates.single.fullName, 'Asha Rao');
    expect(find.text('route:/checkout'), findsOneWidget);
  });

  testWidgets('Back from the step returns to the page below and does not ask again', (tester) async {
    final (deps, router, container) = await _pump(tester, initialLocation: '/cart');
    unawaited(router.push('/login?from=/checkout'));
    await tester.pumpAndSettle();
    await _signIn(tester);
    expect(find.byType(ProfileDetailsScreen), findsOneWidget);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(find.byType(ProfileDetailsScreen), findsNothing);
    expect(find.text('route:/checkout'), findsOneWidget);
    expect(container.read(aboutYouDueProvider), isFalse);
    expect(deps.account.profileUpdates, isEmpty);
    expect(tester.takeException(), isNull);
  });
}
