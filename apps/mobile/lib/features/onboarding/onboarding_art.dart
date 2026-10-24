// Drawn decoration for the onboarding pages, traced from the approved preview (D156,
// docs/design/mockups/app-onboarding-rich.html, designed at 390 × 873): the scene on the colour block (clouds,
// hills, the trawler / cutting board / scooter silhouette, birds and the sea bands that run under the sheet), the
// sun-ray sparks, the snowflake, the swooshes, the banner waves and arrow, and a drawn stand-in for a banner
// picture that has not arrived. All of it is decoration: callers wrap it in ExcludeSemantics, and nothing moves.
//
// Coordinates are the preview's, in its 390-wide design space, scaled by the caller's factor.

import 'dart:math' as math;

import 'package:flutter/material.dart';

/// Which story a page tells; it picks the silhouette on the block and the banner stand-in.
enum OnboardingScene { fresh, cleaned, delivery }

/// The y of the sheet's top edge in the preview: block drawings are anchored to it.
const double kDesignSheetTop = 393;

const Color _paper = Color(0xFFFFFDF8);

/// A gull: two little arcs, [k] times the preview's 20-wide bird.
void _bird(Canvas canvas, double x, double y, double k, Color color, {double width = 2.2}) {
  final path = Path()
    ..moveTo(x, y)
    ..relativeQuadraticBezierTo(5 * k, -6 * k, 10 * k, 0)
    ..relativeQuadraticBezierTo(5 * k, -6 * k, 10 * k, 0);
  canvas.drawPath(
    path,
    Paint()
      ..color = color
      ..style = PaintingStyle.stroke
      ..strokeWidth = width * k
      ..strokeCap = StrokeCap.round,
  );
}

void _cloud(Canvas canvas, double x, double y, double k, Paint paint) {
  canvas
    ..save()
    ..translate(x, y)
    ..scale(k);
  final path = Path()
    ..moveTo(0, 16)
    ..arcToPoint(const Offset(26, 10), radius: const Radius.circular(14))
    ..arcToPoint(const Offset(48, 14), radius: const Radius.circular(12))
    ..arcToPoint(const Offset(52, 32), radius: const Radius.circular(10))
    ..lineTo(0, 32)
    ..arcToPoint(const Offset(0, 16), radius: const Radius.circular(10))
    ..close();
  canvas
    ..drawPath(path, paint)
    ..restore();
}

/// The scene on the colour block. The painter's bottom edge is the sheet's top ([kDesignSheetTop] in the
/// preview); left items are scaled by [scale] from the left edge, right items from the right edge. The sea bands
/// reach below the bottom edge, under the sheet's rounded corner, so the parent must not clip.
class BlockScenePainter extends CustomPainter {
  const BlockScenePainter({required this.scene, required this.dark, required this.soft, required this.scale});

  final OnboardingScene scene;

  /// The darker shade of the block: silhouettes, hills and birds.
  final Color dark;

  /// The pale cloud tint.
  final Color soft;
  final double scale;

  @override
  void paint(Canvas canvas, Size size) {
    final k = scale;
    final top = size.height - kDesignSheetTop * k;
    final fill = Paint()..color = dark;
    final softPaint = Paint()..color = soft;

    // Right-anchored: two clouds and the hills behind the circle.
    canvas
      ..save()
      ..translate(size.width - 390 * k, top)
      ..scale(k);
    _cloud(canvas, 318, 150, 1.1, softPaint);
    _cloud(canvas, 250, 52, 0.7, softPaint);
    canvas.drawPath(
      Path()
        ..moveTo(250, 393)
        ..quadraticBezierTo(290, 353, 330, 363)
        ..quadraticBezierTo(370, 373, 390, 343)
        ..lineTo(390, 393)
        ..close(),
      fill,
    );
    canvas
      ..drawPath(
        Path()
          ..moveTo(300, 370)
          ..quadraticBezierTo(320, 362, 340, 370)
          ..quadraticBezierTo(360, 378, 380, 366),
        Paint()
          ..color = dark.withValues(alpha: 0.9)
          ..style = PaintingStyle.stroke
          ..strokeWidth = 2
          ..strokeCap = StrokeCap.round,
      )
      ..restore();

    // Left-anchored: a cloud, the silhouette, its extras, three birds and the sea.
    canvas
      ..save()
      ..translate(0, top)
      ..scale(k);
    _cloud(canvas, -14, 150, 0.9, softPaint);
    switch (scene) {
      case OnboardingScene.fresh:
        _trawler(canvas, fill);
      case OnboardingScene.cleaned:
        _board(canvas, fill);
        final ring = Paint()
          ..color = dark
          ..style = PaintingStyle.stroke
          ..strokeWidth = 2;
        canvas
          ..drawCircle(const Offset(150, 300), 6, ring)
          ..drawCircle(const Offset(166, 282), 4, ring)
          ..drawCircle(const Offset(140, 270), 3, ring);
      case OnboardingScene.delivery:
        _scooter(canvas, fill);
        _route(canvas, dark);
    }
    _bird(canvas, 118, 300, 1, dark);
    _bird(canvas, 140, 288, 0.75, dark);
    _bird(canvas, 102, 318, 0.6, dark);
    _sea(canvas);
    canvas.restore();
  }

