// Sign-in edges (gap audit ME-3, ME-4, ME-5): resend errors on the code step, pasted numbers, Back on the code step.

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/api_error.dart';
import 'package:fresh_orbit_app/api/models/auth.dart';
import 'package:fresh_orbit_app/features/auth/login_screen.dart';

import '../support/harness.dart';

OtpChallenge _challenge() => OtpChallenge(
  challengeId: 'c1',
  expiresAt: DateTime.utc(2026, 10, 28, 10, 5),
  resendAfterSeconds: 0,
  codeLength: 6,
);

Future<TestDeps> _pumpAtCodeStep(WidgetTester tester, {bool welcome = true}) async {
  final deps = TestDeps();
  deps.auth.onRequest = (_) async => _challenge();
  await pumpScreen(tester, deps, LoginScreen(welcome: welcome));
  await tester.pumpAndSettle();
  await tester.enterText(find.byKey(const Key('phoneField')), '9876543210');
  await tester.tap(find.text('Send code'));
  await tester.pumpAndSettle();
  expect(find.byKey(const Key('otpField')), findsOneWidget);
  return deps;
}

void main() {
  testWidgets('a refused resend shows its error on the code step (ME-3)', (tester) async {
    final deps = await _pumpAtCodeStep(tester);
    deps.auth.onRequest = (_) async =>
        throw const ApiError(status: 429, code: 'OTP_RESEND_TOO_SOON', retryAfterSeconds: 20);

    await tester.tap(find.text('Resend code'));
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('otpField')), findsOneWidget);
    expect(tester.widget<OtpBoxes>(find.byType(OtpBoxes)).errorText, isNotNull);
  });

  testWidgets('an SMS provider failure on resend is shown too', (tester) async {
    final deps = await _pumpAtCodeStep(tester, welcome: false);
    deps.auth.onRequest = (_) async => throw const ApiError(status: 503, code: 'SMS_PROVIDER_UNAVAILABLE');

    await tester.tap(find.text('Resend code'));
    await tester.pumpAndSettle();

    expect(tester.widget<OtpBoxes>(find.byType(OtpBoxes)).errorText, isNotNull);
  });

  testWidgets('a pasted +91 number sends the code to the right phone (ME-4)', (tester) async {
    final deps = TestDeps();
    final sentTo = <String>[];
    deps.auth.onRequest = (phone) async {
      sentTo.add(phone);
      return _challenge();
    };
    await pumpScreen(tester, deps, const LoginScreen());
    await tester.pumpAndSettle();

    await tester.enterText(find.byKey(const Key('phoneField')), '+91 98765 43210');
    await tester.tap(find.text('Send code'));
    await tester.pumpAndSettle();

    expect(sentTo, ['+919876543210']);
  });

  testWidgets('Back on the first-run code step returns to the phone step instead of closing the app (ME-5)', (
    tester,
  ) async {
    await _pumpAtCodeStep(tester);

    // System Back asks the top navigator to pop; on the first run there is no page below the sign-in.
    final navigator = tester.state<NavigatorState>(find.byType(Navigator).last);
    expect(navigator.canPop(), isFalse);
    await navigator.maybePop();
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('phoneField')), findsOneWidget);
    expect(find.byKey(const Key('otpField')), findsNothing);
  });
}
