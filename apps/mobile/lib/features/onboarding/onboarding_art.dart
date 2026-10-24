// Drawn decoration for the onboarding pages (owner's reference, docs/design/mockups/onboarding-reference-owner.jpg):
// the scene behind the character on the colour block (a boat, a cutting board or a scooter, birds, bubbles, a map
// pin, clouds, a sun glow and a wave strip where the block meets the sheet), sun-ray sparks, gulls, the tagline
// swoosh, the soft blobs behind the feature tiles and the banner's wave and stand-in food. Everything here is
// decoration: callers wrap it in ExcludeSemantics, and nothing moves.

import 'dart:math' as math;

import 'package:flutter/material.dart';

/// Which story a page tells; it picks the scene drawn on the block and in the banner stand-in.
enum OnboardingScene { fresh, cleaned, delivery }

/// [color] about 32% darker: the tone of the silhouettes on a colour block.
Color blockShade(Color color, {double amount = 0.32}) {
  final hsl = HSLColor.fromColor(color);
  return hsl.withLightness((hsl.lightness * (1 - amount)).clamp(0.0, 1.0)).toColor();
}

/// The scene on the colour block. [avatar] and [overlap] place the sun glow behind the ringed circle, whose
/// bottom edge sits [overlap] below the block's bottom and whose right edge is [avatarRight] from the right.
class BlockScenePainter extends CustomPainter {
  const BlockScenePainter({
    required this.scene,
    required this.shade,
    required this.light,
    required this.wave,
    required this.glow,
    required this.avatar,
    required this.overlap,
    this.avatarRight = 12,
  });

  final OnboardingScene scene;

  /// Silhouettes and birds: a darker tone of the block.
  final Color shade;

  /// Clouds, bubbles and sparkles: a light tone over the block.
  final Color light;

  /// The wave strip along the bottom.
  final Color wave;

  /// The sun glow behind the circle's top-right edge.
  final Color glow;
  final double avatar;
  final double overlap;
  final double avatarRight;

