// Reusable parts of the rich screens (D181, D207): one line fitted to its width, the yellow chip, the paper pill,
// the small feature card, the category tile, the offer ("Today's special") card, the floating cart bar and a
// column whose last part grows into the room left. Screens use these and never copy them. Every value shown is
// given by the app (rule 4); the parts only lay it out.
//
// The package bundles no fonts: body text names the token family (Inter, which the app bundles), and headings
// that use the app's display family take a style from the app.

import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';

import '../fresh_theme.dart';

TextStyle _sans(Color color, double size, FontWeight weight, {double? height, double? spacing}) => TextStyle(
  fontFamily: FreshFonts.sans,
  fontFamilyFallback: FreshFonts.sansFallback,
  fontWeight: weight,
  fontSize: size,
  height: height,
  letterSpacing: spacing,
  color: color,
);

/// One line of [text] that fits the width it is given. When it is too wide its font size is lowered (down to
/// [minFit] of the size) so it stays on one line, in a box as tall as the full-size line, so the rhythm of a page
/// does not change. If even that is too wide it wraps at that size: the text is never cut. It changes the font
/// size and never scales the widget (no `FittedBox`, D195).
class FreshOneLine extends StatelessWidget {
  const FreshOneLine(this.text, {super.key, required this.style, this.textAlign = TextAlign.center, this.minFit = 0.6});

  final String text;
  final TextStyle style;
  final TextAlign textAlign;

  /// The smallest share of the font size it may be drawn at.
  final double minFit;

  @override
  Widget build(BuildContext context) {
    final scaler = MediaQuery.textScalerOf(context);
    final full = DefaultTextStyle.of(context).style.merge(style);
    final painter = TextPainter(
      text: TextSpan(text: text, style: full),
      textDirection: Directionality.of(context),
      textScaler: scaler,
      maxLines: 1,
    )..layout();
    final natural = painter.size;
    painter.dispose();
    return LayoutBuilder(
      builder: (context, box) {
        final room = box.maxWidth;
        if (!room.isFinite || natural.width <= room || natural.width <= 0) {
          return Text(text, maxLines: 1, softWrap: false, textAlign: textAlign, style: style);
        }
        final want = math.max(0.0, room - 1) / natural.width;
        final fit = math.max(minFit, want);
        // Sized in drawn pixels, so a non-linear system text scale cannot push the line wider again.
        final fitted = style.copyWith(
          fontSize: scaler.scale(full.fontSize ?? 14) * fit,
          letterSpacing: (full.letterSpacing ?? 0) * fit,
        );
        if (want < minFit) {
          return Text(text, textAlign: textAlign, textScaler: TextScaler.noScaling, style: fitted);
        }
        return SizedBox(
          height: natural.height,
          child: Align(
            alignment: textAlign == TextAlign.center ? Alignment.center : AlignmentDirectional.centerStart,
            child: Text(
              text,
              maxLines: 1,
              softWrap: false,
              textAlign: textAlign,
              textScaler: TextScaler.noScaling,
              style: fitted,
            ),
          ),
        );
      },
    );
  }
}

/// A small coloured pill with an optional icon and one line of bold text fitted to the width (the home's
/// next-slot chip). The text is never cut.
class FreshChip extends StatelessWidget {
  const FreshChip({
    super.key,
    required this.text,
    this.icon,
    this.color = FreshHighlightColors.brush,
    this.foreground = FreshHighlightColors.ink,
    this.fontSize = 11.5,
  });

  final String text;
  final IconData? icon;
  final Color color;
  final Color foreground;
  final double fontSize;

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
    decoration: BoxDecoration(color: color, borderRadius: BorderRadius.circular(FreshLayout.pillRadius)),
    child: Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        if (icon != null) ...[Icon(icon, size: 13, color: foreground), const SizedBox(width: 5)],
        Flexible(
          child: FreshOneLine(
            text,
            textAlign: TextAlign.start,
            minFit: 0.7,
            style: _sans(foreground, fontSize, FontWeight.w700),
          ),
        ),
      ],
    ),
  );
}

/// A paper pill with a soft shadow ("Shop fresh catch →").
class FreshPill extends StatelessWidget {
  const FreshPill({
    super.key,
    required this.text,
    required this.color,
    required this.size,
    this.padding = const EdgeInsets.symmetric(horizontal: 13, vertical: 7),
    this.background = FreshHighlightColors.paper,
  });

  final String text;
  final Color color;
  final double size;
  final EdgeInsets padding;
  final Color background;

