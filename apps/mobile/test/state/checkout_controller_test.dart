import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/api_error.dart';
import 'package:fresh_orbit_app/state/checkout_state.dart';
import 'package:fresh_orbit_app/state/location_state.dart';

import '../support/fixtures.dart';
import '../support/harness.dart';

void main() {
  late TestDeps deps;
  late ProviderContainer container;

  CheckoutController controller() => container.read(checkoutProvider.notifier);
  CheckoutState state() => container.read(checkoutProvider);

  Future<void> choose() async {
    await controller().selectAddress('a1', pincode: '600041');
    await controller().selectSlot('s1');
  }

  setUp(() async {
    deps = TestDeps(pincode: '600041', signedIn: true);
    await deps.load();
    deps.orders.onPreview = (_) async => preview();
    container = deps.container();
    container.listen(checkoutProvider, (_, _) {}); // keep the auto-dispose provider alive
  });

  test('preview runs once address and slot are chosen; place sends the preview expected total', () async {
    deps.orders.onPlace = (_, _) async => order();

    await controller().selectAddress('a1', pincode: '600041');
    expect(deps.orders.previews, isEmpty);
    await controller().selectSlot('s1');

    expect(deps.orders.previews.single.deliverySlotId, 's1');
    expect(state().canPlace, isTrue);

    final placed = await controller().placeOrder();

    expect(placed!.orderNumber, 'FO-260001');
    final call = deps.orders.placed.single;
    expect(call.request.expectedTotalPaise, 70850);
    expect(call.request.selection.paymentMethod, 'COD');
    expect(call.key, isNotEmpty);
    expect(state().placedOrder, isNotNull);
    expect(controller().idempotencyKey, isNull);
  });

  test('an unknown outcome (network error) retries with the same Idempotency-Key', () async {
    var attempt = 0;
    deps.orders.onPlace = (_, _) async {
      attempt++;
      if (attempt == 1) throw const ApiError(status: 0, code: ApiError.networkError);
      return order();
    };
    await choose();

    expect(await controller().placeOrder(), isNull);
    expect(state().placeError!.isTransient, isTrue);
    expect(await controller().placeOrder(), isNotNull);

    expect(deps.orders.placed, hasLength(2));
    expect(deps.orders.placed[0].key, deps.orders.placed[1].key);
  });

  test('a definitive refusal gets a new key next time and refreshes the preview blockers', () async {
    var attempt = 0;
    deps.orders.onPlace = (_, _) async {
      attempt++;
      if (attempt == 1) throw const ApiError(status: 422, code: 'SLOT_FULL');
      return order();
    };
    await choose();
    final previewsBefore = deps.orders.previews.length;

    expect(await controller().placeOrder(), isNull);
    expect(state().placeError!.code, 'SLOT_FULL');
    expect(deps.orders.previews.length, previewsBefore + 1);

    await controller().placeOrder();
    expect(deps.orders.placed[0].key, isNot(deps.orders.placed[1].key));
  });

  test('PRICE_CHANGED shows the server preview; accepting places with its expected total and a new key', () async {
    var attempt = 0;
    deps.orders.onPlace = (r, _) async {
      attempt++;
      if (attempt == 1) {
        throw ApiError.fromProblem(409, {
          'code': 'PRICE_CHANGED',
          'status': 409,
          'preview': previewJson(expectedTotal: 72000, maxTotal: 79000),
        });
      }
      return order();
    };
    await choose();

    expect(await controller().placeOrder(), isNull);
    expect(state().priceChanged!.expectedTotalPaise, 72000);
    expect(state().placing, isFalse);

    final placed = await controller().acceptNewPrice();

    expect(placed, isNotNull);
    expect(deps.orders.placed.map((c) => c.request.expectedTotalPaise), [70850, 72000]);
    expect(deps.orders.placed[0].key, isNot(deps.orders.placed[1].key));
    expect(state().priceChanged, isNull);
  });

  test('declining a price change keeps the new preview and places nothing', () async {
    deps.orders.onPlace = (_, _) async =>
        throw ApiError.fromProblem(409, {'code': 'PRICE_CHANGED', 'preview': previewJson(expectedTotal: 72000)});
    await choose();
    await controller().placeOrder();

    controller().declineNewPrice();

    expect(state().priceChanged, isNull);
    expect(state().preview!.value!.expectedTotalPaise, 72000);
    expect(deps.orders.placed, hasLength(1));
  });

  test('blockers in the preview disable placing', () async {
    deps.orders.onPreview = (_) async => preview(
      canPlaceOrder: false,
      blockers: [
        {'code': 'MIN_ORDER_NOT_MET', 'lineId': null, 'message': 'Add ₹100 more to place this order.'},
      ],
    );
    await choose();

    expect(state().canPlace, isFalse);
    expect(await controller().placeOrder(), isNull);
    expect(deps.orders.placed, isEmpty);
    expect(state().preview!.value!.blockers.single.code, 'MIN_ORDER_NOT_MET');
  });

  test('changing the payment method starts a new action (new key) and re-prices', () async {
    var attempt = 0;
    deps.orders.onPlace = (_, _) async {
      attempt++;
      if (attempt == 1) throw const ApiError(status: 503, code: 'SERVICE_UNAVAILABLE');
      return order();
    };
    await choose();
    await controller().placeOrder();
    final keyAfterFailure = controller().idempotencyKey;
    expect(keyAfterFailure, isNotNull);

    await controller().selectPayment('UPI_ON_DELIVERY');
    expect(controller().idempotencyKey, isNull);
    expect(deps.orders.previews.last.paymentMethod, 'UPI_ON_DELIVERY');

    await controller().placeOrder();
    expect(deps.orders.placed.last.key, isNot(keyAfterFailure));
  });

  test('a different address pincode clears the chosen slot', () async {
    await choose();
    await controller().selectAddress('a2', pincode: '600099');

    expect(state().slotId, isNull);
    expect(state().preview, isNull);
  });

  test('a transient failure keeps the key only while the body is the same; a re-priced preview gets a new key', () async {
    var attempt = 0;
    deps.orders.onPlace = (_, _) async {
      attempt++;
      if (attempt == 1) throw const ApiError(status: 0, code: ApiError.networkError);
      return order();
    };
    await choose();
    await controller().placeOrder();
    final firstKey = deps.orders.placed.single.key;

    // The customer pulls a fresh preview and the server's expected total moved: the body differs (D269).
    deps.orders.onPreview = (_) async => preview(expectedTotal: 71900);
    await controller().refreshPreview();
    await controller().placeOrder();

    expect(deps.orders.placed, hasLength(2));
    expect(deps.orders.placed[1].request.expectedTotalPaise, 71900);
    expect(deps.orders.placed[1].key, isNot(firstKey));
  });

  test('changing the delivery note after an unknown outcome sends a new key', () async {
    var attempt = 0;
    deps.orders.onPlace = (_, _) async {
      attempt++;
      if (attempt == 1) throw const ApiError(status: 503, code: 'SERVICE_UNAVAILABLE');
      return order();
    };
    await choose();
    await controller().placeOrder();
    controller().setNotes('Ring the bell');
    await controller().placeOrder();

    expect(deps.orders.placed[1].request.customerNotes, 'Ring the bell');
    expect(deps.orders.placed[1].key, isNot(deps.orders.placed[0].key));
  });

  test('a slot refusal reads the slots again so the customer sees what is open', () async {
    var slotReads = 0;
    deps.location.onSlots = (_) async {
      slotReads++;
      return slots();
    };
    container.listen(deliverySlotsProvider('600041'), (_, _) {});
    await container.read(deliverySlotsProvider('600041').future);
    deps.orders.onPlace = (_, _) async => throw const ApiError(status: 422, code: 'SLOT_FULL');
    await choose();

    await controller().placeOrder();
    await container.read(deliverySlotsProvider('600041').future);

    expect(state().placeError!.code, 'SLOT_FULL');
    expect(slotReads, 2);
  });

  test('editing the chosen address keeps the slot for the same pincode and prices again', () async {
    await choose();
    final previewsBefore = deps.orders.previews.length;

    await controller().addressEdited('a1', pincode: '600041');

    expect(state().slotId, 's1');
    expect(deps.orders.previews.length, previewsBefore + 1);
  });

  test('editing the chosen address to another pincode clears the slot', () async {
    await choose();

    await controller().addressEdited('a1', pincode: '600099');

    expect(state().addressPincode, '600099');
    expect(state().slotId, isNull);
    expect(state().preview, isNull);
  });

  test('editing another address leaves the choice alone', () async {
    await choose();
    final previewsBefore = deps.orders.previews.length;

    await controller().addressEdited('a2', pincode: '600099');

    expect(state().addressPincode, '600041');
    expect(deps.orders.previews.length, previewsBefore);
  });
}