  @override
  void paint(Canvas canvas, Size size) {
    final r = avatar / 2;
    final centre = Offset(size.width - avatarRight - r, size.height - avatar + overlap + r);
    final fill = Paint()..color = shade;
    final stroke = Paint()
      ..color = shade
      ..style = PaintingStyle.stroke
      ..strokeWidth = 2.2
      ..strokeCap = StrokeCap.round
      ..strokeJoin = StrokeJoin.round;
    final lightFill = Paint()..color = light;
    final lightStroke = Paint()
      ..color = light
      ..style = PaintingStyle.stroke
      ..strokeWidth = 2
      ..strokeCap = StrokeCap.round;

    // Sun glow behind the circle's top-right edge.
    final sun = centre + Offset(r * 0.62, -r * 0.66);
    canvas.drawCircle(
      sun,
      r * 0.7,
      Paint()
        ..shader = RadialGradient(
          colors: [glow, glow.withValues(alpha: 0)],
        ).createShader(Rect.fromCircle(center: sun, radius: r * 0.7)),
    );

    // Two soft cloud puffs.
    _cloud(canvas, Offset(size.width - 26, centre.dy - r * 0.95), 22, lightFill);
    _cloud(canvas, Offset(28, size.height * 0.62), 18, lightFill);

    final ground = size.height - 16;
    const left = 12.0;
    final width = math.min(150.0, (size.width - avatar - avatarRight) * 0.82);

    switch (scene) {
      case OnboardingScene.fresh:
        _boat(canvas, Offset(left, ground), width, fill, stroke);
        _waveBand(canvas, left - 12, ground, width + 30, fill);
        for (final b in [
          Offset(centre.dx - r - 20, centre.dy - r * 0.3),
          Offset(centre.dx - r - 2, centre.dy - r * 0.55),
          Offset(centre.dx + r * 0.25, centre.dy - r - 14),
          Offset(centre.dx + r * 0.62, centre.dy - r - 2),
          Offset(left + width * 0.92, ground - width * 0.62),
        ]) {
          _gull(canvas, b, 7, stroke);
        }
      case OnboardingScene.cleaned:
        _board(canvas, Offset(left, ground), width, fill);
        for (final (o, radius) in [
          (Offset(centre.dx - r - 16, centre.dy - r * 0.5), 6.0),
          (Offset(centre.dx - r - 4, centre.dy - r * 0.2), 3.5),
          (Offset(centre.dx - r - 28, centre.dy), 4.5),
          (Offset(centre.dx + r * 0.4, centre.dy - r - 12), 5.0),
          (Offset(left + width * 0.95, ground - width * 0.55), 4.0),
        ]) {
          canvas.drawCircle(o, radius, lightStroke);
        }
        _fishOutline(canvas, Offset(left + width * 0.55, ground - width * 0.62), width * 0.3, stroke);
        for (final o in [
          Offset(centre.dx + r * 0.85, centre.dy - r - 6),
          Offset(centre.dx - r - 34, centre.dy - r * 0.7),
          Offset(left + width * 0.12, ground - width * 0.7),
        ]) {
          _sparkle(canvas, o, 6, lightFill);
        }
      case OnboardingScene.delivery:
        _houses(canvas, Offset(left + width * 0.55, ground), width * 0.5, fill);
        _scooter(canvas, Offset(left, ground), width * 0.7, fill);
        final pin = Offset(centre.dx - r - 18, centre.dy - r * 0.45);
        _dottedPath(canvas, Offset(left + width * 0.7, ground - width * 0.3), pin + const Offset(0, 14), stroke);
        _pin(canvas, pin, 11, fill, light);
        for (final b in [
          Offset(centre.dx + r * 0.25, centre.dy - r - 14),
          Offset(centre.dx + r * 0.62, centre.dy - r - 2),
          Offset(centre.dx - r - 8, centre.dy - r * 0.95),
          Offset(left + width * 0.3, ground - width * 0.85),
        ]) {
          _gull(canvas, b, 7, stroke);
        }
    }

    // The wave strip where the block meets the sheet.
    final strip = Path()..moveTo(0, size.height);
    strip.lineTo(0, size.height - 12);
    const step = 36.0;
    for (var x = 0.0; x < size.width; x += step) {
      strip.quadraticBezierTo(x + step / 4, size.height - 20, x + step / 2, size.height - 12);
      strip.quadraticBezierTo(x + step * 3 / 4, size.height - 4, x + step, size.height - 12);
    }
    strip
      ..lineTo(size.width, size.height)
      ..close();
    canvas.drawPath(strip, Paint()..color = wave);
  }

  static void _cloud(Canvas canvas, Offset o, double s, Paint paint) {
    canvas
      ..drawCircle(o + Offset(-s * 0.6, s * 0.15), s * 0.45, paint)
      ..drawCircle(o + Offset(0, -s * 0.05), s * 0.6, paint)
      ..drawCircle(o + Offset(s * 0.65, s * 0.18), s * 0.42, paint)
      ..drawRRect(
        RRect.fromRectAndRadius(Rect.fromLTWH(o.dx - s * 1.05, o.dy + s * 0.05, s * 2.1, s * 0.5), Radius.circular(s)),
        paint,
      );
  }

  /// A small gull: two curved strokes meeting at [o].
  static void _gull(Canvas canvas, Offset o, double s, Paint paint) {
    final path = Path()
      ..moveTo(o.dx - s, o.dy - s * 0.45)
      ..quadraticBezierTo(o.dx - s * 0.45, o.dy - s * 0.85, o.dx, o.dy)
      ..quadraticBezierTo(o.dx + s * 0.45, o.dy - s * 0.85, o.dx + s, o.dy - s * 0.45);
    canvas.drawPath(path, paint);
  }

