import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/api_client.dart';
import 'package:fresh_orbit_app/api/api_error.dart';
import 'package:fresh_orbit_app/api/token_store.dart';

import '../support/fake_adapter.dart';

void main() {
  ApiClient clientWith(FakeAdapter adapter) => ApiClient(
        createDio(
          baseUrl: 'https://api.test',
          tokens: TokenStore(MemorySecretStore()),
          pincode: () => null,
          adapter: adapter,
        ),
      );

  test('maps a Problem Details body to code, field errors and trace id', () async {
    final api = clientWith(
      FakeAdapter(
        (_) async => FakeReply(
          422,
          problem(
            422,
            'VALIDATION_FAILED',
            extra: {
              'detail': 'Quantity must be a multiple of 250 grams.',
              'errors': {
                'quantity.value': [
                  {'code': 'QUANTITY_STEP_INVALID', 'message': 'Quantity must be a multiple of 250 grams.'},
                ],
              },
            },
          ),
        ),
      ),
    );

    final error = await api.post('cart/items', body: {}).then<ApiError?>((_) => null, onError: (Object e) => e as ApiError);

    expect(error, isNotNull);
    expect(error!.status, 422);
    expect(error.code, 'VALIDATION_FAILED');
    expect(error.errors['quantity.value']!.single.code, 'QUANTITY_STEP_INVALID');
    expect(error.fieldMessage('quantity.value'), isNotEmpty);
    expect(error.traceId, '00-trace-VALIDATION_FAILED');
    expect(error.isTransient, isFalse);
  });

  test('keeps extension members such as preview on PRICE_CHANGED', () async {
    final api = clientWith(
      FakeAdapter((_) async => FakeReply(409, problem(409, 'PRICE_CHANGED', extra: {'preview': {'expectedTotalPaise': 1}}))),
    );

    final error = await api.post('orders').then<ApiError?>((_) => null, onError: (Object e) => e as ApiError);

    expect(error!.code, 'PRICE_CHANGED');
    expect(error.extensions['preview'], isA<Map<String, dynamic>>());
  });

  test('429 carries retryAfterSeconds', () async {
    final api = clientWith(
      FakeAdapter((_) async => FakeReply(429, problem(429, 'RATE_LIMITED', extra: {'retryAfterSeconds': 30}))),
    );

    final error = await api.get('products').then<ApiError?>((_) => null, onError: (Object e) => e as ApiError);

    expect(error!.code, 'RATE_LIMITED');
    expect(error.retryAfterSeconds, 30);
  });

  test('no response becomes NETWORK_ERROR and is transient', () async {
    final api = clientWith(FakeAdapter((_) async => const FakeReply.offline()));

    final error = await api.get('home').then<ApiError?>((_) => null, onError: (Object e) => e as ApiError);

    expect(error!.status, 0);
    expect(error.code, ApiError.networkError);
    expect(error.isTransient, isTrue);
  });

  test('a non-problem error body falls back to a code from the status', () {
    final error = ApiError.fromDio(
      DioException.badResponse(
        statusCode: 503,
        requestOptions: RequestOptions(path: 'x'),
        response: Response<Object?>(requestOptions: RequestOptions(path: 'x'), statusCode: 503, data: 'down'),
      ),
    );

    expect(error.code, 'SERVICE_UNAVAILABLE');
    expect(error.isTransient, isTrue);
  });

  test('a response that breaks the contract becomes RESPONSE_INVALID', () {
    expect(() => parseObject({'nope': 1}, (j) => j['id'] as String), throwsA(isA<ApiError>()));
    final error = ApiError.from(const FormatException('Expected a string for "id".'));
    expect(error.code, ApiError.responseInvalid);
  });

  test('context headers: X-Pincode unless a pincode query is present; X-Cart-Token only for guests', () async {
    final tokens = TokenStore(MemorySecretStore({'fo.guestCartToken': 'gct_1'}));
    await tokens.load();
    final adapter = FakeAdapter((_) async => const FakeReply(200, {}, {'X-Cart-Token': 'gct_2'}));
    final api = ApiClient(createDio(baseUrl: 'https://api.test/', tokens: tokens, pincode: () => '600041', adapter: adapter));

    await api.get('home');
    await api.get('serviceability', query: {'pincode': '600042'});

    expect(adapter.requests[0].uri.toString(), 'https://api.test/api/v1/home');
    expect(adapter.requests[0].headers['X-Pincode'], '600041');
    expect(adapter.requests[0].headers['X-Cart-Token'], 'gct_1');
    expect(adapter.requests[1].headers.containsKey('X-Pincode'), isFalse);
    expect(tokens.guestCartToken, 'gct_2');
  });
}
