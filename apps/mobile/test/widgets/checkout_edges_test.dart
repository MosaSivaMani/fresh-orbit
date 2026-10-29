// Checkout edges (gap audit MO-1 to MO-5): cart at another store, empty-cart preview, Back while placing, the
// order-placed screen when its read fails, and only completable payment methods.

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/api_error.dart';
import 'package:fresh_orbit_app/api/models/checkout.dart';
import 'package:fresh_orbit_app/api/models/orders.dart';
import 'package:fresh_orbit_app/features/checkout/checkout_screen.dart';
import 'package:fresh_orbit_app/features/checkout/order_success_screen.dart';
import 'package:fresh_orbit_app/illustrations/illustration.dart';
import 'package:fresh_orbit_app/state/checkout_state.dart';

import '../support/fixtures.dart';
import '../support/harness.dart';

const _otherStore = {
  'code': 'CART_LOCATION_CHANGED',
  'field': null,
  'message': 'This address is served from another store; update your cart\'s delivery location first.',
};

void main() {
  setUp(() => IllustrationAssets.debugSetAvailable(const {}));
  tearDown(() => IllustrationAssets.debugSetAvailable(null));

  late TestDeps deps;

  setUp(() {
    deps = TestDeps(pincode: '600099', signedIn: true);
    deps.account.addressList = [address()];
    deps.location.onSlots = (_) async => slots();
  });

  Future<void> openAndChoose(WidgetTester tester) async {
    await pumpScreen(tester, deps, const CheckoutScreen());
    await tester.pumpAndSettle();
    await tester.tap(find.text('2:00 pm – 4:00 pm'));
    await tester.pumpAndSettle();
  }

  group('cart served by another store (MO-1)', () {
    testWidgets('"Deliver this cart to" moves the cart and prices again', (tester) async {
      var moved = false;
      deps.orders.onPreview = (_) async =>
          moved ? preview() : preview(canPlaceOrder: false, blockers: [_otherStore]);
      deps.cart.afterWrite = () {
        moved = true;
        return cart(lines: [cartLineJson()]);
      };
      await openAndChoose(tester);

      await tester.tap(find.byKey(const Key('moveCartToAddress')));
      await tester.pumpAndSettle();

      expect(deps.cart.calls, contains('setLocation:600041:false'));
      expect(deps.prefs.pincode, '600041');
      expect(find.byKey(const Key('moveCartToAddress')), findsNothing);
      expect(deps.orders.previews.length, greaterThanOrEqualTo(2));
    });

    testWidgets('items not sold there: the customer is asked, then the move is forced', (tester) async {
      var moved = false;
      deps.orders.onPreview = (_) async =>
          moved ? preview() : preview(canPlaceOrder: false, blockers: [_otherStore]);
      deps.cart.failNext = const ApiError(status: 409, code: 'CART_LOCATION_CHANGED');
      deps.cart.afterWrite = () {
        moved = true;
        return cart(lines: [cartLineJson()]);
      };
      await openAndChoose(tester);

      await tester.tap(find.byKey(const Key('moveCartToAddress')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('moveCartConfirm')), findsOneWidget);

      await tester.tap(find.text('Remove and continue'));
      await tester.pumpAndSettle();

      expect(deps.cart.calls, containsAllInOrder(['setLocation:600041:false', 'setLocation:600041:true']));
      expect(find.byKey(const Key('moveCartToAddress')), findsNothing);
    });
  });

  group('empty cart preview (MO-2)', () {
    Map<String, Object?> emptyPreviewJson() => {
      ...previewJson(canPlaceOrder: false, blockers: [
        {'code': 'CART_EMPTY', 'field': null, 'message': 'Your cart is empty.'},
      ]),
      'cartId': null,
      'slot': {'id': 's1', 'startAt': null, 'endAt': null, 'cutoffAt': null, 'status': 'NOT_AVAILABLE'},
      'lines': <Object?>[],
      'expectedTotalPaise': 0,
    };

    test('the server\'s empty-cart preview with a null slot window is read', () {
      final p = CheckoutPreview.fromJson(emptyPreviewJson());
      expect(p.slot!.startAt, isNull);
      expect(p.blockers.single.code, 'CART_EMPTY');
      expect(p.canPlaceOrder, isFalse);
    });

    testWidgets('the blocker is shown with a way back to the cart', (tester) async {
      deps.orders.onPreview = (_) async => CheckoutPreview.fromJson(emptyPreviewJson());
      await openAndChoose(tester);

      expect(find.byKey(const Key('backToCart')), findsOneWidget);
      await tester.tap(find.byKey(const Key('backToCart')));
      await tester.pumpAndSettle();
      expect(find.text('route:/cart'), findsOneWidget);
    });
  });

  group('placing the order (MO-3, MO-5)', () {
    testWidgets('Back while placing stays on checkout', (tester) async {
      deps.orders.onPreview = (_) async => preview();
      final answer = Completer<Never>();
      deps.orders.onPlace = (_, _) => answer.future;
      await openAndChoose(tester);

      await tester.tap(find.byKey(const Key('placeOrder')));
      await tester.pump();
      final navigator = tester.state<NavigatorState>(find.byType(Navigator).last);
      await navigator.maybePop();
      await tester.pump();

      expect(find.byType(CheckoutScreen), findsOneWidget);
      expect(find.byKey(const Key('placingNote')), findsOneWidget);
      answer.completeError(const ApiError(status: 0, code: ApiError.networkError));
      await tester.pumpAndSettle();
    });

    test('an unknown outcome keeps the key for the next checkout with the same order', () async {
      deps.orders.onPreview = (_) async => preview();
      var attempt = 0;
      deps.orders.onPlace = (_, _) async {
        attempt++;
        if (attempt == 1) throw const ApiError(status: 0, code: ApiError.networkError);
        return order();
      };
      await deps.load();
      final container = deps.container();
      addTearDown(container.dispose);

      Future<void> attemptOnce() async {
        final sub = container.listen(checkoutProvider, (_, _) {});
        final checkout = container.read(checkoutProvider.notifier);
        await checkout.selectAddress('a1', pincode: '600041');
        await checkout.selectSlot('s1');
        await checkout.placeOrder();
        sub.close();
        await Future<void>.delayed(Duration.zero); // the checkout controller is disposed, as when the screen closes
      }

      await attemptOnce();
      expect(container.read(pendingPlaceProvider), isNotNull);
      await attemptOnce();

      expect(deps.orders.placed, hasLength(2));
      expect(deps.orders.placed[1].key, deps.orders.placed[0].key);
      expect(container.read(pendingPlaceProvider), isNull);
      expect(container.read(placedOrdersProvider), contains(order().id));
    });

    testWidgets('only payment methods the app can complete are offered', (tester) async {
      deps.orders.onPreview = (_) async => CheckoutPreview.fromJson({
        ...previewJson(),
        'paymentMethods': [
          {'method': 'COD', 'enabled': true, 'reasonCode': null},
          {'method': 'UPI_ON_DELIVERY', 'enabled': true, 'reasonCode': null},
          {'method': 'ONLINE', 'enabled': true, 'reasonCode': null},
        ],
      });
      await openAndChoose(tester);

      final methods = tester.widgetList<RadioListTile<String>>(find.byType(RadioListTile<String>)).map((r) => r.value);
      expect(methods, containsAll(['COD', 'UPI_ON_DELIVERY']));
      expect(methods, isNot(contains('ONLINE')));
    });
  });

  testWidgets('order placed still shows the placed order when its read fails (MO-4)', (tester) async {
    deps.orders.onOrder = (_) async => throw const ApiError(status: 0, code: ApiError.networkError);
    final placed = order();
    await pumpScreen(
      tester,
      deps,
      OrderSuccessScreen(orderId: placed.id),
      otherPaths: ['/orders/${placed.id}'],
      overrides: [placedOrdersProvider.overrideWith(() => _Seeded({placed.id: placed}))],
    );
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('orderPlacedPage')), findsOneWidget);
  });
}

class _Seeded extends PlacedOrdersController {
  _Seeded(this.seed);

  final Map<String, OrderDetail> seed;

  @override
  Map<String, OrderDetail> build() => seed;
}
