// The sign-in look (D281): coral header with the drawn coast, the ringed avatar on the card's edge, the +91
// phone field and the arrow button. It must fit a small phone (360 × 640) in every step without overflow.
// The owner's pictures fill the avatar (LOGIN_OTP, D285) and the header (SIGN_IN_COAST, D286); the drawn
// portrait and coast are only their fallbacks.

import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/models/auth.dart';
import 'package:fresh_orbit_app/features/auth/login_screen.dart';
import 'package:fresh_orbit_app/illustrations/illustration.dart';
import 'package:fresh_orbit_app/illustrations/portrait_painter.dart';
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

const _avatarAsset = 'assets/illustrations/login_otp.png';
const _coastAsset = 'assets/illustrations/sign_in_coast.png';

/// Width and height from a PNG file's IHDR chunk.
(int, int) _pngSize(List<int> bytes) {
  int read(int at) => (bytes[at] << 24) | (bytes[at + 1] << 16) | (bytes[at + 2] << 8) | bytes[at + 3];
  return (read(16), read(20));
}

void main() {
  setUp(() => IllustrationAssets.debugSetAvailable(const {_avatarAsset, _coastAsset}));
  tearDown(() => IllustrationAssets.debugSetAvailable(null));

  test('the owner\'s avatar and coast pictures are bundled as PNGs small enough for a phone', () {
    for (final (path, width, height) in [(_avatarAsset, 512, 512), (_coastAsset, 800, 533)]) {
      final file = File(path);
      expect(file.existsSync(), isTrue, reason: path);
      final bytes = file.readAsBytesSync();
      expect(bytes.sublist(1, 4), 'PNG'.codeUnits, reason: path);
      expect(_pngSize(bytes), (width, height), reason: path);
      expect(bytes.length, lessThan(300 * 1024), reason: path);
    }
    expect(IllustrationSlot.loginOtp.assetPath, _avatarAsset);
    expect(IllustrationSlot.signInCoast.assetPath, _coastAsset);
  });

  testWidgets('the phone step shows the reference look at 360 x 640 without overflow', (tester) async {
    await _pumpLogin(tester);

    expect(tester.takeException(), isNull);
    expect(find.byType(FreshAuthHeader), findsOneWidget);
    expect(find.byType(FreshAvatarBadge), findsOneWidget);
    // The owner's pictures, not the drawn stand-ins.
    final avatar = find.descendant(
      of: find.byType(FreshAvatarBadge),
      matching: find.byKey(const ValueKey('illustration-image-LOGIN_OTP')),
    );
    expect(avatar, findsOneWidget);
    expect((tester.widget<Image>(avatar).image as AssetImage).assetName, _avatarAsset);
    expect(find.byKey(const ValueKey('illustration-image-SIGN_IN_COAST')), findsOneWidget);
    expect(find.byType(AmmammaPortrait), findsNothing);
    expect(find.byType(FreshCoastScene), findsNothing);
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

  testWidgets('the coast sits at the bottom right with its water under the card edge', (tester) async {
    await _pumpLogin(tester);

    final header = tester.getRect(find.byType(FreshAuthHeader));
    final coast = tester.getRect(find.byKey(const ValueKey('illustration-image-SIGN_IN_COAST')));
    final expected = FreshAuthHeader.pictureSizeFor(const Size(360, 640));
    expect(coast.size, expected);
    expect(coast.right, header.right);
    // The card's top edge is the avatar's centre line; the picture's bottom part runs under it.
    final cardTop = tester.getCenter(find.byType(FreshAvatarBadge)).dy;
    expect(coast.bottom - cardTop, closeTo(expected.height * FreshAuthHeader.pictureUnderCard, 0.5));
  });

  testWidgets('without the bundled pictures the drawn portrait and coast stand in', (tester) async {
    IllustrationAssets.debugSetAvailable(const {});
    await _pumpLogin(tester);

    expect(tester.takeException(), isNull);
    expect(find.byType(AmmammaPortrait), findsOneWidget);
    expect(find.byType(FreshCoastScene), findsOneWidget);
    expect(find.byKey(const ValueKey('illustration-image-LOGIN_OTP')), findsNothing);
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
