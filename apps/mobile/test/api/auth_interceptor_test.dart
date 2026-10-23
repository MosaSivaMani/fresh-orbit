import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/api_client.dart';
import 'package:fresh_orbit_app/api/api_error.dart';
import 'package:fresh_orbit_app/api/auth_interceptor.dart';
import 'package:fresh_orbit_app/api/models/auth.dart';
import 'package:fresh_orbit_app/api/token_store.dart';

import '../support/fake_adapter.dart';

final _now = DateTime.utc(2026, 10, 23, 5, 30);

Map<String, Object?> _pair(String access, String refresh) => {
  'accessToken': access,
  'accessTokenExpiresAt': '2026-10-23T05:45:00Z',
  'refreshToken': refresh,
  'refreshTokenExpiresAt': '2026-11-22T05:30:00Z',
};

Future<TokenStore> _signedIn({String access = 'a1', String refresh = 'r1'}) async {
  final store = TokenStore(MemorySecretStore());
  await store.saveTokens(
    TokenPair(
      accessToken: access,
      accessTokenExpiresAt: DateTime.utc(2026, 10, 23, 5, 45),
      refreshToken: refresh,
      refreshTokenExpiresAt: DateTime.utc(2026, 11, 22),
    ),
  );
  return store;
}

AuthInterceptor _auth(ApiClient api) => api.dio.interceptors.whereType<AuthInterceptor>().single;

