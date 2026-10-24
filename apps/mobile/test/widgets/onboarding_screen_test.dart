import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/features/onboarding/onboarding_screen.dart';
import 'package:fresh_orbit_app/illustrations/illustration.dart';

import '../support/harness.dart';

const _pages = [
  (
    title: 'Hours old, not days',
    tiles: [
      ('Morning catch', 'Direct from local boats'),
      ('Kept chilled', 'On ice all the way'),
      ('Delivered fresh', 'From our store to your door'),
    ],
    banner: ("From today's catch", 'to your kitchen'),
  ),
  (
    title: 'Steaks, curry cut, fillet',
    tiles: [
      ('Your cut', 'Whole, steaks, curry or fillet'),
      ('Cleaned by hand', 'Scaled, gutted, washed'),
      ('Pay net weight', 'Weighed after cleaning'),
    ],
    banner: ('Ready for', 'your pan'),
  ),
  (
    title: 'Pick a delivery slot',
    tiles: [
      ('Pick a slot', 'Morning or evening'),
      ('Cold box', 'Packed on ice'),
      ('Cash or UPI', 'Pay when it arrives'),
    ],
    banner: ('From our store', 'to your door'),
  ),
];

/// Pumps the onboarding on a phone of [size] logical pixels with the given text scale.
Future<TestDeps> _pump(WidgetTester tester, {Size size = const Size(390, 844), double textScale = 1}) async {
  final deps = TestDeps();
  await pumpScreen(tester, deps, const OnboardingScreen());
  tester.view.physicalSize = size * tester.view.devicePixelRatio;
  tester.platformDispatcher.textScaleFactorTestValue = textScale;
  addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
  await tester.pumpAndSettle();
  return deps;
}

Future<void> _next(WidgetTester tester) async {
  await tester.tap(find.byKey(const Key('onboardingNext')));
  await tester.pumpAndSettle();
}

void main() {
  tearDown(() => IllustrationAssets.debugSetAvailable(null));

  testWidgets('every page shows its title, three tiles and the banner line', (tester) async {
    IllustrationAssets.debugSetAvailable(const {});
    await _pump(tester);

    for (var i = 0; i < _pages.length; i++) {
      final page = _pages[i];
      expect(find.text(page.title), findsOneWidget, reason: 'page ${i + 1}');
      expect(find.text('STEP ${i + 1} OF 3'), findsOneWidget);
      for (final (label, caption) in page.tiles) {
        expect(find.text(label), findsWidgets, reason: label);
        expect(find.text(caption), findsOneWidget, reason: caption);
      }
      expect(find.text(page.banner.$1), findsOneWidget);
      expect(find.text(page.banner.$2), findsOneWidget);
      expect(tester.takeException(), isNull);
      if (i < _pages.length - 1) await _next(tester);
    }
  });

  testWidgets('tiles fall back to a drawn blob and icon and the banner to a drawn scene', (tester) async {
    IllustrationAssets.debugSetAvailable(const {});
    await _pump(tester);

    expect(find.byKey(const ValueKey('tile-fallback-ONBOARDING_TILE_MORNING_CATCH')), findsOneWidget);
    expect(find.byKey(const ValueKey('tile-fallback-ONBOARDING_TILE_KEPT_CHILLED')), findsOneWidget);
    expect(find.byKey(const ValueKey('tile-fallback-ONBOARDING_TILE_DELIVERED_FRESH')), findsOneWidget);
    expect(find.byKey(const ValueKey('banner-fallback-ONBOARDING_BANNER_FRESH')), findsOneWidget);
    expect(find.byKey(const ValueKey('illustration-placeholder-ONBOARDING_TILE_MORNING_CATCH')), findsNothing);
  });

  testWidgets('tiles read as "label, caption" and the decoration is not announced', (tester) async {
    IllustrationAssets.debugSetAvailable(const {});
    final handle = tester.ensureSemantics();
    await _pump(tester);

    expect(find.bySemanticsLabel('Morning catch, Direct from local boats'), findsOneWidget);
    expect(find.bySemanticsLabel('Kept chilled, On ice all the way'), findsOneWidget);
    expect(find.bySemanticsLabel('Delivered fresh, From our store to your door'), findsOneWidget);
    expect(find.bySemanticsLabel("From today's catch to your kitchen"), findsOneWidget);
    expect(find.bySemanticsLabel('Straight off the boats'), findsOneWidget);
    expect(find.bySemanticsLabel(IllustrationSlot.onboardingBannerFresh.semanticLabel), findsNothing);
    expect(find.bySemanticsLabel(IllustrationSlot.onboardingFresh.semanticLabel), findsNothing);
    handle.dispose();
  });

  testWidgets('Next walks the pages and Get started finishes onboarding', (tester) async {
    final deps = await _pump(tester);
    deps.prefs.onboardingDone = false;

    expect(find.text('Next →'), findsOneWidget);
    await _next(tester);
    expect(find.text('Steaks, curry cut, fillet'), findsOneWidget);
    expect(find.text('Next →'), findsOneWidget);
    await _next(tester);
    expect(find.text('Pick a delivery slot'), findsOneWidget);
    expect(find.text('Get started →'), findsOneWidget);
    expect(find.text('Next →'), findsNothing);

    await _next(tester);
    expect(deps.prefs.onboardingDone, isTrue);
    expect(find.text('route:/location'), findsOneWidget);
  });

  testWidgets('Next works with reduced motion', (tester) async {
    await _pump(tester);
    tester.platformDispatcher.accessibilityFeaturesTestValue = const FakeAccessibilityFeatures(disableAnimations: true);
    addTearDown(tester.platformDispatcher.clearAccessibilityFeaturesTestValue);
    await tester.pump();

    await _next(tester);
    expect(find.text('Steaks, curry cut, fillet'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('Skip finishes onboarding from the first page', (tester) async {
    final deps = await _pump(tester);
    deps.prefs.onboardingDone = false;

    await tester.tap(find.byKey(const Key('onboardingSkip')).first);
    await tester.pumpAndSettle();

    expect(deps.prefs.onboardingDone, isTrue);
    expect(find.text('route:/location'), findsOneWidget);
  });

  for (final size in const [Size(360, 640), Size(390, 844)]) {
    for (final scale in const [1.0, 1.3]) {
      testWidgets('no overflow at ${size.width.toInt()}×${size.height.toInt()} with text scale $scale', (tester) async {
        IllustrationAssets.debugSetAvailable(const {});
        await _pump(tester, size: size, textScale: scale);

        for (var i = 0; i < _pages.length; i++) {
          expect(tester.takeException(), isNull, reason: 'page ${i + 1}');
          // The button stays pinned inside the screen.
          final button = tester.getRect(find.byKey(const Key('onboardingNext')));
          expect(button.bottom, lessThanOrEqualTo(size.height));
          expect(button.top, greaterThan(size.height / 2));
          if (i < _pages.length - 1) await _next(tester);
        }
        // The banner gives way on a short screen and shows on a tall one.
        expect(
          find.byKey(const ValueKey('banner-ONBOARDING_BANNER_DELIVERY')),
          size.height < 700 ? findsNothing : findsOneWidget,
        );
        expect(tester.takeException(), isNull);
      });
    }
  }
}