  /// A fishing boat: hull, cabin, two masts with rigging and a flag; its keel sits on [base].
  static void _boat(Canvas canvas, Offset base, double w, Paint fill, Paint stroke) {
    final hull = w * 0.2;
    final top = base.dy - hull;
    canvas.drawPath(
      Path()
        ..moveTo(base.dx, top)
        ..lineTo(base.dx + w, top - w * 0.04)
        ..lineTo(base.dx + w * 0.86, base.dy)
        ..lineTo(base.dx + w * 0.1, base.dy)
        ..close(),
      fill,
    );
    canvas.drawRRect(
      RRect.fromRectAndRadius(Rect.fromLTWH(base.dx + w * 0.46, top - w * 0.2, w * 0.3, w * 0.2), Radius.circular(w * 0.02)),
      fill,
    );
    canvas.drawRect(Rect.fromLTWH(base.dx + w * 0.5, top - w * 0.27, w * 0.18, w * 0.07), fill);
    final mast = Offset(base.dx + w * 0.3, top - w * 0.62);
    final aft = Offset(base.dx + w * 0.62, top - w * 0.46);
    final rig = Paint()
      ..color = fill.color
      ..strokeWidth = math.max(1.2, w * 0.012)
      ..style = PaintingStyle.stroke
      ..strokeCap = StrokeCap.round;
    canvas
      ..drawLine(Offset(mast.dx, top), mast, stroke)
      ..drawLine(Offset(aft.dx, top - w * 0.27), aft, stroke)
      ..drawLine(mast, Offset(base.dx + w * 0.02, top), rig)
      ..drawLine(mast, aft, rig)
      ..drawLine(aft, Offset(base.dx + w * 0.98, top - w * 0.04), rig)
      ..drawPath(
        Path()
          ..moveTo(mast.dx, mast.dy)
          ..lineTo(mast.dx + w * 0.14, mast.dy + w * 0.04)
          ..lineTo(mast.dx, mast.dy + w * 0.08)
          ..close(),
        fill,
      );
  }

  /// The small wave band the boat stands on.
  static void _waveBand(Canvas canvas, double x, double y, double w, Paint fill) {
    final path = Path()..moveTo(x, y + 2);
    const step = 18.0;
    for (var dx = 0.0; dx < w; dx += step) {
      path.quadraticBezierTo(x + dx + step / 2, y - 5, x + dx + step, y + 2);
    }
    path
      ..lineTo(x + w, y + 7)
      ..lineTo(x, y + 7)
      ..close();
    canvas.drawPath(path, fill);
  }

  /// A cutting board with a handle hole and a knife lying across it.
  static void _board(Canvas canvas, Offset base, double w, Paint fill) {
    final h = w * 0.36;
    final board = Path()
      ..addRRect(RRect.fromRectAndRadius(Rect.fromLTWH(base.dx, base.dy - h, w * 0.78, h), Radius.circular(h * 0.3)))
      ..addRRect(
        RRect.fromRectAndRadius(Rect.fromLTWH(base.dx + w * 0.78 - 2, base.dy - h * 0.66, w * 0.16, h * 0.32), Radius.circular(h * 0.16)),
      );
    final hole = Path()..addOval(Rect.fromCircle(center: Offset(base.dx + w * 0.88, base.dy - h * 0.5), radius: h * 0.06));
    canvas.drawPath(Path.combine(PathOperation.difference, board, hole), fill);
    // The knife floats just above the board.
    final y = base.dy - h - 10;
    canvas
      ..drawPath(
        Path()
          ..moveTo(base.dx + w * 0.1, y)
          ..lineTo(base.dx + w * 0.56, y - 4)
          ..quadraticBezierTo(base.dx + w * 0.6, y + 5, base.dx + w * 0.56, y + 7)
          ..lineTo(base.dx + w * 0.1, y + 7)
          ..close(),
        fill,
      )
      ..drawRRect(
        RRect.fromRectAndRadius(Rect.fromLTWH(base.dx + w * 0.56, y - 2, w * 0.26, 9), const Radius.circular(4.5)),
        fill,
      );
  }

