// First run (D277, D278): onboarding → "Sign in or create your account" → home or the Location screen.

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/models/auth.dart';
import 'package:fresh_orbit_app/features/auth/login_screen.dart';
import 'package:fresh_orbit_app/features/onboarding/onboarding_screen.dart';

import '../support/fakes.dart';
import '../support/fixtures.dart';
import '../support/harness.dart';

VerifyOtpResult _verified() => VerifyOtpResult.fromJson({
  'accessToken': 'a1',
  'accessTokenExpiresAt': '2026-10-28T10:15:00Z',
  'refreshToken': 'r1',
  'refreshTokenExpiresAt': '2026-11-27T10:00:00Z',
  'user': {'id': 'u1', 'phone': '+919876543210', 'fullName': null, 'isNewCustomer': false},
});

/// The welcome login with OTP request and verify answered by the fakes.
Future<TestDeps> _pumpWelcome(WidgetTester tester) async {
  final deps = TestDeps();
  deps.prefs.onboardingDone = true;
  deps.auth.onRequest = (_) async => OtpChallenge(
    challengeId: 'c1',
    expiresAt: DateTime.utc(2026, 10, 28, 10, 5),
    resendAfterSeconds: 30,
    codeLength: 6,
  );
  deps.auth.onVerify = (_, _) async => _verified();
  await pumpScreen(tester, deps, const LoginScreen(welcome: true));
  await tester.pumpAndSettle();
  return deps;
}

Future<void> _signIn(WidgetTester tester) async {
  await tester.enterText(find.byKey(const Key('phoneField')), '9876543210');
  await tester.tap(find.text('Send code'));
  await tester.pumpAndSettle();
  await tester.enterText(find.byKey(const Key('otpField')), '123456');
  await tester.tap(find.text('Verify'));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('finishing onboarding opens the first-run sign-in', (tester) async {
    final deps = TestDeps();
    deps.prefs.onboardingDone = false;
    await pumpScreen(tester, deps, const OnboardingScreen());
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('onboardingSkip')).first);
    await tester.pumpAndSettle();

    expect(deps.prefs.onboardingDone, isTrue);
    expect(find.text('route:/login?welcome=1'), findsOneWidget);
  });

  testWidgets('once the sign-in step was shown, onboarding goes to the Location screen', (tester) async {
    final deps = TestDeps();
    deps.prefs
      ..onboardingDone = false
      ..welcomeSignInShown = true;
    await pumpScreen(tester, deps, const OnboardingScreen());
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('onboardingSkip')).first);
    await tester.pumpAndSettle();

    expect(find.text('route:/location'), findsOneWidget);
  });

  testWidgets('the welcome login asks to sign in or create an account and is remembered as shown', (tester) async {
    final deps = await _pumpWelcome(tester);

    expect(find.text('Sign in or create your account'), findsOneWidget);
    expect(find.byKey(const Key('phoneField')), findsOneWidget);
    expect(find.byKey(const Key('skipSignIn')), findsOneWidget);
    expect(deps.prefs.welcomeSignInShown, isTrue);
    expect(tester.takeException(), isNull);
  });

  testWidgets('"Skip for now, just browse" goes to the Location screen', (tester) async {
    final deps = await _pumpWelcome(tester);

    await tester.tap(find.text('Skip for now, just browse'));
    await tester.pumpAndSettle();

    expect(find.text('route:/location'), findsOneWidget);
    expect(deps.prefs.pincode, isNull);
  });

  testWidgets('signed in with a saved serviceable address goes home with its pincode', (tester) async {
    final deps = await _pumpWelcome(tester);
    deps.account.addressList = [address(id: 'a2', pincode: '600099', isDefault: false), address()];
    deps.location.onServiceability = (pincode) async =>
        pincode == '600041' ? serviceable() : notServiceable(pincode);

    await _signIn(tester);

    expect(find.text('route:/home'), findsOneWidget);
    expect(deps.prefs.pincode, '600041');
    // The default address is asked first.
    expect(deps.location.checked.first, '600041');
  });

  testWidgets('signed in without a saved address goes to the Location screen', (tester) async {
    final deps = await _pumpWelcome(tester);
    deps.account.addressList = [];

    await _signIn(tester);

    expect(find.text('route:/location'), findsOneWidget);
    expect(deps.prefs.pincode, isNull);
    expect(deps.location.checked, isEmpty);
  });

  testWidgets('signed in with only unserviceable addresses goes to the Location screen', (tester) async {
    final deps = await _pumpWelcome(tester);
    deps.account.addressList = [address(pincode: '110001')];
    deps.location.onServiceability = (pincode) async => notServiceable(pincode);

    await _signIn(tester);

    expect(find.text('route:/location'), findsOneWidget);
    expect(deps.prefs.pincode, isNull);
    expect(deps.location.checked, ['110001']);
  });
}
