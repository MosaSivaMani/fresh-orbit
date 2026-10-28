// Drawn decoration that only the onboarding pages use, traced from the approved preview (D156,
// docs/design/mockups/app-onboarding-rich.html, designed at 390 × 873): the scene on the colour block (the
// cutting board and scooter silhouettes and the sea), the banner waves and arrow, and a drawn stand-in for a
// banner picture that has not arrived. The shared parts (birds, clouds, hills, the sea bands, sparks, the
// snowflake and the swooshes, and the owner's real trawler and gull pictures, D287) live in the theme package
// (`fresh_theme`, D176). All of it is decoration:
// callers wrap it in ExcludeSemantics, and nothing moves.
//
// Coordinates are the preview's, in its 390-wide design space, scaled by the caller's factor.

import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:fresh_theme/fresh_theme.dart';

/// Which story a page tells; it picks the silhouette on the block and the banner stand-in.
enum OnboardingScene { fresh, cleaned, delivery }

/// The y of the sheet's top edge in the preview: the right-hand clouds and hills are anchored to it.
const double kDesignSheetTop = 393;

const Color _paper = FreshHighlightColors.paper;

/// A gull, [k] times the preview's 20-wide bird.
void _bird(Canvas canvas, double x, double y, double k, Color color, {double width = 2.2}) =>
    FreshScenery.bird(canvas, Offset(x, y), span: 20 * k, color: color, strokeWidth: width * k);

/// The scene on the colour block, as a widget that fills the block: the drawn scene ([BlockScenePainter]) and, on
/// the fresh page, the owner's real trawler sitting in the sea with three real gulls above its deck (D287) in place
/// of a drawn silhouette. Decoration: the caller wraps it in ExcludeSemantics.
class BlockScene extends StatelessWidget {
  const BlockScene({super.key, required this.painter});

  final BlockScenePainter painter;

  /// Each gull over the trawler picture: top-left and width as fractions of the picture, and the variant. They
  /// sit in the empty sky at its top corners, inside the picture's box, so they never reach the tagline.
  static const List<(double, double, double, int)> _gulls = [
    (0.03, 0.04, 0.2, 0),
    (0.2, 0.0, 0.1, 2),
    (0.74, 0.02, 0.12, 1),
  ];

  @override
  Widget build(BuildContext context) => LayoutBuilder(
    builder: (context, box) {
      final size = box.biggest;
      final ship = painter.scene == OnboardingScene.fresh ? painter.trawlerRect(size) : null;
      return Stack(
        children: [
          Positioned.fill(child: CustomPaint(painter: painter)),
          if (ship != null) ...[
            Positioned.fromRect(
              rect: ship,
              child: FreshTrawler(key: const Key('onboardingTrawler'), width: ship.width),
            ),
            for (final (x, y, w, variant) in _gulls)
              Positioned(
                left: ship.left + x * ship.width,
                top: ship.top + y * ship.height,
                child: FreshGull(width: w * ship.width, variant: variant),
              ),
          ],
        ],
      );
    },
  );
}

/// The drawn scene on the colour block. The painter covers the block exactly (its bottom edge is the sheet's top)
/// and draws nothing below it: the sea bands end [seaGap] above the sheet, so no wave cuts the sheet's rounded
/// edge. The silhouette (board or scooter) sits on the sea between x 8 and [circleLeft] − 8 and below [sceneTop]
/// (the tagline's bottom), scaled to fit, with three birds above it; the fresh page leaves that room to the real
/// trawler ([trawlerRect], placed by [BlockScene]). The clouds and hills are drawn at [scale] from the right edge,
/// behind the circle.
class BlockScenePainter extends CustomPainter {
  const BlockScenePainter({
    required this.scene,
    required this.dark,
    required this.soft,
    required this.scale,
    required this.circleLeft,
    required this.circleCenter,
    required this.sceneTop,
  });

  final OnboardingScene scene;

  /// The darker shade of the block: silhouettes, hills and birds.
  final Color dark;

