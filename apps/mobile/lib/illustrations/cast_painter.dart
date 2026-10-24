// Placeholder cast, drawn in code until the final clay-render images ship (D53). Soft radial gradients give
// each head, body and prop a lit-from-above clay look. Our own drawing: simple shapes, no text.
//
// The cast: Padma (fishmonger, teal apron, coral headscarf), Ravi (rider, teal helmet, ice-blue cold box),
// Anil (support, headset), Ammamma (home cook) and Chanduva (the pomfret sidekick).

import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../theme/app_theme.dart';

enum CastMember { padma, ravi, anil, ammamma, chanduva }

/// The circle's tint behind the character.
enum CastBackdrop { sand, teal, ice, coral }

class CastPainter extends CustomPainter {
  CastPainter({required this.member, required this.colors, this.backdrop = CastBackdrop.sand, this.sidekick = false});

  final CastMember member;
  final IllustratedColors colors;
  final CastBackdrop backdrop;

  /// Adds a small Chanduva next to a person.
  final bool sidekick;

  // Fixed character colours from the approved sheet; the circle behind them follows the theme.
  static const _skin = Color(0xFFDDA57A);
  static const _skinShade = Color(0xFFA9683F);
  static const _hair = Color(0xFF241C18);
  static const _kurta = Color(0xFFF4EFE6);
  static const _jacket = Color(0xFF14958A);
  static const _iceBox = Color(0xFF8CC8E4);
  static const _silver = Color(0xFFEEF2F5);
  static const _silverShade = Color(0xFF8FA3B4);
  static const _finSilver = Color(0xFFB7C4D2);

  @override
  void paint(Canvas canvas, Size size) {
    final s = size.shortestSide;
    final o = Offset((size.width - s) / 2, (size.height - s) / 2);
    canvas.save();
    canvas.translate(o.dx, o.dy);

    final center = Offset(s / 2, s / 2);
    final radius = s * 0.47;
    final circle = Rect.fromCircle(center: center, radius: radius);

    // Ground shadow, then the tinted circle.
    canvas.drawCircle(center + Offset(0, s * 0.02), radius, Paint()..color = colors.softShadow);
    final tint = switch (backdrop) {
      CastBackdrop.sand => colors.sandDeep,
      CastBackdrop.teal => colors.tealSoft,
      CastBackdrop.ice => colors.ice,
      CastBackdrop.coral => colors.coralSoft,
    };
    canvas.drawCircle(center, radius, _clay(circle, Color.lerp(colors.paper, tint, 0.35)!, tint));

    canvas.save();
    canvas.clipPath(Path()..addOval(circle));
    if (member == CastMember.chanduva) {
      _pomfret(canvas, Offset(s * 0.5, s * 0.52), s * 0.62, mood: 1);
    } else {
      _person(canvas, s);
      if (sidekick) _pomfret(canvas, Offset(s * 0.2, s * 0.78), s * 0.22, mood: 1);
    }
    canvas.restore();

    // Two bubbles outside the character.
    canvas.drawCircle(Offset(s * 0.86, s * 0.16), s * 0.035, Paint()..color = colors.coral);
    canvas.drawCircle(Offset(s * 0.12, s * 0.28), s * 0.022, Paint()..color = colors.tealDecor);
    canvas.restore();
  }

  /// A radial gradient lit from the top left: the clay look.
  Paint _clay(Rect r, Color light, Color dark) => Paint()
    ..shader = RadialGradient(center: const Alignment(-0.4, -0.5), radius: 1.1, colors: [light, dark]).createShader(r);

  Color _lighter(Color c) => Color.lerp(c, Colors.white, 0.35)!;
  Color _darker(Color c) => Color.lerp(c, Colors.black, 0.18)!;

