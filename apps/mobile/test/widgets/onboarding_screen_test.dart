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
    banner: "From\ntoday's catch\nto your kitchen",
  ),
  (
    title: 'Steaks, curry cut, fillet',
    tiles: [
      ('Your cut', 'Whole, steaks, curry or fillet'),
      ('Cleaned by hand', 'Scaled, gutted, washed'),
      ('Pay net weight', 'Weighed after cleaning'),
    ],
    banner: 'Ready\nfor your pan',
  ),
  (
    title: 'Pick a delivery slot',
    tiles: [
      ('Pick a slot', 'Morning or evening'),
      ('Cold box', 'Packed on ice'),
      ('Cash or UPI', 'Pay when it arrives'),
    ],
    banner: 'From our store\nto your door',
  ),
];

const _avatars = ['ONBOARDING_FRESH', 'ONBOARDING_CLEANED', 'ONBOARDING_DELIVERY'];

/// A tall phone, where the food banner always has room (the test font is wider than Inter, so on a 390 × 844
/// phone the banner may give way).
const _tall = Size(430, 1100);

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
    await _pump(tester, size: _tall);

    for (var i = 0; i < _pages.length; i++) {
      final page = _pages[i];
      expect(find.text(page.title), findsOneWidget, reason: 'page ${i + 1}');
      expect(find.text('STEP ${i + 1} OF 3'), findsOneWidget);
      for (final (label, caption) in page.tiles) {
        expect(find.text(label), findsWidgets, reason: label);
        expect(find.text(caption), findsOneWidget, reason: caption);
      }
      expect(find.text(page.banner), findsOneWidget);
      expect(tester.takeException(), isNull);
      if (i < _pages.length - 1) await _next(tester);
    }
  });

  testWidgets('on the owner phone (411×891) every page shows its banner, all the same size', (tester) async {
    const banners = ['ONBOARDING_BANNER_FRESH', 'ONBOARDING_BANNER_CLEANED', 'ONBOARDING_BANNER_DELIVERY'];
    IllustrationAssets.debugSetAvailable(const {});
    await _pump(tester, size: const Size(411, 891));

    final heights = <double>[];
    for (var i = 0; i < banners.length; i++) {
      final banner = find.byKey(ValueKey('banner-${banners[i]}'));
      expect(banner, findsOneWidget, reason: 'page ${i + 1}');
      final height = tester.getSize(banner).height;
      expect(height, inInclusiveRange(96, 150), reason: 'page ${i + 1}');
      heights.add(height);
      // The banner sits inside the sheet, above the page dots.
      final sheet = tester.getRect(find.byKey(ValueKey('onboardingSheet-${i + 1}')));
      expect(tester.getRect(banner).bottom, lessThanOrEqualTo(sheet.bottom + 0.01), reason: 'page ${i + 1}');
      expect(
        find.descendant(of: find.byType(PageView), matching: find.byType(SingleChildScrollView)),
        findsNothing,
      );
      expect(tester.takeException(), isNull);
      if (i < banners.length - 1) await _next(tester);
    }
    // The three pages share one sheet layout, so the banner is the same size on all of them.
    for (final height in heights) {
      expect(height, moreOrLessEquals(heights.first, epsilon: 0.5), reason: '$heights');
    }
  });

  testWidgets('tile labels and the title stay on one line', (tester) async {
    IllustrationAssets.debugSetAvailable(const {});
    await _pump(tester, size: const Size(411, 891));

    for (var i = 0; i < _pages.length; i++) {
      final page = _pages[i];
      for (final (label, _) in page.tiles) {
        final sheet = find.byKey(ValueKey('onboardingSheet-${i + 1}'));
        final text = tester.widget<Text>(find.descendant(of: sheet, matching: find.text(label)));
        expect(text.maxLines, 1, reason: label);
        expect(text.softWrap, isFalse, reason: label);
      }
      final title = tester.widget<Text>(find.text(page.title));
      expect(title.maxLines, 1, reason: page.title);
      if (i < _pages.length - 1) await _next(tester);
    }
  });

  testWidgets('tiles fall back to a drawn blob and icon and the banner to a drawn scene', (tester) async {
    IllustrationAssets.debugSetAvailable(const {});
    await _pump(tester, size: _tall);

    expect(find.byKey(const ValueKey('tile-fallback-ONBOARDING_TILE_MORNING_CATCH')), findsOneWidget);
    expect(find.byKey(const ValueKey('tile-fallback-ONBOARDING_TILE_KEPT_CHILLED')), findsOneWidget);
    expect(find.byKey(const ValueKey('tile-fallback-ONBOARDING_TILE_DELIVERED_FRESH')), findsOneWidget);
    expect(find.byKey(const ValueKey('banner-fallback-ONBOARDING_BANNER_FRESH')), findsOneWidget);
    expect(find.byKey(const ValueKey('illustration-placeholder-ONBOARDING_TILE_MORNING_CATCH')), findsNothing);
  });

  testWidgets('with the owner pictures bundled every tile and banner shows its picture, no fallback', (tester) async {
    const pages = [
      (tiles: ['morning_catch', 'kept_chilled', 'delivered_fresh'], banner: 'fresh'),
      (tiles: ['your_cut', 'cleaned_by_hand', 'net_weight'], banner: 'cleaned'),
      (tiles: ['pick_slot', 'cold_box', 'cash_upi'], banner: 'delivery'),
    ];
    IllustrationAssets.debugSetAvailable({
      for (final page in pages) ...[
        for (final tile in page.tiles) 'assets/illustrations/onboarding_tile_$tile.png',
        'assets/illustrations/onboarding_banner_${page.banner}.png',
      ],
    });
    await _pump(tester, size: _tall);

    for (var i = 0; i < pages.length; i++) {
      final page = pages[i];
      for (final tile in page.tiles) {
        final code = 'ONBOARDING_TILE_${tile.toUpperCase()}';
        expect(find.byKey(ValueKey('illustration-image-$code')), findsOneWidget, reason: code);
        expect(find.byKey(ValueKey('tile-fallback-$code')), findsNothing, reason: code);
      }
      final banner = 'ONBOARDING_BANNER_${page.banner.toUpperCase()}';
      expect(find.byKey(ValueKey('illustration-image-$banner')), findsOneWidget, reason: banner);
      expect(find.byKey(ValueKey('banner-fallback-$banner')), findsNothing, reason: banner);
      expect(tester.takeException(), isNull);
      if (i < pages.length - 1) await _next(tester);
    }
  });

  testWidgets('tiles read as "label, caption" and the decoration is not announced', (tester) async {
    IllustrationAssets.debugSetAvailable(const {});
    final handle = tester.ensureSemantics();
    await _pump(tester, size: _tall);

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
          // The page stands still: it fits above the button instead of scrolling (D182).
          expect(
            find.descendant(of: find.byType(PageView), matching: find.byType(SingleChildScrollView)),
            findsNothing,
            reason: 'page ${i + 1}',
          );
          // The button stays pinned inside the screen.
          final button = tester.getRect(find.byKey(const Key('onboardingNext')));
          expect(button.bottom, lessThanOrEqualTo(size.height));
          expect(button.top, greaterThan(size.height / 2));
          if (i < _pages.length - 1) await _next(tester);
        }
        // The banner gives way on a short screen; when it shows it is 96 to 150 tall.
        final banner = find.byKey(const ValueKey('banner-ONBOARDING_BANNER_DELIVERY'));
        if (size.height < 700) {
          expect(banner, findsNothing);
        } else if (banner.evaluate().isNotEmpty) {
          final height = tester.getSize(banner).height;
          expect(height, inInclusiveRange(96, 150));
        }
        expect(tester.takeException(), isNull);
      });
    }
  }

  for (final size in const [Size(360, 640), Size(390, 844)]) {
    testWidgets('block and sheet span the full width at ${size.width.toInt()}×${size.height.toInt()}', (tester) async {
      IllustrationAssets.debugSetAvailable(const {});
      await _pump(tester, size: size);

      for (var step = 1; step <= _pages.length; step++) {
        for (final part in ['onboardingBlock', 'onboardingSheet']) {
          final rect = tester.getRect(find.byKey(ValueKey('$part-$step')));
          expect(rect.left, 0, reason: '$part $step');
          expect(rect.width, size.width, reason: '$part $step');
        }
        // The sheet fills the rest of the page: block on top, sheet right under it, no scrolling.
        final block = tester.getRect(find.byKey(ValueKey('onboardingBlock-$step')));
        final sheet = tester.getRect(find.byKey(ValueKey('onboardingSheet-$step')));
        expect(sheet.top, moreOrLessEquals(block.bottom, epsilon: 0.01));
        expect(block.height, inInclusiveRange(300, 400));
        if (step < _pages.length) await _next(tester);
      }
    });
  }

  testWidgets('the sticker sits on the lower half of the circle, never over the face', (tester) async {
    IllustrationAssets.debugSetAvailable(const {});
    await _pump(tester);

    for (var i = 0; i < _pages.length; i++) {
      final avatar = tester.getRect(find.byKey(ValueKey('avatar-${_avatars[i]}')));
      final sticker = tester.getRect(find.byKey(const Key('onboardingSticker')));
      expect(sticker.top, greaterThan(avatar.center.dy), reason: 'page ${i + 1}');
      // It overlaps the circle's left edge.
      expect(sticker.left, lessThan(avatar.left));
      expect(sticker.right, greaterThan(avatar.left));
      if (i < _pages.length - 1) await _next(tester);
    }
  });

  testWidgets('the circle is about 212 on a 390 phone, centred near 62% and 28 into the sheet', (tester) async {
    IllustrationAssets.debugSetAvailable(const {});
    await _pump(tester);

    final avatar = tester.getRect(find.byKey(const ValueKey('avatar-ONBOARDING_FRESH')));
    final sheet = tester.getRect(find.byKey(const ValueKey('onboardingSheet-1')));
    expect(avatar.width, inInclusiveRange(150, 212));
    expect(avatar.center.dx, moreOrLessEquals(390 * 0.62, epsilon: 0.5));
    expect(avatar.bottom - sheet.top, moreOrLessEquals(28, epsilon: 0.5));
  });
}
