import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/api_error.dart';
import 'package:fresh_orbit_app/api/models/orders.dart';
import 'package:fresh_orbit_app/core/photo_picker.dart';
import 'package:fresh_orbit_app/features/orders/order_tracking_screen.dart';
import 'package:fresh_orbit_app/state/orders_state.dart';

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
    // The tracking illustration follows the server status.
    expect(find.byKey(const ValueKey('illustration-placeholder-ORDER_ON_THE_WAY')), findsOneWidget);
  });

  testWidgets('a delivered order shows the delivered illustration', (tester) async {
    final deps = TestDeps(pincode: '600041', signedIn: true);
    deps.orders.onOrder = (_) async => order(
      status: 'DELIVERED',
      allowedActions: {'cancel': false, 'raiseComplaint': false, 'downloadInvoice': false, 'reorder': false},
    );
    deps.orders.onTracking = (_) async => tracking('DELIVERED');

    await pumpScreen(tester, deps, const OrderTrackingScreen(orderId: 'o1'));
    await tester.pumpAndSettle();

    expect(find.byKey(const ValueKey('illustration-placeholder-ORDER_DELIVERED')), findsOneWidget);
    expect(find.byKey(const ValueKey('illustration-placeholder-ORDER_ON_THE_WAY')), findsNothing);
  });

  testWidgets('a proposed substitution can be accepted; the server answer replaces the order', (tester) async {
    final deps = TestDeps(pincode: '600041', signedIn: true);
    Map<String, Object?> withSubstitution(String status) => {
      ...orderJson(status: 'PREPARING'),
      'substitutions': [
        {
          'id': 's1',
          'orderItemId': 'oi1',
          'proposedName': 'King Fish (Surmai)',
          'proposedQuantity': {'value': 500, 'unit': 'GRAM'},
          'priceDifferencePaise': -5000,
          'status': status,
          'expiresAt': '2026-10-24T09:00:00Z',
          'respondedAt': null,
          'newOrderItemId': null,
        },
      ],
    };
    var answered = false;
    deps.orders.onOrder = (_) async => OrderDetail.fromJson(withSubstitution(answered ? 'ACCEPTED' : 'PROPOSED'));
    deps.orders.onTracking = (_) async => tracking('PREPARING');
    deps.orders.onAnswer = (_, _) {
      answered = true;
      return OrderDetail.fromJson(withSubstitution('ACCEPTED'));
    };

    await pumpScreen(tester, deps, const OrderTrackingScreen(orderId: 'o1'));
    await tester.pumpAndSettle();

    expect(find.byKey(const ValueKey('substitution-s1')), findsOneWidget);
    await tester.ensureVisible(find.byKey(const ValueKey('acceptSubstitution-s1')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const ValueKey('acceptSubstitution-s1')));
    await tester.pumpAndSettle();

    expect(deps.orders.answers, ['accept:s1']);
    expect(find.byKey(const ValueKey('acceptSubstitution-s1')), findsNothing);
  });

  testWidgets('report a problem sends the complaint, then each picked photo to it', (tester) async {
    final deps = TestDeps(pincode: '600041', signedIn: true);
    deps.orders.onOrder = (_) async => order(
      status: 'DELIVERED',
      allowedActions: {'cancel': false, 'raiseComplaint': true, 'downloadInvoice': false, 'reorder': false},
    );
    deps.orders.onTracking = (_) async => tracking('DELIVERED');
    await pumpScreen(
      tester,
      deps,
      const OrderTrackingScreen(orderId: 'o1'),
      overrides: [
        photoPickerProvider.overrideWithValue(() async => ComplaintPhotoUpload(bytes: utf8.encode('not a picture'), fileName: 'fish.png')),
      ],
    );
    await tester.pumpAndSettle();

    await tester.ensureVisible(find.text('Report a problem'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Report a problem'));
    await tester.pumpAndSettle();
    await tester.tap(find.byType(DropdownButtonFormField<String>));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Seer Fish (Vanjaram)').last);
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField).last, 'The fish was not fresh.');
    await tester.tap(find.byKey(const Key('addComplaintPhoto')));
    await tester.pumpAndSettle();
    expect(find.byTooltip('Remove photo 1'), findsOneWidget);

    await tester.ensureVisible(find.text('Send'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Send'));
    await tester.pumpAndSettle();

    expect(deps.orders.complaintCalls, ['raise:QUALITY', 'photo:c1:fish.png']);
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

  group('report a problem (D266)', () {
    late TestDeps deps;

    setUp(() {
      deps = TestDeps(pincode: '600041', signedIn: true);
      deps.orders.onOrder = (_) async => order(
        status: 'DELIVERED',
        allowedActions: {'cancel': false, 'raiseComplaint': true, 'downloadInvoice': true, 'reorder': false},
      );
      deps.orders.onTracking = (_) async => tracking('DELIVERED');
    });

    Future<void> openSheet(WidgetTester tester) async {
      await pumpScreen(
        tester,
        deps,
        const OrderTrackingScreen(orderId: 'o1'),
        overrides: [
          photoPickerProvider.overrideWithValue(
            () async => ComplaintPhotoUpload(bytes: utf8.encode('not a picture'), fileName: 'fish.png'),
          ),
        ],
      );
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.byKey(const Key('reportProblem')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('reportProblem')));
      await tester.pumpAndSettle();
    }

    Future<void> chooseItem(WidgetTester tester) async {
      await tester.tap(find.byKey(const Key('complaintItem')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Seer Fish (Vanjaram)').last);
      await tester.pumpAndSettle();
    }

    Future<void> send(WidgetTester tester) async {
      await tester.ensureVisible(find.byKey(const Key('sendComplaint')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('sendComplaint')));
      await tester.pumpAndSettle();
    }

    testWidgets('"Delivered late" still names the item, and a description under 10 characters is not sent', (
      tester,
    ) async {
      await openSheet(tester);
      await tester.tap(find.byKey(const ValueKey('complaintKind-LATE')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('complaintItem')), findsOneWidget);

      await tester.enterText(find.byKey(const Key('complaintDescription')), 'Too late');
      await send(tester);
      expect(deps.orders.complaintCalls, isEmpty);

      await chooseItem(tester);
      await send(tester);
      expect(deps.orders.complaintCalls, isEmpty); // still under 10 characters

      await tester.enterText(find.byKey(const Key('complaintDescription')), 'It came two hours late.');
      await send(tester);
      expect(deps.orders.complaintCalls, ['raise:LATE']);
    });

    testWidgets('a failed photo is sent again without raising the complaint twice', (tester) async {
      deps.orders.failPhotoOnce = const ApiError(status: 413, code: 'PAYLOAD_TOO_LARGE');
      await openSheet(tester);
      await chooseItem(tester);
      await tester.enterText(find.byKey(const Key('complaintDescription')), 'The fish was not fresh.');
      await tester.tap(find.byKey(const Key('addComplaintPhoto')));
      await tester.pumpAndSettle();

      await send(tester);
      expect(deps.orders.complaintCalls, ['raise:QUALITY', 'photoFailed:c1:fish.png']);
      expect(find.byKey(const Key('complaintRaised')), findsOneWidget);
      expect(find.byKey(const Key('complaintError')), findsOneWidget);

      await send(tester);
      expect(deps.orders.complaintCalls, ['raise:QUALITY', 'photoFailed:c1:fish.png', 'photo:c1:fish.png']);
      expect(find.byKey(const Key('sendComplaint')), findsNothing); // the sheet closed
    });

    testWidgets('an invoice that is not issued yet is a calm note, not an error', (tester) async {
      deps.orders.onInvoice = () async => throw const ApiError(status: 404, code: 'INVOICE_NOT_ISSUED');
      await pumpScreen(tester, deps, const OrderTrackingScreen(orderId: 'o1'));
      await tester.pumpAndSettle();

      await tester.ensureVisible(find.byKey(const Key('orderInvoice')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('orderInvoice')));
      await tester.pumpAndSettle();

      expect(find.byType(SnackBar), findsOneWidget);
      // Not the error snack bar (that one shows the problem's own sentence).
      expect(find.text(const ApiError(status: 404, code: 'INVOICE_NOT_ISSUED').message), findsNothing);
    });
  });
}