  @override
  Widget build(BuildContext context) => Container(
    padding: padding,
    decoration: BoxDecoration(
      color: background,
      borderRadius: BorderRadius.circular(FreshLayout.pillRadius),
      boxShadow: const [BoxShadow(color: Color(0x1F000000), blurRadius: 6, offset: Offset(0, 2))],
    ),
    child: Text(text, maxLines: 1, overflow: TextOverflow.ellipsis, style: _sans(color, size, FontWeight.w700)),
  );
}

/// One of the home's two equal cards: an icon and a two-line title (the second line on the brush or plain), a
/// short line, a pill at the bottom and a picture on the lower right. The picture sits below the title, never
/// over it: the part under the title is at least as tall as the picture reaches.
class FreshFeatureCard extends StatelessWidget {
  const FreshFeatureCard({
    super.key,
    required this.color,
    required this.accent,
    required this.icon,
    required this.title,
    required this.titleStyle,
    required this.body,
    required this.action,
    required this.picture,
    required this.pictureSize,
    required this.onTap,
    this.brushed,
    this.secondLine,
    this.pictureRight = -6,
    this.pictureBottom = 26,
  });

  final Color color;

  /// The pill's text colour.
  final Color accent;
  final Widget icon;
  final String title;

  /// The title's style (the app's display family).
  final TextStyle titleStyle;

  /// The second title line, on the yellow brush.
  final String? brushed;

  /// The second title line, plain.
  final String? secondLine;
  final String body;
  final String action;

  /// The picture, drawn at [pictureSize] with its bottom-right corner [pictureRight] / [pictureBottom] from the
  /// card's.
  final Widget picture;
  final Size pictureSize;
  final double pictureRight;
  final double pictureBottom;
  final VoidCallback onTap;

  static const double _padding = 10;

