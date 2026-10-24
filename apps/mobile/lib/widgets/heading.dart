// Big headings of the Bold blocks look (D89 amended): heavy Baloo 2 lines paired with a handwritten Dancing
// Script line (tilted −3°, 1.12×), sometimes on a mango brush swipe, and a small dash doodle at the heading's
// top corner (the mockup's `.block .big`, `.scr`, `.brush` and `.big::after`). Inline titles use
// [ScriptTitle] ("Your *cart*").

import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:fresh_theme/fresh_theme.dart';

import '../theme/app_theme.dart';

/// One line of a [Heading].
@immutable
class HeadingLine {
  const HeadingLine(this.text, {this.script = false, this.brush = false});

  final String text;

  /// Handwritten Dancing Script, tilted −3°, 1.12× the heading size.
  final bool script;

  /// On a painted brush swipe (ink text).
  final bool brush;
}

/// Script style: Dancing Script 700. The bundled file is the variable font, so the weight is set on its `wght`
/// axis as well as in [TextStyle.fontWeight].
TextStyle scriptStyle(Color color, double size) => TextStyle(
  fontFamily: AppFonts.script,
  fontFamilyFallback: FreshFonts.sansFallback,
  fontWeight: FontWeight.w700,
  fontVariations: const [FontVariation('wght', 700)],
  fontSize: size * 1.12,
  height: 1.05,
  color: color,
);

/// A big one- or two-line heading. Screen readers hear one header with the lines joined.
class Heading extends StatelessWidget {
  const Heading({
    super.key,
    required this.lines,
    required this.color,
    this.size = 35,
    this.doodle = true,
    this.brushColor,
    this.scriptColor,
  });

  final List<HeadingLine> lines;
  final Color color;
  final double size;

  /// The three-dash doodle at the top right corner.
  final bool doodle;

  /// Brush paint; mango by default (paper on a mango block).
  final Color? brushColor;

  /// Script colour when not on a brush (defaults to [color]).
  final Color? scriptColor;

  String get label => lines.map((l) => l.text).join(' ');

  @override
  Widget build(BuildContext context) {
    final brush = brushColor ?? const Color(0xFFF5B544);
    final base = AppText.display(color, size, height: 0.98);
    Widget line(HeadingLine l) {
      final textColor = l.brush ? BlockInk.ink : (l.script ? (scriptColor ?? color) : color);
      Widget text = Text(l.text, style: l.script ? scriptStyle(textColor, size) : base.copyWith(color: textColor));
      if (l.brush) text = BrushSwipe(color: brush, child: text);
      if (l.script) text = Transform.rotate(angle: -3 * math.pi / 180, alignment: Alignment.centerLeft, child: text);
      return Padding(
        padding: EdgeInsets.only(top: l.script ? 2 : 0),
        child: text,
      );
    }

    return Semantics(
      header: true,
      label: label,
      excludeSemantics: true,
      child: Stack(
        clipBehavior: Clip.none,
        children: [
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: lines.map(line).toList(),
          ),
          if (doodle)
            Positioned(
              right: 0,
              top: -20,
              child: CustomPaint(size: const Size(30, 22), painter: _DoodlePainter(color.withValues(alpha: 0.8))),
            ),
        ],
      ),
    );
  }
}

/// A title with one handwritten word after it, on one line: "Your *cart*", "My *account*".
class ScriptTitle extends StatelessWidget {
  const ScriptTitle({
    super.key,
    required this.text,
    required this.script,
    this.size = 20,
    this.color,
    this.scriptColor,
  });

  final String text;
  final String script;
  final double size;
  final Color? color;
  final Color? scriptColor;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    return Semantics(
      header: true,
      label: '$text $script',
      excludeSemantics: true,
      child: Row(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          Flexible(
            child: Text('$text ', style: AppText.display(color ?? c.ink, size), overflow: TextOverflow.ellipsis),
          ),
          Transform.rotate(
            angle: -3 * math.pi / 180,
            child: Text(script, style: scriptStyle(scriptColor ?? c.tealText, size)),
          ),
        ],
      ),
    );
  }
}

/// A painted mango swipe behind its child (the mockup's `.brush`).
class BrushSwipe extends StatelessWidget {
  const BrushSwipe({super.key, required this.child, this.color = const Color(0xFFF5B544)});

  final Widget child;
  final Color color;

  @override
  Widget build(BuildContext context) => CustomPaint(
    painter: _BrushPainter(color),
    child: Padding(padding: const EdgeInsets.fromLTRB(8, 1, 8, 3), child: child),
  );
}

class _BrushPainter extends CustomPainter {
  const _BrushPainter(this.color);

  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    // The mockup's swipe path in a 200 × 60 box, stretched to the text.
    final sx = size.width / 200;
    final sy = size.height / 60;
    Offset p(double x, double y) => Offset(x * sx, y * sy);
    void c(Path path, Offset a, Offset b, Offset e) => path.cubicTo(a.dx, a.dy, b.dx, b.dy, e.dx, e.dy);
    final path = Path()..moveTo(p(6, 14).dx, p(6, 14).dy);
    c(path, p(40, 6), p(110, 4), p(192, 9));
    c(path, p(196, 10), p(198, 15), p(195, 19));
    c(path, p(193, 22), p(198, 26), p(196, 31));
    c(path, p(193, 39), p(198, 43), p(194, 48));
    c(path, p(164, 54), p(74, 56), p(8, 51));
    c(path, p(2, 50), p(0, 43), p(4, 39));
    c(path, p(7, 36), p(2, 31), p(4, 26));
    c(path, p(6, 21), p(-2, 18), p(2, 14));
    path.close();
    canvas.drawPath(path, Paint()..color = color);
  }

  @override
  bool shouldRepaint(_BrushPainter old) => old.color != color;
}

class _DoodlePainter extends CustomPainter {
  const _DoodlePainter(this.color);

  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final sx = size.width / 40;
    final sy = size.height / 30;
    final paint = Paint()
      ..color = color
      ..strokeWidth = 3 * sx
      ..strokeCap = StrokeCap.round;
    for (final (a, b) in const [
      ((6.0, 22.0), (12.0, 10.0)),
      ((17.0, 20.0), (20.0, 6.0)),
      ((27.0, 21.0), (33.0, 12.0)),
    ]) {
      canvas.drawLine(Offset(a.$1 * sx, a.$2 * sy), Offset(b.$1 * sx, b.$2 * sy), paint);
    }
  }

  @override
  bool shouldRepaint(_DoodlePainter old) => old.color != color;
}
