// One grid for every rich screen (D176, D181): every block starts and ends [FreshLayout.gutter] from the screen
// edges, cards sit [FreshLayout.cardGap] apart and sections [FreshLayout.sectionGap] apart. The numbers come from
// the approved home preview (docs/design/mockups/app-home-rich.html, designed 390 wide).

import 'dart:math' as math;

import 'package:flutter/rendering.dart';
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

  /// Round icon buttons in headers (search, wishlist, back).
  static const double iconButton = 46;

  /// Height of a screen header row ([FreshPageHeader]), below the status bar.
  static const double headerHeight = 64;

  /// The shape of every illustration frame in cards and banners (D193): 3 wide, 2 high.
  static const double pictureAspect = 3 / 2;

  /// Scrolling pages stand still at their ends (D182): no bounce on any platform.
  static const ScrollPhysics physics = ClampingScrollPhysics();

  /// Horizontal page padding.
  static const EdgeInsets page = EdgeInsets.symmetric(horizontal: gutter);

  /// Padding of a scrolling list or page body: the gutters, a card gap on top and a section gap at the end.
  static const EdgeInsets list = EdgeInsets.fromLTRB(gutter, cardGap, gutter, sectionGap);

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
    if (stretch) return _EqualHeightColumns(gap: gap, children: children);
    return Row(
      crossAxisAlignment: crossAxisAlignment,
      children: [
        for (var i = 0; i < children.length; i++) ...[
          if (i > 0) SizedBox(width: gap),
          Expanded(child: children[i]),
        ],
      ],
    );
  }
}

/// Lays every child out at its natural height in one column, then again at the tallest height, so the columns
/// match without relying on intrinsic sizes. A child must not need a bounded height on either pass.
///
/// The second pass gives the tallest height as a minimum, not as a tight height: a child laid out with tight
/// constraints becomes its own relayout boundary, so when its content later grows (a server answer changes a
/// card's words) only the child would lay out again, inside the old height, and overflow. With a minimum and
/// `parentUsesSize` every change in a child measures all the columns again.
class _EqualHeightColumns extends MultiChildRenderObjectWidget {
  const _EqualHeightColumns({required this.gap, required super.children});

  final double gap;

  @override
  RenderObject createRenderObject(BuildContext context) => _RenderEqualHeightColumns(gap);

  @override
  void updateRenderObject(BuildContext context, _RenderEqualHeightColumns renderObject) => renderObject.gap = gap;
}

class _ColumnParentData extends ContainerBoxParentData<RenderBox> {}

class _RenderEqualHeightColumns extends RenderBox
    with
        ContainerRenderObjectMixin<RenderBox, _ColumnParentData>,
        RenderBoxContainerDefaultsMixin<RenderBox, _ColumnParentData> {
  _RenderEqualHeightColumns(this._gap);

  double _gap;
  double get gap => _gap;
  set gap(double value) {
    if (value == _gap) return;
    _gap = value;
    markNeedsLayout();
  }

  @override
  void setupParentData(RenderBox child) {
    if (child.parentData is! _ColumnParentData) child.parentData = _ColumnParentData();
  }

  double _columnWidth(double width) => childCount == 0 ? 0 : math.max(0, (width - gap * (childCount - 1)) / childCount);

  @override
  Size computeDryLayout(BoxConstraints constraints) {
    final column = _columnWidth(constraints.maxWidth);
    var height = 0.0;
    for (var child = firstChild; child != null; child = childAfter(child)) {
      height = math.max(height, child.getDryLayout(BoxConstraints(minWidth: column, maxWidth: column)).height);
    }
    return constraints.constrain(Size(constraints.maxWidth, height));
  }

  @override
  void performLayout() {
    final width = constraints.maxWidth;
    final column = _columnWidth(width);
    var height = 0.0;
    for (var child = firstChild; child != null; child = childAfter(child)) {
      child.layout(BoxConstraints(minWidth: column, maxWidth: column), parentUsesSize: true);
      height = math.max(height, child.size.height);
    }
    height = constraints.constrainHeight(height);
    var tallest = 0.0;
    var x = 0.0;
    for (var child = firstChild; child != null; child = childAfter(child)) {
      // No maximum: a column that fills its height (a Column) would otherwise take the whole space given.
      child.layout(BoxConstraints(minWidth: column, maxWidth: column, minHeight: height), parentUsesSize: true);
      tallest = math.max(tallest, child.size.height);
      (child.parentData! as _ColumnParentData).offset = Offset(x, 0);
      x += column + gap;
    }
    size = constraints.constrain(Size(width, tallest));
  }

  @override
  double computeMinIntrinsicHeight(double width) => computeMaxIntrinsicHeight(width);

  @override
  double computeMaxIntrinsicHeight(double width) {
    final column = _columnWidth(width);
    var height = 0.0;
    for (var child = firstChild; child != null; child = childAfter(child)) {
      height = math.max(height, child.getMaxIntrinsicHeight(column));
    }
    return height;
  }

  @override
  void paint(PaintingContext context, Offset offset) => defaultPaint(context, offset);

  @override
  bool hitTestChildren(BoxHitTestResult result, {required Offset position}) =>
      defaultHitTestChildren(result, position: position);
}