  /// The pale cloud tint.
  final Color soft;
  final double scale;

  /// The avatar circle's left edge and centre x: the silhouette stays left of it, the sea runs behind it.
  final double circleLeft;
  final double circleCenter;

  /// The lowest y the block's text reaches; the silhouette stays below it.
  final double sceneTop;

  /// Height of the sea and the gap it keeps above the sheet.
  static const double seaHeight = 30;
  static const double seaGap = 4;

  /// The real trawler's widest size, its left edge and the room it keeps under the tagline.
  static const double trawlerMaxWidth = 140;
  static const double trawlerLeft = 6;
  static const double trawlerGap = 6;

  /// The smallest trawler worth showing; below it the page shows only the sea.
  static const double trawlerMinWidth = 40;

  /// The silhouettes' design boxes (preview coordinates).
  static const _boardBox = Rect.fromLTRB(4, 250, 136, 364);
  static const _scooterBox = Rect.fromLTRB(12, 282, 131, 365);

  /// Where the real trawler sits in a block of [size]: its faded waves end on the sea's bottom edge (so its hull
  /// rides in the drawn sea), as large as fits between [sceneTop] and the sea and left of the circle, at most
  /// [trawlerMaxWidth] wide. Null when the room is too small.
  Rect? trawlerRect(Size size) {
    final bottom = size.height - seaGap;
    final width = math.min(
      trawlerMaxWidth,
      math.min(circleLeft - trawlerLeft - 8, (bottom - sceneTop - trawlerGap) * FreshSceneryPictures.trawlerAspect),
    );
    if (width < trawlerMinWidth) return null;
    final height = FreshTrawler.heightFor(width);
    return Rect.fromLTWH(trawlerLeft, bottom - height, width, height);
  }

  @override
  void paint(Canvas canvas, Size size) {
    final k = scale;
    final fill = Paint()..color = dark;
    final seaTop = size.height - seaGap - seaHeight;

    // Right-anchored: two clouds and the hills behind the circle, resting on the sheet's top edge.
    canvas
      ..save()
      ..translate(size.width - 390 * k, size.height - kDesignSheetTop * k)
      ..scale(k);
    FreshScenery.cloud(canvas, const Offset(318, 150), 1.1, soft);
    FreshScenery.cloud(canvas, const Offset(250, 52), 0.7, soft);
    FreshScenery.hills(canvas, dark);
    canvas
      ..restore()
      ..save()
      ..translate(0, size.height - kDesignSheetTop * k)
      ..scale(k);
    FreshScenery.cloud(canvas, const Offset(-14, 150), 0.9, soft);
    canvas.restore();

    // The silhouette: fully visible left of the circle, its base in the water. The fresh page has the real
    // trawler picture instead (BlockScene).
    final box = switch (scene) {
      OnboardingScene.fresh => null,
      OnboardingScene.cleaned => _boardBox,
      OnboardingScene.delivery => _scooterBox,
    };
    if (box == null) {
      _sea(canvas, seaTop, size.height - seaGap, circleCenter);
      return;
    }
    final base = seaTop + 8;
    final s = math.min(0.9, math.min((circleLeft - 16) / box.width, (base - sceneTop - 18) / box.height));
    if (s >= 0.25) {
      canvas
        ..save()
        ..translate(8 - box.left * s, base - box.bottom * s)
        ..scale(s);
      if (scene == OnboardingScene.cleaned) {
        _board(canvas, fill);
      } else {
        _scooter(canvas, fill);
      }
      canvas.restore();
      // Three birds just above it.
      final top = base - box.height * s;
      final right = 8 + box.width * s;
      _bird(canvas, right - 26, top - 4, 0.8, dark);
      _bird(canvas, right - 6, top - 12, 0.6, dark);
      _bird(canvas, right - 44, top + 6, 0.5, dark);
    }

    _sea(canvas, seaTop, size.height - seaGap, circleCenter);
  }