void main() {
  test('sends Bearer and retries once after refreshing on 401 TOKEN_EXPIRED', () async {
    final tokens = await _signedIn();
    final adapter = FakeAdapter((o) async {
      if (o.path.endsWith('auth/refresh')) return FakeReply(200, _pair('a2', 'r2'));
      final bearer = o.headers['Authorization'];
      return bearer == 'Bearer a2' ? const FakeReply(200, {'ok': true}) : FakeReply(401, problem(401, 'TOKEN_EXPIRED'));
    });
    final api = ApiClient(
      createDio(baseUrl: 'https://api.test', tokens: tokens, pincode: () => null, adapter: adapter, clock: () => _now),
    );

    final data = await api.get('me');

    expect(data, {'ok': true});
    expect(adapter.to('auth/refresh').single.data, {'refreshToken': 'r1'});
    expect(adapter.to('auth/refresh').single.headers.containsKey('Authorization'), isFalse);
    expect(tokens.accessToken, 'a2');
    expect(tokens.refreshToken, 'r2');
    expect(adapter.to('me').map((r) => r.headers['Authorization']), ['Bearer a1', 'Bearer a2']);
  });

  test('concurrent 401s share one refresh call (single flight)', () async {
    final tokens = await _signedIn();
    final gate = Completer<void>();
    final adapter = FakeAdapter((o) async {
      if (o.path.endsWith('auth/refresh')) {
        await gate.future;
        return FakeReply(200, _pair('a2', 'r2'));
      }
      return o.headers['Authorization'] == 'Bearer a2'
          ? const FakeReply(200, {'ok': true})
          : FakeReply(401, problem(401, 'TOKEN_EXPIRED'));
    });
    final api = ApiClient(
      createDio(baseUrl: 'https://api.test', tokens: tokens, pincode: () => null, adapter: adapter, clock: () => _now),
    );

    final calls = [api.get('me'), api.get('cart'), api.get('orders')];
    await Future<void>.delayed(const Duration(milliseconds: 20));
    gate.complete();
    final results = await Future.wait(calls);

    expect(results, everyElement({'ok': true}));
    expect(adapter.to('auth/refresh'), hasLength(1));
    expect(_auth(api).refreshCount, 1);
  });

  test('refreshes before the request when the access token is missing (app restart)', () async {
    final tokens = TokenStore(MemorySecretStore({'fo.refreshToken': 'r1'}));
    await tokens.load();
    final adapter = FakeAdapter((o) async {
      if (o.path.endsWith('auth/refresh')) return FakeReply(200, _pair('a2', 'r2'));
      return const FakeReply(200, {'ok': true});
    });
    final api = ApiClient(
      createDio(baseUrl: 'https://api.test', tokens: tokens, pincode: () => null, adapter: adapter, clock: () => _now),
    );

    await api.get('me');

    expect(adapter.requests.map((r) => r.path), ['auth/refresh', 'me']);
    expect(adapter.to('me').single.headers['Authorization'], 'Bearer a2');
  });

  test('a rejected refresh ends the session and surfaces the original 401', () async {
    final tokens = await _signedIn();
    var ended = 0;
    final adapter = FakeAdapter((o) async {
      if (o.path.endsWith('auth/refresh')) return FakeReply(401, problem(401, 'REFRESH_TOKEN_REUSED'));
      return FakeReply(401, problem(401, 'TOKEN_EXPIRED'));
    });
    final api = ApiClient(
      createDio(
        baseUrl: 'https://api.test',
        tokens: tokens,
        pincode: () => null,
        adapter: adapter,
        clock: () => _now,
        onSessionEnded: () => ended++,
      ),
    );

    final error = await api.get('me').then<ApiError?>((_) => null, onError: (Object e) => e as ApiError);

    expect(error!.code, 'TOKEN_EXPIRED');
    expect(ended, 1);
    expect(tokens.hasSession, isFalse);
    expect(adapter.to('me'), hasLength(1));
  });

  test('a second 401 after a successful refresh ends the session without looping', () async {
    final tokens = await _signedIn();
    var ended = 0;
    final adapter = FakeAdapter((o) async {
      if (o.path.endsWith('auth/refresh')) return FakeReply(200, _pair('a2', 'r2'));
      return FakeReply(401, problem(401, 'TOKEN_EXPIRED'));
    });
    final api = ApiClient(
      createDio(
        baseUrl: 'https://api.test',
        tokens: tokens,
        pincode: () => null,
        adapter: adapter,
        clock: () => _now,
        onSessionEnded: () => ended++,
      ),
    );

    await expectLater(api.get('me'), throwsA(isA<ApiError>()));

    expect(adapter.to('me'), hasLength(2));
    expect(adapter.to('auth/refresh'), hasLength(1));
    expect(ended, 1);
    expect(tokens.hasSession, isFalse);
  });

  test('other 401 codes and auth endpoints are not refreshed', () async {
    final tokens = await _signedIn();
    final adapter = FakeAdapter((o) async => FakeReply(401, problem(401, 'UNAUTHENTICATED')));
    final api = ApiClient(
      createDio(baseUrl: 'https://api.test', tokens: tokens, pincode: () => null, adapter: adapter, clock: () => _now),
    );

    await expectLater(api.get('me'), throwsA(isA<ApiError>()));
    await expectLater(api.post('auth/otp/verify', skipAuth: true), throwsA(isA<ApiError>()));

    expect(adapter.to('auth/refresh'), isEmpty);
    expect(adapter.to('auth/otp/verify').single.headers.containsKey('Authorization'), isFalse);
    expect(tokens.hasSession, isTrue);
  });

  test('a network error during refresh keeps the session', () async {
    final tokens = await _signedIn();
    final adapter = FakeAdapter((o) async {
      if (o.path.endsWith('auth/refresh')) return const FakeReply.offline();
      return FakeReply(401, problem(401, 'TOKEN_EXPIRED'));
    });
    final api = ApiClient(
      createDio(baseUrl: 'https://api.test', tokens: tokens, pincode: () => null, adapter: adapter, clock: () => _now),
    );

    await expectLater(api.get('me'), throwsA(isA<ApiError>()));

    expect(tokens.hasSession, isTrue);
  });

  test('RequestFlags are stable strings', () {
    expect(RequestFlags.skipAuth, isNotEmpty);
  });
}