  /// A fish drawn as an outline, nose to the left.
  static void _fishOutline(Canvas canvas, Offset c, double s, Paint stroke) {
    final body = Path()
      ..moveTo(c.dx - s, c.dy)
      ..quadraticBezierTo(c.dx - s * 0.2, c.dy - s * 0.62, c.dx + s * 0.55, c.dy)
      ..quadraticBezierTo(c.dx - s * 0.2, c.dy + s * 0.62, c.dx - s, c.dy)
      ..moveTo(c.dx + s * 0.55, c.dy)
      ..lineTo(c.dx + s, c.dy - s * 0.32)
      ..lineTo(c.dx + s, c.dy + s * 0.32)
      ..close();
    canvas
      ..drawPath(body, stroke)
      ..drawCircle(Offset(c.dx - s * 0.6, c.dy - s * 0.06), 1.8, Paint()..color = stroke.color);
  }

  /// A four-pointed sparkle.
  static void _sparkle(Canvas canvas, Offset c, double s, Paint fill) {
    final path = Path()
      ..moveTo(c.dx, c.dy - s)
      ..quadraticBezierTo(c.dx, c.dy, c.dx + s, c.dy)
      ..quadraticBezierTo(c.dx, c.dy, c.dx, c.dy + s)
      ..quadraticBezierTo(c.dx, c.dy, c.dx - s, c.dy)
      ..quadraticBezierTo(c.dx, c.dy, c.dx, c.dy - s)
      ..close();
    canvas.drawPath(path, fill);
  }

  /// A delivery scooter with a cold box on the back, facing right.
  static void _scooter(Canvas canvas, Offset base, double w, Paint fill) {
    final wheel = w * 0.13;
    final axle = base.dy - wheel;
    final body = Path()
      ..moveTo(base.dx + w * 0.1, axle - wheel * 0.2)
      ..quadraticBezierTo(base.dx + w * 0.1, axle - w * 0.3, base.dx + w * 0.42, axle - w * 0.28)
      ..lineTo(base.dx + w * 0.64, axle - w * 0.28)
      ..lineTo(base.dx + w * 0.76, axle - w * 0.62)
      ..lineTo(base.dx + w * 0.84, axle - w * 0.62)
      ..lineTo(base.dx + w * 0.82, axle - wheel * 0.2)
      ..close();
    canvas
      ..drawPath(body, fill)
      ..drawRRect(
        RRect.fromRectAndRadius(Rect.fromLTWH(base.dx + w * 0.02, axle - w * 0.62, w * 0.34, w * 0.3), Radius.circular(w * 0.04)),
        fill,
      )
      ..drawRect(Rect.fromLTWH(base.dx + w * 0.7, axle - w * 0.66, w * 0.18, w * 0.04), fill)
      ..drawCircle(Offset(base.dx + w * 0.18, axle), wheel, fill)
      ..drawCircle(Offset(base.dx + w * 0.82, axle), wheel, fill);
  }

  /// Two small houses with pitched roofs.
  static void _houses(Canvas canvas, Offset base, double w, Paint fill) {
    for (final (x, s) in [(0.0, 0.5), (0.52, 0.42)]) {
      final hw = w * s;
      final left = base.dx + w * x;
      canvas.drawPath(
        Path()
          ..moveTo(left, base.dy)
          ..lineTo(left, base.dy - hw * 0.6)
          ..lineTo(left + hw / 2, base.dy - hw * 1.05)
          ..lineTo(left + hw, base.dy - hw * 0.6)
          ..lineTo(left + hw, base.dy)
          ..close(),
        fill,
      );
    }
  }

  /// A dotted route from [a] curving up to [b].
  static void _dottedPath(Canvas canvas, Offset a, Offset b, Paint stroke) {
    final control = Offset((a.dx + b.dx) / 2 + 10, math.min(a.dy, b.dy) - 18);
    final dot = Paint()..color = stroke.color;
    const count = 11;
    for (var i = 0; i <= count; i++) {
      final t = i / count;
      final p = Offset(
        (1 - t) * (1 - t) * a.dx + 2 * (1 - t) * t * control.dx + t * t * b.dx,
        (1 - t) * (1 - t) * a.dy + 2 * (1 - t) * t * control.dy + t * t * b.dy,
      );
      canvas.drawCircle(p, 1.7, dot);
    }
  }

