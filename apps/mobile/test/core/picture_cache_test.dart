// Picture disk cache (D310, gap audit MB-7).

import 'dart:io';
import 'dart:typed_data' as td;

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:fresh_theme/picture_cache_io.dart';

void main() {
  late Directory dir;
  late List<Uri> fetched;
  late bool offline;

  setUp(() async {
    dir = await Directory.systemTemp.createTemp('fresh_pictures_test');
    fetched = [];
    offline = false;
  });
  tearDown(() => dir.delete(recursive: true));

  DiskPictureCache cache({Duration maxAge = const Duration(days: 7)}) => DiskPictureCache(
    directory: () async => dir,
    maxAge: maxAge,
    fetch: (uri) async {
      if (offline) throw const SocketException('offline');
      fetched.add(uri);
      return td.Uint8List.fromList([1, 2, 3]);
    },
  );

  Future<void> saved() async {
    for (var i = 0; i < 50 && dir.listSync().whereType<File>().where((f) => f.path.endsWith('.img')).isEmpty; i++) {
      await Future<void>.delayed(const Duration(milliseconds: 10));
    }
  }

  test('a picture is downloaded once and then read from disk', () async {
    final c = cache();
    expect(await c.bytes('https://img.test/p1.webp'), [1, 2, 3]);
    await saved();
    expect(await cache().bytes('https://img.test/p1.webp'), [1, 2, 3]);
    expect(fetched, hasLength(1));
  });

  test('an old file is downloaded again, and used when offline', () async {
    await cache().bytes('https://img.test/p2.webp');
    await saved();
    expect(await cache(maxAge: Duration.zero).bytes('https://img.test/p2.webp'), [1, 2, 3]);
    expect(fetched, hasLength(2));

    offline = true;
    expect(await cache(maxAge: Duration.zero).bytes('https://img.test/p2.webp'), [1, 2, 3]);
  });

  test('without a cache folder the picture still downloads; offline and uncached it fails', () async {
    final noDir = DiskPictureCache(directory: () async => null, fetch: (_) async => td.Uint8List.fromList([9]));
    expect(await noDir.bytes('https://img.test/p3.webp'), [9]);
    offline = true;
    await expectLater(cache().bytes('https://img.test/never.webp'), throwsA(isA<SocketException>()));
  });

  test('file names differ per address', () {
    expect(cacheFileName('https://img.test/a.webp'), isNot(cacheFileName('https://img.test/b.webp')));
    expect(cacheFileName('https://img.test/a.webp'), cacheFileName('https://img.test/a.webp'));
  });

  testWidgets('pictures decode at the laid-out width times the pixel ratio', (tester) async {
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);
    late int? width;
    await tester.pumpWidget(
      MaterialApp(
        home: Center(
          child: SizedBox(
            width: 120,
            child: Builder(
              builder: (context) {
                width = freshDecodeWidth(context, 120);
                return const SizedBox();
              },
            ),
          ),
        ),
      ),
    );
    expect(width, 360);
    expect(freshNetworkImageProvider('https://img.test/a.webp', cacheWidth: 360), isA<ResizeImage>());
  });
}
