// Drawn coastal scenery for the rich screens (D156, D176, D181): gulls, clouds, hills, the fishing trawler, the
// sea bands, palms and the home harbour (sky, sun, three boats, birds, a palm). Traced from the approved
// previews, in their 390-wide design space; callers scale the canvas. Every colour and size is explicit.
// Scenery is decoration: wrap it in ExcludeSemantics (the widgets here do). Nothing moves.

import 'package:flutter/widgets.dart';

import 'highlights.dart';

/// Drawing helpers shared by the scenery painters. Coordinates are in the caller's canvas.
abstract final class FreshScenery {
  /// The sea colours under the onboarding sheet and in the home harbour.
  static const Color seaDeep = Color(0xFF9ED3CC);
  static const Color seaPale = Color(0xFFBFE3E0);

  /// The soft teal of palms that peek in at the screen edges.
  static const Color palmSoft = Color(0xFFBFE3E0);

  /// A gull: two little arcs starting at [at], [span] wide, rising [span] × [rise].
  static void bird(
    Canvas canvas,
    Offset at, {
    required double span,
    required Color color,
    required double strokeWidth,
    double rise = 0.3,
  }) {
    final half = span / 2;
    final path = Path()
      ..moveTo(at.dx, at.dy)
      ..relativeQuadraticBezierTo(half / 2, -span * rise, half, 0)
      ..relativeQuadraticBezierTo(half / 2, -span * rise, half, 0);
    canvas.drawPath(
      path,
      Paint()
        ..color = color
        ..style = PaintingStyle.stroke
        ..strokeWidth = strokeWidth
        ..strokeCap = StrokeCap.round,
    );
  }

  /// A flat-bottomed cloud, 52 × 32 times [scale], its top-left at [at].
  static void cloud(Canvas canvas, Offset at, double scale, Color color) {
    canvas
      ..save()
      ..translate(at.dx, at.dy)
      ..scale(scale);
    final path = Path()
      ..moveTo(0, 16)
      ..arcToPoint(const Offset(26, 10), radius: const Radius.circular(14))
      ..arcToPoint(const Offset(48, 14), radius: const Radius.circular(12))
      ..arcToPoint(const Offset(52, 32), radius: const Radius.circular(10))
      ..lineTo(0, 32)
      ..arcToPoint(const Offset(0, 16), radius: const Radius.circular(10))
      ..close();
    canvas
      ..drawPath(path, Paint()..color = color)
      ..restore();
  }

  /// Two rolling hills with a ridge line, in the right part of a 390 × 393 design box (x 250–390, y 343–393).
  static void hills(Canvas canvas, Color fill) {
    canvas
      ..drawPath(
        Path()
          ..moveTo(250, 393)
          ..quadraticBezierTo(290, 353, 330, 363)
          ..quadraticBezierTo(370, 373, 390, 343)
          ..lineTo(390, 393)
          ..close(),
        Paint()..color = fill,
      )
      ..drawPath(
        Path()
          ..moveTo(300, 370)
          ..quadraticBezierTo(320, 362, 340, 370)
          ..quadraticBezierTo(360, 378, 380, 366),
        Paint()
          ..color = fill.withValues(alpha: fill.a * 0.9)
          ..style = PaintingStyle.stroke
          ..strokeWidth = 2
          ..strokeCap = StrokeCap.round,
      );
  }