  void _person(Canvas canvas, double s) {
    final head = Offset(s * 0.5, s * 0.43);
    final hr = s * 0.15;
    final body = Rect.fromCenter(center: Offset(s * 0.5, s * 0.95), width: s * 0.64, height: s * 0.52);

    // Clothes follow the approved character sheet (docs/design/characters).
    final shirt = switch (member) {
      CastMember.padma => _kurta,
      CastMember.ravi => _jacket,
      CastMember.anil => _jacket,
      CastMember.ammamma => colors.mango,
      CastMember.chanduva => _silver,
    };

    // Ravi's ice-blue backpack box peeks out behind his shoulder.
    if (member == CastMember.ravi) {
      final box = RRect.fromRectAndRadius(
        Rect.fromCenter(center: Offset(s * 0.22, s * 0.74), width: s * 0.26, height: s * 0.3),
        Radius.circular(s * 0.04),
      );
      canvas
        ..drawRRect(box, _clay(box.outerRect, _lighter(_iceBox), _darker(_iceBox)))
        ..drawRRect(box, _outline(s));
      _snowflake(canvas, box.center, s * 0.07);
    }

    // Neck and body.
    final neck = Rect.fromCenter(center: Offset(s * 0.5, s * 0.62), width: s * 0.1, height: s * 0.1);
    canvas
      ..drawRRect(RRect.fromRectAndRadius(neck, Radius.circular(s * 0.03)), Paint()..color = _skinShade)
      ..drawOval(body, _clay(body, _lighter(shirt), _darker(shirt)));

    switch (member) {
      case CastMember.padma:
        // Glossy teal apron over a white kurta.
        final apron = RRect.fromRectAndRadius(
          Rect.fromCenter(center: Offset(s * 0.5, s * 0.9), width: s * 0.32, height: s * 0.36),
          Radius.circular(s * 0.06),
        );
        canvas.drawRRect(apron, _clay(apron.outerRect, _lighter(_jacket), _darker(_jacket)));
        final strap = Paint()
          ..color = _darker(_jacket)
          ..strokeWidth = s * 0.025
          ..strokeCap = StrokeCap.round;
        canvas
          ..drawLine(Offset(s * 0.37, s * 0.74), Offset(s * 0.43, s * 0.66), strap)
          ..drawLine(Offset(s * 0.63, s * 0.74), Offset(s * 0.57, s * 0.66), strap);
      case CastMember.ravi:
        // Reflective stripe and a backpack strap on the teal jacket.
        final stripe = Paint()
          ..color = const Color(0xFFDDE7E6)
          ..strokeWidth = s * 0.018;
        canvas.drawLine(Offset(s * 0.2, s * 0.86), Offset(s * 0.8, s * 0.86), stripe);
        canvas.drawLine(
          Offset(s * 0.36, s * 0.7),
          Offset(s * 0.33, s * 0.98),
          Paint()
            ..color = const Color(0xFF26302F)
            ..strokeWidth = s * 0.03,
        );
      case CastMember.anil:
        // Polo collar.
        final collar = Path()
          ..moveTo(s * 0.42, s * 0.68)
          ..lineTo(s * 0.5, s * 0.75)
          ..lineTo(s * 0.58, s * 0.68)
          ..close();
        canvas.drawPath(collar, Paint()..color = _darker(_jacket));
      case CastMember.ammamma:
        // Coral saree border across the shoulder.
        final border = Paint()
          ..color = colors.coral
          ..style = PaintingStyle.stroke
          ..strokeWidth = s * 0.04;
        canvas.drawLine(Offset(s * 0.36, s * 0.7), Offset(s * 0.66, s * 1.0), border);
      case CastMember.chanduva:
        break;
    }

    // Hair behind the head.
    switch (member) {
      case CastMember.padma:
        canvas.drawCircle(head + Offset(-hr * 0.85, hr * 0.55), hr * 0.32, Paint()..color = _hair);
        canvas.drawCircle(head + Offset(0, -hr * 0.05), hr * 1.06, Paint()..color = _hair);
      case CastMember.ammamma:
        final silver = _clay(_r(head, hr), const Color(0xFFEDEBE7), const Color(0xFFA9A39B));
        canvas
          ..drawCircle(head + Offset(0, -hr * 0.95), hr * 0.42, silver)
          ..drawCircle(head + Offset(0, -hr * 0.1), hr * 1.08, silver);
      case CastMember.anil:
        // Wavy black hair: a few overlapping curls on top.
        final hair = Paint()..color = _hair;
        canvas.drawCircle(head + Offset(0, -hr * 0.12), hr * 1.04, hair);
        for (final dx in [-0.55, -0.15, 0.25, 0.6]) {
          canvas.drawCircle(head + Offset(hr * dx, -hr * 0.9), hr * 0.32, hair);
        }
      case CastMember.ravi:
      case CastMember.chanduva:
        break;
    }

    // Head and ears.
    canvas
      ..drawCircle(head + Offset(-hr, hr * 0.1), hr * 0.22, Paint()..color = _skinShade)
      ..drawCircle(head + Offset(hr, hr * 0.1), hr * 0.22, Paint()..color = _skinShade)
      ..drawCircle(head + Offset(0, hr * 0.08), hr * 0.98, _clay(_r(head, hr), _skin, _skinShade));

    // Headwear, beard, glasses.
    switch (member) {
      case CastMember.padma:
        // Coral headscarf over the top of the head, knotted at the side.
        final scarf = Path()
          ..addArc(Rect.fromCircle(center: head, radius: hr * 1.1), math.pi * 1.08, math.pi * 0.84)
          ..close();
        canvas
          ..drawPath(scarf, _clay(_r(head, hr), _lighter(colors.coral), _darker(colors.coral)))
          ..drawCircle(head + Offset(hr * 1.0, -hr * 0.32), hr * 0.24, Paint()..color = _darker(colors.coral));
        canvas.drawCircle(head + Offset(0, -hr * 0.42), hr * 0.07, Paint()..color = const Color(0xFFB3261E));
      case CastMember.ravi:
        // Short beard, then the teal helmet with a clear visor.
        canvas.drawArc(
          Rect.fromCircle(center: head + Offset(0, hr * 0.12), radius: hr * 0.9),
          math.pi * 0.12,
          math.pi * 0.76,
          false,
          Paint()
            ..color = _hair
            ..style = PaintingStyle.stroke
            ..strokeWidth = hr * 0.22,
        );
        final helmet = Path()
          ..addArc(
            Rect.fromCircle(center: head + Offset(0, -hr * 0.05), radius: hr * 1.18),
            math.pi * 1.04,
            math.pi * 0.92,
          )
          ..close();
        canvas.drawPath(helmet, _clay(_r(head, hr), _lighter(_jacket), _darker(_jacket)));
        canvas.drawRRect(
          RRect.fromRectAndRadius(
            Rect.fromCenter(center: head + Offset(0, -hr * 0.28), width: hr * 2.1, height: hr * 0.3),
            Radius.circular(hr * 0.15),
          ),
          Paint()..color = Colors.white.withValues(alpha: 0.45),
        );
      case CastMember.anil:
        _glasses(canvas, head, hr);
        // Slim headset: band, ear pad and a microphone.
        final band = Paint()
          ..color = const Color(0xFF2B2F33)
          ..style = PaintingStyle.stroke
          ..strokeWidth = hr * 0.1
          ..strokeCap = StrokeCap.round;
        canvas
          ..drawArc(Rect.fromCircle(center: head, radius: hr * 1.12), math.pi * 1.05, math.pi * 0.9, false, band)
          ..drawCircle(head + Offset(hr * 1.06, hr * 0.1), hr * 0.24, Paint()..color = const Color(0xFF2B2F33));
        final mic = Path()
          ..moveTo(head.dx + hr * 1.05, head.dy + hr * 0.3)
          ..quadraticBezierTo(head.dx + hr * 0.9, head.dy + hr * 0.9, head.dx + hr * 0.3, head.dy + hr * 0.72);
        canvas.drawPath(mic, band..strokeWidth = hr * 0.07);
      case CastMember.ammamma:
        canvas.drawCircle(head + Offset(0, -hr * 0.42), hr * 0.09, Paint()..color = const Color(0xFFB3261E));
      case CastMember.chanduva:
        break;
    }

    _face(canvas, head, hr);
  }

