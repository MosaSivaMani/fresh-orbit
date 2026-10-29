// Server pictures with a disk cache and decode-at-display-size (D310, gap audit MB-7). Every product, category and
// cart picture goes through [FreshNetworkImage], so a cold start does not download every picture again on mobile
// data, and a card's Medium rendition is decoded at the card's width, not at full size (memory on low-end phones).
//
// Our own small cache (no third-party image cache package): files in the app's cache directory (path_provider,
// BSD-3), named by a hash of the address, used for [freshImageMaxAge]; the system may clear the folder at any time.
// The web build has no file system and uses the browser's HTTP cache (network_picture_stub.dart).

import 'package:flutter/material.dart';

import 'network_picture_stub.dart' if (dart.library.io) 'network_picture_io.dart' as platform;

/// How long a cached picture is used before it is downloaded again. Picture addresses change when the picture
/// changes (renditions are versioned by the server), so a week only bounds the disk use.
const freshImageMaxAge = Duration(days: 7);

/// The image provider for a server picture: disk-cached, and resized to [cacheWidth] physical pixels when given.
ImageProvider freshNetworkImageProvider(String url, {int? cacheWidth}) =>
    ResizeImage.resizeIfNeeded(cacheWidth, null, platform.cachedNetworkImage(url));

/// The decode width for a picture laid out [logicalWidth] wide, or null when unknown (infinite or zero).
int? freshDecodeWidth(BuildContext context, double logicalWidth) {
  if (!logicalWidth.isFinite || logicalWidth <= 0) return null;
  return (logicalWidth * MediaQuery.devicePixelRatioOf(context)).ceil();
}

/// `Image.network` with the disk cache and a decode width ([width], [decodeWidth] or the screen width).
class FreshNetworkImage extends StatelessWidget {
  const FreshNetworkImage(
    this.url, {
    super.key,
    this.width,
    this.height,
    this.fit = BoxFit.cover,
    this.semanticLabel,
    this.excludeFromSemantics = false,
    this.errorBuilder,
    this.loadingBuilder,
    this.frameBuilder,
    this.decodeWidth,
  });

  final String url;

  /// The logical width the picture is shown at, when it is not [width] (a card in a two-column grid).
  final double? decodeWidth;
  final double? width;
  final double? height;
  final BoxFit fit;
  final String? semanticLabel;
  final bool excludeFromSemantics;
  final ImageErrorWidgetBuilder? errorBuilder;
  final ImageLoadingBuilder? loadingBuilder;
  final ImageFrameBuilder? frameBuilder;

  @override
  Widget build(BuildContext context) {
    // No LayoutBuilder (it breaks intrinsic sizing): [width] when given, else [decodeWidth], else the screen width,
    // so a picture is never decoded wider than the phone can show it.
    final w = width ?? decodeWidth ?? MediaQuery.sizeOf(context).width;
    return Image(
      image: freshNetworkImageProvider(url, cacheWidth: freshDecodeWidth(context, w)),
      width: width,
      height: height,
      fit: fit,
      semanticLabel: semanticLabel,
      excludeFromSemantics: excludeFromSemantics,
      errorBuilder: errorBuilder,
      loadingBuilder: loadingBuilder,
      frameBuilder: frameBuilder,
    );
  }
}
