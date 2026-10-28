// A swipeable picture gallery with page dots (D262): the product page shows every picture the server sends,
// one per page, and the dots say which one is on screen. Pictures arrive as widgets, so the package knows no
// product model.

import 'package:flutter/material.dart';
import 'package:fresh_theme/fresh_theme.dart';

/// Pages of pictures the customer swipes through, with [PageDots] on a paper pill at the bottom. One picture
/// shows alone (no swipe, no dots); none shows [empty]. Screen readers hear "Picture 2 of 4" and can move with
/// the increase and decrease actions, so the pictures are reachable without a swipe.
class FreshGallery extends StatefulWidget {
  const FreshGallery({
    super.key,
    required this.itemCount,
    required this.itemBuilder,
    this.empty,
    this.onPageChanged,
    this.dotsBottom = 12,
  });

  final int itemCount;
  final IndexedWidgetBuilder itemBuilder;

  /// Shown when there is no picture (for example the app's placeholder).
  final Widget? empty;
  final ValueChanged<int>? onPageChanged;

  /// Space between the dots and the bottom edge.
  final double dotsBottom;

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
      _controller.animateToPage(page, duration: FreshDurations.normal, curve: Curves.easeOut);
    } else {
      _controller.jumpToPage(page);
    }
  }

  @override
  Widget build(BuildContext context) {
    final count = widget.itemCount;
    if (count == 0) return widget.empty ?? const SizedBox.expand();
    if (count == 1) return KeyedSubtree(key: const Key('freshGallerySingle'), child: widget.itemBuilder(context, 0));
    final c = illustratedColors(context);
    return Semantics(
      key: const Key('freshGallery'),
      container: true,
      label: 'Picture ${_index + 1} of $count',
      onIncrease: _index < count - 1 ? () => _go(_index + 1) : null,
      onDecrease: _index > 0 ? () => _go(_index - 1) : null,
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
          Positioned(
            left: 0,
            right: 0,
            bottom: widget.dotsBottom,
            child: IgnorePointer(
              child: Center(
                child: Container(
                  key: const Key('freshGalleryDots'),
                  padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 5),
                  decoration: BoxDecoration(
                    color: c.paper.withValues(alpha: 0.9),
                    borderRadius: BorderRadius.circular(FreshRadius.full),
                  ),
                  child: ExcludeSemantics(child: PageDots(count: count, index: _index)),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
