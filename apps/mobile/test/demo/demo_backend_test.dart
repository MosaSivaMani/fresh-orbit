import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/api_error.dart';
import 'package:fresh_orbit_app/api/models/catalog.dart';
import 'package:fresh_orbit_app/demo/demo_backend.dart';
import 'package:fresh_orbit_app/demo/demo_data.dart';

void main() {
  test('demo mode is off unless built with DEMO=true', () {
    expect(demoMode, isFalse);
  });

  test('every demo fixture parses with the app models', () async {
    final b = DemoBackend();
    expect((await b.catalog.home()).featuredProducts, isNotEmpty);
    expect((await b.catalog.products(const ProductQuery(category: 'crabs'))).items.single.name, 'Mud Crab');
    expect((await b.catalog.product('squid')).variants.single.availability!.status, 'OUT_OF_STOCK');
    expect((await b.cart.get()).lines, hasLength(2));
    for (final id in demoOrderStatuses.keys) {
      expect((await b.orders.order(id)).status, demoOrderStatuses[id]);
      expect((await b.orders.tracking(id)).timeline, isNotEmpty);
    }
    expect((await b.orders.orders()).items, hasLength(demoOrderStatuses.length));
    expect((await b.account.wishlist()).every((e) => e.product != null), isTrue);
    expect((await b.account.notifications()).unreadCount, 1);
    expect((await b.locations.serviceability(demoUnservedPincode)).serviceable, isFalse);
    expect((await b.locations.serviceability('600001')).serviceable, isTrue);
  });

  test('the empty switch empties every list and the offline switch fails with NETWORK_ERROR', () async {
    final b = DemoBackend();
    b.switches.empty.value = true;
    expect((await b.cart.get()).isEmpty, isTrue);
    expect((await b.orders.orders()).items, isEmpty);
    expect(await b.account.wishlist(), isEmpty);
    expect(await b.account.addresses(), isEmpty);

    b.switches.offline.value = true;
    await expectLater(b.catalog.home(), throwsA(isA<ApiError>().having((e) => e.code, 'code', ApiError.networkError)));
  });

  test('an unknown order is a 404', () async {
    await expectLater(
      DemoBackend().orders.order('nope'),
      throwsA(isA<ApiError>().having((e) => e.status, 'status', 404)),
    );
  });
}
