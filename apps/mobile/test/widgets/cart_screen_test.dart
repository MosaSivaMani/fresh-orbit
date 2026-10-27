import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/api_error.dart';
import 'package:fresh_orbit_app/api/models/cart.dart';
import 'package:fresh_orbit_app/features/cart/cart_screen.dart';

import '../support/fixtures.dart';
import '../support/harness.dart';

void main() {
  testWidgets('renders server line prices, availability, totals and enables checkout', (tester) async {
    final deps = TestDeps(pincode: '600041');
    deps.cart.current = Cart.fromJson(
      cartJson(
        lines: [cartLineJson(availability: 'LOW_STOCK', availabilityMessage: 'Only 1.5 kg left today.')],
      ),
    );
    await pumpScreen(tester, deps, const CartScreen());
    await tester.pumpAndSettle();

    expect(find.text('Estimated'), findsOneWidget);
    expect(find.text('₹669.50'), findsOneWidget);
    expect(find.text('At most ₹736.45 for up to 550 g'), findsOneWidget);
    expect(find.text('Only 1.5 kg left today.'), findsOneWidget);
    expect(tester.widget<Text>(find.byKey(const Key('estimatedTotal'))).data, '₹708.50');
    expect(tester.widget<Text>(find.byKey(const Key('maxTotal'))).data, '₹775.45');
    expect(find.text('₹39'), findsOneWidget); // delivery fee from the server
    final checkout = find.byKey(const Key('checkoutButton'));
    expect(find.descendant(of: checkout, matching: find.text('Checkout')), findsOneWidget);
    expect(find.descendant(of: checkout, matching: find.text('₹708.50')), findsOneWidget);

    await tester.tap(checkout);
    await tester.pumpAndSettle();
    expect(find.text('route:/checkout'), findsOneWidget);
  });

  testWidgets('server blockers are shown and checkout stays disabled', (tester) async {
    final deps = TestDeps(pincode: '600041');
    deps.cart.current = Cart.fromJson(
      cartJson(
        canCheckout: false,
        blockers: [
          {'code': 'MIN_ORDER_NOT_MET', 'message': 'Add ₹100 more to reach the minimum order.'},
        ],
      ),
    );
    await pumpScreen(tester, deps, const CartScreen());
    await tester.pumpAndSettle();

    expect(find.text('Add ₹100 more to reach the minimum order.'), findsOneWidget);
    final button = tester.widget<FilledButton>(
      find.descendant(of: find.byKey(const Key('checkoutButton')), matching: find.byType(FilledButton)),
    );
    expect(button.onPressed, isNull);
  });

  testWidgets('removing a line sends it to the server and shows the returned (empty) cart', (tester) async {
    final deps = TestDeps(pincode: '600041');
    deps.cart.current = cart();
    await pumpScreen(tester, deps, const CartScreen());
    await tester.pumpAndSettle();

    await tester.tap(find.byTooltip('Remove Seer Fish (Vanjaram)'));
    await tester.pumpAndSettle();

    expect(deps.cart.calls, contains('remove:line1'));
    expect(find.text('Nothing in here yet'), findsOneWidget);
    expect(find.byKey(const Key('checkoutButton')), findsNothing);
  });

  testWidgets('change cut: lists the server cuts for the line size and sends the chosen one', (tester) async {
    final deps = TestDeps(pincode: '600041');
    deps.cart.current = cart();
    deps.catalog.onProduct = (_, _) async => productDetail();
    await pumpScreen(tester, deps, const CartScreen());
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('pageHeader')), findsOneWidget);
    await tester.tap(find.byKey(const ValueKey('changeCut-line1')));
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('changeCutSheet')), findsOneWidget);
    expect(find.byKey(const ValueKey('cut-WHOLE')), findsOneWidget);
    expect(find.byKey(const ValueKey('cut-STEAK')), findsOneWidget);
    await tester.tap(find.byKey(const ValueKey('cut-FILLET')));
    await tester.pumpAndSettle();

    expect(deps.cart.calls, contains('changePreparation'));
    expect(find.byKey(const Key('changeCutSheet')), findsNothing);
  });

  testWidgets('coupon: applied by the server, refusal shown at the field', (tester) async {
    final deps = TestDeps(pincode: '600041', signedIn: true);
    deps.cart.current = cart();
    await pumpScreen(tester, deps, const CartScreen());
    await tester.pumpAndSettle();

    deps.cart.failNext = const ApiError(
      status: 422,
      code: 'COUPON_INVALID',
      errors: {
        'code': [FieldError(code: 'COUPON_INVALID', message: 'The code is not valid for this cart.')],
      },
    );
    await tester.enterText(find.widgetWithText(TextField, 'Coupon code'), 'nope');
    await tester.tap(find.text('Apply'));
    await tester.pumpAndSettle();
    expect(find.text('The code is not valid for this cart.'), findsOneWidget);

    deps.cart.afterWrite = () =>
        cart(coupon: {'code': 'FRESH100', 'status': 'APPLIED', 'discountPaise': 10000, 'message': null});
    await tester.enterText(find.widgetWithText(TextField, 'Coupon code'), 'fresh100');
    await tester.tap(find.text('Apply'));
    await tester.pumpAndSettle();

    expect(deps.cart.calls, contains('applyCoupon:FRESH100'));
    expect(find.text('Applied: you save ₹100.'), findsOneWidget);
  });

  testWidgets('view offers lists the server coupons for the area and applies the chosen one', (tester) async {
    final deps = TestDeps(pincode: '600041', signedIn: true);
    deps.cart.current = cart();
    deps.cart.couponList = [
      VisibleCoupon.fromJson({
        'code': 'FRESH100',
        'description': 'Rs 100 off your order',
        'minOrderPaise': 49900,
        'validTo': null,
        'firstOrderOnly': false,
      }),
    ];
    await pumpScreen(tester, deps, const CartScreen());
    await tester.pumpAndSettle();

    await tester.ensureVisible(find.byKey(const Key('viewOffers')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('viewOffers')));
    await tester.pumpAndSettle();

    expect(deps.cart.calls, contains('coupons'));
    final offer = find.byKey(const ValueKey('offer-FRESH100'));
    expect(offer, findsOneWidget);
    deps.cart.afterWrite = () =>
        cart(coupon: {'code': 'FRESH100', 'status': 'APPLIED', 'discountPaise': 10000, 'message': null});
    await tester.tap(find.descendant(of: offer, matching: find.byType(TextButton)));
    await tester.pumpAndSettle();

    expect(deps.cart.calls, contains('applyCoupon:FRESH100'));
    expect(find.byKey(const Key('offersSheet')), findsNothing);
  });
}
