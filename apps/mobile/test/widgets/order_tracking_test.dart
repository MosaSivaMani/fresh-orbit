import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/models/orders.dart';
import 'package:fresh_orbit_app/features/orders/order_tracking_screen.dart';

import '../support/fixtures.dart';
import '../support/harness.dart';

OrderTracking tracking(String status, {String? code}) => OrderTracking.fromJson({
  'orderNumber': 'FO-260001',
  'status': status,
  'timeline': [
    {'status': 'PLACED', 'at': '2026-10-24T04:00:00Z'},
    {'status': status, 'at': '2026-10-24T08:00:00Z'},
  ],
  'deliveryCode': code,
});

void main() {
  testWidgets('shows the delivery code only while out for delivery and hides refused actions', (tester) async {
    final deps = TestDeps(pincode: '600041', signedIn: true);
    deps.orders.onOrder = (_) async => order(
      status: 'OUT_FOR_DELIVERY',
      allowedActions: {'cancel': false, 'raiseComplaint': false, 'downloadInvoice': false, 'reorder': false},
    );
    deps.orders.onTracking = (_) async => tracking('OUT_FOR_DELIVERY', code: '4821');

    await pumpScreen(tester, deps, const OrderTrackingScreen(orderId: 'o1'));
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('deliveryCode')), findsOneWidget);
    expect(find.text('4821'), findsOneWidget);
    expect(find.byKey(const Key('cancelOrder')), findsNothing);
  });

  testWidgets('cancel uses the reason and an Idempotency-Key when the server allows it', (tester) async {
    final deps = TestDeps(pincode: '600041', signedIn: true);
    deps.orders.onOrder = (_) async => order();
    deps.orders.onTracking = (_) async => tracking('CONFIRMED');

    await pumpScreen(tester, deps, const OrderTrackingScreen(orderId: 'o1'));
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('deliveryCode')), findsNothing);
    await tester.tap(find.byKey(const Key('cancelOrder')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Wrong address'));
    await tester.pump();
    await tester.tap(find.widgetWithText(FilledButton, 'Cancel order'));
    await tester.pumpAndSettle();

    expect(deps.orders.cancelled, hasLength(1));
    final parts = deps.orders.cancelled.single.split(':');
    expect(parts[0], 'o1');
    expect(parts[1], 'WRONG_ADDRESS');
    expect(parts[2], isNotEmpty);
  });
}
