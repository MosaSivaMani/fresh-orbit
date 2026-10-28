// Problem Details (RFC 7807, docs/api/README.md §1.6) → typed ApiError. Screens branch on [ApiError.code],
// never on message text.

import 'package:dio/dio.dart';

import 'models/json.dart';

/// One field error: `errors["items[1].quantity"] = [{ code, message }]`.
class FieldError {
  const FieldError({required this.code, required this.message});

  final String code;
  final String message;
}

class ApiError implements Exception {
  const ApiError({
    required this.status,
    required this.code,
    this.title,
    this.detail,
    this.errors = const {},
    this.traceId,
    this.retryAfterSeconds,
    this.extensions = const {},
  });

  /// Client-side codes for failures that never reached a Problem Details body.
  static const networkError = 'NETWORK_ERROR';
  static const timeout = 'TIMEOUT';
  static const cancelled = 'CANCELLED';
  static const responseInvalid = 'RESPONSE_INVALID';
  static const unknown = 'UNKNOWN_ERROR';

  /// HTTP status, or 0 when no response arrived.
  final int status;
  final String code;
  final String? title;
  final String? detail;
  final Map<String, List<FieldError>> errors;
  final String? traceId;
  final int? retryAfterSeconds;

  /// Every member of the problem body (for code-specific members such as `preview` on PRICE_CHANGED).
  final Json extensions;

  /// No definitive answer from the server: the same Idempotency-Key must be reused when retrying.
  bool get isTransient =>
      status == 0 || status >= 500 || code == 'IDEMPOTENCY_IN_PROGRESS' || code == 'CONCURRENT_SAVE';

  /// The catalogue needs a pincode it serves: none was sent (LOCATION_REQUIRED) or the stored one is not (or no
  /// longer) served (PINCODE_NOT_SERVICEABLE). The way out is choosing another pincode (D236).
  bool get needsPincode => code == 'LOCATION_REQUIRED' || code == 'PINCODE_NOT_SERVICEABLE';

  /// First message for a field (camelCase JSON path), for form field errors.
  String? fieldMessage(String field) {
    final list = errors[field];
    return list == null || list.isEmpty ? null : list.first.message;
  }

  /// Text for a toast or banner: the server's sentence when present, else a calm generic line.
  String get message {
    if (detail != null && detail!.isNotEmpty) return detail!;
    if (errors.isNotEmpty) return errors.values.first.first.message;
    // 429 without a sentence: say how long to wait (Retry-After), not the bare title.
    if (status == 429) {
      return retryAfterSeconds == null
          ? 'Too many tries. Please wait a moment and try again.'
          : 'Too many tries. Please try again in $retryAfterSeconds seconds.';
    }
    if (title != null && title!.isNotEmpty) return title!;
    return switch (code) {
      networkError => 'You seem to be offline. Check your connection and try again.',
      timeout => 'The server took too long to answer. Please try again.',
      responseInvalid => 'We could not read the server response. Please try again later.',
      _ => 'Something went wrong. Please try again.',
    };
  }

  factory ApiError.fromProblem(int status, Json body, {String? retryAfterHeader}) {
    final errors = <String, List<FieldError>>{};
    final raw = body['errors'];
    if (raw is Map) {
      raw.forEach((key, value) {
        if (value is List) {
          errors[key.toString()] = [
            for (final e in value)
              if (e is Map)
                FieldError(
                  code: (e['code'] ?? 'VALIDATION_FAILED').toString(),
                  message: (e['message'] ?? '').toString(),
                )
              else
                FieldError(code: 'VALIDATION_FAILED', message: e.toString()),
          ];
        }
      });
    }
    final retry = body['retryAfterSeconds'];
    return ApiError(
      status: status,
      code: body['code'] is String ? body['code'] as String : _fallbackCode(status),
      title: body['title'] is String ? body['title'] as String : null,
      detail: body['detail'] is String ? body['detail'] as String : null,
      errors: errors,
      traceId: body['traceId'] is String ? body['traceId'] as String : null,
      retryAfterSeconds: retry is int ? retry : int.tryParse(retryAfterHeader ?? ''),
      extensions: body,
    );
  }

  factory ApiError.fromDio(DioException e) {
    final response = e.response;
    if (response == null) {
      return ApiError(
        status: 0,
        code: switch (e.type) {
          DioExceptionType.connectionTimeout ||
          DioExceptionType.sendTimeout ||
          DioExceptionType.receiveTimeout => timeout,
          DioExceptionType.cancel => cancelled,
          _ => networkError,
        },
      );
    }
    final status = response.statusCode ?? 0;
    final data = response.data;
    if (data is Map) {
      return ApiError.fromProblem(
        status,
        data.cast<String, dynamic>(),
        retryAfterHeader: response.headers.value('retry-after'),
      );
    }
    return ApiError(
      status: status,
      code: _fallbackCode(status),
      retryAfterSeconds: int.tryParse(response.headers.value('retry-after') ?? ''),
    );
  }

  /// Any thrown object → ApiError (FormatException from a model means the response did not match the contract).
  factory ApiError.from(Object error) => switch (error) {
    final ApiError e => e,
    final DioException e when e.error is ApiError => e.error! as ApiError,
    final DioException e => ApiError.fromDio(e),
    final FormatException e => ApiError(
      status: 0,
      code: responseInvalid,
      detail: null,
      extensions: {'reason': e.message},
    ),
    _ => const ApiError(status: 0, code: unknown),
  };

  static String _fallbackCode(int status) => switch (status) {
    400 => 'MALFORMED_REQUEST',
    401 => 'UNAUTHENTICATED',
    403 => 'FORBIDDEN',
    404 => 'NOT_FOUND',
    409 => 'CONFLICT',
    412 => 'CONCURRENCY_CONFLICT',
    422 => 'VALIDATION_FAILED',
    428 => 'VERSION_REQUIRED',
    429 => 'RATE_LIMITED',
    503 => 'SERVICE_UNAVAILABLE',
    >= 500 => 'INTERNAL_ERROR',
    _ => unknown,
  };

  @override
  String toString() => 'ApiError($status $code${traceId == null ? '' : ' trace=$traceId'})';
}
