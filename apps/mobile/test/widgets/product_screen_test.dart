import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/api_error.dart';
import 'package:fresh_orbit_app/api/models/cart.dart';
import 'package:fresh_orbit_app/api/models/catalog.dart';
import 'package:fresh_orbit_app/core/format.dart';
import 'package:fresh_orbit_app/features/catalog/product_screen.dart';

import '../support/fixtures.dart';
import '../support/harness.dart';

void main() {
  late TestDeps deps;
  late List<String?> requestedPreparations;

  setUp(() {
    deps = TestDeps(pincode: '600041');
    requestedPreparations = [];
    deps.catalog.onProduct = (slug, prep) async {
      requestedPreparations.add(prep);
      return productDetail();
    };
  });

  Future<void> pump(WidgetTester tester) async {
    await pumpScreen(tester, deps, const ProductScreen(slug: 'seer-fish-vanjaram'));
    await tester.pumpAndSettle();
  }

  testWidgets('shows server price, preparations with charges, yield note and net-weight note', (tester) async {
    await pump(tester);

    expect(find.text('₹1,299/kg'), findsOneWidget);
    expect(find.text('₹1,499'), findsOneWidget); // compare-at, struck through
    // Cut tiles: the name and the server's charge for it.
    expect(find.text('Steaks (1 cm)'), findsOneWidget);
    expect(find.text('+₹40/kg'), findsOneWidget);
    expect(find.text('Whole, uncleaned'), findsOneWidget);
    expect(find.text('Free'), findsOneWidget);
    expect(find.textContaining('About 70% of the whole weight'), findsOneWidget);
    expect(find.textContaining('Prices are for net weight after cleaning'), findsOneWidget);
    expect(find.text('500 g'), findsOneWidget); // starts at the server minOrder
  });

  testWidgets('stepper proposes quantities in server steps and add sends them; estimate comes from the server line', (
    tester,
  ) async {
    // D347: the server's maximum equals its estimate, so the price is shown plainly.
    deps.cart.afterWrite = () => cart(lines: [cartLineJson(quantity: 750, estimated: 100425)]);
    await pump(tester);

    final increase = find.byTooltip('Increase quantity of Seer Fish');
    await tester.ensureVisible(increase);
    await tester.tap(increase);
    await tester.pump();
    expect(find.text('750 g'), findsOneWidget);

    await tester.tap(find.byKey(const Key('addToCart')));
    await tester.pumpAndSettle();

    final item = deps.cart.added.single;
    expect(item.variantId, 'v1');
    expect(item.preparationCode, 'STEAK');
    expect(item.quantity.value, 750);
    expect(item.quantity.unit, 'GRAM');
    expect(find.text('Added. ₹1,004.25 for 750 g.'), findsOneWidget);
    expect(find.text('₹1,004.25 for 750 g.'), findsOneWidget); // the "In your cart" banner
    expect(find.textContaining('at most'), findsNothing);
    expect(find.textContaining('Estimated'), findsNothing);
  });

  testWidgets('with an over-weight allowance the added line still says the most it can cost', (tester) async {
    deps.cart.afterWrite = () =>
        cart(lines: [cartLineJson(quantity: 750, estimated: 100425, max: 110467, maxQuantity: 825)]);
    await pump(tester);

    final increase = find.byTooltip('Increase quantity of Seer Fish');
    await tester.ensureVisible(increase);
    await tester.tap(increase);
    await tester.pump();
    await tester.tap(find.byKey(const Key('addToCart')));
    await tester.pumpAndSettle();

    expect(find.text('Added. Estimated ₹1,004.25, at most ₹1,104.67.'), findsOneWidget);
    expect(find.textContaining('You pay at most ₹1,104.67'), findsOneWidget);
  });

  testWidgets('changing preparation asks the server again for that preparation', (tester) async {
    await pump(tester);

    expect(find.text('+₹80/kg'), findsOneWidget);
    final fillet = find.text('Boneless fillet');
    await tester.ensureVisible(fillet);
    await tester.tap(fillet);
    await tester.pumpAndSettle();

    expect(requestedPreparations, [null, 'FILLET']);
    expect(find.textContaining('About 55% of the whole weight'), findsOneWidget);
  });

  testWidgets('a quantity refusal from the server is shown at the stepper', (tester) async {
    deps.cart.failNext = const ApiError(
      status: 422,
      code: 'VALIDATION_FAILED',
      errors: {
        'quantity.value': [
          FieldError(code: 'QUANTITY_STEP_INVALID', message: 'Quantity must be a multiple of 250 grams.'),
        ],
      },
    );
    await pump(tester);

    await tester.tap(find.byKey(const Key('addToCart')));
    await tester.pumpAndSettle();

    expect(find.text('Quantity must be a multiple of 250 grams.'), findsOneWidget);
  });

  testWidgets('without a price for the location the add button asks for a pincode', (tester) async {
    final json = productDetailJson();
    final variant = Map<String, Object?>.from((json['variants']! as List).first as Map);
    variant['unitPricePaise'] = null;
    variant['compareAtPaise'] = null;
    variant['availability'] = null;
    deps.catalog.onProduct = (_, _) async => ProductDetail.fromJson({
      ...json,
      'variants': [variant],
      'locationRequired': true,
    });
    await pump(tester);

    expect(find.text('Set pincode to buy'), findsOneWidget);
    expect(find.byKey(const Key('addToCart')), findsNothing);
  });

  testWidgets('the price for the chosen quantity is the server quote, re-asked when the quantity changes', (
    tester,
  ) async {
    // D347: no over-weight charge, so the server's maximum equals its estimate.
    deps.catalog.onQuote = (variantId, prep, quantity) async => PriceQuote.fromJson({
      'quantity': {'value': quantity.value, 'unit': quantity.unit},
      'maxChargeableQuantity': {'value': quantity.value, 'unit': quantity.unit},
      'estimatedTotalPaise': quantity.value == 500 ? 67000 : 100500,
      'maxChargeablePaise': quantity.value == 500 ? 67000 : 100500,
      'availability': {'status': 'IN_STOCK', 'message': null},
    });
    await pump(tester);

    final quote = find.byKey(const Key('priceQuote'));
    expect(quote, findsOneWidget);
    expect(find.descendant(of: quote, matching: find.text('₹670 for 500 g')), findsOneWidget);
    expect(find.descendant(of: quote, matching: find.textContaining('at most')), findsNothing);
    expect(find.textContaining('Estimated'), findsNothing);

    final increase = find.byTooltip('Increase quantity of Seer Fish');
    await tester.ensureVisible(increase);
    await tester.tap(increase);
    await tester.pumpAndSettle();

    expect(deps.catalog.quotes.map((q) => q.split(':').last), containsAllInOrder(['500', '750']));
    expect(find.descendant(of: quote, matching: find.text('₹1,005 for 750 g')), findsOneWidget);
  });

  testWidgets('with an over-weight allowance the quote shows the estimate and the most you pay', (tester) async {
    deps.catalog.onQuote = (variantId, prep, quantity) async => PriceQuote.fromJson({
      'quantity': {'value': quantity.value, 'unit': quantity.unit},
      'maxChargeableQuantity': {'value': quantity.value + 50, 'unit': quantity.unit},
      'estimatedTotalPaise': 67000,
      'maxChargeablePaise': 73700,
      'availability': {'status': 'IN_STOCK', 'message': null},
    });
    await pump(tester);

    final quote = find.byKey(const Key('priceQuote'));
    expect(find.descendant(of: quote, matching: find.text('Estimated ₹670 for 500 g')), findsOneWidget);
    expect(find.descendant(of: quote, matching: find.text('You pay at most ₹737 (up to 550 g).')), findsOneWidget);
  });

  testWidgets('a sold-out size offers Notify me, which asks the server for a back-in-stock alert', (tester) async {
    deps = TestDeps(pincode: '600041', signedIn: true);
    deps.location.onServiceability = (_) async => serviceable();
    final json = productDetailJson();
    final variant = Map<String, Object?>.from((json['variants']! as List).first as Map);
    variant['availability'] = {'status': 'OUT_OF_STOCK', 'maxOrderableNow': null, 'message': null};
    deps.catalog.onProduct = (_, _) async => ProductDetail.fromJson({
      ...json,
      'variants': [variant],
    });
    await pump(tester);

    expect(find.byKey(const Key('addToCart')), findsNothing);
    await tester.tap(find.byKey(const Key('notifyMe')));
    await tester.pumpAndSettle();

    expect(deps.account.stockAlerts, ['v1@l1']);
  });

  testWidgets('shows the catch date of the chosen size only when the server sends one (CAT-R25, D240)', (tester) async {
    final json = productDetailJson();
    ((json['variants']! as List<Object?>).first! as Map<String, Object?>)['freshness'] = {
      'caughtOn': '2026-10-23',
      'receivedAt': '2026-10-24T00:45:00Z',
    };
    deps.catalog.onProduct = (_, _) async => ProductDetail.fromJson(json);
    await pump(tester);

    expect(find.byKey(const Key('productCaught')), findsOneWidget);
    expect(find.text('Caught Fri, 23 Oct'), findsOneWidget);
  });

  testWidgets('no freshness from the server shows no catch date', (tester) async {
    await pump(tester);

    expect(find.byKey(const Key('productCaught')), findsNothing);
  });

  group('fixed packs (D348)', () {
    testWidgets('a PACK size shows its net weight, pieces and price per pack, and the stepper counts packs', (
      tester,
    ) async {
      deps.catalog.onProduct = (_, _) async => ProductDetail.fromJson(packProductDetailJson());
      deps.cart.afterWrite = () => cart(lines: [packCartLineJson(packs: 2)]);
      await pump(tester);

      final facts = find.byKey(const Key('productPackFacts'));
      expect(facts, findsOneWidget);
      expect(find.descendant(of: facts, matching: find.text('500 g net · 5–7 pieces · ₹749')), findsOneWidget);
      expect(find.descendant(of: facts, matching: find.text(netWeightCaption)), findsOneWidget);
      final price = find.byKey(const Key('productPrice'));
      expect(find.descendant(of: price, matching: find.text('₹749 per pack')), findsOneWidget);
      expect(find.textContaining('/kg'), findsNothing);
      expect(find.text('Serves 3 per pack'), findsOneWidget);
      expect(find.text('1 pack'), findsOneWidget); // the stepper starts at the server minOrder

      final increase = find.byTooltip('Increase quantity of Seer Fish');
      await tester.ensureVisible(increase);
      await tester.tap(increase);
      await tester.pump();
      expect(find.text('2 packs'), findsOneWidget);

      await tester.tap(find.byKey(const Key('addToCart')));
      await tester.pumpAndSettle();

      final item = deps.cart.added.single;
      expect(item.variantId, 'v2');
      expect((item.quantity.value, item.quantity.unit), (2, 'PACK'));
      expect(find.textContaining('Estimated'), findsNothing);
    });

    testWidgets('size chips carry the pack net weight; a KG size keeps its per-kg price and no pack facts', (
      tester,
    ) async {
      final json = productDetailJson();
      final pack = {...packVariantJson(), 'name': 'Steaks'};
      deps.catalog.onProduct = (_, _) async => ProductDetail.fromJson({
        ...json,
        'variants': [...(json['variants']! as List<Object?>), pack],
      });
      await pump(tester);

      expect(find.text('₹1,299/kg'), findsOneWidget);
      expect(find.byKey(const Key('productPackFacts')), findsNothing);
      final chip = find.byKey(const ValueKey('size-v2'));
      expect(find.descendant(of: chip, matching: find.text('Steaks · 500 g net')), findsOneWidget);

      await tester.ensureVisible(chip);
      await tester.tap(chip);
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('productPackFacts')), findsOneWidget);
      expect(find.textContaining('500 g net · 5–7 pieces'), findsOneWidget);
      expect(find.text('₹749 per pack'), findsOneWidget);
    });

    test('Variant parses packSizeGrams, avgPieceGrams and the serving net weight', () {
      final v = Variant.fromJson(packVariantJson());
      expect((v.isPack, v.packSizeGrams, v.avgPieceGrams, v.packNetGrams), (true, 500, null, 500));
      expect((v.serving!.netWeightGrams, v.serving!.piecesMin, v.serving!.piecesMax), (500, 5, 7));
      expect(productDetail().variants.single.packNetGrams, isNull);
    });
  });

  test('fixture cart parses', () => expect(cart().lines.single, isA<CartLine>()));
}
