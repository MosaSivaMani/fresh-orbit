import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/models/catalog.dart';
import 'package:fresh_orbit_app/api/models/locations.dart';
import 'package:fresh_orbit_app/features/catalog/home_screen.dart';
import 'package:fresh_orbit_app/illustrations/illustration.dart';

import '../support/fixtures.dart';
import '../support/harness.dart';

Map<String, Object?> _product(int i) => {
  'id': 'p$i',
  'slug': 'product-$i',
  'name': 'Product $i',
  'localNames': <Object?>[],
  'fromPrice': {'unitPricePaise': 50000 + i, 'compareAtPaise': null, 'perUnit': 'KG'},
  'availability': {'status': 'IN_STOCK', 'message': null},
  'locationRequired': false,
};

HomeData _home({bool banners = true, int categories = 6, int products = 0}) => HomeData.fromJson({
  'banners': banners
      ? [
          {
            'title': 'Weekend seer fish',
            'subtitle': 'Steaks cut the way you like.',
            'imageUrl': null,
            'linkType': 'CATEGORY',
            'linkSlug': 'fish',
          },
          {
            'title': 'Prawns, fresh catch',
            'subtitle': 'Cleaned and ready to cook.',
            'imageUrl': null,
            'linkType': 'PRODUCT',
            'linkSlug': 'tiger-prawns',
          },
          {'title': 'Coupon day', 'subtitle': null, 'imageUrl': null, 'linkType': 'COUPON', 'linkSlug': null},
        ]
      : <Object?>[],
  'featuredCategories': [
    for (final name in ['Fish', 'Prawns', 'Crabs', 'Squid', 'Shellfish', 'Lobster'].take(categories))
      {'id': name, 'slug': name.toLowerCase(), 'name': name, 'imageUrl': null},
  ],
  'featuredProducts': [for (var i = 1; i <= products; i++) _product(i)],
  'locationRequired': false,
});

Future<TestDeps> _pump(
  WidgetTester tester, {
  Size size = const Size(390, 844),
  double textScale = 1,
  HomeData? home,
  Serviceability? service,
}) async {
  final deps = TestDeps(pincode: '600041');
  deps.location.onServiceability = (_) async => service ?? serviceable();
  deps.catalog.onHome = () async => home ?? _home();
  await pumpScreen(tester, deps, const HomeScreen(), otherPaths: const ['/shop', '/search', '/wishlist', '/account/help']);
  tester.view.physicalSize = size * tester.view.devicePixelRatio;
  tester.platformDispatcher.textScaleFactorTestValue = textScale;
  addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
  await tester.pumpAndSettle();
  return deps;
}

/// The page is laid out at the screen width and may be scaled to fit, so edges are compared after the scale.
double _scale(WidgetTester tester) {
  final fit = find.byKey(const Key('homeFit'));
  if (fit.evaluate().isEmpty) return 1;
  final page = tester.getSize(find.descendant(of: fit, matching: find.byType(SizedBox)).first);
  return tester.getSize(fit).width / page.width;
}

/// The sections below the first screen are laid out but off screen until scrolled to.
Finder _below(String key) => find.byKey(Key(key), skipOffstage: false);

final _verticalScroll = find.byWidgetPredicate(
  (w) => w is Scrollable && axisDirectionToAxis(w.axisDirection) == Axis.vertical,
);