  /// Two sea bands (teal, then pale) with a white wave line, from the left edge to [right], ending at [bottom].
  static void _sea(Canvas canvas, double top, double bottom, double right) {
    Path band(double y, double amp) {
      final path = Path()..moveTo(0, y);
      const step = 60.0;
      for (var x = 0.0; x < right; x += step) {
        path
          ..quadraticBezierTo(x + step / 4, y - amp, x + step / 2, y)
          ..quadraticBezierTo(x + step * 3 / 4, y + amp, x + step, y);
      }
      return path
        ..lineTo(right + step, bottom)
        ..lineTo(0, bottom)
        ..close();
    }

    canvas
      ..drawPath(band(top + 4, 4), Paint()..color = FreshScenery.seaDeep)
      ..drawPath(band(top + 16, 3), Paint()..color = FreshScenery.seaPale);
    final line = Path()..moveTo(0, top + 12);
    for (var x = 0.0; x < right; x += 60) {
      line
        ..quadraticBezierTo(x + 15, top + 9, x + 30, top + 12)
        ..quadraticBezierTo(x + 45, top + 15, x + 60, top + 12);
    }
    canvas.drawPath(
      line,
      Paint()
        ..color = _paper.withValues(alpha: 0.85)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 2,
    );
  }

  /// A cutting board with a fish outline cut into it and a knife above.
  static void _board(Canvas canvas, Paint fill) {
    final mark = Paint()..color = _paper.withValues(alpha: 0.25);
    canvas
      ..drawRRect(RRect.fromLTRBR(4, 318, 136, 364, const Radius.circular(12)), fill)
      ..drawCircle(const Offset(124, 330), 5, mark)
      ..drawPath(
        Path()
          ..moveTo(18, 312)
          ..lineTo(96, 266)
          ..arcToPoint(const Offset(105, 276), radius: const Radius.circular(7))
          ..lineTo(29, 320)
          ..close(),
        fill,
      )
      ..save()
      ..translate(95, 258)
      ..rotate(-30 * math.pi / 180)
      ..drawRRect(RRect.fromLTRBR(0, 0, 26, 9, const Radius.circular(4)), fill)
      ..restore()
      ..drawPath(
        Path()
          ..moveTo(24, 342)
          ..quadraticBezierTo(54, 320, 88, 342)
          ..quadraticBezierTo(54, 362, 24, 342)
          ..close()
          ..moveTo(88, 342)
          ..lineTo(102, 332)
          ..lineTo(102, 352)
          ..close(),
        mark,
      );
  }

  /// A delivery scooter with a cold box on the back.
  static void _scooter(Canvas canvas, Paint fill) {
    canvas
      ..drawCircle(const Offset(30, 350), 15, fill)
      ..drawCircle(const Offset(112, 350), 15, fill)
      ..drawPath(
        Path()
          ..moveTo(18, 340)
          ..lineTo(80, 340)
          ..quadraticBezierTo(88, 316, 108, 312)
          ..lineTo(112, 288)
          ..lineTo(121, 288)
          ..lineTo(117, 316)
          ..quadraticBezierTo(131, 324, 131, 340)
          ..close(),
        fill,
      )
      ..drawRRect(RRect.fromLTRBR(12, 292, 56, 328, const Radius.circular(5)), fill)
      ..drawRRect(RRect.fromLTRBR(104, 282, 124, 287, const Radius.circular(2)), fill);
  }

  @override
  bool shouldRepaint(BlockScenePainter old) =>
      old.scene != scene ||
      old.dark != dark ||
      old.soft != soft ||
      old.scale != scale ||
      old.circleLeft != circleLeft ||
      old.circleCenter != circleCenter ||
      old.sceneTop != sceneTop;
}

/// Gulls in the sheet's top corners (grey-teal), the preview's two sheet birds.
class SheetBirdsPainter extends CustomPainter {
  const SheetBirdsPainter(this.color);

  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    _bird(canvas, 30, 22, 0.95, color, width: 2);
    _bird(canvas, size.width - 50, 30, 0.95, color, width: 2);
  }

  @override
  bool shouldRepaint(SheetBirdsPainter old) => old.color != color;
}

