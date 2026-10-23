// Bearer auth with single-flight refresh (docs/api/README.md §1.2):
// - before a request, if we hold a refresh token but the access token is missing/expiring, refresh first
//   (after an app restart the access token is gone: it lives in memory only);
// - on 401 TOKEN_EXPIRED, refresh once and retry the request once;
// - concurrent callers share one refresh call (the refresh token rotates, so two parallel refreshes would
//   trip REFRESH_TOKEN_REUSED and revoke the family);
// - a failed refresh (401) or a second 401 ends the session → [onSessionEnded] (app shows sign-in).

import 'dart:async';

import 'package:dio/dio.dart';

import 'models/auth.dart';
import 'models/json.dart';
import 'token_store.dart';

/// `Options.extra` flags understood by the interceptors.
abstract final class RequestFlags {
  /// Do not attach or refresh the access token (auth endpoints).
  static const skipAuth = 'fo.skipAuth';

  /// Internal: this request is already the retry after a refresh.
  static const authRetried = 'fo.authRetried';
}

class AuthInterceptor extends Interceptor {
  AuthInterceptor({
    required Dio dio,
    required this.tokens,
    required this.refreshDio,
    this.onSessionEnded,
    DateTime Function()? clock,
  })  : _dio = dio, // ignore: prefer_initializing_formals
        _clock = clock ?? DateTime.now;

  final Dio _dio;
  final TokenStore tokens;

  /// Dio without this interceptor, used only for `POST auth/refresh`.
  final Dio refreshDio;
  final void Function()? onSessionEnded;
  final DateTime Function() _clock;

  Future<bool>? _inFlight;

  /// Number of refresh calls actually sent (for tests and diagnostics).
  int refreshCount = 0;

  @override
  Future<void> onRequest(RequestOptions options, RequestInterceptorHandler handler) async {
    if (options.extra[RequestFlags.skipAuth] == true) return handler.next(options);
    if (tokens.hasSession && tokens.accessExpired(_clock())) {
      await refresh();
    }
    final token = tokens.accessToken;
    if (token != null) options.headers['Authorization'] = 'Bearer $token';
    handler.next(options);
  }

  @override
  Future<void> onError(DioException err, ErrorInterceptorHandler handler) async {
    final options = err.requestOptions;
    final response = err.response;
    if (response?.statusCode != 401 || options.extra[RequestFlags.skipAuth] == true) return handler.next(err);

    final code = _code(response!.data);
    if (options.extra[RequestFlags.authRetried] == true) {
      // Second 401 after a successful refresh: the session is not usable any more.
      await _endSession();
      return handler.next(err);
    }
    if (code != 'TOKEN_EXPIRED' || !tokens.hasSession) return handler.next(err);

    final refreshed = await refresh();
    if (!refreshed) return handler.next(err);

    options.extra[RequestFlags.authRetried] = true;
    options.headers['Authorization'] = 'Bearer ${tokens.accessToken}';
    try {
      final retried = await _dio.fetch<Object?>(options);
      return handler.resolve(retried);
    } on DioException catch (e) {
      return handler.next(e);
    }
  }

  /// Refreshes once for all concurrent callers. True when a new access token is stored.
  Future<bool> refresh() => _inFlight ??= _refresh().whenComplete(() => _inFlight = null);

  Future<bool> _refresh() async {
    final refreshToken = tokens.refreshToken;
    if (refreshToken == null) return false;
    refreshCount++;
    try {
      final response = await refreshDio.post<Object?>(
        'auth/refresh',
        data: {'refreshToken': refreshToken},
        options: Options(extra: {RequestFlags.skipAuth: true}),
      );
      await tokens.saveTokens(TokenPair.fromJson(asJson(response.data, 'refresh')));
      return true;
    } on DioException catch (e) {
      final status = e.response?.statusCode;
      if (status == 401 || status == 403) {
        // REFRESH_TOKEN_INVALID / REFRESH_TOKEN_REUSED / ACCOUNT_DISABLED: sign in again.
        await _endSession();
      }
      // Network errors keep the session: the user may be offline.
      return false;
    } on FormatException {
      return false;
    }
  }

  Future<void> _endSession() async {
    await tokens.clearSession();
    onSessionEnded?.call();
  }

  static String? _code(Object? data) => data is Map && data['code'] is String ? data['code'] as String : null;
}