void main() {
  setUp(() => IllustrationAssets.debugSetAvailable(const {}));
  tearDown(() => IllustrationAssets.debugSetAvailable(null));

  testWidgets('shows the server pincode, city and next slot, and no name when signed out', (tester) async {
    await _pump(tester);

    expect(find.text('Delivering to 600041'), findsOneWidget);
    expect(find.text('Sample City'), findsOneWidget);
    final hello = tester.widget<Text>(find.byKey(const Key('homeGreeting'))).data!;
    expect(hello, matches(RegExp(r'^Good (morning|afternoon|evening)$')), reason: 'no name when signed out');
    expect(find.byKey(const Key('homeNextSlot')), findsOneWidget);
    expect(find.text('Today’s catch'), findsOneWidget);
    expect(find.text('Shop fresh catch →'), findsOneWidget);
    expect(find.text('Choose cut →'), findsOneWidget);
    expect(find.text('See delivery →'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('hides the next-slot chip when the server sends none', (tester) async {
    final json = serviceableJson()..['nextSlot'] = null;
    await _pump(tester, service: Serviceability.fromJson(json));

    expect(find.byKey(const Key('homeNextSlot')), findsNothing);
    expect(find.text('Today’s catch'), findsOneWidget);
  });

  testWidgets('every block starts and ends on the 16 dp gutter; cards and categories are equal', (tester) async {
    await _pump(tester);
    final k = _scale(tester);
    final screen = tester.getRect(find.byKey(const Key('homeFit')));
    double left(Rect r) => (r.left - screen.left) / k;
    double right(Rect r) => (screen.right - r.right) / k;

    final hero = tester.getRect(find.byKey(const Key('homeHero')));
    expect(left(hero), moreOrLessEquals(16, epsilon: 0.5));
    expect(right(hero), moreOrLessEquals(16, epsilon: 0.5));

    final cleaned = tester.getRect(find.byKey(const Key('homeCardCleaned')));
    final cold = tester.getRect(find.byKey(const Key('homeCardCold')));
    expect(left(cleaned), moreOrLessEquals(16, epsilon: 0.5));
    expect(right(cold), moreOrLessEquals(16, epsilon: 0.5));
    expect(cleaned.width, moreOrLessEquals(cold.width, epsilon: 0.01));
    expect(cleaned.height, moreOrLessEquals(cold.height, epsilon: 0.01));
    expect((cold.left - cleaned.right) / k, moreOrLessEquals(12, epsilon: 0.5));

    final circles = [
      for (final slug in ['fish', 'prawns', 'crabs', 'squid', 'shellfish'])
        tester.getRect(find.byKey(ValueKey('homeCategory-$slug'))),
      tester.getRect(find.byKey(const Key('homeCategoryMore'))),
    ];
    expect(find.byKey(const ValueKey('homeCategory-lobster')), findsNothing, reason: 'five plus More');
    final columns = tester.getRect(find.byKey(const Key('homeCategories')));
    expect(left(columns), moreOrLessEquals(16, epsilon: 0.5));
    expect(right(columns), moreOrLessEquals(16, epsilon: 0.5));
    for (var i = 1; i < circles.length; i++) {
      final step = circles[i].center.dx - circles[i - 1].center.dx;
      expect(step, moreOrLessEquals(columns.width / 6, epsilon: 0.5), reason: 'column $i');
    }

    final firstOffer = tester.getRect(find.ancestor(of: find.text('Weekend seer fish'), matching: find.byType(Container)).first);
    expect(left(firstOffer), moreOrLessEquals(16, epsilon: 0.5));
  });

  testWidgets('the first screen fills the screen as before and the page scrolls on into the sections (D292)', (
    tester,
  ) async {
    await _pump(tester);

    final fit = tester.getRect(find.byKey(const Key('homeFit')));
    expect(fit.top, 0);
    expect(fit.bottom, moreOrLessEquals(844, epsilon: 0.01));
    expect(find.descendant(of: find.byType(HomeScreen), matching: _verticalScroll), findsOneWidget);
    final scroll = tester.widget<SingleChildScrollView>(find.byKey(const Key('homeScroll')));
    expect(scroll.physics, isA<ClampingScrollPhysics>());
    expect(find.byType(RefreshIndicator), findsNothing);
    // The sections start below the first screen.
    final sections = tester.getRect(_below('homeSections'));
    expect(sections.top, moreOrLessEquals(fit.bottom, epsilon: 0.01));
    expect(tester.takeException(), isNull);
  });

  testWidgets('at a large system text scale the first screen keeps its natural size and still scrolls', (
    tester,
  ) async {
    await _pump(tester, textScale: 1.5);

    expect(find.byKey(const Key('homeFit')), findsNothing);
    final scroll = tester.widget<SingleChildScrollView>(find.byKey(const Key('homeScroll')));
    expect(scroll.physics, isA<ClampingScrollPhysics>());
    expect(_below('homeSections'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('"Popular today" shows the server featured products, at most six, and See all opens the shop', (
    tester,
  ) async {
    await _pump(tester, home: _home(products: 8));

    expect(_below('homePopular'), findsOneWidget);
    for (var i = 1; i <= homePopularCount; i++) {
      expect(find.text('Product $i', skipOffstage: false), findsOneWidget, reason: 'product $i');
    }
    expect(find.text('Product 7', skipOffstage: false), findsNothing);
    final seeAll = _below('homePopularSeeAll');
    await tester.ensureVisible(seeAll);
    await tester.pumpAndSettle();
    await tester.tap(seeAll);
    await tester.pumpAndSettle();
    expect(find.text('route:/shop'), findsOneWidget);
  });

  testWidgets('"Popular today" is hidden when the server sends no featured products (D186)', (tester) async {
    await _pump(tester);

    expect(_below('homePopular'), findsNothing);
    expect(_below('homePopularSeeAll'), findsNothing);
    expect(_below('homeSteps'), findsOneWidget);
  });

  testWidgets('the steps, the promise, the help card and the sea floor follow in order', (tester) async {
    await _pump(tester, home: _home(products: 2));

    double top(String key) => tester.getRect(_below(key)).top;
    expect(top('homePopular'), lessThan(top('homeSteps')));
    expect(top('homeSteps'), lessThan(top('homePromise')));
    expect(top('homePromise'), lessThan(top('homeHelp')));
    expect(top('homeHelp'), lessThan(top('homeSeaFloor')));
    expect(tester.takeException(), isNull);
  });

  testWidgets('the section headings and tiles are labelled for screen readers', (tester) async {
    final handle = tester.ensureSemantics();
    await _pump(tester);

    await tester.ensureVisible(_below('homeSteps'));
    await tester.pumpAndSettle();
    expect(find.bySemanticsLabel('Fresh in three steps'), findsOneWidget);
    expect(find.bySemanticsLabel('Step 1, Pick your cut, Whole, steaks, curry or fillet'), findsOneWidget);
    await tester.ensureVisible(_below('homePromise'));
    await tester.pumpAndSettle();
    expect(find.bySemanticsLabel('Our promise'), findsOneWidget);
    expect(find.bySemanticsLabel('Pay net weight, Weighed after cleaning'), findsOneWidget);
    await tester.ensureVisible(_below('homeHelp'));
    await tester.pumpAndSettle();
    expect(find.bySemanticsLabel(RegExp(r'^Need a hand\?')), findsOneWidget);
    handle.dispose();
  });

  testWidgets('the promise says "Kept chilled" only when the serving store has cold storage (D212)', (tester) async {
    await _pump(tester);
    expect(find.text('Kept chilled', skipOffstage: false), findsOneWidget);
    expect(find.text('Cash or UPI', skipOffstage: false), findsNothing);
  });

  testWidgets('without cold storage the promise names how to pay instead', (tester) async {
    final json = serviceableJson()
      ..['fulfillmentLocation'] = {'id': 'l1', 'code': 'LOC1', 'name': 'Sample Store', 'hasColdStorage': false};
    await _pump(tester, service: Serviceability.fromJson(json));

    expect(find.text('Kept chilled', skipOffstage: false), findsNothing);
    expect(find.text('Cash or UPI', skipOffstage: false), findsOneWidget);
  });

  testWidgets('the help card opens Help', (tester) async {
    await _pump(tester);

    final help = _below('homeHelp');
    await tester.ensureVisible(help);
    await tester.pumpAndSettle();
    await tester.tap(help);
    await tester.pumpAndSettle();
    expect(find.text('route:/account/help'), findsOneWidget);
  });

  testWidgets('a step tile shows its picture once bundled and its icon until then', (
    tester,
  ) async {
    IllustrationAssets.debugSetAvailable(const {'assets/illustrations/onboarding_tile_your_cut.png'});
    await _pump(tester);

    final picture = find.byKey(const ValueKey('illustration-image-ONBOARDING_TILE_YOUR_CUT'), skipOffstage: false);
    final icon = find.byKey(const ValueKey('homeTile-fallback-ONBOARDING_TILE_CLEANED_BY_HAND'), skipOffstage: false);
    expect(picture, findsOneWidget);
    expect(icon, findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  for (final size in const [Size(360, 640), Size(390, 844)]) {
    for (final scale in const [1.0, 1.3]) {
      testWidgets('no overflow at ${size.width.toInt()}×${size.height.toInt()} with text scale $scale', (tester) async {
        await _pump(tester, size: size, textScale: scale);

        expect(tester.takeException(), isNull);
        final fit = tester.getRect(find.byKey(const Key('homeFit')));
        expect(fit.bottom, lessThanOrEqualTo(size.height + 0.01));
        // Always the full width: scaling to fit never leaves side gaps.
        expect(fit.left, 0);
        expect(fit.width, size.width);
      });
    }
  }

  testWidgets('an offer opens its category or product; one with no slug is not a button', (tester) async {
    final handle = tester.ensureSemantics();
    await _pump(tester);

    // The third offer starts past the screen edge: slide the row to it, then back.
    final coupon = find.text('Coupon day', skipOffstage: false);
    expect(coupon, findsOneWidget);
    await tester.ensureVisible(coupon);
    await tester.pumpAndSettle();
    expect(tester.getSemantics(find.bySemanticsLabel('Coupon day')), isSemantics(isButton: false));
    await tester.ensureVisible(find.text('Weekend seer fish', skipOffstage: false));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Weekend seer fish'));
    await tester.pumpAndSettle();
    expect(find.text('route:/shop?category=fish'), findsOneWidget);
    handle.dispose();
  });

  testWidgets('a product offer opens the product page', (tester) async {
    await _pump(tester);

    await tester.ensureVisible(find.text('Prawns, fresh catch', skipOffstage: false));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Prawns, fresh catch'));
    await tester.pumpAndSettle();
    expect(find.text('route:/product/tiger-prawns'), findsOneWidget);
  });

  testWidgets('hides the specials when the server sends no offers', (tester) async {
    await _pump(tester, home: _home(banners: false));

    expect(find.byKey(const Key('homeSpecials')), findsNothing);
    expect(find.byKey(const Key('homeSeeAll')), findsNothing);
    expect(find.byKey(const Key('homeCategories')), findsOneWidget);
  });

  testWidgets('a category circle opens the shop on that category, More opens the shop', (tester) async {
    await _pump(tester);

    await tester.tap(find.byKey(const ValueKey('homeCategory-prawns')));
    await tester.pumpAndSettle();
    expect(find.text('route:/shop?category=prawns'), findsOneWidget);
  });

  testWidgets('See delivery shows the server delivery terms', (tester) async {
    await _pump(tester);

    await tester.tap(find.byKey(const Key('homeCardCold')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('homeDeliverySheet')), findsOneWidget);
    expect(find.text('Sample Area'), findsOneWidget);
    expect(find.textContaining('Delivery fee'), findsOneWidget);
  });

  testWidgets('cards and buttons are labelled; the drawn scenery is not announced', (tester) async {
    final handle = tester.ensureSemantics();
    await _pump(tester);

    expect(find.bySemanticsLabel(RegExp(r'^Today’s catch is in!')), findsOneWidget);
    expect(find.bySemanticsLabel(RegExp(r'^Cleaned your way\. Pick your cut')), findsOneWidget);
    expect(find.bySemanticsLabel(RegExp(r'^Kept cold till your door\.')), findsOneWidget);
    expect(find.bySemanticsLabel('What’s cooking today?'), findsOneWidget);
    expect(find.bySemanticsLabel('Search'), findsOneWidget);
    expect(find.bySemanticsLabel('Saved'), findsOneWidget);
    expect(find.bySemanticsLabel('Fish'), findsOneWidget);
    expect(find.bySemanticsLabel('More'), findsOneWidget);
    expect(find.bySemanticsLabel(IllustrationSlot.onboardingFresh.semanticLabel), findsNothing);
    expect(find.bySemanticsLabel(IllustrationSlot.homeHeroCatch.semanticLabel), findsNothing);
    final harbour = find.byKey(const Key('homeHarbour'));
    expect(find.descendant(of: harbour, matching: find.byType(ExcludeSemantics)), findsOneWidget);
    handle.dispose();
  });

  testWidgets('pictures fall back to the onboarding stand-ins when a home picture is missing', (tester) async {
    IllustrationAssets.debugSetAvailable(const {
      'assets/illustrations/onboarding_fresh.jpg',
      'assets/illustrations/onboarding_banner_cleaned.png',
      'assets/illustrations/onboarding_tile_cold_box.png',
    });
    await _pump(tester);

    expect(find.byKey(const ValueKey('illustration-image-ONBOARDING_FRESH')), findsOneWidget);
    expect(find.byKey(const ValueKey('illustration-image-ONBOARDING_BANNER_CLEANED')), findsOneWidget);
    expect(find.byKey(const ValueKey('illustration-image-ONBOARDING_TILE_COLD_BOX')), findsOneWidget);
  });

  testWidgets('the home pictures replace the stand-ins once bundled', (tester) async {
    IllustrationAssets.debugSetAvailable(const {
      'assets/illustrations/home_hero_catch.png',
      'assets/illustrations/home_card_rider.png',
      'assets/illustrations/onboarding_fresh.jpg',
      'assets/illustrations/onboarding_tile_cold_box.png',
    });
    await _pump(tester);

    expect(find.byKey(const ValueKey('illustration-image-HOME_HERO_CATCH')), findsOneWidget);
    expect(find.byKey(const ValueKey('illustration-image-ONBOARDING_FRESH')), findsNothing);
    expect(find.byKey(const ValueKey('illustration-image-HOME_CARD_RIDER')), findsOneWidget);
    expect(find.byKey(const ValueKey('illustration-image-ONBOARDING_TILE_COLD_BOX')), findsNothing);
    // Ravi sits under the title, like the cleaned card's picture: he never covers "door".
    final card = tester.getRect(find.byKey(const Key('homeCardCold')));
    final rider = tester.getRect(find.byKey(const ValueKey('homePicture-HOME_CARD_RIDER')));
    final title = tester.getRect(find.text('till your door'));
    expect(rider.top, greaterThanOrEqualTo(title.bottom - 0.5));
    expect(rider.top, greaterThan(card.top));
  });

  testWidgets('categories and offers show the bundled owner pictures when the server sends none', (tester) async {
    IllustrationAssets.debugSetAvailable(const {
      'assets/illustrations/category_fish.webp',
      'assets/illustrations/category_prawns.webp',
      'assets/illustrations/category_crabs.webp',
      'assets/illustrations/category_shellfish.webp',
      'assets/illustrations/sample_banner_seer_steaks.webp',
      'assets/illustrations/sample_banner_prawns.webp',
    });
    await _pump(tester);

    for (final picture in ['fish', 'prawns', 'crabs', 'shellfish']) {
      expect(
        find.byKey(ValueKey('bundled-picture-assets/illustrations/category_$picture.webp')),
        findsWidgets,
        reason: picture,
      );
    }
    expect(
      find.byKey(const ValueKey('bundled-picture-assets/illustrations/sample_banner_seer_steaks.webp')),
      findsWidgets,
    );
    expect(tester.takeException(), isNull);
  });

  test('the object categories get their own bundled pictures; marinated keeps its letter (D294)', () {
    const dir = 'assets/illustrations';
    expect(BundledPictures.category('fillets-steaks'), '$dir/category_fillets_steaks.webp');
    expect(BundledPictures.category('ready-to-cook'), '$dir/category_ready_to_cook.webp');
    expect(BundledPictures.category('fish-eggs'), '$dir/category_fish_eggs.webp');
    expect(BundledPictures.category('ready-to-eat'), '$dir/category_ready_to_eat.webp');
    expect(BundledPictures.category('marinated'), isNull);
    expect(BundledPictures.category('sea-fish'), '$dir/category_fish.webp');
    expect(BundledPictures.category('prawns-shrimp'), '$dir/category_prawns.webp');
  });

  testWidgets('category names keep one size and wrap to two lines at most', (tester) async {
    final home = HomeData.fromJson({
      'banners': <Object?>[],
      'featuredCategories': [
        {'id': 'f', 'slug': 'fish', 'name': 'Fish', 'imageUrl': null},
        {'id': 's', 'slug': 'shellfish', 'name': 'Squid and shellfish', 'imageUrl': null},
      ],
      'featuredProducts': <Object?>[],
      'locationRequired': false,
    });
    await _pump(tester, home: home);

    final short = tester.widget<Text>(find.text('Fish'));
    final long = tester.widget<Text>(find.text('Squid and shellfish'));
    expect(long.style?.fontSize, short.style?.fontSize);
    expect(long.maxLines, 2);
    expect(tester.takeException(), isNull);
  });

  testWidgets('the next-slot chip shows the whole time window', (tester) async {
    await _pump(tester, size: const Size(360, 740));

    final chip = find.byKey(const Key('homeNextSlot'));
    final text = tester.widget<Text>(find.descendant(of: chip, matching: find.textContaining('Next slot')));
    expect(text.overflow, isNot(TextOverflow.ellipsis));
    expect(text.data, endsWith('›'));
    expect(tester.takeException(), isNull);
  });

  testWidgets('the page fills the screen: no dead gap under the offers', (tester) async {
    await _pump(tester);

    final fit = tester.getRect(find.byKey(const Key('homeFit')));
    expect(fit.bottom, moreOrLessEquals(844, epsilon: 1));
    final specials = tester.getRect(find.byKey(const Key('homeSpecials')));
    // The offer row ends near the bottom (only the small end gap is left), not halfway up the screen.
    expect(fit.bottom - specials.bottom, lessThan(80));
    expect(tester.takeException(), isNull);
  });

  testWidgets('with no pictures at all it draws placeholders, never a broken image', (tester) async {
    await _pump(tester);

    expect(find.byKey(const ValueKey('illustration-placeholder-ONBOARDING_FRESH')), findsOneWidget);
    // The only pictures are the harbour's real boats and gulls, bundled in the theme package (D287).
    final images = find.byType(Image);
    final scenery = find.descendant(of: find.byKey(const Key('homeHarbour')), matching: find.byType(Image));
    expect(images.evaluate().length, scenery.evaluate().length);
    expect(scenery, findsNWidgets(6));
    expect(tester.takeException(), isNull);
  });

  testWidgets('the city line carries the state name when the server sends one (D209, D238)', (tester) async {
    final json = serviceableJson()
      ..['city'] = {'id': 'c1', 'code': 'CITY1', 'name': 'Sample City', 'stateCode': 'AP', 'stateName': 'Andhra'};
    await _pump(tester, service: Serviceability.fromJson(json));

    expect(find.text('Sample City, Andhra'), findsOneWidget);
  });

  testWidgets('says "Kept cold" only when the serving store has cold storage (D212, D239)', (tester) async {
    final json = serviceableJson()
      ..['fulfillmentLocation'] = {'id': 'l1', 'code': 'LOC1', 'name': 'Sample Store', 'hasColdStorage': false};
    await _pump(tester, service: Serviceability.fromJson(json));

    expect(find.textContaining('Kept cold'), findsNothing);
    expect(find.text('See delivery →'), findsOneWidget);
    await tester.tap(find.byKey(const Key('homeCardCold')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('homeDeliverySheet')), findsOneWidget);
    expect(find.text('Packed on ice in a cold box.'), findsNothing);
  });

  testWidgets('with cold storage the card and the delivery sheet say so', (tester) async {
    await _pump(tester);

    expect(find.textContaining('Kept cold'), findsOneWidget);
    await tester.tap(find.byKey(const Key('homeCardCold')));
    await tester.pumpAndSettle();
    expect(find.text('Packed on ice in a cold box.'), findsOneWidget);
  });

  test('banner routes follow the link type and slug', () {
    HomeBanner b(String? type, String? slug) =>
        HomeBanner.fromJson({'title': 't', 'linkType': type, 'linkSlug': slug});
    expect(b('CATEGORY', 'fish').route, '/shop?category=fish');
    expect(b('PRODUCT', 'seer-fish').route, '/product/seer-fish');
    expect(b('COUPON', null).route, isNull);
    expect(b('CATEGORY', null).route, isNull);
    expect(b('SOMETHING', 'x').route, isNull);
  });
}
