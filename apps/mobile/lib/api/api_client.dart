// Dio setup: base URL from `--dart-define=API_BASE_URL`, context headers (X-Pincode, X-Cart-Token),
// Bearer auth with single-flight refresh, and every failure surfaced as [ApiError].

import 'package:dio/dio.dart';
import 'package:uuid/uuid.dart';

import 'api_error.dart';
import 'auth_interceptor.dart';
import 'models/json.dart';
import 'token_store.dart';

/// `--dart-define=API_BASE_URL=https://api.example.in` (origin only; `/api/v1` is added here).
/// Empty means not configured: the app shows a configuration error instead of guessing a URL (rule 7).
const String apiBaseUrlDefine = String.fromEnvironment('API_BASE_URL');

String apiV1(String origin) => '${origin.endsWith('/') ? origin.substring(0, origin.length - 1) : origin}/api/v1/';

/// One key per user action, reused on retries of that action (docs/api §1.8).
String newIdempotencyKey() => const Uuid().v4();

/// Adds `X-Pincode` (location context, §1.4) and `X-Cart-Token` (guest cart, §1.3), and stores a cart token
/// issued by the server.
class ContextHeadersInterceptor extends Interceptor {
  ContextHeadersInterceptor({required this.tokens, required this.pincode});

  final TokenStore tokens;
  final String? Function() pincode;

  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) {
    final hasPincodeQuery = options.queryParameters.containsKey('pincode');
    if (!hasPincodeQuery && !options.headers.containsKey('X-Pincode')) {
      final p = pincode();
      if (p != null) options.headers['X-Pincode'] = p;
    }
    final cartToken = tokens.guestCartToken;
    if (!tokens.hasSession && cartToken != null) options.headers['X-Cart-Token'] = cartToken;
    handler.next(options);
  }

  @override
  Future<void> onResponse(Response<dynamic> response, ResponseInterceptorHandler handler) async {
    final issued = response.headers.value('x-cart-token');
    if (issued != null && issued.isNotEmpty && !tokens.hasSession) await tokens.saveGuestCartToken(issued);
    handler.next(response);
  }
}

Dio createDio({
  required String baseUrl,
  required TokenStore tokens,
  required String? Function() pincode,
  void Function()? onSessionEnded,
  HttpClientAdapter? adapter,
  DateTime Function()? clock,
}) {
  BaseOptions options() => BaseOptions(
        baseUrl: apiV1(baseUrl),
        connectTimeout: const Duration(seconds: 10),
        receiveTimeout: const Duration(seconds: 20),
        responseType: ResponseType.json,
        contentType: Headers.jsonContentType,
        headers: {'Accept': 'application/json, application/problem+json'},
      );
  final refreshDio = Dio(options());
  final dio = Dio(options());
  if (adapter != null) {
    refreshDio.httpClientAdapter = adapter;
    dio.httpClientAdapter = adapter;
  }
  dio.interceptors.addAll([
    ContextHeadersInterceptor(tokens: tokens, pincode: pincode),
    AuthInterceptor(dio: dio, tokens: tokens, refreshDio: refreshDio, onSessionEnded: onSessionEnded, clock: clock),
  ]);
  return dio;
}

/// Thin wrapper that returns decoded JSON and throws [ApiError] for every failure.
class ApiClient {
  ApiClient(this.dio);

  final Dio dio;

  Future<Object?> get(String path, {Map<String, dynamic>? query, Map<String, String>? headers}) =>
      _send(() => dio.get<Object?>(path, queryParameters: query, options: Options(headers: headers)));

  Future<Object?> post(
    String path, {
    Object? body,
    Map<String, dynamic>? query,
    Map<String, String>? headers,
    bool skipAuth = false,
  }) =>
      _send(
        () => dio.post<Object?>(
          path,
          data: body,
          queryParameters: query,
          options: Options(headers: headers, extra: {if (skipAuth) RequestFlags.skipAuth: true}),
        ),
      );

  Future<Object?> put(String path, {Object? body, Map<String, dynamic>? query, Map<String, String>? headers}) =>
      _send(() => dio.put<Object?>(path, data: body, queryParameters: query, options: Options(headers: headers)));

  Future<Object?> patch(String path, {Object? body, Map<String, String>? headers}) =>
      _send(() => dio.patch<Object?>(path, data: body, options: Options(headers: headers)));

  Future<Object?> delete(String path, {Map<String, String>? headers}) =>
      _send(() => dio.delete<Object?>(path, options: Options(headers: headers)));

  Future<Object?> _send(Future<Response<Object?>> Function() call) async {
    try {
      final response = await call();
      return response.data;
    } on DioException catch (e) {
      throw ApiError.from(e);
    }
  }
}

/// Parses a JSON object with [read]; a contract mismatch becomes ApiError RESPONSE_INVALID.
T parseObject<T>(Object? data, T Function(Json) read) {
  try {
    return read(asJson(data, 'response'));
  } on FormatException catch (e) {
    throw ApiError.from(e);
  } on TypeError {
    throw const ApiError(status: 0, code: ApiError.responseInvalid);
  }
}

/// Parses a JSON array (some endpoints return a bare array).
List<T> parseArray<T>(Object? data, T Function(Json) read) {
  try {
    if (data is List) return [for (final e in data) read(asJson(e, 'item'))];
    if (data is Map && data['items'] is List) return parseArray(data['items'], read);
    throw const FormatException('Expected a list.');
  } on FormatException catch (e) {
    throw ApiError.from(e);
  } on TypeError {
    throw const ApiError(status: 0, code: ApiError.responseInvalid);
  }
}

/// `If-Match: "918273"` header map.
Map<String, String> ifMatch(String version) => {'If-Match': '"$version"'};

/// `Idempotency-Key` header map.
Map<String, String> idempotencyKey(String key) => {'Idempotency-Key': key};
