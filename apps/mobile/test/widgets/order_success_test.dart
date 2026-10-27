import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/features/checkout/order_success_screen.dart';
import 'package:fresh_theme/fresh_theme.dart';

import '../support/fixtures.dart';
import '../support/harness.dart';

void main() {
  testWidgets('order success shows the server order on a receipt with the ORDER_PLACED character', (tester) async {
    final deps = TestDeps(pincode: '600041', signedIn: true);
    deps.orders.onOrder = (_) async => order();

    await pumpScreen(tester, deps, const OrderSuccessScreen(orderId: 'o1'), otherPaths: ['/orders/o1']);
    await tester.pumpAndSettle();

    expect(find.byKey(const ValueKey('illustration-placeholder-ORDER_PLACED')), findsOneWidget);
    final receipt = find.byType(ReceiptCard);
    expect(receipt, findsOneWidget);
    Finder inReceipt(String text) => find.descendant(of: receipt, matching: find.text(text));
    expect(inReceipt('Order FO-260001'), findsOneWidget);
    expect(inReceipt('Cash on delivery'), findsOneWidget);
    // The server's estimate (70850 paise), formatted, never recomputed from the lines.
    expect(inReceipt('₹708.50'), findsOneWidget);
  });
}