  /// The fishing trawler (curved hull, cabin with a window row, wheelhouse, mast with a flag and rigging), in a
  /// design box where the hull sits at x 2–120, y 250–364.
  static void trawler(Canvas canvas, Color fill, {Color window = const Color(0x47FFFDF8)}) {
    final paint = Paint()..color = fill;
    canvas
      ..drawPath(
        Path()
          ..moveTo(2, 330)
          ..lineTo(120, 330)
          ..quadraticBezierTo(114, 352, 94, 364)
          ..lineTo(20, 364)
          ..quadraticBezierTo(8, 354, 2, 330)
          ..close(),
        paint,
      )
      ..drawRRect(RRect.fromLTRBR(26, 306, 80, 331, const Radius.circular(2)), paint)
      ..drawRRect(RRect.fromLTRBR(34, 292, 68, 307, const Radius.circular(2)), paint);
    final glass = Paint()..color = window;
    for (final r in const [
      Rect.fromLTWH(32, 312, 9, 8),
      Rect.fromLTWH(45, 312, 9, 8),
      Rect.fromLTWH(58, 312, 9, 8),
      Rect.fromLTWH(40, 296, 7, 6),
      Rect.fromLTWH(51, 296, 7, 6),
    ]) {
      canvas.drawRect(r, glass);
    }
    canvas
      ..drawRect(const Rect.fromLTWH(84, 250, 3.5, 82), paint)
      ..drawRect(const Rect.fromLTWH(52, 268, 3, 26), paint)
      ..drawPath(
        Path()
          ..moveTo(87.5, 252)
          ..lineTo(103.5, 257)
          ..lineTo(87.5, 263)
          ..close(),
        paint,
      );
    final rig = Paint()
      ..color = fill
      ..strokeWidth = 1.4;
    canvas
      ..drawLine(const Offset(86, 256), const Offset(118, 330), rig)
      ..drawLine(const Offset(86, 262), const Offset(8, 330), rig)
      ..drawLine(const Offset(54, 270), const Offset(86, 270), rig)
      ..drawLine(const Offset(98, 300), const Offset(114, 300), rig);
  }

  /// Deep and pale sea bands with a wave line, x 0–180, y 350–424 of the design box (they run under a sheet).
  static void seaBands(
    Canvas canvas, {
    Color deep = seaDeep,
    Color pale = seaPale,
    Color line = const Color(0xE6FFFDF8),
  }) {
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
        Paint()..color = deep,
      )
      ..drawPath(
        Path()
          ..moveTo(0, 376)
          ..quadraticBezierTo(30, 368, 60, 376)
          ..quadraticBezierTo(90, 384, 120, 376)
          ..quadraticBezierTo(150, 368, 180, 380),
        Paint()
          ..color = line
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
        Paint()..color = pale,
      );
  }

  /// A palm: a curved trunk 120 tall growing up to [at] (the crown) and five leaves, scaled by [scale];
  /// [mirrored] flips it left to right. Leaves reach about 54 left and 60 right of the crown, 30 above it.
  static void palm(Canvas canvas, Offset at, {required double scale, required Color color, bool mirrored = false}) {
    canvas
      ..save()
      ..translate(at.dx, at.dy)
      ..scale(mirrored ? -scale : scale, scale);
    final paint = Paint()..color = color;
    canvas
      ..drawPath(
        Path()
          ..moveTo(0, 120)
          ..cubicTo(4, 80, 6, 44, 2, 16)
          ..lineTo(8, 16)
          ..cubicTo(12, 46, 10, 82, 6, 120)
          ..close(),
        paint,
      )
      ..drawPath(
        Path()
          ..moveTo(4, 16)
          ..cubicTo(-16, 2, -40, 6, -54, 20)
          ..cubicTo(-36, 12, -16, 12, 4, 20)
          ..close(),
        paint,
      )
      ..drawPath(
        Path()
          ..moveTo(6, 16)
          ..cubicTo(0, -4, -18, -14, -38, -12)
          ..cubicTo(-20, -6, -6, 2, 6, 16)
          ..close(),
        paint,
      )
      ..drawPath(
        Path()
          ..moveTo(6, 14)
          ..cubicTo(20, -4, 42, -6, 60, 4)
          ..cubicTo(40, 4, 22, 8, 6, 16)
          ..close(),
        paint,
      )
      ..drawPath(
        Path()
          ..moveTo(6, 16)
          ..cubicTo(28, 12, 50, 22, 58, 38)
          ..cubicTo(42, 26, 24, 22, 6, 18)
          ..close(),
        paint,
      )
      ..restore();
  }
}

/// One palm for [PalmsPainter]: [at] is the crown, measured from the right edge when [fromRight] is set.
@immutable
class ScenePalm {
  const ScenePalm({
    required this.at,
    required this.scale,
    this.color = FreshScenery.palmSoft,
    this.mirrored = false,
    this.fromRight = false,
  });

