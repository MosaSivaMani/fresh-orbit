// The sea floor (D289, owner's order-placed reference docs/design/mockups/app-order-placed-reference-2026-10-29.png):
// a decorative band at the very bottom of calm pages — three teal waves, two small fish, bubbles, coral branches
// at the bottom left and seaweed at the bottom right. Drawn in code, static (nothing moves), excluded from
// semantics. The waves stretch with the width; fish, coral and seaweed keep their shape and scale with the band's
// height. Drop [FreshSeaFloor] at the end of a column, or build the page with [FreshSeaFloorScroll], which keeps
// the band at the bottom of a short page and at the end of a long one.

import 'dart:math' as math;

import 'package:flutter/widgets.dart';

/// Colours of the sea floor (the same in light and dark: the band is its own little scene).
abstract final class FreshSeaFloorColors {
  /// The far wave, nearly the page colour.
  static const Color waveBack = Color(0xFFD5EEE9);

  /// The middle wave, where the fish swim.
  static const Color waveMid = Color(0xFF9ED3CC);

  /// The near wave and the floor under it.
  static const Color waveFront = Color(0xFF4E9F95);

  /// The fish silhouettes.
  static const Color fish = Color(0xFF3E8F86);

  /// Bubble rims.
  static const Color bubble = Color(0xFFFFFDF8);

  /// Coral branches (bottom left) and the small coral behind the seaweed.
  static const Color coral = Color(0xFFF07A5A);
  static const Color coralLight = Color(0xFFF59B7C);

  /// Seaweed blades (bottom right).
  static const Color seaweed = Color(0xFF1F6F67);
  static const Color seaweedLight = Color(0xFF2E8F84);
}

/// The sea-floor band, full width and [height] tall, plus the bottom system inset (painted in the floor colour)
/// when [includeBottomInset] is set, so it can sit at the very bottom of a screen. Decorative: excluded from
/// semantics.
class FreshSeaFloor extends StatelessWidget {
  const FreshSeaFloor({super.key, this.height = defaultHeight, this.includeBottomInset = true});

  /// The band's height without the inset.
  static const double defaultHeight = 100;

  final double height;
  final bool includeBottomInset;

  @override
  Widget build(BuildContext context) {
    final inset = includeBottomInset ? MediaQuery.paddingOf(context).bottom : 0.0;
    return ExcludeSemantics(
      child: SizedBox(
        width: double.infinity,
        height: height + inset,
        child: CustomPaint(painter: FreshSeaFloorPainter(inset: inset)),
      ),
    );
  }
}

/// A scrolling page that ends with the sea floor: [children] in a list (inside [padding]), then the band. On a
/// short page the band sits at the bottom of the screen; on a long page it ends the scroll. Use it as a
/// Scaffold body.
class FreshSeaFloorScroll extends StatelessWidget {
  const FreshSeaFloorScroll({
    super.key,
    required this.children,
    this.padding = EdgeInsets.zero,
    this.gap = 16,
    this.floorHeight = FreshSeaFloor.defaultHeight,
    this.controller,
  });

  final List<Widget> children;
  final EdgeInsetsGeometry padding;

  /// The least space between the last child and the band.
  final double gap;
  final double floorHeight;
  final ScrollController? controller;

  @override
  Widget build(BuildContext context) => CustomScrollView(
    controller: controller,
    slivers: [
      SliverPadding(padding: padding, sliver: SliverList.list(children: children)),
      SliverFillRemaining(
        hasScrollBody: false,
        child: Column(
          mainAxisAlignment: MainAxisAlignment.end,
          children: [
            SizedBox(height: gap),
            FreshSeaFloor(height: floorHeight),
          ],
        ),
      ),
    ],
  );
}

/// Paints the sea floor in its box: the band fills the box minus [inset] at the bottom, which gets the floor
/// colour. Designed [designHeight] tall; the waves stretch with the width.
class FreshSeaFloorPainter extends CustomPainter {
  const FreshSeaFloorPainter({this.inset = 0});

  static const double designHeight = 100;

  final double inset;

  @override
  void paint(Canvas canvas, Size size) {
    final w = size.width;
    final band = math.max(0.0, size.height - inset);
    if (w <= 0 || band <= 0) return;
    final k = band / designHeight;
    final bottom = size.height;

    _wave(
      canvas,
      w,
      bottom,
      top: 30 * k,
      amp: 5 * k,
      segment: 120 * k,
      phase: 0.3,
      color: FreshSeaFloorColors.waveBack,
    );

    // Bubbles rise through the far wave.
    final rim = Paint()
      ..color = FreshSeaFloorColors.bubble.withValues(alpha: 0.9)
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.4 * k;
    final fill = Paint()..color = FreshSeaFloorColors.bubble.withValues(alpha: 0.45);
    for (final (fx, y, r) in const [
      (0.22, 40.0, 3.2),
      (0.26, 30.0, 2.0),
      (0.47, 44.0, 2.4),
      (0.60, 32.0, 3.6),
      (0.64, 44.0, 2.2),
      (0.83, 36.0, 2.8),
    ]) {
      final at = Offset(fx * w, y * k);
      canvas
        ..drawCircle(at, r * k, fill)
        ..drawCircle(at, r * k, rim);
    }

    _wave(canvas, w, bottom, top: 50 * k, amp: 6 * k, segment: 86 * k, phase: 0, color: FreshSeaFloorColors.waveMid);

    // Two small fish in the middle wave, swimming towards each other.
    _fish(canvas, Offset(0.34 * w, 64 * k), k, facingRight: true);
    _fish(canvas, Offset(0.70 * w, 60 * k), k * 0.9, facingRight: false);

    _wave(
      canvas,
      w,
      bottom,
      top: 76 * k,
      amp: 5 * k,
      segment: 130 * k,
      phase: 0.6,
      color: FreshSeaFloorColors.waveFront,
    );

    // Coral at the bottom left, seaweed (with a little coral behind it) at the bottom right.
    canvas
      ..save()
      ..translate(0, size.height - inset)
      ..scale(k);
    _coral(canvas);
    canvas
      ..restore()
      ..save()
      ..translate(w, size.height - inset)
      ..scale(-k, k);
    _seaweed(canvas);
    canvas.restore();

    if (inset > 0) {
      canvas.drawRect(Rect.fromLTWH(0, band, w, inset), Paint()..color = FreshSeaFloorColors.waveFront);
    }
  }

