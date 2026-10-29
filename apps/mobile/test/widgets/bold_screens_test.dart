import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/models/catalog.dart';
import 'package:fresh_orbit_app/app.dart';
import 'package:fresh_orbit_app/features/account/settings_screen.dart';
import 'package:fresh_orbit_app/features/auth/login_screen.dart';
import 'package:fresh_orbit_app/features/catalog/product_grid.dart';
import 'package:fresh_orbit_app/features/catalog/wishlist_screen.dart';
import 'package:fresh_orbit_app/features/orders/orders_screen.dart';
import 'package:fresh_orbit_app/illustrations/illustration.dart';

import '../support/fixtures.dart';
import '../support/harness.dart';

const _seerJson = <String, Object?>{
  'id': 'p1',
  'slug': 'seer-fish-vanjaram',
  'name': 'Seer Fish (Vanjaram)',
  'localNames': ['Neymeen'],
  'fromPrice': {'unitPricePaise': 129900, 'compareAtPaise': null, 'perUnit': 'KG'},
  'availability': {'status': 'IN_STOCK', 'message': null},
  'locationRequired': false,
};

ProductSummary _seer() => ProductSummary.fromJson(_seerJson);

void main() {
  setUp(() => IllustrationAssets.debugSetAvailable(const {}));
  tearDown(() => IllustrationAssets.debugSetAvailable(null));

  group('shop card ADD', () {
    late TestDeps deps;

    setUp(() {
      deps = TestDeps(pincode: '600041');
      deps.catalog.onProduct = (_, _) async => productDetail();
    });

    Future<void> pumpCard(WidgetTester tester) async {
      await pumpScreen(
        tester,
        deps,
        Scaffold(
          body: Center(
            child: SizedBox(width: 180, height: 260, child: ShopProductCard(product: _seer())),
          ),
        ),
      );
      await tester.pumpAndSettle();
    }

    testWidgets('adds the server defaults (first size, default cut, minimum) and becomes a stepper', (tester) async {
      deps.cart.afterWrite = () => cart(lines: [cartLineJson()]);
      await pumpCard(tester);

      await tester.tap(find.text('ADD'));
      await tester.pumpAndSettle();

      final item = deps.cart.added.single;
      expect(item.variantId, 'v1');
      expect(item.preparationCode, 'STEAK');
      expect(item.quantity.value, 500);
      expect(item.quantity.unit, 'GRAM');
      expect(find.text('ADD'), findsNothing);
      expect(find.text('500 g'), findsOneWidget);
      // The snack names the size added (MB-2) and opens the cart, not the product page (MB-9).
      expect(find.byKey(const ValueKey('quickAddSnack')), findsOneWidget);
      expect(find.textContaining('Medium (3-5 kg fish)'), findsOneWidget);
      expect(find.byKey(const ValueKey('quickAddViewCart')), findsOneWidget);
    });

    testWidgets('with the list item\'s defaultVariant, ADD adds that size without reading the detail (MB-2)', (
      tester,
    ) async {
      deps.catalog.onProduct = null;
      deps.cart.afterWrite = () => cart(lines: [cartLineJson()]);
      await pumpScreen(
        tester,
        deps,
        Scaffold(
          body: Center(
            child: SizedBox(
              width: 180,
              height: 260,
              child: ShopProductCard(
                product: ProductSummary.fromJson({
                  ..._seerJson,
                  'defaultVariant': {
                    'id': 'v1',
                    'name': 'Medium (3-5 kg fish)',
                    'sellUnit': 'KG',
                    'minOrder': {'value': 500, 'unit': 'GRAM'},
                    'maxOrder': {'value': 5000, 'unit': 'GRAM'},
                    'orderStep': {'value': 250, 'unit': 'GRAM'},
                    'defaultPreparationCode': 'STEAK',
                  },
                }),
              ),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.text('ADD'));
      await tester.pumpAndSettle();

      final item = deps.cart.added.single;
      expect(item.variantId, 'v1');
      expect(item.preparationCode, 'STEAK');
      expect(item.quantity.value, 500);
      expect(find.textContaining('Medium (3-5 kg fish)'), findsOneWidget);

      await tester.tap(find.byKey(const ValueKey('quickAddViewCart')));
      await tester.pumpAndSettle();
      expect(find.text('route:/cart'), findsOneWidget);
      expect(deps.cart.added, hasLength(1));
    });

    testWidgets('+ proposes the next server step; − below the minimum removes the line', (tester) async {
      deps.cart.current = cart(lines: [cartLineJson()]);
      await pumpCard(tester);

      await tester.tap(find.byTooltip('Increase quantity of Seer Fish (Vanjaram)'));
      await tester.pumpAndSettle();
      expect(deps.cart.calls, contains('changeQuantity:line1'));
      expect(deps.cart.quantities.single.value, 750);

      deps.cart.afterWrite = null;
      await tester.tap(find.byTooltip('Decrease quantity of Seer Fish (Vanjaram)'));
      await tester.pumpAndSettle();
      expect(deps.cart.calls, contains('remove:line1'));
      expect(find.text('ADD'), findsOneWidget);
    });
  });

  group('settings', () {
    testWidgets('theme is stored; the language choice is hidden until translations exist (D312)', (tester) async {
      final deps = TestDeps();
      await pumpScreen(tester, deps, const SettingsScreen());
      await tester.pumpAndSettle();

      expect(find.text('Sign in to choose notifications'), findsOneWidget);
      expect(find.byKey(const Key('logOut')), findsNothing);

      await tester.tap(find.text('Dark'));
      await tester.pumpAndSettle();
      expect(deps.prefs.themeMode, 'dark');

      expect(availableLanguages, {'en'});
      expect(find.byKey(const Key('languagePicker')), findsNothing);
      expect(find.text('తెలుగు'), findsNothing);
    });

    test('a language stored by an earlier version without translations reads as English', () {
      final deps = TestDeps()..prefs.language = 'te';
      final container = deps.container();
      addTearDown(container.dispose);
      expect(container.read(languageProvider), 'en');
    });

    testWidgets('signed in: notification switches and log out', (tester) async {
      final deps = TestDeps(signedIn: true);
      await pumpScreen(tester, deps, const SettingsScreen());
      await tester.pumpAndSettle();

      expect(find.text('Sign in to choose notifications'), findsNothing);
      expect(find.byKey(const Key('logOut')), findsOneWidget);
    });
  });

  testWidgets('OTP boxes show each typed digit in its own box', (tester) async {
    final controller = TextEditingController();
    addTearDown(controller.dispose);
    var submitted = 0;
    await pumpScreen(
      tester,
      TestDeps(),
      Scaffold(
        body: OtpBoxes(
          controller: controller,
          length: 6,
          onSubmitted: () => submitted++,
          fieldKey: const Key('otpField'),
        ),
      ),
    );

    await tester.enterText(find.byKey(const Key('otpField')), '4821');
    await tester.pump();
    for (final d in ['4', '8', '2', '1']) {
      expect(find.text(d), findsOneWidget);
    }
    expect(controller.text, '4821');
  });

  testWidgets('no orders yet: the mango block with the first-order prompt', (tester) async {
    final deps = TestDeps(signedIn: true);
    await pumpScreen(tester, deps, const OrdersScreen(), otherPaths: ['/shop']);
    await tester.pumpAndSettle();

    expect(find.text('No orders yet'), findsOneWidget);
    expect(find.byKey(const ValueKey('avatar-EMPTY_ORDERS')), findsOneWidget);
    await tester.tap(find.text('Start shopping'));
    await tester.pumpAndSettle();
    expect(find.text('route:/shop'), findsOneWidget);
  });

  testWidgets('nothing saved: the coral block with the browse action', (tester) async {
    final deps = TestDeps(signedIn: true);
    await pumpScreen(tester, deps, const WishlistScreen(), otherPaths: ['/shop']);
    await tester.pumpAndSettle();

    expect(find.text('Tap the ♥ on any fish'), findsOneWidget);
    expect(find.byKey(const ValueKey('avatar-EMPTY_WISHLIST')), findsOneWidget);
  });
}
