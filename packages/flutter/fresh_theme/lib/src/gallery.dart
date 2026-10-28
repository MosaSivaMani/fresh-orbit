// The swipeable picture gallery of a product page (D193, D207): every picture as a page, page dots over the
// bottom of the picture (tap one to go to that picture) and "Photo i of n" for screen readers. It fills the
// space it is given, so the screen keeps its own 3:2 frame ([FreshPictureFrame]) and the header never changes
// size. The app may pass its own image widget; nothing here reads an app model.

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:fresh_theme/fresh_theme.dart';

/// One picture of a [FreshImageGallery]: its address and the server's alt text (empty or null = decorative).
@immutable
class FreshGalleryImage {
  const FreshGalleryImage({required this.url, this.alt});

  final String url;
  final String? alt;
}

/// Builds the widget of one picture; it is given the whole page (or the whole gallery for a single picture).
typedef FreshGalleryImageBuilder = Widget Function(BuildContext context, FreshGalleryImage image, int index);

/// All pictures of a product, one page each, filling the space it is given.
///
/// - No pictures: [emptyBuilder], or the fish placeholder of [ProductPhoto].
/// - One picture: just that picture (no pages, no dots).
/// - More: a [PageView] to swipe, page dots at [dotsAlignment] (the current one a wider ink pill; tapping a dot
///   goes to that picture, at once when the platform asks for less motion) and "Photo i of n" for screen readers.
///
/// [imageBuilder] draws one picture; by default a [ProductPhoto] in the 3:2 frame with the alt text.
class FreshImageGallery extends StatefulWidget {
  const FreshImageGallery({
    super.key,
    required this.images,
    this.imageBuilder,
    this.emptyBuilder,
    this.dotsAlignment = Alignment.bottomCenter,
    this.dotsPadding = const EdgeInsets.all(12),
    this.onPageChanged,
  });

  final List<FreshGalleryImage> images;
  final FreshGalleryImageBuilder? imageBuilder;
  final WidgetBuilder? emptyBuilder;

  /// Where the dots sit over the picture; move them aside when the screen puts a sticker at the bottom centre.
  final Alignment dotsAlignment;
  final EdgeInsets dotsPadding;
  final ValueChanged<int>? onPageChanged;

  /// How long a tap on a dot takes to slide to its picture (when motion is allowed).
  static const Duration slideDuration = Duration(milliseconds: 280);

  @override
  State<FreshImageGallery> createState() => _FreshImageGalleryState();
}

class _FreshImageGalleryState extends State<FreshImageGallery> {
  final PageController _controller = PageController();
  int _index = 0;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Widget _image(BuildContext context, int index) {
    final image = widget.images[index];
    final builder = widget.imageBuilder;
    if (builder != null) return builder(context, image, index);
    final alt = image.alt;
    return ProductPhoto(
      url: image.url,
      semanticLabel: alt == null || alt.isEmpty ? null : alt,
      aspectRatio: FreshLayout.pictureAspect,
      radius: 0,
      transparent: true,
    );
  }

  void _showPage(int index) {
    if (!_controller.hasClients) return;
    if (motionAllowed(context)) {
      unawaited(
        _controller.animateToPage(index, duration: FreshImageGallery.slideDuration, curve: Curves.easeOutCubic),
      );
    } else {
      _controller.jumpToPage(index);
    }
  }

  @override
  Widget build(BuildContext context) {
    final count = widget.images.length;
    if (count == 0) {
      final empty = widget.emptyBuilder;
      if (empty != null) return empty(context);
      return const ProductPhoto(url: null, aspectRatio: FreshLayout.pictureAspect, radius: 0, transparent: true);
    }
    if (count == 1) return _image(context, 0);

    final current = _index < count ? _index : count - 1;
    return Semantics(
      container: true,
      label: 'Photo ${current + 1} of $count',
      child: Stack(
        children: [
          Positioned.fill(
            child: PageView.builder(
              controller: _controller,
              itemCount: count,
              onPageChanged: (i) {
                setState(() => _index = i);
                widget.onPageChanged?.call(i);
              },
              itemBuilder: _image,
            ),
          ),
          Positioned.fill(
            child: Padding(
              padding: widget.dotsPadding,
              child: Align(
                alignment: widget.dotsAlignment,
                child: _GalleryDots(count: count, index: current, onSelect: _showPage),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// The gallery's dots on a soft paper pill (so they read on any picture): the current one a 24 px ink pill, the
/// others 7 px line dots. Each dot is a button for its picture.
class _GalleryDots extends StatelessWidget {
  const _GalleryDots({required this.count, required this.index, required this.onSelect});

  final int count;
  final int index;
  final ValueChanged<int> onSelect;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    return DecoratedBox(
      decoration: BoxDecoration(
        color: c.paper.withValues(alpha: 0.85),
        borderRadius: BorderRadius.circular(FreshRadius.full),
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 4),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            for (var i = 0; i < count; i++)
              Semantics(
                container: true,
                button: true,
                selected: i == index,
                label: 'Show photo ${i + 1}',
                onTap: () => onSelect(i),
                excludeSemantics: true,
                child: GestureDetector(
                  key: ValueKey('fresh-gallery-dot-$i'),
                  behavior: HitTestBehavior.opaque,
                  onTap: () => onSelect(i),
                  child: Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 8),
                    child: Container(
                      key: ValueKey('fresh-gallery-dot-mark-$i'),
                      width: i == index ? 24 : 7,
                      height: 7,
                      decoration: BoxDecoration(
                        color: i == index ? c.ink : c.line,
                        borderRadius: BorderRadius.circular(FreshRadius.full),
                      ),
                    ),
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}