/// The banner's two teal wave layers, white wave line, bubbles and a wooden strip (preview 390 × 150, stretched).
class BannerWavePainter extends CustomPainter {
  const BannerWavePainter({required this.light, required this.mid, required this.dots, required this.line});

  final Color light;
  final Color mid;
  final Color dots;
  final Color line;

  @override
  void paint(Canvas canvas, Size size) {
    canvas
      ..save()
      ..scale(size.width / 390, size.height / 150)
      ..drawPath(
        Path()
          ..moveTo(0, 26)
          ..quadraticBezierTo(50, 0, 110, 18)
          ..quadraticBezierTo(170, 36, 240, 38)
          ..quadraticBezierTo(310, 40, 390, 12)
          ..lineTo(390, 150)
          ..lineTo(0, 150)
          ..close(),
        Paint()..color = light,
      )
      ..drawPath(
        Path()
          ..moveTo(0, 62)
          ..quadraticBezierTo(60, 42, 130, 60)
          ..quadraticBezierTo(200, 78, 270, 64)
          ..quadraticBezierTo(340, 50, 390, 54)
          ..lineTo(390, 150)
          ..lineTo(0, 150)
          ..close(),
        Paint()..color = mid,
      )
      ..drawPath(
        Path()
          ..moveTo(150, 52)
          ..quadraticBezierTo(190, 38, 230, 48),
        Paint()
          ..color = line
          ..style = PaintingStyle.stroke
          ..strokeWidth = 2.5
          ..strokeCap = StrokeCap.round,
      );
    final dot = Paint()..color = dots;
    canvas
      ..drawCircle(const Offset(262, 30), 3, dot)
      ..drawCircle(const Offset(276, 22), 2, dot)
      ..drawCircle(const Offset(372, 58), 2.5, dot)
      ..drawRect(const Rect.fromLTWH(210, 128, 180, 22), Paint()..color = const Color(0x59C89A6A))
      ..restore();
  }

  @override
  bool shouldRepaint(BannerWavePainter old) =>
      old.light != light || old.mid != mid || old.dots != dots || old.line != line;
}

/// The small coral arrow curving from the banner line down towards the food (preview 40 × 40).
class ArrowDoodlePainter extends CustomPainter {
  const ArrowDoodlePainter(this.color);

  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = color
      ..style = PaintingStyle.stroke
      ..strokeWidth = 2.6
      ..strokeCap = StrokeCap.round
      ..strokeJoin = StrokeJoin.round;
    canvas
      ..save()
      ..scale(size.width / 40, size.height / 40)
      ..drawPath(
        Path()
          ..moveTo(34, 6)
          ..quadraticBezierTo(12, 8, 10, 32),
        paint,
      )
      ..drawPath(
        Path()
          ..moveTo(4, 24)
          ..lineTo(10, 33)
          ..lineTo(17, 25),
        paint,
      )
      ..restore();
  }

  @override
  bool shouldRepaint(ArrowDoodlePainter old) => old.color != color;
}

/// A simple drawn still life standing in for a banner picture that has not arrived yet.
class BannerFoodPainter extends CustomPainter {
  const BannerFoodPainter({required this.scene, required this.coral, required this.teal});

  final OnboardingScene scene;
  final Color coral;
  final Color teal;

  static const _silver = Color(0xFF9AA8AD);
  static const _wood = Color(0xFFD9A86C);
  static const _lemon = Color(0xFFF5B544);

