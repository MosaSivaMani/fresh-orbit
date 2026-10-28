// The sign-in look (D281): coral header with the drawn coast, the ringed avatar on the card's edge, the +91
// phone field and the arrow button. It must fit a small phone (360 × 640) in every step without overflow.

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/models/auth.dart';
import 'package:fresh_orbit_app/features/auth/login_screen.dart';
import 'package:fresh_orbit_app/theme/app_theme.dart';
import 'package:fresh_theme/fresh_theme.dart';

import '../support/harness.dart';

Future<TestDeps> _pumpLogin(WidgetTester tester, {bool welcome = false}) async {
  final deps = TestDeps();
  deps.auth.onRequest = (_) async => OtpChallenge(
    challengeId: 'c1',
    expiresAt: DateTime.utc(2026, 10, 29, 10, 5),
    resendAfterSeconds: 30,
    codeLength: 6,
  );
  await pumpScreen(tester, deps, LoginScreen(welcome: welcome));
  // A small phone.
  tester.view.physicalSize = const Size(360 * 3, 640 * 3);
  await tester.pumpAndSettle();
  return deps;
}

/// Leaves the screen so the resend timer is cancelled before the test ends.
Future<void> _leave(WidgetTester tester) async {
  await tester.pumpWidget(const SizedBox());
}

void main() {
  testWidgets('the phone step shows the reference look at 360 x 640 without overflow', (tester) async {
    await _pumpLogin(tester);

    expect(tester.takeException(), isNull);
    expect(find.byType(FreshAuthHeader), findsOneWidget);
    expect(find.byType(FreshCoastScene), findsOneWidget);
    expect(find.byType(FreshAvatarBadge), findsOneWidget);
    expect(find.text('SIGN IN'), findsOneWidget);
    expect(find.text('Your mobile'), findsOneWidget);
    expect(find.text('number'), findsOneWidget);
    expect(find.text('+91'), findsOneWidget);
    expect(find.byKey(const Key('phoneField')), findsOneWidget);
    expect(find.text('Send code'), findsOneWidget);
    expect(find.byKey(const Key('skipSignIn')), findsNothing);
    // Opened as the only page: nothing to go back to, so no back button.
    expect(find.byTooltip('Back'), findsNothing);
  });

  testWidgets('a wrong number shows the error under the phone field', (tester) async {
    await _pumpLogin(tester);

    await tester.enterText(find.byKey(const Key('phoneField')), '12345');
    await tester.tap(find.text('Send code'));
    await tester.pumpAndSettle();

    expect(find.text('Enter a 10-digit mobile number.'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('the welcome step fits 360 x 640 and keeps the skip link', (tester) async {
    await _pumpLogin(tester, welcome: true);

    expect(tester.takeException(), isNull);
    expect(find.text('WELCOME'), findsOneWidget);
    expect(find.text('Sign in or create your account'), findsOneWidget);
    await tester.ensureVisible(find.byKey(const Key('skipSignIn')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('skipSignIn')).hitTestable(), findsOneWidget);
  });

  testWidgets('the code step uses the same shell and fits 360 x 640 with large text', (tester) async {
    tester.platformDispatcher.textScaleFactorTestValue = 1.3;
    addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
    await _pumpLogin(tester);

    await tester.enterText(find.byKey(const Key('phoneField')), '9876543210');
    await tester.ensureVisible(find.text('Send code'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Send code'));
    await tester.pumpAndSettle();

    expect(tester.takeException(), isNull);
    expect(find.text('VERIFY'), findsOneWidget);
    expect(find.text('Enter the'), findsOneWidget);
    expect(find.text('code'), findsOneWidget);
    expect(find.byKey(const Key('otpField')), findsOneWidget);
    expect(find.text('Verify'), findsOneWidget);
    expect(find.text('Change number'), findsOneWidget);
    expect(find.text('Resend in 30 s'), findsOneWidget);

    await tester.ensureVisible(find.text('Change number'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Change number'));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('phoneField')), findsOneWidget);
    await _leave(tester);
  });

  group('fresh_theme sign-in parts', () {
    Future<void> pumpPart(WidgetTester tester, Widget child) async {
      tester.view.devicePixelRatio = 3;
      tester.view.physicalSize = const Size(360 * 3, 640 * 3);
      addTearDown(tester.view.reset);
      await tester.pumpWidget(
        MaterialApp(theme: AppTheme.light(), home: Scaffold(body: child)),
      );
    }

    testWidgets('FreshAuthPage shows the back button only when it can go back', (tester) async {
      var backs = 0;
      await pumpPart(
        tester,
        FreshAuthPage(
          eyebrow: 'Sign in',
          scriptLine: 'Your mobile',
          brushLine: 'number',
          avatar: const ColoredBox(color: Colors.white),
          onBack: () => backs++,
          children: const [Text('body')],
        ),
      );

      await tester.tap(find.byTooltip('Back'));
      expect(backs, 1);
      expect(find.text('body'), findsOneWidget);
      expect(tester.takeException(), isNull);
    });

    testWidgets('FreshArrowButton calls back once and ignores taps while loading', (tester) async {
      var taps = 0;
      await pumpPart(tester, FreshArrowButton(label: 'Send code', onPressed: () => taps++));
      await tester.tap(find.text('Send code'));
      expect(taps, 1);
      expect(find.byIcon(Icons.arrow_forward_rounded), findsOneWidget);

      await pumpPart(tester, FreshArrowButton(label: 'Send code', loading: true, onPressed: () => taps++));
      await tester.tap(find.byType(FilledButton));
      expect(taps, 1);
      expect(find.byType(CircularProgressIndicator), findsOneWidget);
    });

    testWidgets('FreshPhoneField shows the India prefix and its error', (tester) async {
      final controller = TextEditingController();
      addTearDown(controller.dispose);
      await pumpPart(
        tester,
        FreshPhoneField(controller: controller, fieldKey: const Key('f'), errorText: 'Enter a 10-digit mobile number.'),
      );

      expect(find.byType(FreshIndiaFlag), findsOneWidget);
      expect(find.text('+91'), findsOneWidget);
      expect(find.text('Mobile number'), findsOneWidget);
      expect(find.text('Enter a 10-digit mobile number.'), findsOneWidget);
      await tester.enterText(find.byKey(const Key('f')), '9876543210');
      expect(controller.text, '9876543210');
    });
  });
}
