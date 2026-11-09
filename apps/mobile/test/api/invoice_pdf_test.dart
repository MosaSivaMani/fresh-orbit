// GET /orders/{id}/invoice/pdf (ORD-R53): the PDF is fetched with the customer's token as bytes, its file name
// comes from Content-Disposition, and refusals (Problem Details bytes) become ApiError codes.

import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/api_client.dart';
import 'package:fresh_orbit_app/api/api_error.dart';
import 'package:fresh_orbit_app/api/models/auth.dart';
import 'package:fresh_orbit_app/api/repositories.dart';
import 'package:fresh_orbit_app/api/token_store.dart';
import 'package:fresh_orbit_app/core/document_share.dart';

import '../support/fake_adapter.dart';

final _pdfBytes = utf8.encode('%PDF-1.7 fake invoice');

/// Answers the PDF route with raw bytes (or a Problem Details body) and auth/refresh with a new token pair.
class _PdfAdapter implements HttpClientAdapter {
  _PdfAdapter(this.reply);

  final ResponseBody Function(RequestOptions options) reply;
  final List<RequestOptions> requests = [];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    requests.add(options.copyWith(headers: Map.of(options.headers)));
    if (options.path.endsWith('auth/refresh')) {
      return _json(200, {
        'accessToken': 'a2',
        'accessTokenExpiresAt': '2026-10-23T05:45:00Z',
        'refreshToken': 'r2',
        'refreshTokenExpiresAt': '2026-11-22T05:30:00Z',
      });
    }
    return reply(options);
  }

  @override
  void close({bool force = false}) {}
}

ResponseBody _json(int status, Map<String, Object?> body) => ResponseBody.fromString(
  jsonEncode(body),
  status,
  headers: {
    Headers.contentTypeHeader: [status >= 400 ? 'application/problem+json' : 'application/json'],
  },
);

ResponseBody _pdf() => ResponseBody.fromBytes(
  _pdfBytes,
  200,
  headers: {
    Headers.contentTypeHeader: ['application/pdf'],
    'content-disposition': ["attachment; filename=INV-26-27-000001.pdf; filename*=UTF-8''INV-26-27-000001.pdf"],
  },
);

Future<TokenStore> _signedIn() async {
  final store = TokenStore(MemorySecretStore());
  await store.saveTokens(
    TokenPair(
      accessToken: 'a1',
      accessTokenExpiresAt: DateTime.utc(2026, 10, 23, 5, 45),
      refreshToken: 'r1',
      refreshTokenExpiresAt: DateTime.utc(2026, 11, 22),
    ),
  );
  return store;
}

Future<HttpOrderRepository> _orders(_PdfAdapter adapter) async => HttpOrderRepository(
  ApiClient(
    createDio(
      baseUrl: 'https://api.test',
      tokens: await _signedIn(),
      pincode: () => null,
      adapter: adapter,
      clock: () => DateTime.utc(2026, 10, 23, 5, 30),
    ),
  ),
);

void main() {
  test('downloads the PDF bytes with the bearer token and the server file name', () async {
    final adapter = _PdfAdapter((_) => _pdf());

    final pdf = await (await _orders(adapter)).invoicePdf('o1');

    final request = adapter.requests.single;
    expect(request.path, endsWith('orders/o1/invoice/pdf'));
    expect(request.method, 'GET');
    expect(request.headers['Authorization'], 'Bearer a1');
    expect(request.headers['Accept'].toString(), contains('application/pdf'));
    expect(request.responseType, ResponseType.bytes);
    expect(pdf.bytes, _pdfBytes);
    expect(pdf.fileName, 'INV-26-27-000001.pdf');
  });

  test('a PDF still being rendered is 404 INVOICE_PDF_NOT_READY', () async {
    final adapter = _PdfAdapter((_) => _json(404, problem(404, 'INVOICE_PDF_NOT_READY')));

    await expectLater(
      (await _orders(adapter)).invoicePdf('o1'),
      throwsA(
        isA<ApiError>()
            .having((e) => e.code, 'code', 'INVOICE_PDF_NOT_READY')
            .having((e) => e.status, 'status', 404),
      ),
    );
  });

  test('an expired token on the download is refreshed and the download retried once', () async {
    final adapter = _PdfAdapter(
      (o) => o.headers['Authorization'] == 'Bearer a2' ? _pdf() : _json(401, problem(401, 'TOKEN_EXPIRED')),
    );

    final pdf = await (await _orders(adapter)).invoicePdf('o1');

    expect(pdf.bytes, _pdfBytes);
    expect(adapter.requests.where((r) => r.path.endsWith('auth/refresh')), hasLength(1));
  });

  test('without Content-Disposition the file is still named', () {
    expect(const BinaryResponse(bytes: [1]).fileName, isNull);
    const quoted = BinaryResponse(bytes: [1], contentDisposition: 'attachment; filename="a/b/INV-1.pdf"');
    expect(quoted.fileName, 'INV-1.pdf');
    expect(safeFileName('INV/26-27/000001.pdf'), '000001.pdf');
    expect(safeFileName('INV 26.pdf'), 'INV-26.pdf');
    expect(safeFileName('.pdf'), 'document.pdf');
  });

  test('problem bodies that arrive as bytes are read like JSON ones', () {
    expect(problemBody(utf8.encode(jsonEncode(problem(404, 'INVOICE_NOT_ISSUED'))))?['code'], 'INVOICE_NOT_ISSUED');
    expect(problemBody(_pdfBytes), isNull);
  });
}
