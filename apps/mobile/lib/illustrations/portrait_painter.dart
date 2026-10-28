// Ammamma, the home cook, drawn chest up for round avatars (D281): silver hair in a bun, a red bindi, a warm
// smile, a mango-yellow saree with a coral sash. Our own stand-in for the LOGIN_OTP picture until the owner's
// final PNG is bundled (prompt in docs/design/prompts-app-signin-2026-10-29.md). It fills its square; the
// caller clips it to a circle. Static.

import 'package:flutter/widgets.dart';

class AmmammaPortrait extends StatelessWidget {
  const AmmammaPortrait({super.key, required this.size});

  final double size;

  @override
  Widget build(BuildContext context) =>
      ExcludeSemantics(child: CustomPaint(size: Size.square(size), painter: const AmmammaPortraitPainter()));
}

/// Draws [AmmammaPortrait] in a 100 × 100 design box scaled to the canvas.
class AmmammaPortraitPainter extends CustomPainter {
  const AmmammaPortraitPainter();

  static const Color skin = Color(0xFFC98A5E);
  static const Color skinShade = Color(0xFFB0744B);
  static const Color hair = Color(0xFFF1F0EC);
  static const Color hairShade = Color(0xFFCFCBC4);
  static const Color saree = Color(0xFFF5C451);
  static const Color sareeShade = Color(0xFFE6AE36);
  static const Color sash = Color(0xFFE8674A);
  static const Color ink = Color(0xFF14302F);
  static const Color bindi = Color(0xFFC62828);
  static const Color cheek = Color(0x55EE7A5C);

  @override
  void paint(Canvas canvas, Size size) {
    final k = size.width / 100;
    canvas
      ..save()
      ..scale(k);

    // Shoulders in the saree, the coral sash across them.
    canvas
      ..drawOval(const Rect.fromLTWH(12, 72, 76, 56), Paint()..color = saree)
      ..drawPath(
        Path()
          ..moveTo(18, 88)
          ..quadraticBezierTo(30, 80, 40, 78)
          ..lineTo(40, 84)
          ..quadraticBezierTo(30, 88, 22, 96)
          ..close(),
        Paint()..color = sareeShade,
      )
      ..drawPath(
        Path()
          ..moveTo(30, 78)
          ..lineTo(38, 75)
          ..lineTo(76, 104)
          ..lineTo(64, 104)
          ..close(),
        Paint()..color = sash,
      );

    // Neck and ears.
    final skinPaint = Paint()..color = skin;
    canvas
      ..drawRRect(RRect.fromLTRBR(43, 58, 57, 78, const Radius.circular(5)), Paint()..color = skinShade)
      ..drawCircle(const Offset(32, 48), 4.5, skinPaint)
      ..drawCircle(const Offset(68, 48), 4.5, skinPaint);

    // Hair behind the face, the bun on top.
    final hairPaint = Paint()..color = hair;
    canvas
      ..drawOval(const Rect.fromLTWH(30, 24, 40, 32), hairPaint)
      ..drawCircle(const Offset(50, 21), 9, hairPaint)
      ..drawArc(
        const Rect.fromLTWH(42, 13, 16, 16),
        3.6,
        2.0,
        false,
        Paint()
          ..color = hairShade
          ..style = PaintingStyle.stroke
          ..strokeWidth = 1.2,
      );

    // The face, with the hair parting over the forehead.
    canvas
      ..drawOval(const Rect.fromLTWH(33, 31, 34, 36), skinPaint)
      ..drawPath(
        Path()
          ..moveTo(33, 44)
          ..quadraticBezierTo(36, 30, 50, 30)
          ..quadraticBezierTo(64, 30, 67, 44)
          ..quadraticBezierTo(60, 36, 50, 36)
          ..quadraticBezierTo(40, 36, 33, 44)
          ..close(),
        hairPaint,
      );

    // Bindi, closed smiling eyes, cheeks and the smile.
    final line = Paint()
      ..color = ink
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.6
      ..strokeCap = StrokeCap.round;
    canvas
      ..drawCircle(const Offset(50, 41), 1.9, Paint()..color = bindi)
      ..drawCircle(const Offset(43, 48), 1.6, Paint()..color = ink)
      ..drawCircle(const Offset(57, 48), 1.6, Paint()..color = ink)
      ..drawCircle(const Offset(40, 54), 3.2, Paint()..color = cheek)
      ..drawCircle(const Offset(60, 54), 3.2, Paint()..color = cheek)
      ..drawPath(
        Path()
          ..moveTo(44, 55)
          ..quadraticBezierTo(50, 61, 56, 55),
        line,
      )
      ..restore();
  }

  @override
  bool shouldRepaint(AmmammaPortraitPainter old) => false;
}