  final Offset at;
  final double scale;
  final Color color;
  final bool mirrored;
  final bool fromRight;
}

/// Palms peeking in at the edges of the painter's box (the home page's soft palms). It paints outside its box.
class PalmsPainter extends CustomPainter {
  const PalmsPainter(this.palms);

  final List<ScenePalm> palms;

  @override
  void paint(Canvas canvas, Size size) {
    for (final p in palms) {
      final at = p.fromRight ? Offset(size.width - p.at.dx, p.at.dy) : p.at;
      FreshScenery.palm(canvas, at, scale: p.scale, color: p.color, mirrored: p.mirrored);
    }
  }

  @override
  bool shouldRepaint(PalmsPainter old) => !identical(old.palms, palms);
}

/// The home harbour (D176 preview, x 168–390 and y 56–206 of the 390-wide screen): a warm sky, the sun, low
/// hills, the sea with a wave line, three boats, four gulls and a teal palm cut by the right edge. Designed in a
/// 222 × 150 box ([designSize]) and scaled to the painter's width.
class HarbourScenePainter extends CustomPainter {
  const HarbourScenePainter({
    this.skyTop = const Color(0xFFF7F1E6),
    this.skyMid = const Color(0xFFFCE1C2),
    this.skyBottom = const Color(0xFFF9CFA4),
    this.sun = const Color(0xE6F8B65F),
    this.hills = const Color(0x8CE9B79A),
    this.sea = FreshScenery.seaDeep,
    this.wave = FreshHighlightColors.paper,
    this.boat = FreshHighlightColors.coralDeep,
    this.farBoat = const Color(0xFFB98571),
    this.birds = FreshHighlightColors.ink,
    this.palm = const Color(0xFF0B7D70),
  });

  static const Size designSize = Size(222, 150);

  final Color skyTop;
  final Color skyMid;
  final Color skyBottom;
  final Color sun;
  final Color hills;
  final Color sea;
  final Color wave;
  final Color boat;
  final Color farBoat;
  final Color birds;
  final Color palm;

