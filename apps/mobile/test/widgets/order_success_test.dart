// Order placed (D288): the teal header with the ORDER_PLACED character and the server's status, the server's
// order on the ticket card, the delivery-code note, the two buttons and the sea floor (D289). It must fit a small
// phone (360 × 640) without overflow.

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/features/checkout/order_success_screen.dart';
import 'package:fresh_orbit_app/illustrations/illustration.dart';
import 'package:fresh_theme/fresh_theme.dart';

import '../support/fixtures.dart';
import '../support/harness.dart';

Future<void> _pumpPlaced(WidgetTester tester, {Size? screen}) async {
  final deps = TestDeps(pincode: '600041', signedIn: true);
  deps.orders.onOrder = (_) async => order();
  await pumpScreen(tester, deps, const OrderSuccessScreen(orderId: 'o1'), otherPaths: ['/orders/o1']);
  if (screen != null) tester.view.physicalSize = screen * 3;
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('order placed shows the server order on the ticket with the ORDER_PLACED character', (tester) async {
    await _pumpPlaced(tester);

    expect(find.byType(FreshCelebrationHeader), findsOneWidget);
    expect(find.byKey(const ValueKey('illustration-placeholder-ORDER_PLACED')), findsOneWidget);
    expect(find.bySemanticsLabel('Order placed'), findsOneWidget);
    // The server's status code, shown with its label.
    expect(find.widgetWithText(FreshStatusPill, 'Confirmed'), findsOneWidget);
    expect(find.text('Thank you! Your fresh seafood is on its way.'), findsOneWidget);

    final ticket = find.byType(FreshTicketCard);
    expect(ticket, findsOneWidget);
    Finder inTicket(String text) => find.descendant(of: ticket, matching: find.text(text));
    expect(inTicket('Order FO-260001'), findsOneWidget);
    // The server's slot in the store time zone, day over time.
    expect(inTicket('Fri, 23 Oct\n2:00 pm – 4:00 pm'), findsOneWidget);
    expect(inTicket('Cash on delivery'), findsOneWidget);
    expect(inTicket('12, 3rd Cross Street, Sample Nagar, Sample City, 600041'), findsOneWidget);
    // The server's estimate (70850 paise), formatted, never recomputed from the lines.
    expect(inTicket('₹708.50'), findsOneWidget);
    expect(inTicket('The final total is set when your order is weighed and packed.'), findsOneWidget);

    final note = find.byKey(const Key('deliveryCodeNote'));
    expect(note, findsOneWidget);
    final rider = tester.widget<Illustration>(find.descendant(of: note, matching: find.byType(Illustration)));
    expect(rider.slot, IllustrationSlot.homeCardRider);
    expect(find.byType(FreshSeaFloor), findsOneWidget);
  });

  testWidgets('Track order opens the order and Continue shopping goes home', (tester) async {
    await _pumpPlaced(tester);

    await tester.tap(find.byKey(const Key('trackOrder')));
    await tester.pumpAndSettle();
    expect(find.text('route:/orders/o1'), findsOneWidget);
  });

  testWidgets('Continue shopping goes home', (tester) async {
    await _pumpPlaced(tester);

    await tester.tap(find.byKey(const Key('continueShopping')));
    await tester.pumpAndSettle();
    expect(find.text('route:/home'), findsOneWidget);
  });

  testWidgets('fits 360 x 640 without overflow and ends with the sea floor', (tester) async {
    await _pumpPlaced(tester, screen: const Size(360, 640));

    expect(tester.takeException(), isNull);
    await tester.drag(find.byKey(const Key('orderPlacedPage')), const Offset(0, -3000));
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
    expect(find.byKey(const Key('continueShopping')).hitTestable(), findsOneWidget);
    // The band is the last thing on the page: its bottom is the screen's bottom.
    expect(tester.getBottomLeft(find.byType(FreshSeaFloor)).dy, closeTo(640, 0.5));
  });

  testWidgets('fits 360 x 640 with large text', (tester) async {
    tester.platformDispatcher.textScaleFactorTestValue = 1.3;
    addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
    await _pumpPlaced(tester, screen: const Size(360, 640));

    expect(tester.takeException(), isNull);
    await tester.drag(find.byKey(const Key('orderPlacedPage')), const Offset(0, -3000));
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
    expect(tester.getBottomLeft(find.byType(FreshSeaFloor)).dy, closeTo(640, 0.5));
  });
}
