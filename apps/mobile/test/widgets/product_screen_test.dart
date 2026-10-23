import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/api_error.dart';
import 'package:fresh_orbit_app/api/models/cart.dart';
import 'package:fresh_orbit_app/api/models/catalog.dart';
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
    expect(find.text('Steaks (1 cm) · +₹40/kg'), findsOneWidget);
    expect(find.text('Whole, uncleaned · free'), findsOneWidget);
    expect(find.textContaining('About 70% of the whole weight'), findsOneWidget);
    expect(find.textContaining('You pay for net weight after cleaning'), findsOneWidget);
    expect(find.text('500 g'), findsOneWidget); // starts at the server minOrder
  });

  testWidgets('stepper proposes quantities in server steps and add sends them; estimate comes from the server line', (tester) async {
    deps.cart.afterWrite = () => cart(lines: [cartLineJson(quantity: 750, estimated: 100425, max: 110467)]);
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
    expect(find.text('Added. Estimated ₹1,004.25, at most ₹1,104.67.'), findsOneWidget);
    expect(find.textContaining('You pay at most ₹1,104.67'), findsOneWidget);
  });

  testWidgets('changing preparation asks the server again for that preparation', (tester) async {
    await pump(tester);

    final fillet = find.text('Boneless fillet · +₹80/kg');
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
        'quantity.value': [FieldError(code: 'QUANTITY_STEP_INVALID', message: 'Quantity must be a multiple of 250 grams.')],
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

  test('fixture cart parses', () => expect(cart().lines.single, isA<CartLine>()));
}