  void _glasses(Canvas canvas, Offset head, double hr) {
    final frame = Paint()
      ..color = const Color(0xFF2B2F33)
      ..style = PaintingStyle.stroke
      ..strokeWidth = hr * 0.06;
    canvas
      ..drawCircle(head + Offset(-hr * 0.38, -hr * 0.05), hr * 0.26, frame)
      ..drawCircle(head + Offset(hr * 0.38, -hr * 0.05), hr * 0.26, frame)
      ..drawLine(head + Offset(-hr * 0.12, -hr * 0.08), head + Offset(hr * 0.12, -hr * 0.08), frame);
  }

  void _snowflake(Canvas canvas, Offset c, double r) {
    final p = Paint()
      ..color = Colors.white
      ..strokeWidth = r * 0.22
      ..strokeCap = StrokeCap.round;
    for (var i = 0; i < 3; i++) {
      final a = i * math.pi / 3;
      final d = Offset(math.cos(a) * r, math.sin(a) * r);
      canvas.drawLine(c - d, c + d, p);
    }
  }

  void _face(Canvas canvas, Offset head, double hr) {
    final ink = Paint()..color = const Color(0xFF14302F);
    canvas
      ..drawOval(
        Rect.fromCenter(center: head + Offset(-hr * 0.38, -hr * 0.05), width: hr * 0.16, height: hr * 0.22),
        ink,
      )
      ..drawOval(
        Rect.fromCenter(center: head + Offset(hr * 0.38, -hr * 0.05), width: hr * 0.16, height: hr * 0.22),
        ink,
      );
    final blush = Paint()..color = colors.coral.withValues(alpha: 0.35);
    canvas
      ..drawOval(
        Rect.fromCenter(center: head + Offset(-hr * 0.58, hr * 0.3), width: hr * 0.3, height: hr * 0.18),
        blush,
      )
      ..drawOval(
        Rect.fromCenter(center: head + Offset(hr * 0.58, hr * 0.3), width: hr * 0.3, height: hr * 0.18),
        blush,
      );
    canvas.drawArc(
      Rect.fromCenter(center: head + Offset(0, hr * 0.28), width: hr * 0.6, height: hr * 0.4),
      math.pi * 0.15,
      math.pi * 0.7,
      false,
      Paint()
        ..color = const Color(0xFF14302F)
        ..style = PaintingStyle.stroke
        ..strokeWidth = hr * 0.09
        ..strokeCap = StrokeCap.round,
    );
  }

