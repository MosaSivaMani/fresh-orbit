import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';

/// A scripted HTTP adapter: each request is answered by [handler]; every request is recorded.
class FakeAdapter implements HttpClientAdapter {
  FakeAdapter(this.handler);

  final Future<FakeReply> Function(RequestOptions options) handler;
  final List<RequestOptions> requests = [];

  @override
  Future<ResponseBody> fetch(RequestOptions options, Stream<Uint8List>? requestStream, Future<void>? cancelFuture) async {
    requests.add(options.copyWith(headers: Map.of(options.headers), extra: Map.of(options.extra)));
    final reply = await handler(options);
    if (reply.networkError) {
      throw DioException.connectionError(requestOptions: options, reason: 'offline');
    }
    return ResponseBody.fromString(
      reply.body == null ? '' : jsonEncode(reply.body),
      reply.status,
      headers: {
        Headers.contentTypeHeader: [reply.status >= 400 ? 'application/problem+json' : 'application/json'],
        for (final e in reply.headers.entries) e.key: [e.value],
      },
    );
  }

  @override
  void close({bool force = false}) {}

  Iterable<RequestOptions> to(String pathSuffix) => requests.where((r) => r.path.endsWith(pathSuffix));
}

class FakeReply {
  const FakeReply(this.status, [this.body, this.headers = const {}]) : networkError = false;

  const FakeReply.offline()
      : status = 0,
        body = null,
        headers = const {},
        networkError = true;

  final int status;
  final Object? body;
  final Map<String, String> headers;
  final bool networkError;
}

Map<String, Object?> problem(int status, String code, {Map<String, Object?> extra = const {}}) => {
      'type': 'https://api.freshorbit.in/problems/${code.toLowerCase()}',
      'title': 'Refused.',
      'status': status,
      'code': code,
      'traceId': '00-trace-$code',
      ...extra,
    };
