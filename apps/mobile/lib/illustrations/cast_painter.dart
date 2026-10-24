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

  static const _skin = Color(0xFFE9B78E);
  static const _skinShade = Color(0xFFC08158);
  static const _hair = Color(0xFF2F2622);
  static const _silver = Color(0xFFE7EEF0);
  static const _silverShade = Color(0xFF97ADB5);

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

    final shirt = switch (member) {
      CastMember.padma => colors.mango,
      CastMember.ravi => colors.tealDeep,
      CastMember.anil => colors.mango,
      CastMember.ammamma => colors.coral,
      CastMember.chanduva => colors.mango,
    };

    // Neck and body.
    final neck = Rect.fromCenter(center: Offset(s * 0.5, s * 0.62), width: s * 0.1, height: s * 0.1);
    canvas.drawRRect(RRect.fromRectAndRadius(neck, Radius.circular(s * 0.03)), Paint()..color = _skinShade);
    canvas.drawOval(body, _clay(body, _lighter(shirt), _darker(shirt)));

    switch (member) {
      case CastMember.padma:
        // Teal apron: a bib and a skirt over the shirt.
        final apron = RRect.fromRectAndRadius(
          Rect.fromCenter(center: Offset(s * 0.5, s * 0.9), width: s * 0.3, height: s * 0.36),
          Radius.circular(s * 0.06),
        );
        canvas.drawRRect(apron, _clay(apron.outerRect, _lighter(colors.tealText), colors.tealDeep));
        final strap = Paint()
          ..color = colors.tealDeep
          ..strokeWidth = s * 0.025
          ..strokeCap = StrokeCap.round;
        canvas
          ..drawLine(Offset(s * 0.38, s * 0.74), Offset(s * 0.43, s * 0.67), strap)
          ..drawLine(Offset(s * 0.62, s * 0.74), Offset(s * 0.57, s * 0.67), strap);
      case CastMember.ravi:
        // Ice-blue cold box held in front.
        final box = RRect.fromRectAndRadius(
          Rect.fromCenter(center: Offset(s * 0.66, s * 0.84), width: s * 0.3, height: s * 0.22),
          Radius.circular(s * 0.04),
        );
        final ice = Color.lerp(colors.ice, const Color(0xFF9FD3E3), 0.5)!;
        canvas.drawRRect(box, _clay(box.outerRect, _lighter(ice), _darker(ice)));
        canvas.drawRRect(box, _outline(s));
        canvas.drawLine(
          Offset(box.left + s * 0.03, box.top + s * 0.06),
          Offset(box.right - s * 0.03, box.top + s * 0.06),
          _outline(s),
        );
      case CastMember.ammamma:
        // Saree border.
        final border = Paint()
          ..color = colors.mango
          ..style = PaintingStyle.stroke
          ..strokeWidth = s * 0.035;
        canvas.drawArc(body.deflate(s * 0.05), math.pi * 1.1, math.pi * 0.55, false, border);
      case CastMember.anil:
      case CastMember.chanduva:
        break;
    }

    // Hair behind the head.
    switch (member) {
      case CastMember.ammamma:
        canvas.drawCircle(
          head + Offset(0, -hr * 0.95),
          hr * 0.45,
          _clay(_r(head, hr), const Color(0xFFE6E2DC), const Color(0xFFA9A39B)),
        );
        canvas.drawCircle(
          head + Offset(0, -hr * 0.1),
          hr * 1.06,
          _clay(_r(head, hr), const Color(0xFFE6E2DC), const Color(0xFFA9A39B)),
        );
      case CastMember.anil:
        canvas.drawCircle(head + Offset(0, -hr * 0.12), hr * 1.05, _clay(_r(head, hr), _lighter(_hair), _hair));
      case CastMember.padma:
      case CastMember.ravi:
      case CastMember.chanduva:
        break;
    }

    // Head and ears.
    final skin = _clay(_r(head, hr), _lighter(_skin), _skinShade);
    canvas
      ..drawCircle(head + Offset(-hr, hr * 0.1), hr * 0.22, Paint()..color = _skinShade)
      ..drawCircle(head + Offset(hr, hr * 0.1), hr * 0.22, Paint()..color = _skinShade)
      ..drawCircle(head, hr, skin);

    // Headwear.
    switch (member) {
      case CastMember.padma:
        // Coral headscarf over the top of the head with a knot at the side.
        final scarf = Path()
          ..addArc(Rect.fromCircle(center: head, radius: hr * 1.08), math.pi * 1.1, math.pi * 0.8)
          ..close();
        canvas.drawPath(scarf, _clay(_r(head, hr), _lighter(colors.coral), _darker(colors.coral)));
        canvas.drawCircle(head + Offset(hr * 0.98, -hr * 0.3), hr * 0.24, Paint()..color = _darker(colors.coral));
      case CastMember.ravi:
        // Teal helmet with a dark visor band.
        final helmet = Path()
          ..addArc(Rect.fromCircle(center: head + Offset(0, -hr * 0.05), radius: hr * 1.15), math.pi * 1.06, math.pi * 0.88)
          ..close();
        canvas.drawPath(helmet, _clay(_r(head, hr), _lighter(colors.tealText), colors.tealDeep));
        canvas.drawRRect(
          RRect.fromRectAndRadius(
            Rect.fromCenter(center: head + Offset(0, -hr * 0.3), width: hr * 2.2, height: hr * 0.22),
            Radius.circular(hr * 0.1),
          ),
          Paint()..color = colors.tealDeep,
        );
      case CastMember.anil:
        // Headset: band over the head, ear pads and a microphone.
        final band = Paint()
          ..color = const Color(0xFF3A3F44)
          ..style = PaintingStyle.stroke
          ..strokeWidth = hr * 0.16
          ..strokeCap = StrokeCap.round;
        canvas.drawArc(Rect.fromCircle(center: head, radius: hr * 1.12), math.pi * 1.05, math.pi * 0.9, false, band);
        final pad = Paint()..color = colors.tealText;
        canvas
          ..drawCircle(head + Offset(-hr * 1.08, hr * 0.1), hr * 0.3, pad)
          ..drawCircle(head + Offset(hr * 1.08, hr * 0.1), hr * 0.3, pad);
        final mic = Path()
          ..moveTo(head.dx + hr * 1.05, head.dy + hr * 0.3)
          ..quadraticBezierTo(head.dx + hr * 0.9, head.dy + hr * 0.95, head.dx + hr * 0.25, head.dy + hr * 0.75);
        canvas
          ..drawPath(mic, band..strokeWidth = hr * 0.1)
          ..drawCircle(head + Offset(hr * 0.22, hr * 0.75), hr * 0.12, Paint()..color = const Color(0xFF3A3F44));
      case CastMember.ammamma:
        // Bindi and round glasses.
        canvas.drawCircle(head + Offset(0, -hr * 0.45), hr * 0.08, Paint()..color = colors.coralText);
        final glasses = Paint()
          ..color = const Color(0xFF5A4A40)
          ..style = PaintingStyle.stroke
          ..strokeWidth = hr * 0.07;
        canvas
          ..drawCircle(head + Offset(-hr * 0.38, -hr * 0.05), hr * 0.24, glasses)
          ..drawCircle(head + Offset(hr * 0.38, -hr * 0.05), hr * 0.24, glasses);
      case CastMember.chanduva:
        break;
    }

    _face(canvas, head, hr);
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
    final fin = _clay(bounds, _lighter(colors.mango), _darker(colors.mango));
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
    canvas.drawArc(
      Rect.fromCenter(center: c + Offset(-w * 0.3, h * 0.12), width: w * 0.12, height: h * 0.1 * mood),
      0.2,
      math.pi * 0.8,
      false,
      Paint()
        ..color = const Color(0xFF14302F)
        ..style = PaintingStyle.stroke
        ..strokeWidth = w * 0.022
        ..strokeCap = StrokeCap.round,
    );
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