  /// Chanduva: a round silver pomfret with mango fins, a forked tail, a big eye and a smile. [w] is its length.
  void _pomfret(Canvas canvas, Offset c, double w, {required double mood}) {
    final h = w * 0.78;
    final body = Path()
      ..moveTo(c.dx - w * 0.42, c.dy)
      ..cubicTo(c.dx - w * 0.36, c.dy - h * 0.62, c.dx + w * 0.22, c.dy - h * 0.6, c.dx + w * 0.3, c.dy)
      ..cubicTo(c.dx + w * 0.22, c.dy + h * 0.6, c.dx - w * 0.36, c.dy + h * 0.62, c.dx - w * 0.42, c.dy)
      ..close();
    final tail = Path()
      ..moveTo(c.dx + w * 0.26, c.dy)
      ..lineTo(c.dx + w * 0.5, c.dy - h * 0.3)
      ..quadraticBezierTo(c.dx + w * 0.42, c.dy, c.dx + w * 0.5, c.dy + h * 0.3)
      ..close();
    final finTop = Path()
      ..moveTo(c.dx - w * 0.12, c.dy - h * 0.44)
      ..quadraticBezierTo(c.dx + w * 0.05, c.dy - h * 0.72, c.dx + w * 0.16, c.dy - h * 0.36)
      ..close();
    final finBottom = Path()
      ..moveTo(c.dx - w * 0.12, c.dy + h * 0.44)
      ..quadraticBezierTo(c.dx + w * 0.05, c.dy + h * 0.72, c.dx + w * 0.16, c.dy + h * 0.36)
      ..close();
    final bounds = Rect.fromCenter(center: c, width: w, height: h);
    final fin = _clay(bounds, _lighter(_finSilver), _darker(_finSilver));
    final outline = _outline(w * 0.9);
    for (final p in [tail, finTop, finBottom]) {
      canvas
        ..drawPath(p, fin)
        ..drawPath(p, outline);
    }
    canvas
      ..drawPath(body, _clay(bounds, _silver, _silverShade))
      ..drawPath(body, outline);
    // Gill line, eye, smile.
    final line = Paint()
      ..color = _silverShade
      ..style = PaintingStyle.stroke
      ..strokeWidth = w * 0.02
      ..strokeCap = StrokeCap.round;
    canvas.drawArc(
      Rect.fromCenter(center: c + Offset(-w * 0.12, 0), width: w * 0.2, height: h * 0.5),
      -math.pi / 2.6,
      math.pi / 1.3,
      false,
      line,
    );
    final eye = c + Offset(-w * 0.26, -h * 0.1);
    canvas
      ..drawCircle(eye, w * 0.075, Paint()..color = Colors.white)
      ..drawCircle(eye + Offset(-w * 0.012, w * 0.01), w * 0.042, Paint()..color = const Color(0xFF14302F))
      ..drawCircle(eye + Offset(-w * 0.025, -w * 0.015), w * 0.014, Paint()..color = Colors.white);
    // Rosy cheek and an open, happy smile.
    canvas.drawOval(
      Rect.fromCenter(center: c + Offset(-w * 0.12, h * 0.1), width: w * 0.12, height: w * 0.07),
      Paint()..color = colors.coral.withValues(alpha: 0.45),
    );
    final mouth = Path()
      ..moveTo(c.dx - w * 0.37, c.dy + h * 0.08)
      ..quadraticBezierTo(c.dx - w * 0.3, c.dy + h * 0.1, c.dx - w * 0.22, c.dy + h * 0.06)
      ..quadraticBezierTo(c.dx - w * 0.27, c.dy + h * 0.26 * mood, c.dx - w * 0.37, c.dy + h * 0.08)
      ..close();
    canvas
      ..drawPath(mouth, Paint()..color = const Color(0xFF5A1E1E))
      ..drawCircle(c + Offset(-w * 0.29, h * 0.15), w * 0.025, Paint()..color = const Color(0xFFF08A8A));
  }

  Paint _outline(double s) => Paint()
    ..color = const Color(0xFF14302F)
    ..style = PaintingStyle.stroke
    ..strokeWidth = math.max(1, s * 0.012)
    ..strokeJoin = StrokeJoin.round;

  Rect _r(Offset c, double r) => Rect.fromCircle(center: c, radius: r);

  @override
  bool shouldRepaint(CastPainter old) =>
      old.member != member || old.colors != colors || old.backdrop != backdrop || old.sidekick != sidekick;
}