  /// The fishing trawler: curved hull, cabin with a window row, wheelhouse, mast with a flag and rigging.
  static void _trawler(Canvas canvas, Paint fill) {
    canvas
      ..drawPath(
        Path()
          ..moveTo(2, 330)
          ..lineTo(120, 330)
          ..quadraticBezierTo(114, 352, 94, 364)
          ..lineTo(20, 364)
          ..quadraticBezierTo(8, 354, 2, 330)
          ..close(),
        fill,
      )
      ..drawRRect(RRect.fromLTRBR(26, 306, 80, 331, const Radius.circular(2)), fill)
      ..drawRRect(RRect.fromLTRBR(34, 292, 68, 307, const Radius.circular(2)), fill);
    final window = Paint()..color = _paper.withValues(alpha: 0.28);
    for (final r in const [
      Rect.fromLTWH(32, 312, 9, 8),
      Rect.fromLTWH(45, 312, 9, 8),
      Rect.fromLTWH(58, 312, 9, 8),
      Rect.fromLTWH(40, 296, 7, 6),
      Rect.fromLTWH(51, 296, 7, 6),
    ]) {
      canvas.drawRect(r, window);
    }
    canvas
      ..drawRect(const Rect.fromLTWH(84, 250, 3.5, 82), fill)
      ..drawRect(const Rect.fromLTWH(52, 268, 3, 26), fill)
      ..drawPath(
        Path()
          ..moveTo(87.5, 252)
          ..lineTo(103.5, 257)
          ..lineTo(87.5, 263)
          ..close(),
        fill,
      );
    final rig = Paint()
      ..color = fill.color
      ..strokeWidth = 1.4;
    canvas
      ..drawLine(const Offset(86, 256), const Offset(118, 330), rig)
      ..drawLine(const Offset(86, 262), const Offset(8, 330), rig)
      ..drawLine(const Offset(54, 270), const Offset(86, 270), rig)
      ..drawLine(const Offset(98, 300), const Offset(114, 300), rig);
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

  /// The dotted route from the scooter up to a map pin.
  static void _route(Canvas canvas, Color color) {
    final dot = Paint()..color = color;
    Offset quad(Offset a, Offset c, Offset b, double t) => a * ((1 - t) * (1 - t)) + c * (2 * (1 - t) * t) + b * (t * t);
    const a = Offset(140, 356);
    const c1 = Offset(180, 350);
    const m = Offset(202, 316);
    const c2 = Offset(224, 282);
    const b = Offset(252, 250);
    var last = a;
    var travelled = 10.0;
    for (var i = 0; i <= 120; i++) {
      final t = i / 60;
      final p = t <= 1 ? quad(a, c1, m, t) : quad(m, c2, b, t - 1);
      travelled += (p - last).distance;
      last = p;
      if (travelled >= 10) {
        canvas.drawCircle(p, 1.5, dot);
        travelled = 0;
      }
    }
    canvas
      ..drawPath(
        Path()
          ..moveTo(250, 212)
          ..arcToPoint(const Offset(274, 212), radius: const Radius.circular(12))
          ..cubicTo(274, 222, 262, 234, 262, 234)
          ..cubicTo(262, 234, 250, 222, 250, 212)
          ..close(),
        dot,
      )
      ..drawCircle(const Offset(262, 212), 4, Paint()..color = _paper.withValues(alpha: 0.4));
  }

  /// Teal and pale bands with a white wave line, under the silhouette and into the sheet's corner.
  static void _sea(Canvas canvas) {
    canvas
      ..drawPath(
        Path()
          ..moveTo(0, 360)
          ..quadraticBezierTo(30, 350, 60, 358)
          ..quadraticBezierTo(90, 366, 120, 358)
          ..quadraticBezierTo(150, 350, 170, 364)
          ..lineTo(170, 424)
          ..lineTo(0, 424)
          ..close(),
        Paint()..color = const Color(0xFF9ED3CC),
      )
      ..drawPath(
        Path()
          ..moveTo(0, 376)
          ..quadraticBezierTo(30, 368, 60, 376)
          ..quadraticBezierTo(90, 384, 120, 376)
          ..quadraticBezierTo(150, 368, 180, 380),
        Paint()
          ..color = _paper.withValues(alpha: 0.9)
          ..style = PaintingStyle.stroke
          ..strokeWidth = 2.4
          ..strokeCap = StrokeCap.round,
      )
      ..drawPath(
        Path()
          ..moveTo(0, 388)
          ..quadraticBezierTo(40, 382, 80, 388)
          ..quadraticBezierTo(120, 394, 170, 392)
          ..lineTo(170, 412)
          ..lineTo(0, 412)
          ..close(),
        Paint()..color = const Color(0xFFBFE3E0),
      );
  }

  @override
  bool shouldRepaint(BlockScenePainter old) =>
      old.scene != scene || old.dark != dark || old.soft != soft || old.scale != scale;
}

/// Three short fanned dashes (the preview's `spark`), turned by [degrees]. Its box is 34 × 30 times [scale].
class Spark extends StatelessWidget {
  const Spark({super.key, required this.color, this.degrees = 0, this.scale = 1});

