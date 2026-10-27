// Request shapes the server accepts (docs/research/mobile-api-audit-2026-10.md): each test pins one request the
// audit found refused or ignored, or one response field the app now reads.

import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/api_client.dart';
import 'package:fresh_orbit_app/api/api_error.dart';
import 'package:fresh_orbit_app/api/models/catalog.dart';
import 'package:fresh_orbit_app/api/models/locations.dart';
import 'package:fresh_orbit_app/api/repositories.dart';
import 'package:fresh_orbit_app/api/token_store.dart';

import '../support/fake_adapter.dart';
import '../support/fixtures.dart';

ApiClient _api(FakeAdapter adapter) => ApiClient(
  createDio(
    baseUrl: 'https://api.test',
    tokens: TokenStore(MemorySecretStore()),
    pincode: () => '530017',
    adapter: adapter,
  ),
);

void main() {
  group('DELETE /cart', () {
    test('a cart that is already gone (404 CART_NOT_FOUND) clears to the empty cart (D237)', () async {
      final adapter = FakeAdapter((_) async => FakeReply(404, problem(404, 'CART_NOT_FOUND')));

      final cart = await HttpCartRepository(_api(adapter)).clear();

      expect(adapter.to('cart').single.method, 'DELETE');
      expect(cart.isEmpty, isTrue);
      expect(cart.id, isNull);
    });

    test('any other refusal still reaches the screen', () async {
      final adapter = FakeAdapter((_) async => FakeReply(409, problem(409, 'CONCURRENT_SAVE')));

      await expectLater(
        HttpCartRepository(_api(adapter)).clear(),
        throwsA(isA<ApiError>().having((e) => e.code, 'code', 'CONCURRENT_SAVE')),
      );
    });
  });

  test('GET /orders sends only page and pageSize, no sort the server ignores (D241)', () async {
    final adapter = FakeAdapter(
      (_) async => const FakeReply(200, {'items': <Object?>[], 'page': 2, 'pageSize': 20, 'totalCount': 0}),
    );

    await HttpOrderRepository(_api(adapter)).orders(page: 2);

    expect(adapter.to('orders').single.queryParameters, {'page': 2, 'pageSize': 20});
  });

  group('PR #25 fields', () {
    test('serviceability reads the city state name and the store cold storage (D209, D212)', () {
      final json = serviceableJson()
        ..['city'] = {'id': 'c1', 'code': 'CITY1', 'name': 'Sample City', 'stateCode': 'AP', 'stateName': 'Andhra'};

      final s = Serviceability.fromJson(json);

      expect(s.cityStateCode, 'AP');
      expect(s.cityStateName, 'Andhra');
      expect(s.hasColdStorage, isTrue);
      expect(notServiceable('999999').hasColdStorage, isNull);
    });

    test('product list items and detail variants read freshness (CAT-R25)', () {
      final item = ProductSummary.fromJson({
        'id': 'p1',
        'slug': 'seer',
        'name': 'Seer Fish',
        'localNames': <Object?>[],
        'locationRequired': false,
        'freshness': {'caughtOn': '2026-10-23', 'receivedAt': '2026-10-24T13:43:29.675724+00:00'},
      });
      expect(item.freshness?.caughtOn, '2026-10-23');
      expect(item.freshness?.receivedAt, DateTime.utc(2026, 10, 24, 13, 43, 29, 675, 724));

      final json = productDetailJson();
      ((json['variants']! as List<Object?>).first! as Map<String, Object?>)['freshness'] = {
        'caughtOn': null,
        'receivedAt': '2026-10-24T00:45:00Z',
      };
      final detail = ProductDetail.fromJson(json);
      expect(detail.variants.first.freshness?.caughtOn, isNull);
      expect(productDetail().variants.first.freshness, isNull);
    });
  });
}
