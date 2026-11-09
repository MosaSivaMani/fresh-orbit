// Saves a downloaded document (the GST invoice PDF) in the app's cache folder and hands it to the platform's
// share sheet, where the customer opens it in a PDF viewer, saves it to Files or Downloads, or sends it on
// (path_provider and share_plus, BSD-3-Clause). Behind a provider so widget tests replace it.

import 'dart:io';

import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:path_provider/path_provider.dart';
import 'package:share_plus/share_plus.dart';

/// Saves [bytes] as [fileName] and offers it to the platform; [context] places the share sheet on tablets.
typedef DocumentSharer =
    Future<void> Function(
      BuildContext context, {
      required List<int> bytes,
      required String fileName,
      required String mimeType,
    });

final documentSharerProvider = Provider<DocumentSharer>((ref) => saveAndShareDocument);

/// Writes the file to `<app cache>/documents/` (replacing an earlier copy of the same name; the platform may
/// clear the cache, the server keeps the original) and opens the share sheet with it.
Future<void> saveAndShareDocument(
  BuildContext context, {
  required List<int> bytes,
  required String fileName,
  required String mimeType,
}) async {
  final box = context.findRenderObject();
  final origin = box is RenderBox && box.hasSize ? box.localToGlobal(Offset.zero) & box.size : null;
  final folder = Directory('${(await getApplicationCacheDirectory()).path}/documents');
  await folder.create(recursive: true);
  final file = File('${folder.path}/${safeFileName(fileName)}');
  await file.writeAsBytes(bytes, flush: true);
  await SharePlus.instance.share(
    ShareParams(
      files: [XFile(file.path, mimeType: mimeType)],
      sharePositionOrigin: origin,
    ),
  );
}

/// The server's file name without folder parts or characters a file system refuses; `document` when nothing is
/// left.
String safeFileName(String name) {
  final cleaned = name.split(RegExp(r'[/\\]')).last.replaceAll(RegExp(r'[^A-Za-z0-9._-]'), '-');
  return cleaned.isEmpty || cleaned.startsWith('.') ? 'document$cleaned' : cleaned;
}