  /// A wave filled down to [bottom]: its crest line at [top], rising and falling [amp] every [segment].
  static void _wave(
    Canvas canvas,
    double width,
    double bottom, {
    required double top,
    required double amp,
    required double segment,
    required double phase,
    required Color color,
  }) {
    final path = Path();
    var x = -phase * segment;
    path.moveTo(x, top);
    while (x < width) {
      path
        ..quadraticBezierTo(x + segment / 4, top - amp, x + segment / 2, top)
        ..quadraticBezierTo(x + segment * 3 / 4, top + amp, x + segment, top);
      x += segment;
    }
    path
      ..lineTo(x, bottom)
      ..lineTo(-phase * segment, bottom)
      ..close();
    canvas.drawPath(path, Paint()..color = color);
  }

  /// A fish silhouette (oval body, forked tail, an eye), 24 × 10 at scale 1, centred on [at].
  static void _fish(Canvas canvas, Offset at, double scale, {required bool facingRight}) {
    canvas
      ..save()
      ..translate(at.dx, at.dy)
      ..scale(facingRight ? scale : -scale, scale);
    final paint = Paint()..color = FreshSeaFloorColors.fish;
    canvas
      ..drawOval(Rect.fromCenter(center: const Offset(2, 0), width: 17, height: 9), paint)
      ..drawPath(
        Path()
          ..moveTo(-5, 0)
          ..lineTo(-12, -5)
          ..quadraticBezierTo(-10, 0, -12, 5)
          ..close(),
        paint,
      )
      ..drawCircle(const Offset(7, -1), 1.1, Paint()..color = FreshSeaFloorColors.bubble)
      ..restore();
  }

  /// Two coral branches growing up from the origin (y is negative upwards), in design units.
  static void _coral(Canvas canvas) {
    Paint stroke(Color color, double width) => Paint()
      ..color = color
      ..style = PaintingStyle.stroke
      ..strokeWidth = width
      ..strokeCap = StrokeCap.round
      ..strokeJoin = StrokeJoin.round;

    final back = stroke(FreshSeaFloorColors.coralLight, 4.5);
    canvas.drawPath(
      Path()
        ..moveTo(40, 2)
        ..quadraticBezierTo(42, -14, 38, -30)
        ..moveTo(40, -12)
        ..quadraticBezierTo(48, -18, 50, -30)
        ..moveTo(39, -22)
        ..quadraticBezierTo(32, -28, 30, -38),
      back,
    );

    final front = stroke(FreshSeaFloorColors.coral, 6);
    canvas.drawPath(
      Path()
        ..moveTo(16, 2)
        ..quadraticBezierTo(18, -24, 12, -50)
        ..moveTo(17, -14)
        ..quadraticBezierTo(6, -20, 2, -36)
        ..moveTo(16, -28)
        ..quadraticBezierTo(26, -36, 28, -54)
        ..moveTo(13, -40)
        ..quadraticBezierTo(6, -48, 4, -60)
        ..moveTo(27, -44)
        ..quadraticBezierTo(34, -50, 36, -58)
        ..moveTo(4, -28)
        ..quadraticBezierTo(-2, -32, -4, -38),
      front,
    );
  }

  /// Seaweed blades and a small coral, growing up from the origin; drawn mirrored at the right edge.
  static void _seaweed(Canvas canvas) {
    Paint stroke(Color color, double width) => Paint()
      ..color = color
      ..style = PaintingStyle.stroke
      ..strokeWidth = width
      ..strokeCap = StrokeCap.round
      ..strokeJoin = StrokeJoin.round;

    canvas.drawPath(
      Path()
        ..moveTo(10, 2)
        ..quadraticBezierTo(12, -12, 8, -24)
        ..moveTo(10, -10)
        ..quadraticBezierTo(2, -14, 0, -22),
      stroke(FreshSeaFloorColors.coral, 4.5),
    );

    final light = stroke(FreshSeaFloorColors.seaweedLight, 5);
    canvas
      ..drawPath(
        Path()
          ..moveTo(38, 2)
          ..cubicTo(30, -14, 46, -30, 36, -48)
          ..cubicTo(32, -56, 36, -62, 40, -66),
        light,
      )
      ..drawPath(
        Path()
          ..moveTo(50, 2)
          ..cubicTo(56, -10, 46, -22, 54, -36),
        light,
      );

    final dark = stroke(FreshSeaFloorColors.seaweed, 6);
    canvas
      ..drawPath(
        Path()
          ..moveTo(22, 2)
          ..cubicTo(14, -16, 32, -30, 20, -50)
          ..cubicTo(14, -60, 20, -70, 26, -76),
        dark,
      )
      ..drawPath(
        Path()
          ..moveTo(28, 2)
          ..cubicTo(34, -12, 24, -26, 30, -40),
        dark,
      );
  }

  @override
  bool shouldRepaint(FreshSeaFloorPainter old) => old.inset != inset;
}
