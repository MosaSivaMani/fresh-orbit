// The one swipeable picture gallery of the app (D193, D207, D266, D276): the product page shows every picture
// the server sends, one per page, with page dots on a paper pill (tap one to go to that picture). It fills the
// space it is given, so the screen keeps its own 3:2 frame and the header never changes size. Pictures arrive
// as widgets (the app passes its image widget with the server's alt text), so the package knows no product model.

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:fresh_theme/fresh_theme.dart';

/// Pages of pictures the customer swipes through, with tappable dots on a paper pill. One picture shows alone
/// (no swipe, no dots); none shows [empty]. Screen readers hear "Picture 2 of 4", can move with the increase and
/// decrease actions, and each dot is a button ("Show picture 3"), so the pictures are reachable without a swipe.
/// Under reduced motion a dot or an action shows its picture at once.
class FreshGallery extends StatefulWidget {
  const FreshGallery({
    super.key,
    required this.itemCount,
    required this.itemBuilder,
    this.empty,
    this.onPageChanged,
    this.dotsAlignment = Alignment.bottomCenter,
    this.dotsPadding = const EdgeInsets.all(12),
  });

  final int itemCount;

  /// Builds one picture; it is given the whole page (or the whole gallery for a single picture). Pass the
  /// server's alt text to the picture's semantics label here.
  final IndexedWidgetBuilder itemBuilder;

  /// Shown when there is no picture (for example the app's placeholder).
  final Widget? empty;
  final ValueChanged<int>? onPageChanged;

  /// Where the dots sit over the picture; move them aside when the screen puts a sticker at the bottom centre.
  final Alignment dotsAlignment;
  final EdgeInsets dotsPadding;

  /// How long a tap on a dot takes to slide to its picture (when motion is allowed).
  static const Duration slideDuration = Duration(milliseconds: 280);

  @override
  State<FreshGallery> createState() => _FreshGalleryState();
}

class _FreshGalleryState extends State<FreshGallery> {
  final _controller = PageController();
  int _index = 0;

  @override
  void didUpdateWidget(FreshGallery oldWidget) {
    super.didUpdateWidget(oldWidget);
    // Fewer pictures after a refresh: stay on the last one that exists.
    if (_index >= widget.itemCount && widget.itemCount > 0) {
      _index = widget.itemCount - 1;
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (_controller.hasClients) _controller.jumpToPage(_index);
      });
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _go(int page) {
    if (page < 0 || page >= widget.itemCount || !_controller.hasClients) return;
    if (motionAllowed(context)) {
      unawaited(_controller.animateToPage(page, duration: FreshGallery.slideDuration, curve: Curves.easeOutCubic));
    } else {
      _controller.jumpToPage(page);
    }
  }

  @override
  Widget build(BuildContext context) {
    final count = widget.itemCount;
    if (count == 0) return widget.empty ?? const SizedBox.expand();
    if (count == 1) return KeyedSubtree(key: const Key('freshGallerySingle'), child: widget.itemBuilder(context, 0));
    final current = _index < count ? _index : count - 1;
    return Semantics(
      key: const Key('freshGallery'),
      container: true,
      label: 'Picture ${current + 1} of $count',
      onIncrease: current < count - 1 ? () => _go(current + 1) : null,
      onDecrease: current > 0 ? () => _go(current - 1) : null,
      child: Stack(
        children: [
          Positioned.fill(
            child: PageView.builder(
              key: const Key('freshGalleryPages'),
              controller: _controller,
              itemCount: count,
              onPageChanged: (i) {
                setState(() => _index = i);
                widget.onPageChanged?.call(i);
              },
              itemBuilder: widget.itemBuilder,
            ),
          ),
          Positioned.fill(
            child: Padding(
              padding: widget.dotsPadding,
              child: Align(
                alignment: widget.dotsAlignment,
                child: _GalleryDots(count: count, index: current, onSelect: _go),
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
      key: const Key('freshGalleryDots'),
      decoration: BoxDecoration(
        color: c.paper.withValues(alpha: 0.9),
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
                label: 'Show picture ${i + 1}',
                onTap: () => onSelect(i),
                excludeSemantics: true,
                child: GestureDetector(
                  key: ValueKey('freshGalleryDot-$i'),
                  behavior: HitTestBehavior.opaque,
                  onTap: () => onSelect(i),
                  child: Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 8),
                    child: Container(
                      key: ValueKey('freshGalleryDotMark-$i'),
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