  @override
  void paint(Canvas canvas, Size size) {
    final k = size.width / designSize.width;
    canvas
      ..save()
      ..scale(k);
    final sky = Path()
      ..moveTo(0, 8)
      ..quadraticBezierTo(110, -16, 222, 0)
      ..lineTo(222, 150)
      ..lineTo(0, 150)
      ..close();
    canvas
      ..drawPath(
        sky,
        Paint()
          ..shader = LinearGradient(
            begin: Alignment.topCenter,
            end: Alignment.bottomCenter,
            colors: [skyTop, skyMid, skyBottom],
            stops: const [0, 0.55, 1],
          ).createShader(const Rect.fromLTWH(0, -4, 222, 154)),
      )
      ..drawCircle(const Offset(162, 94), 20, Paint()..color = sun)
      ..drawPath(
        Path()
          ..moveTo(0, 94)
          ..relativeQuadraticBezierTo(40, -14, 70, -6)
          ..relativeQuadraticBezierTo(30, -16, 60, -4)
          ..relativeQuadraticBezierTo(40, -10, 92, 0)
          ..lineTo(222, 114)
          ..lineTo(0, 114)
          ..close(),
        Paint()..color = hills,
      )
      ..drawPath(
        Path()
          ..moveTo(0, 120)
          ..quadraticBezierTo(50, 110, 110, 118)
          ..quadraticBezierTo(170, 126, 222, 112)
          ..lineTo(222, 152)
          ..lineTo(0, 152)
          ..close(),
        Paint()..color = sea,
      )
      ..drawPath(
        Path()
          ..moveTo(12, 130)
          ..quadraticBezierTo(42, 125, 72, 130)
          ..quadraticBezierTo(102, 135, 132, 130)
          ..quadraticBezierTo(162, 125, 192, 132),
        Paint()
          ..color = wave
          ..style = PaintingStyle.stroke
          ..strokeWidth = 2
          ..strokeCap = StrokeCap.round,
      );

    // The big boat: hull, cabin, wheelhouse, mast, flag, three windows and rigging.
    final hull = Paint()..color = boat;
    canvas
      ..drawPath(
        Path()
          ..moveTo(28, 114)
          ..lineTo(94, 114)
          ..quadraticBezierTo(89, 125, 78, 129)
          ..lineTo(42, 129)
          ..quadraticBezierTo(33, 124, 28, 114)
          ..close(),
        hull,
      )
      ..drawRRect(RRect.fromLTRBR(44, 100, 70, 114, const Radius.circular(2)), hull)
      ..drawRRect(RRect.fromLTRBR(50, 92, 64, 100, const Radius.circular(1)), hull)
      ..drawRect(const Rect.fromLTWH(74, 70, 2.6, 44), hull)
      ..drawPath(
        Path()
          ..moveTo(76.6, 72)
          ..lineTo(87.6, 75)
          ..lineTo(76.6, 79.5)
          ..close(),
        hull,
      );
    final glass = Paint()..color = wave.withValues(alpha: 0.35);
    for (final r in const [Rect.fromLTWH(48, 104, 5, 5), Rect.fromLTWH(56, 104, 5, 5), Rect.fromLTWH(64, 104, 4, 5)]) {
      canvas.drawRect(r, glass);
    }
    final rig = Paint()
      ..color = boat
      ..strokeWidth = 1;
    canvas
      ..drawLine(const Offset(75, 74), const Offset(94, 114), rig)
      ..drawLine(const Offset(75, 78), const Offset(30, 114), rig);

    // Two boats further out.
    final far = Paint()..color = farBoat.withValues(alpha: farBoat.a * 0.8);
    canvas
      ..drawPath(
        Path()
          ..moveTo(132, 116)
          ..lineTo(178, 116)
          ..quadraticBezierTo(175, 124, 167, 127)
          ..lineTo(142, 127)
          ..quadraticBezierTo(135, 123, 132, 116)
          ..close(),
        far,
      )
      ..drawRRect(RRect.fromLTRBR(144, 106, 160, 116, const Radius.circular(2)), far)
      ..drawRect(const Rect.fromLTWH(157, 88, 2, 28), far);
    final farther = Paint()..color = farBoat.withValues(alpha: farBoat.a * 0.55);
    canvas
      ..drawPath(
        Path()
          ..moveTo(104, 108)
          ..lineTo(126, 108)
          ..quadraticBezierTo(124, 113, 120, 115)
          ..lineTo(109, 115)
          ..quadraticBezierTo(106, 112, 104, 108)
          ..close(),
        farther,
      )
      ..drawRect(const Rect.fromLTWH(114, 96, 1.5, 13), farther);

    FreshScenery.palm(canvas, const Offset(204, 18), scale: 0.95, color: palm);
    for (final (at, span) in const [
      (Offset(28, 48), 16.0),
      (Offset(48, 36), 12.0),
      (Offset(118, 40), 12.0),
      (Offset(94, 62), 10.0),
    ]) {
      FreshScenery.bird(canvas, at, span: span, color: birds, strokeWidth: 1.8, rise: 0.3);
    }
    canvas.restore();
  }

  @override
  bool shouldRepaint(HarbourScenePainter old) =>
      old.skyTop != skyTop ||
      old.skyMid != skyMid ||
      old.skyBottom != skyBottom ||
      old.sun != sun ||
      old.hills != hills ||
      old.sea != sea ||
      old.wave != wave ||
      old.boat != boat ||
      old.farBoat != farBoat ||
      old.birds != birds ||
      old.palm != palm;
}

/// The home harbour as a widget, [width] wide (height follows the 222 × 150 design), excluded from semantics.
class HarbourScene extends StatelessWidget {
  const HarbourScene({super.key, this.width = 222, this.painter = const HarbourScenePainter()});

  final double width;
  final HarbourScenePainter painter;

  @override
  Widget build(BuildContext context) => ExcludeSemantics(
    child: CustomPaint(
      size: Size(width, width * HarbourScenePainter.designSize.height / HarbourScenePainter.designSize.width),
      painter: painter,
    ),
  );
}