  /// A map pin whose point is [s] * 1.3 below [c].
  static void _pin(Canvas canvas, Offset c, double s, Paint fill, Color hole) {
    final path = Path()
      ..moveTo(c.dx, c.dy + s * 1.3)
      ..quadraticBezierTo(c.dx - s * 1.1, c.dy + s * 0.2, c.dx - s, c.dy - s * 0.1)
      ..arcToPoint(Offset(c.dx + s, c.dy - s * 0.1), radius: Radius.circular(s))
      ..quadraticBezierTo(c.dx + s * 1.1, c.dy + s * 0.2, c.dx, c.dy + s * 1.3)
      ..close();
    canvas
      ..drawPath(path, fill)
      ..drawCircle(c - Offset(0, s * 0.1), s * 0.38, Paint()..color = hole);
  }

  @override
  bool shouldRepaint(BlockScenePainter old) =>
      old.scene != scene ||
      old.shade != shade ||
      old.light != light ||
      old.wave != wave ||
      old.glow != glow ||
      old.avatar != avatar ||
      old.overlap != overlap ||
      old.avatarRight != avatarRight;
}

/// Three short sun-ray dashes fanning up and to the right (the reference's sparks by the heading and sticker).
class SparkRays extends StatelessWidget {
  const SparkRays({super.key, required this.color, this.size = 24, this.flip = false});

  final Color color;
  final double size;

  /// Fan to the left instead.
  final bool flip;

  @override
  Widget build(BuildContext context) => ExcludeSemantics(
    child: Transform.flip(
      flipX: flip,
      child: CustomPaint(size: Size.square(size), painter: _RaysPainter(color)),
    ),
  );
}

class _RaysPainter extends CustomPainter {
  const _RaysPainter(this.color);

  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = color
      ..strokeWidth = size.width * 0.11
      ..strokeCap = StrokeCap.round;
    final origin = Offset(0, size.height);
    for (final angle in const [-0.15, -0.8, -1.42]) {
      final dir = Offset(math.cos(angle), math.sin(angle));
      canvas.drawLine(origin + dir * size.width * 0.45, origin + dir * size.width * 0.92, paint);
    }
  }

  @override
  bool shouldRepaint(_RaysPainter old) => old.color != color;
}

/// Two small gulls in flight, for the sheet's top corner.
class GullsPainter extends CustomPainter {
  const GullsPainter(this.color);

  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = color
      ..style = PaintingStyle.stroke
      ..strokeWidth = 2
      ..strokeCap = StrokeCap.round;
    BlockScenePainter._gull(canvas, Offset(size.width * 0.25, size.height * 0.75), 8, paint);
    BlockScenePainter._gull(canvas, Offset(size.width * 0.72, size.height * 0.4), 6, paint);
  }

  @override
  bool shouldRepaint(GullsPainter old) => old.color != color;
}

/// The handwritten swoosh under the tagline.
class SwooshPainter extends CustomPainter {
  const SwooshPainter(this.color);

  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final y = size.height - 4;
    final w = math.min(size.width * 0.7, 110.0);
    final path = Path()
      ..moveTo(4, y)
      ..quadraticBezierTo(w * 0.5, y - 9, w, y - 3);
    canvas.drawPath(
      path,
      Paint()
        ..color = color
        ..style = PaintingStyle.stroke
        ..strokeWidth = 2.4
        ..strokeCap = StrokeCap.round,
    );
  }

  @override
  bool shouldRepaint(SwooshPainter old) => old.color != color;
}

/// An organic pastel blob behind a feature tile's picture.
class BlobPainter extends CustomPainter {
  const BlobPainter(this.color, {this.seed = 0});

  final Color color;

  /// Turns the blob so neighbouring tiles differ.
  final int seed;

