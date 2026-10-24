import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/api_error.dart';
import 'package:fresh_orbit_app/features/checkout/checkout_screen.dart';
import 'package:fresh_orbit_app/widgets/bold.dart';

import '../support/fakes.dart';
import '../support/fixtures.dart';
import '../support/harness.dart';

void main() {
  late TestDeps deps;
  late int attempts;

  setUp(() {
    deps = TestDeps(pincode: '600041', signedIn: true);
    deps.account.addressList = [address()];
    deps.location.onSlots = (_) async => slots();
    deps.orders.onPreview = (_) async => preview();
    attempts = 0;
    deps.orders.onPlace = (_, _) async {
      attempts++;
      if (attempts == 1) {
        throw ApiError.fromProblem(409, {
          'code': 'PRICE_CHANGED',
          'status': 409,
          'traceId': 'trace-pc',
          'preview': previewJson(expectedTotal: 72000, maxTotal: 79000),
        });
      }
      return order();
    };
  });

  Finder placeOrderAmount(String amount) =>
      find.descendant(of: find.byKey(const Key('placeOrder')), matching: find.text(amount));

  Future<void> openAndChoose(WidgetTester tester) async {
    await pumpScreen(tester, deps, const CheckoutScreen());
    await tester.pumpAndSettle(); // default address is selected automatically
    await tester.tap(find.text('2:00 pm – 4:00 pm'));
    await tester.pumpAndSettle();
  }

  testWidgets('full slots are shown but cannot be chosen', (tester) async {
    await pumpScreen(tester, deps, const CheckoutScreen());
    await tester.pumpAndSettle();

    expect(find.text('Full'), findsOneWidget);
    final full = tester.widget<SlotButton>(find.widgetWithText(SlotButton, '6:00 pm – 8:00 pm'));
    expect(full.onTap, isNull);
  });

  testWidgets('PRICE_CHANGED: dialog shows old and new server totals; accepting places the order', (tester) async {
    await openAndChoose(tester);
    expect(placeOrderAmount('₹708.50'), findsOneWidget);

    await tester.tap(find.byKey(const Key('placeOrder')));
    await tester.pumpAndSettle();

    expect(find.text('Prices have changed'), findsOneWidget);
    expect(tester.widget<Text>(find.byKey(const Key('oldTotal'))).data, 'Before: ₹708.50');
    expect(tester.widget<Text>(find.byKey(const Key('newTotal'))).data, 'Now: ₹720');
    expect(find.text('At most ₹790'), findsOneWidget);

    await tester.tap(find.text('Accept new price'));
    await tester.pumpAndSettle();

    expect(deps.orders.placed.map((c) => c.request.expectedTotalPaise), [70850, 72000]);
    expect(deps.orders.placed[0].key, isNot(deps.orders.placed[1].key));
    expect(find.text('route:/order-success/o1'), findsOneWidget);
  });

  testWidgets('PRICE_CHANGED: going back keeps the customer on checkout with the new total', (tester) async {
    await openAndChoose(tester);

    await tester.tap(find.byKey(const Key('placeOrder')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Go back'));
    await tester.pumpAndSettle();

    expect(find.text('Prices have changed'), findsNothing);
    expect(placeOrderAmount('₹720'), findsOneWidget);
    expect(deps.orders.placed, hasLength(1));
  });

  testWidgets('preview blockers are listed and placing is disabled', (tester) async {
    deps.orders.onPreview = (_) async => preview(
      canPlaceOrder: false,
      blockers: [
        {'code': 'SLOT_FULL', 'lineId': null, 'message': 'This slot is full. Choose another slot.'},
      ],
    );
    await openAndChoose(tester);

    expect(find.text('This slot is full. Choose another slot.'), findsOneWidget);
    final button = tester.widget<FilledButton>(
      find.descendant(of: find.byKey(const Key('placeOrder')), matching: find.byType(FilledButton)),
    );
    expect(button.onPressed, isNull);
  });
}