  @override
  Widget build(BuildContext context) {
    const ink = FreshHighlightColors.ink;
    final second = brushed ?? secondLine;
    // The pill: 5 + 5 padding around one line of 11.5 text.
    final pillHeight = 10 + MediaQuery.textScalerOf(context).scale(11.5) * 1.3;
    // Under the title, room down to the picture's top, so the picture never covers a title word.
    final below = math.max(0.0, pictureSize.height + pictureBottom - _padding);
    return Semantics(
      button: true,
      label: '$title ${second ?? ''}. $body $action'.replaceAll('  ', ' '),
      excludeSemantics: true,
      child: Stack(
        clipBehavior: Clip.none,
        children: [
          Positioned.fill(
            child: Material(
              color: color,
              borderRadius: BorderRadius.circular(FreshLayout.cardRadius),
              child: InkWell(borderRadius: BorderRadius.circular(FreshLayout.cardRadius), onTap: onTap),
            ),
          ),
          Positioned(
            right: pictureRight,
            bottom: pictureBottom,
            width: pictureSize.width,
            height: pictureSize.height,
            child: IgnorePointer(child: picture),
          ),
          if (brushed != null)
            const Positioned(
              right: 58,
              top: 6,
              child: Sparks(color: FreshHighlightColors.brush, degrees: -10, scale: 0.6),
            ),
          IgnorePointer(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(12, _padding, 12, _padding),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Padding(padding: const EdgeInsets.only(top: 1), child: icon),
                      const SizedBox(width: 6),
                      Flexible(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Text(title, style: titleStyle),
                            if (brushed != null)
                              BrushHighlight(
                                degrees: -2,
                                padding: const EdgeInsets.symmetric(horizontal: 3),
                                child: Text(brushed!, style: titleStyle),
                              )
                            else if (secondLine != null)
                              Text(secondLine!, style: titleStyle),
                          ],
                        ),
                      ),
                    ],
                  ),
                  ConstrainedBox(
                    constraints: BoxConstraints(minHeight: below),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        const SizedBox(height: 4),
                        SizedBox(width: 88, child: Text(body, style: _sans(ink, 11.5, FontWeight.w400, height: 1.3))),
                        // Room for the pill pinned at the bottom.
                        SizedBox(height: 8 + pillHeight),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
          Positioned(
            left: 12,
            right: 12,
            bottom: _padding,
            child: IgnorePointer(
              child: Align(
                alignment: Alignment.bottomLeft,
                child: FreshPill(
                  text: '$action →',
                  color: accent,
                  size: 11.5,
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// A category in a row of equal columns: a 50 dp tinted circle with the picture (or an icon, or the first letter
/// when there is no picture) and the name under it in one size, on at most two lines.
class FreshCategoryTile extends StatelessWidget {
  const FreshCategoryTile({
    super.key,
    required this.label,
    required this.tint,
    required this.onTap,
    this.picture,
    this.icon,
    this.labelColor = FreshHighlightColors.ink,
    this.shadow = const Color(0x1A14302F),
  });

  final String label;
  final Color tint;
  final VoidCallback onTap;

  /// Fills the circle (cropped to it); null shows [icon] or the first letter.
  final Widget? picture;
  final IconData? icon;
  final Color labelColor;
  final Color shadow;

  static const double circle = 50;

  /// What the circle shows with no picture: the first letter of [label]. Also the fallback for a picture that
  /// is missing or fails to load.
  static Widget initial(String label) => Center(
    child: Text(
      label.isEmpty ? '' : label.characters.first.toUpperCase(),
      style: _sans(FreshHighlightColors.ink, 19, FontWeight.w800),
    ),
  );

  @override
  Widget build(BuildContext context) {
    final mark = icon != null
        ? Center(child: Icon(icon, size: 22, color: FreshHighlightColors.ink))
        : FreshCategoryTile.initial(label);
    return Semantics(
      button: true,
      label: label,
      excludeSemantics: true,
      child: InkWell(
        borderRadius: BorderRadius.circular(FreshLayout.tileRadius),
        onTap: onTap,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              width: circle,
              height: circle,
              decoration: BoxDecoration(
                color: tint,
                shape: BoxShape.circle,
                border: Border.all(color: FreshHighlightColors.paper, width: 2),
                boxShadow: [BoxShadow(color: shadow, blurRadius: 6, offset: const Offset(0, 2))],
              ),
              clipBehavior: Clip.antiAlias,
              child: picture ?? mark,
            ),
            const SizedBox(height: 5),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 1),
              child: Text(
                label,
                textAlign: TextAlign.center,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: _sans(labelColor, 11.5, FontWeight.w600, height: 1.2),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// An offer card ("Today's special"): the picture on top, then the title on one line and the description on up
/// to two, an optional badge on the picture and a chevron when it opens something.
class FreshSpecialCard extends StatelessWidget {
  const FreshSpecialCard({
    super.key,
    required this.title,
    required this.tint,
    required this.pictureHeight,
    this.subtitle,
    this.badge,
    this.picture,
    this.onTap,
    this.width = defaultWidth,
    this.paper = FreshHighlightColors.paper,
    this.ink = FreshHighlightColors.ink,
    this.muted = const Color(0xFF5B6F6B),
    this.chevronBackground = const Color(0xFFF3EBDD),
    this.shadow = const Color(0x1A14302F),
  });

  final String title;
  final String? subtitle;
  final String? badge;

  /// Fills the picture area (cropped); null shows the [tint].
  final Widget? picture;
  final Color tint;
  final double pictureHeight;

  /// Null: the card does not open anything and is not a button.
  final VoidCallback? onTap;
  final double width;
  final Color paper;
  final Color ink;
  final Color muted;
  final Color chevronBackground;
  final Color shadow;

  /// 200 wide, so the second card peeks in on a phone.
  static const double defaultWidth = 200;

  /// The picture's smallest height.
  static const double minPictureHeight = 54;

  /// The text part's height under the picture: one title line and two description lines.
  static double textHeight(TextScaler scaler) => 4 + scaler.scale(13) * 1.25 + 2 * scaler.scale(11) * 1.3 + 8;

  @override
  Widget build(BuildContext context) {
    final soft = ColoredBox(color: tint);
    return Semantics(
      button: onTap != null,
      label: [title, ?subtitle, ?badge].join(', '),
      excludeSemantics: true,
      child: Container(
        width: width,
        decoration: BoxDecoration(
          color: paper,
          borderRadius: BorderRadius.circular(FreshLayout.tileRadius),
          boxShadow: [BoxShadow(color: shadow, blurRadius: 10, offset: const Offset(0, 2))],
        ),
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: onTap,
          child: Stack(
            children: [
              Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  SizedBox(height: pictureHeight, child: picture ?? soft),
                  Padding(
                    padding: const EdgeInsets.fromLTRB(10, 4, 34, 0),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          title,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: _sans(ink, 13, FontWeight.w700, height: 1.25),
                        ),
                        if (subtitle != null)
                          Text(
                            subtitle!,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                            style: _sans(muted, 11, FontWeight.w400, height: 1.3),
                          ),
                      ],
                    ),
                  ),
                ],
              ),
              if (badge != null)
                Positioned(
                  left: 8,
                  top: 7,
                  child: Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8),
                    decoration: BoxDecoration(
                      color: FreshHighlightColors.brush,
                      borderRadius: BorderRadius.circular(FreshLayout.pillRadius),
                      border: Border.all(color: FreshHighlightColors.ink, width: 1.8),
                    ),
                    child: Text(badge!, style: _sans(FreshHighlightColors.ink, 10.5, FontWeight.w800, height: 1.6)),
                  ),
                ),
              if (onTap != null)
                Positioned(
                  right: 8,
                  bottom: 9,
                  child: Container(
                    width: 22,
                    height: 22,
                    decoration: BoxDecoration(color: chevronBackground, shape: BoxShape.circle),
                    alignment: Alignment.center,
                    child: Icon(Icons.chevron_right_rounded, size: 16, color: ink),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

/// The floating dark-teal cart bar above the menu while the cart has lines (D176): 52 tall, inside the gutters,
/// "2 ITEMS ADDED", the amount the app formatted from the server, the line pictures that exist (none when no
/// line has one) and "View cart". It shows only what it is given (rule 4).
class FreshCartBar extends StatelessWidget {
  const FreshCartBar({
    super.key,
    required this.count,
    required this.amount,
    required this.onTap,
    this.thumbnails = const [],
  });

  final int count;

  /// Already formatted from a server value (the cart's estimated total).
  final String amount;
  final VoidCallback onTap;

  /// Picture URLs of cart lines that have one; at most two are shown, and none leaves the space out.
  final List<String> thumbnails;

  /// The bar's colour, the same in light and dark.
  static const Color background = Color(0xFF0C4A43);
  static const Color _caption = Color(0xFFBFE3E0);

  @override
  Widget build(BuildContext context) {
    const paper = FreshHighlightColors.paper;
    final items = '$count ${count == 1 ? 'item' : 'items'}';
    final thumbs = thumbnails.where((u) => u.isNotEmpty).take(2).toList();
    return Padding(
      padding: const EdgeInsets.fromLTRB(FreshLayout.gutter, 0, FreshLayout.gutter, 8),
      child: Semantics(
        button: true,
        label: '$items added, $amount. View cart',
        excludeSemantics: true,
        child: Material(
          key: const Key('viewCartBar'),
          color: background,
          borderRadius: BorderRadius.circular(FreshLayout.barRadius),
          elevation: 6,
          shadowColor: background.withValues(alpha: 0.6),
          child: InkWell(
            borderRadius: BorderRadius.circular(FreshLayout.barRadius),
            onTap: onTap,
            child: ConstrainedBox(
              constraints: const BoxConstraints(minHeight: 52),
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 6),
                child: Row(
                  children: [
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text(
                            '$items added'.toUpperCase(),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: _sans(_caption, 10.5, FontWeight.w700, height: 1.15, spacing: 0.84),
                          ),
                          Text(
                            amount,
                            maxLines: 1,
                            style: _sans(
                              paper,
                              17,
                              FontWeight.w800,
                              height: 1.15,
                            ).copyWith(fontFeatures: const [FontFeature.tabularFigures()]),
                          ),
                        ],
                      ),
                    ),
                    if (thumbs.isNotEmpty) ...[
                      _CartThumbs(urls: thumbs),
                      const SizedBox(width: 10),
                    ],
                    Container(
                      padding: const EdgeInsets.only(left: 12),
                      decoration: BoxDecoration(
                        border: Border(left: BorderSide(color: paper.withValues(alpha: 0.3))),
                      ),
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text('View cart', style: _sans(paper, 15, FontWeight.w700)),
                          const Icon(Icons.chevron_right_rounded, color: paper, size: 20),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// Up to two overlapping round line pictures with a paper ring. A picture that fails to load leaves its circle
/// out rather than showing an empty one.
class _CartThumbs extends StatelessWidget {
  const _CartThumbs({required this.urls});

  final List<String> urls;

  static const double size = 32;

  @override
  Widget build(BuildContext context) => ExcludeSemantics(
    child: SizedBox(
      width: size + (urls.length - 1) * (size - 8),
      height: size,
      child: Stack(
        children: [
          for (var i = 0; i < urls.length; i++)
            Positioned(
              left: i * (size - 8),
              child: Image.network(
                urls[i],
                key: ValueKey('cartThumb-$i'),
                width: size,
                height: size,
                fit: BoxFit.cover,
                excludeFromSemantics: true,
                frameBuilder: (context, child, frame, _) => frame == null
                    ? const SizedBox.square(dimension: size)
                    : Container(
                        width: size,
                        height: size,
                        decoration: BoxDecoration(
                          shape: BoxShape.circle,
                          border: Border.all(color: FreshHighlightColors.paper, width: 2),
                        ),
                        clipBehavior: Clip.antiAlias,
                        child: child,
                      ),
                errorBuilder: (_, _, _) => const SizedBox.square(dimension: size),
              ),
            ),
        ],
      ),
    ),
  );
}

/// [head] then [tail] in one column across the width. When both together are shorter than [minHeight], the tail
/// is laid out again with exactly the room left, so a page fills the screen with no dead gap at the bottom
/// (D221). With no room to give (or an unbounded [minHeight] of 0) both keep their natural height.
class FreshGrowTail extends MultiChildRenderObjectWidget {
  FreshGrowTail({super.key, required this.minHeight, required Widget head, required Widget tail})
    : super(children: [head, tail]);

  final double minHeight;

  @override
  RenderObject createRenderObject(BuildContext context) => _RenderGrowTail(minHeight);

  @override
  void updateRenderObject(BuildContext context, _RenderGrowTail renderObject) => renderObject.minHeight = minHeight;
}

class _GrowParentData extends ContainerBoxParentData<RenderBox> {}

class _RenderGrowTail extends RenderBox
    with
        ContainerRenderObjectMixin<RenderBox, _GrowParentData>,
        RenderBoxContainerDefaultsMixin<RenderBox, _GrowParentData> {
  _RenderGrowTail(this._minHeight);

  double _minHeight;
  double get minHeight => _minHeight;
  set minHeight(double value) {
    if (value == _minHeight) return;
    _minHeight = value;
    markNeedsLayout();
  }

  @override
  void setupParentData(RenderBox child) {
    if (child.parentData is! _GrowParentData) child.parentData = _GrowParentData();
  }

  @override
  Size computeDryLayout(BoxConstraints constraints) {
    final width = constraints.maxWidth;
    var height = 0.0;
    for (var child = firstChild; child != null; child = childAfter(child)) {
      height += child.getDryLayout(BoxConstraints(minWidth: width, maxWidth: width)).height;
    }
    final target = minHeight.isFinite ? math.max(minHeight, height) : height;
    return constraints.constrain(Size(width, target));
  }

  @override
  void performLayout() {
    final width = constraints.maxWidth;
    final natural = BoxConstraints(minWidth: width, maxWidth: width);
    final head = firstChild;
    final tail = head == null ? null : childAfter(head);
    var y = 0.0;
    if (head != null) {
      head.layout(natural, parentUsesSize: true);
      (head.parentData! as _GrowParentData).offset = Offset.zero;
      y = head.size.height;
    }
    if (tail != null) {
      tail.layout(natural, parentUsesSize: true);
      final room = minHeight - y;
      if (minHeight.isFinite && room > tail.size.height) {
        tail.layout(BoxConstraints.tightFor(width: width, height: room), parentUsesSize: true);
      }
      (tail.parentData! as _GrowParentData).offset = Offset(0, y);
      y += tail.size.height;
    }
    size = constraints.constrain(Size(width, y));
  }

  @override
  double computeMinIntrinsicWidth(double height) {
    var width = 0.0;
    for (var child = firstChild; child != null; child = childAfter(child)) {
      width = math.max(width, child.getMinIntrinsicWidth(double.infinity));
    }
    return width;
  }

  @override
  double computeMaxIntrinsicWidth(double height) {
    var width = 0.0;
    for (var child = firstChild; child != null; child = childAfter(child)) {
      width = math.max(width, child.getMaxIntrinsicWidth(double.infinity));
    }
    return width;
  }

  @override
  double computeMinIntrinsicHeight(double width) => computeMaxIntrinsicHeight(width);

  @override
  double computeMaxIntrinsicHeight(double width) {
    var height = 0.0;
    for (var child = firstChild; child != null; child = childAfter(child)) {
      height += child.getMaxIntrinsicHeight(width);
    }
    return minHeight.isFinite ? math.max(minHeight, height) : height;
  }

  @override
  void paint(PaintingContext context, Offset offset) => defaultPaint(context, offset);

  @override
  bool hitTestChildren(BoxHitTestResult result, {required Offset position}) =>
      defaultHitTestChildren(result, position: position);
}