  @override
  void paint(Canvas canvas, Size size) {
    const radii = [1.0, 0.9, 1.04, 0.92, 1.0, 0.88];
    final c = size.center(Offset.zero);
    final base = math.min(size.width, size.height) / 2 * 0.94;
    final points = [
      for (var i = 0; i < radii.length; i++)
        c +
            Offset.fromDirection(
              2 * math.pi * i / radii.length + seed * 0.7,
              base * radii[(i + seed) % radii.length],
            ),
    ];
    Offset mid(Offset a, Offset b) => (a + b) / 2;
    final start = mid(points.last, points.first);
    final path = Path()..moveTo(start.dx, start.dy);
    for (var i = 0; i < points.length; i++) {
      final p = points[i];
      final next = mid(p, points[(i + 1) % points.length]);
      path.quadraticBezierTo(p.dx, p.dy, next.dx, next.dy);
    }
    canvas.drawPath(path..close(), Paint()..color = color);
  }

  @override
  bool shouldRepaint(BlobPainter old) => old.color != color || old.seed != seed;
}

/// The banner's soft background: a pale wash with two gentle waves.
class BannerWavePainter extends CustomPainter {
  const BannerWavePainter({required this.base, required this.wave});

  final Color base;
  final Color wave;

  @override
  void paint(Canvas canvas, Size size) {
    canvas.drawRect(Offset.zero & size, Paint()..color = base);
    for (final (top, alpha) in [(0.42, 0.55), (0.62, 1.0)]) {
      final y = size.height * top;
      final path = Path()..moveTo(0, y);
      final step = size.width / 4;
      for (var x = 0.0; x < size.width - 0.5; x += step) {
        path
          ..quadraticBezierTo(x + step / 4, y - 10, x + step / 2, y)
          ..quadraticBezierTo(x + step * 3 / 4, y + 10, x + step, y);
      }
      path
        ..lineTo(size.width, size.height)
        ..lineTo(0, size.height)
        ..close();
      canvas.drawPath(path, Paint()..color = wave.withValues(alpha: wave.a * alpha));
    }
  }

  @override
  bool shouldRepaint(BannerWavePainter old) => old.base != base || old.wave != wave;
}

/// The banner's stand-in until its picture is bundled: a simple drawn seafood still life for each scene.
class BannerFoodPainter extends CustomPainter {
  const BannerFoodPainter({
    required this.scene,
    required this.ice,
    required this.silver,
    required this.coral,
    required this.mango,
    required this.teal,
    required this.wood,
  });

  final OnboardingScene scene;
  final Color ice;
  final Color silver;
  final Color coral;
  final Color mango;
  final Color teal;
  final Color wood;