  @override
  void paint(Canvas canvas, Size size) {
    final w = size.width;
    final h = size.height;
    final floor = h * 0.9;
    Paint p(Color c) => Paint()..color = c;
    switch (scene) {
      case OnboardingScene.fresh:
        for (var i = 0; i < 8; i++) {
          canvas.drawRRect(
            RRect.fromRectAndRadius(
              Rect.fromCenter(center: Offset(w * (0.08 + i * 0.12), floor - (i.isEven ? 4 : 0)), width: 15, height: 12),
              const Radius.circular(4),
            ),
            p(_paper.withValues(alpha: 0.95)),
          );
        }
        _fish(canvas, Offset(w * 0.5, floor - h * 0.3), w * 0.3, p(_silver));
        for (final (x, y) in const [(0.14, 0.5), (0.25, 0.62)]) {
          canvas.drawArc(
            Rect.fromCircle(center: Offset(w * x, h * y), radius: h * 0.12),
            0.5,
            math.pi * 1.5,
            false,
            Paint()
              ..color = coral
              ..style = PaintingStyle.stroke
              ..strokeWidth = h * 0.08
              ..strokeCap = StrokeCap.round,
          );
        }
        canvas
          ..drawOval(
            Rect.fromCenter(center: Offset(w * 0.82, floor - h * 0.16), width: w * 0.2, height: h * 0.24),
            p(coral),
          )
          ..drawCircle(Offset(w * 0.68, floor - h * 0.08), h * 0.09, p(_lemon));
      case OnboardingScene.cleaned:
        canvas
          ..drawRRect(
            RRect.fromRectAndRadius(
              Rect.fromLTWH(w * 0.06, floor - h * 0.3, w * 0.8, h * 0.3),
              Radius.circular(h * 0.08),
            ),
            p(_wood),
          )
          ..drawOval(
            Rect.fromCenter(center: Offset(w * 0.22, floor - h * 0.38), width: w * 0.18, height: h * 0.2),
            p(coral),
          );
        for (final x in const [0.34, 0.42]) {
          canvas.drawRRect(
            RRect.fromRectAndRadius(Rect.fromLTWH(w * x, floor - h * 0.46, w * 0.06, w * 0.06), const Radius.circular(3)),
            p(coral),
          );
        }
        _fish(canvas, Offset(w * 0.68, floor - h * 0.38), w * 0.14, p(_silver));
      case OnboardingScene.delivery:
        canvas
          ..drawRRect(
            RRect.fromRectAndRadius(Rect.fromLTWH(w * 0.7, floor - h * 0.75, w * 0.2, h * 0.75), const Radius.circular(4)),
            p(teal),
          )
          ..drawRRect(
            RRect.fromRectAndRadius(
              Rect.fromLTWH(w * 0.44, floor - h * 0.36, w * 0.2, h * 0.36),
              Radius.circular(h * 0.05),
            ),
            p(teal.withValues(alpha: 0.8)),
          )
          ..drawPath(
            Path()
              ..moveTo(w * 0.06, floor - h * 0.12)
              ..quadraticBezierTo(w * 0.06, floor - h * 0.34, w * 0.2, floor - h * 0.32)
              ..lineTo(w * 0.28, floor - h * 0.32)
              ..lineTo(w * 0.33, floor - h * 0.6)
              ..lineTo(w * 0.37, floor - h * 0.6)
              ..lineTo(w * 0.36, floor - h * 0.12)
              ..close(),
            p(coral),
          )
          ..drawCircle(Offset(w * 0.1, floor - h * 0.08), h * 0.09, p(_silver))
          ..drawCircle(Offset(w * 0.34, floor - h * 0.08), h * 0.09, p(_silver));
    }
  }

  static void _fish(Canvas canvas, Offset c, double s, Paint paint) {
    canvas.drawPath(
      Path()
        ..moveTo(c.dx - s, c.dy)
        ..quadraticBezierTo(c.dx - s * 0.2, c.dy - s * 0.5, c.dx + s * 0.6, c.dy)
        ..lineTo(c.dx + s, c.dy - s * 0.3)
        ..lineTo(c.dx + s, c.dy + s * 0.3)
        ..lineTo(c.dx + s * 0.6, c.dy)
        ..quadraticBezierTo(c.dx - s * 0.2, c.dy + s * 0.5, c.dx - s, c.dy)
        ..close(),
      paint,
    );
  }

  @override
  bool shouldRepaint(BannerFoodPainter old) => old.scene != scene || old.coral != coral || old.teal != teal;
}
