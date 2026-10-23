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
      cartJson(lines: [cartLineJson(availability: 'LOW_STOCK', availabilityMessage: 'Only 1.5 kg left today.')]),
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
    expect(find.text('Checkout · ₹708.50'), findsOneWidget);

    await tester.tap(find.text('Checkout · ₹708.50'));
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
    expect(find.text('Your cart is empty'), findsOneWidget);
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

    deps.cart.afterWrite = () => cart(
          coupon: {'code': 'FRESH100', 'status': 'APPLIED', 'discountPaise': 10000, 'message': null},
        );
    await tester.enterText(find.widgetWithText(TextField, 'Coupon code'), 'fresh100');
    await tester.tap(find.text('Apply'));
    await tester.pumpAndSettle();

    expect(deps.cart.calls, contains('applyCoupon:FRESH100'));
    expect(find.text('Applied: you save ₹100.'), findsOneWidget);
  });
}