  @override
  void paint(Canvas canvas, Size size) {
    final w = size.width;
    final h = size.height;
    final floor = h * 0.86;
    Paint p(Color c) => Paint()..color = c;
    switch (scene) {
      case OnboardingScene.fresh:
        // Crushed ice, a whole fish, prawns, a crab and a lemon.
        for (var i = 0; i < 9; i++) {
          final x = w * (0.06 + i * 0.11);
          canvas.drawRRect(
            RRect.fromRectAndRadius(Rect.fromCenter(center: Offset(x, floor - (i.isEven ? 4 : 0)), width: 16, height: 13), const Radius.circular(4)),
            p(ice),
          );
        }
        _fish(canvas, Offset(w * 0.5, floor - h * 0.26), w * 0.3, p(silver));
        for (final (x, y) in [(0.14, 0.5), (0.24, 0.62)]) {
          canvas.drawArc(
            Rect.fromCircle(center: Offset(w * x, h * y), radius: h * 0.13),
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
          ..drawOval(Rect.fromCenter(center: Offset(w * 0.82, floor - h * 0.14), width: w * 0.2, height: h * 0.24), p(coral))
          ..drawCircle(Offset(w * 0.68, floor - h * 0.06), h * 0.09, p(mango));
      case OnboardingScene.cleaned:
        // A board with a steak, curry-cut chunks, a fillet and a knife.
        canvas.drawRRect(
          RRect.fromRectAndRadius(Rect.fromLTWH(w * 0.06, floor - h * 0.3, w * 0.8, h * 0.3), Radius.circular(h * 0.08)),
          p(wood),
        );
        canvas
          ..drawOval(Rect.fromCenter(center: Offset(w * 0.22, floor - h * 0.36), width: w * 0.18, height: h * 0.2), p(coral))
          ..drawPath(
            Path()
              ..moveTo(w * 0.52, floor - h * 0.34)
              ..quadraticBezierTo(w * 0.66, floor - h * 0.5, w * 0.8, floor - h * 0.36)
              ..quadraticBezierTo(w * 0.66, floor - h * 0.26, w * 0.52, floor - h * 0.34)
              ..close(),
            p(silver),
          );
        for (final x in [0.34, 0.41]) {
          canvas.drawRRect(
            RRect.fromRectAndRadius(Rect.fromLTWH(w * x, floor - h * 0.44, w * 0.06, w * 0.06), const Radius.circular(3)),
            p(coral),
          );
        }
        canvas.drawRRect(
          RRect.fromRectAndRadius(Rect.fromLTWH(w * 0.12, floor - h * 0.08, w * 0.5, h * 0.06), const Radius.circular(3)),
          p(silver),
        );
      case OnboardingScene.delivery:
        // A cold box beside a scooter at a door.
        canvas
          ..drawRRect(
            RRect.fromRectAndRadius(Rect.fromLTWH(w * 0.7, floor - h * 0.7, w * 0.2, h * 0.7), const Radius.circular(4)),
            p(teal),
          )
          ..drawRRect(
            RRect.fromRectAndRadius(Rect.fromLTWH(w * 0.44, floor - h * 0.34, w * 0.2, h * 0.34), Radius.circular(h * 0.05)),
            p(teal.withValues(alpha: 0.8)),
          );
        final scooter = Paint()..color = coral;
        canvas
          ..drawPath(
            Path()
              ..moveTo(w * 0.06, floor - h * 0.12)
              ..quadraticBezierTo(w * 0.06, floor - h * 0.34, w * 0.2, floor - h * 0.32)
              ..lineTo(w * 0.28, floor - h * 0.32)
              ..lineTo(w * 0.33, floor - h * 0.6)
              ..lineTo(w * 0.37, floor - h * 0.6)
              ..lineTo(w * 0.36, floor - h * 0.12)
              ..close(),
            scooter,
          )
          ..drawCircle(Offset(w * 0.1, floor - h * 0.08), h * 0.09, p(silver))
          ..drawCircle(Offset(w * 0.34, floor - h * 0.08), h * 0.09, p(silver));
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
  bool shouldRepaint(BannerFoodPainter old) =>
      old.scene != scene ||
      old.ice != ice ||
      old.silver != silver ||
      old.coral != coral ||
      old.mango != mango ||
      old.teal != teal ||
      old.wood != wood;
}

/// A small hand-drawn arrow curving from the banner line towards the food.
class ArrowDoodlePainter extends CustomPainter {
  const ArrowDoodlePainter(this.color);

  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = color
      ..style = PaintingStyle.stroke
      ..strokeWidth = 2
      ..strokeCap = StrokeCap.round
      ..strokeJoin = StrokeJoin.round;
    final tip = Offset(2, size.height * 0.75);
    canvas
      ..drawPath(
        Path()
          ..moveTo(size.width - 2, 2)
          ..quadraticBezierTo(size.width * 0.3, size.height * 0.05, tip.dx, tip.dy),
        paint,
      )
      ..drawPath(
        Path()
          ..moveTo(tip.dx + 8, tip.dy - 3)
          ..lineTo(tip.dx, tip.dy)
          ..lineTo(tip.dx + 2, tip.dy - 9),
        paint,
      );
  }

  @override
  bool shouldRepaint(ArrowDoodlePainter old) => old.color != color;
}
