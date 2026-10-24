// One grid for every rich screen (D176, D181): every block starts and ends [FreshLayout.gutter] from the screen
// edges, cards sit [FreshLayout.cardGap] apart and sections [FreshLayout.sectionGap] apart. The numbers come from
// the approved home preview (docs/design/mockups/app-home-rich.html, designed 390 wide).

import 'dart:math' as math;

import 'package:flutter/widgets.dart';

abstract final class FreshLayout {
  /// The width the previews are drawn at.
  static const double designWidth = 390;

  /// Left and right page margin.
  static const double gutter = 16;

  /// Gap between cards in a row or a list.
  static const double cardGap = 12;

  /// Gap between sections.
  static const double sectionGap = 24;

  /// Corner radii: small cards (the home duo), the hero card, list tiles (offers), the cart bar, the floating
  /// menu, and pills.
  static const double cardRadius = 20;
  static const double heroRadius = 24;
  static const double tileRadius = 16;
  static const double barRadius = 18;
  static const double navRadius = 22;
  static const double pillRadius = 99;

  /// Round icon buttons in headers (search, wishlist).
  static const double iconButton = 46;

  /// Horizontal page padding.
  static const EdgeInsets page = EdgeInsets.symmetric(horizontal: gutter);

  /// The width between the gutters of a screen [screenWidth] wide.
  static double contentWidth(double screenWidth) => math.max(0, screenWidth - 2 * gutter);

  /// The width of one of [columns] equal columns inside the gutters, [gap] apart.
  static double columnWidth(double screenWidth, int columns, {double gap = cardGap}) {
    assert(columns > 0, 'columns must be at least 1');
    return math.max(0, (contentWidth(screenWidth) - gap * (columns - 1)) / columns);
  }
}

/// [children] in equal columns [gap] apart across the width it is given (put it inside [FreshLayout.page]).
/// Each child gets exactly one column; [stretch] makes them all as tall as the tallest.
class FreshColumns extends StatelessWidget {
  const FreshColumns({
    super.key,
    required this.children,
    this.gap = FreshLayout.cardGap,
    this.stretch = false,
    this.crossAxisAlignment = CrossAxisAlignment.start,
  });

  final List<Widget> children;
  final double gap;
  final bool stretch;
  final CrossAxisAlignment crossAxisAlignment;

  @override
  Widget build(BuildContext context) {
    final row = Row(
      crossAxisAlignment: stretch ? CrossAxisAlignment.stretch : crossAxisAlignment,
      children: [
        for (var i = 0; i < children.length; i++) ...[
          if (i > 0) SizedBox(width: gap),
          Expanded(child: children[i]),
        ],
      ],
    );
    return stretch ? IntrinsicHeight(child: row) : row;
  }
}