/// Shows [child] at the full width it is given. When the child is taller than the space, it is laid out wider
/// and scaled down so that it still spans the full width and fits the height (a screen that "does not move up and
/// down", D182) — unlike a `FittedBox`, which shrinks the width too and leaves side gaps.
class FreshFitWidth extends SingleChildRenderObjectWidget {
  const FreshFitWidth({super.key, super.child});

  @override
  RenderObject createRenderObject(BuildContext context) => RenderFreshFitWidth();
}

/// The render object of [FreshFitWidth]; [scale] is the factor the child is drawn at (1 when it fits).
class RenderFreshFitWidth extends RenderProxyBox {
  double _scale = 1;
  double get scale => _scale;

  Matrix4 get _transform => Matrix4.diagonal3Values(_scale, _scale, 1);

  @override
  Size computeDryLayout(BoxConstraints constraints) =>
      constraints.constrain(Size(constraints.maxWidth, constraints.hasBoundedHeight ? constraints.maxHeight : 0));

  @override
  void performLayout() {
    final width = constraints.maxWidth;
    final height = constraints.maxHeight;
    final box = child;
    if (box == null) {
      _scale = 1;
      size = constraints.constrain(Size(width, 0));
      return;
    }
    box.layout(BoxConstraints.tightFor(width: width), parentUsesSize: true);
    var scale = 1.0;
    if (height.isFinite && box.size.height > height && box.size.height > 0) {
      // Laid out wider, the text wraps less, so the child only gets shorter: this scale fits the height.
      scale = height / box.size.height;
      box.layout(BoxConstraints.tightFor(width: width / scale), parentUsesSize: true);
    }
    _scale = scale;
    size = constraints.constrain(Size(width, box.size.height * scale));
  }

  @override
  void paint(PaintingContext context, Offset offset) {
    if (child == null) return;
    if (_scale == 1) {
      layer = null;
      context.paintChild(child!, offset);
      return;
    }
    layer = context.pushTransform(
      needsCompositing,
      offset,
      _transform,
      (context, offset) => context.paintChild(child!, offset),
      oldLayer: layer is TransformLayer ? layer! as TransformLayer : null,
    );
  }

  @override
  bool hitTestChildren(BoxHitTestResult result, {required Offset position}) {
    if (child == null) return false;
    return result.addWithPaintTransform(
      transform: _transform,
      position: position,
      hitTest: (result, position) => child!.hitTest(result, position: position),
    );
  }

  @override
  void applyPaintTransform(RenderBox child, Matrix4 transform) => transform.multiply(_transform);
}