  final Color color;
  final double degrees;
  final double scale;

  @override
  Widget build(BuildContext context) => ExcludeSemantics(
    child: Transform.rotate(
      angle: degrees * math.pi / 180,
      child: CustomPaint(size: Size(34 * scale, 30 * scale), painter: _SparkPainter(color)),
    ),
  );
}

class _SparkPainter extends CustomPainter {
  const _SparkPainter(this.color);

  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final k = size.width / 34;
    final paint = Paint()
      ..color = color
      ..strokeWidth = 3.6 * k
      ..strokeCap = StrokeCap.round;
    for (final (a, b) in const [
      (Offset(6, 22), Offset(10, 10)),
      (Offset(14, 24), Offset(24, 8)),
      (Offset(22, 26), Offset(32, 20)),
    ]) {
      canvas.drawLine(a * k, b * k, paint);
    }
  }

  @override
  bool shouldRepaint(_SparkPainter old) => old.color != color;
}

/// A small snowflake (the ice tile's spark), 30 × 26.
class Snowflake extends StatelessWidget {
  const Snowflake({super.key, required this.color});

  final Color color;

  @override
  Widget build(BuildContext context) =>
      ExcludeSemantics(child: CustomPaint(size: const Size(30, 26), painter: _SnowflakePainter(color)));
}

class _SnowflakePainter extends CustomPainter {
  const _SnowflakePainter(this.color);

  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = color
      ..strokeWidth = 2
      ..strokeCap = StrokeCap.round;
    canvas
      ..drawLine(const Offset(15, 3), const Offset(15, 23), paint)
      ..drawLine(const Offset(6, 8), const Offset(24, 18), paint)
      ..drawLine(const Offset(24, 8), const Offset(6, 18), paint);
  }

  @override
  bool shouldRepaint(_SnowflakePainter old) => old.color != color;
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

/// A hand-drawn underline: a gentle rising curve across the box.
class SwooshPainter extends CustomPainter {
  const SwooshPainter(this.color, {this.width = 2.6});

  final Color color;
  final double width;

  @override
  void paint(Canvas canvas, Size size) {
    final path = Path()
      ..moveTo(2, size.height * 0.75)
      ..quadraticBezierTo(size.width * 0.4, size.height * -0.1, size.width - 2, size.height * 0.4);
    canvas.drawPath(
      path,
      Paint()
        ..color = color
        ..style = PaintingStyle.stroke
        ..strokeWidth = width
        ..strokeCap = StrokeCap.round,
    );
  }

  @override
  bool shouldRepaint(SwooshPainter old) => old.color != color || old.width != width;
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
