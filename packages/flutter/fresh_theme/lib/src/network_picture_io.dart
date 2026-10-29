// Android and iOS: pictures cached as files in the app's cache directory (network_picture.dart, D310).

import 'dart:async';
import 'dart:io';
import 'dart:typed_data';
import 'dart:ui' as ui;

import 'package:flutter/foundation.dart';
import 'package:flutter/painting.dart';
import 'package:path_provider/path_provider.dart';

ImageProvider<Object> cachedNetworkImage(String url) =>
    // Widget tests answer every HTTP request with 400 and have no path_provider: keep Flutter's own behaviour there.
    Platform.environment.containsKey('FLUTTER_TEST') ? NetworkImage(url) : DiskCachedNetworkImage(url);

/// Downloads a picture once and keeps it as a file (public for tests: a directory and a fetcher can be given).
class DiskPictureCache {
  DiskPictureCache({Future<Directory?> Function()? directory, Future<Uint8List> Function(Uri uri)? fetch, this.maxAge = const Duration(days: 7)})
    : _directory = directory ?? _defaultDirectory,
      _fetch = fetch ?? _download;

  static final instance = DiskPictureCache();

  final Future<Directory?> Function() _directory;
  final Future<Uint8List> Function(Uri uri) _fetch;
  final Duration maxAge;
  Future<Directory?>? _dir;

  /// The picture's bytes: the cached file while it is younger than [maxAge], else downloaded (and saved). A failed
  /// download falls back to an older cached file, if there is one.
  Future<Uint8List> bytes(String url) async {
    final dir = await (_dir ??= _directory().catchError((Object _) => null));
    final file = dir == null ? null : File('${dir.path}/${cacheFileName(url)}');
    FileStat? stat;
    if (file != null) {
      try {
        stat = await file.stat();
        if (stat.type == FileSystemEntityType.file && DateTime.now().difference(stat.modified) < maxAge) {
          return await file.readAsBytes();
        }
      } catch (_) {
        stat = null;
      }
    }
    try {
      final data = await _fetch(Uri.parse(url));
      if (file != null) unawaited(_save(file, data));
      return data;
    } catch (_) {
      if (file != null && stat?.type == FileSystemEntityType.file) return file.readAsBytes();
      rethrow;
    }
  }

  static Future<void> _save(File file, Uint8List data) async {
    try {
      final temp = File('${file.path}.part');
      await temp.writeAsBytes(data, flush: true);
      await temp.rename(file.path);
    } catch (_) {
      // A full disk only means no cache.
    }
  }

  static Future<Directory?> _defaultDirectory() async {
    final base = await getApplicationCacheDirectory();
    final dir = Directory('${base.path}/fresh_pictures');
    await dir.create(recursive: true);
    return dir;
  }

  static final _client = HttpClient()..connectionTimeout = const Duration(seconds: 15);

  static Future<Uint8List> _download(Uri uri) async {
    final request = await _client.getUrl(uri);
    final response = await request.close();
    if (response.statusCode != HttpStatus.ok) {
      await response.drain<void>();
      throw HttpException('Picture answered ${response.statusCode}', uri: uri);
    }
    final data = await consolidateHttpClientResponseBytes(response);
    if (data.isEmpty) throw HttpException('Picture is empty', uri: uri);
    return data;
  }
}

/// A file name for [url]: 64-bit FNV-1a hash in hex plus the length (no crypto package needed; collisions only
/// show a wrong picture until the file ages out, and are vanishingly rare for a shop's few thousand pictures).
String cacheFileName(String url) {
  var hash = 0xcbf29ce484222325;
  for (final unit in url.codeUnits) {
    hash ^= unit;
    hash *= 0x100000001b3; // wraps at 64 bits
  }
  return '${hash.toUnsigned(64).toRadixString(16)}-${url.length}.img';
}

/// [NetworkImage] with the disk cache.
@immutable
class DiskCachedNetworkImage extends ImageProvider<DiskCachedNetworkImage> {
  const DiskCachedNetworkImage(this.url, {this.cache});

  final String url;
  final DiskPictureCache? cache;

  @override
  Future<DiskCachedNetworkImage> obtainKey(ImageConfiguration configuration) => SynchronousFuture(this);

  @override
  ImageStreamCompleter loadImage(DiskCachedNetworkImage key, ImageDecoderCallback decode) =>
      MultiFrameImageStreamCompleter(
        codec: _load(key, decode),
        scale: 1,
        debugLabel: key.url,
        informationCollector: () => [DiagnosticsProperty<ImageProvider>('Image provider', this)],
      );

  Future<ui.Codec> _load(DiskCachedNetworkImage key, ImageDecoderCallback decode) async {
    try {
      final data = await (cache ?? DiskPictureCache.instance).bytes(key.url);
      return decode(await ui.ImmutableBuffer.fromUint8List(data));
    } catch (_) {
      // Let a later widget try again instead of keeping the failure in the memory cache.
      scheduleMicrotask(() => PaintingBinding.instance.imageCache.evict(key));
      rethrow;
    }
  }

  @override
  bool operator ==(Object other) => other is DiskCachedNetworkImage && other.url == url;

  @override
  int get hashCode => url.hashCode;

  @override
  String toString() => 'DiskCachedNetworkImage("$url")';
}
