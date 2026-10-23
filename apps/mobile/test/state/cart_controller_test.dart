import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/api_error.dart';
import 'package:fresh_orbit_app/api/models/cart.dart';
import 'package:fresh_orbit_app/api/models/common.dart';
import 'package:fresh_orbit_app/state/cart_state.dart';

import '../support/fixtures.dart';
import '../support/harness.dart';

void main() {
  test('state is always the cart the server returned', () async {
    final deps = TestDeps(pincode: '600041');
    await deps.load();
    deps.cart.current = cart();
    final container = deps.container();

    final first = await container.read(cartProvider.future);
    expect(first.totals!.estimatedTotalPaise, 70850);

    deps.cart.afterWrite = () => cart(lines: [cartLineJson(quantity: 750, estimated: 100425)], estimatedTotal: 104325);
    await container.read(cartProvider.notifier).changeQuantity('line1', const Quantity(750, 'GRAM'));

    final now = container.read(cartProvider).value!;
    expect(deps.cart.quantities.single, const Quantity(750, 'GRAM'));
    expect(now.lines.single.estimatedLineTotalPaise, 100425);
    expect(now.totals!.estimatedTotalPaise, 104325);
    expect(container.read(cartLineCountProvider), 1);
  });

  test('a refused write keeps the last server cart and rethrows the ApiError', () async {
    final deps = TestDeps(pincode: '600041');
    await deps.load();
    deps.cart.current = cart();
    final container = deps.container();
    await container.read(cartProvider.future);

    deps.cart.failNext = const ApiError(status: 422, code: 'OUT_OF_STOCK');
    await expectLater(
      container
          .read(cartProvider.notifier)
          .add(const AddCartItem(variantId: 'v1', preparationCode: 'STEAK', quantity: Quantity(5000, 'GRAM'))),
      throwsA(isA<ApiError>().having((e) => e.code, 'code', 'OUT_OF_STOCK')),
    );

    expect(container.read(cartProvider).value!.totals!.estimatedTotalPaise, 70850);
  });

  test('a guest cart token issued in the body is stored for the next request', () async {
    final deps = TestDeps(pincode: '600041');
    await deps.load();
    final container = deps.container();
    await container.read(cartProvider.future);

    deps.cart.afterWrite = () => Cart.fromJson({...cartJson(), 'cartToken': 'gct_new'});
    await container
        .read(cartProvider.notifier)
        .add(const AddCartItem(variantId: 'v1', preparationCode: 'STEAK', quantity: Quantity(500, 'GRAM')));

    expect(deps.tokens.guestCartToken, 'gct_new');
  });

  test('CART_NOT_FOUND for a stale guest token becomes an empty cart and drops the token', () async {
    final deps = TestDeps();
    await deps.load();
    await deps.tokens.saveGuestCartToken('gct_old');
    deps.cart.failGet = const ApiError(status: 404, code: 'CART_NOT_FOUND');
    final container = deps.container();

    final result = await container.read(cartProvider.future);

    expect(result.isEmpty, isTrue);
    expect(deps.tokens.guestCartToken, isNull);
  });

  test('CART_LOCATION_CHANGED is rethrown so the screen can confirm, then force moves the cart', () async {
    final deps = TestDeps(pincode: '600041');
    await deps.load();
    deps.cart.current = cart();
    final container = deps.container();
    await container.read(cartProvider.future);

    deps.cart.failNext = const ApiError(status: 409, code: 'CART_LOCATION_CHANGED');
    await expectLater(
      container.read(cartProvider.notifier).changeLocation('600099'),
      throwsA(isA<ApiError>().having((e) => e.code, 'code', 'CART_LOCATION_CHANGED')),
    );
    await container.read(cartProvider.notifier).changeLocation('600099', force: true);

    expect(deps.cart.calls, containsAllInOrder(['setLocation:600099:false', 'setLocation:600099:true']));
  });
}
