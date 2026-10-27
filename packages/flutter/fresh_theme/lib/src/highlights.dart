// The highlights of the rich screens (D156, D176, D181): the yellow brush behind one key word, three fanned
// dashes ("sparks"), a handwritten line with a swoosh under it, and the small snowflake. Traced from the
// approved previews (docs/design/mockups/app-onboarding-rich.html and app-home-rich.html). All of it is
// decoration: sparks, swooshes and snowflakes are excluded from semantics, and the brush keeps its child's
// semantics. Nothing moves.
//
// Colours and sizes are explicit. The package bundles no fonts: [ScriptLine] takes the family name the app
// registers (the app bundles Dancing Script).

import 'dart:math' as math;

import 'package:flutter/widgets.dart';

/// Colours of the highlights that stay the same in light and dark (they sit on colour blocks or on the brush).
abstract final class FreshHighlightColors {
  /// The brush behind one key word.
  static const Color brush = Color(0xFFF5D04A);

  /// Sparks next to a heading on paper.
  static const Color coral = Color(0xFFF07A5A);

  /// The handwritten line and its swoosh on paper.
  static const Color coralDeep = Color(0xFFC65A3F);

  /// Text on the brush.
  static const Color ink = Color(0xFF14302F);

  /// The paper colour used for drawn highlights (windows, wave lines).
  static const Color paper = Color(0xFFFFFDF8);
}

/// A painted brush swipe behind [child] (the previews' `.brush`): the swipe path of a 200 × 60 box stretched to
/// the child plus [padding], turned by [degrees].
class BrushHighlight extends StatelessWidget {
  const BrushHighlight({
    super.key,
    required this.child,
    this.color = FreshHighlightColors.brush,
    this.degrees = 0,
    this.padding = const EdgeInsets.fromLTRB(8, 1, 8, 3),
  });

  final Widget child;
  final Color color;

  /// Tilt in degrees (the previews use −1.5 to −2).
  final double degrees;
  final EdgeInsets padding;

  @override
  Widget build(BuildContext context) {
    final brush = CustomPaint(
      painter: BrushPainter(color),
      child: Padding(padding: padding, child: child),
    );
    if (degrees == 0) return brush;
    return Transform.rotate(angle: degrees * math.pi / 180, child: brush);
  }
}

/// The brush swipe filling its whole box.
class BrushPainter extends CustomPainter {
  const BrushPainter(this.color);

  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final sx = size.width / 200;
    final sy = size.height / 60;
    Offset p(double x, double y) => Offset(x * sx, y * sy);
    void c(Path path, Offset a, Offset b, Offset e) => path.cubicTo(a.dx, a.dy, b.dx, b.dy, e.dx, e.dy);
    final start = p(6, 14);
    final path = Path()..moveTo(start.dx, start.dy);
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
  bool shouldRepaint(BrushPainter old) => old.color != color;
}

/// Three short fanned dashes (the previews' spark), turned by [degrees]. Its box is 34 × 30 times [scale].
class Sparks extends StatelessWidget {
  const Sparks({super.key, required this.color, this.degrees = 0, this.scale = 1});

  final Color color;
  final double degrees;
  final double scale;

  /// The unscaled box.
  static const Size designSize = Size(34, 30);

  @override
  Widget build(BuildContext context) => ExcludeSemantics(
    child: Transform.rotate(
      angle: degrees * math.pi / 180,
      child: CustomPaint(size: designSize * scale, painter: SparksPainter(color)),
    ),
  );
}

/// The three dashes of [Sparks], scaled to the box width.
class SparksPainter extends CustomPainter {
  const SparksPainter(this.color);

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
  bool shouldRepaint(SparksPainter old) => old.color != color;
}

/// A small snowflake (three crossing strokes), 30 × 26 times [scale].
class Snowflake extends StatelessWidget {
  const Snowflake({super.key, required this.color, this.scale = 1});

  final Color color;
  final double scale;

  @override
  Widget build(BuildContext context) =>
      ExcludeSemantics(child: CustomPaint(size: const Size(30, 26) * scale, painter: SnowflakePainter(color)));
}

class SnowflakePainter extends CustomPainter {
  const SnowflakePainter(this.color);

  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final k = size.width / 30;
    final paint = Paint()
      ..color = color
      ..strokeWidth = 2 * k
      ..strokeCap = StrokeCap.round;
    canvas
      ..drawLine(const Offset(15, 3) * k, const Offset(15, 23) * k, paint)
      ..drawLine(const Offset(6, 8) * k, const Offset(24, 18) * k, paint)
      ..drawLine(const Offset(24, 8) * k, const Offset(6, 18) * k, paint);
  }

  @override
  bool shouldRepaint(SnowflakePainter old) => old.color != color;
}

/// A hand-drawn underline of [size]: a gentle rising curve across the box.
class SwooshUnderline extends StatelessWidget {
  const SwooshUnderline({super.key, required this.color, this.size = const Size(92, 12), this.strokeWidth = 2.6});

  final Color color;
  final Size size;
  final double strokeWidth;

  @override
  Widget build(BuildContext context) =>
      ExcludeSemantics(child: CustomPaint(size: size, painter: SwooshPainter(color, width: strokeWidth)));
}

/// The curve of [SwooshUnderline], filling its box.
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

/// A handwritten line (Dancing Script 700) tilted by [degrees], with a [SwooshUnderline] under it. The text is
/// read by screen readers; the swoosh is not.
class ScriptLine extends StatelessWidget {
  const ScriptLine({
    super.key,
    required this.text,
    required this.fontFamily,
    required this.color,
    required this.fontSize,
    this.height = 1.1,
    this.degrees = -3,
    this.swooshColor,
    this.swooshSize = const Size(92, 12),
    this.swooshWidth = 2.6,
    this.swooshIndent = 0,
    this.swooshGap = 1,
    this.rotationAlignment = Alignment.centerLeft,
    this.fontFamilyFallback,
  });

  final String text;

  /// The family name as the app registers it in its pubspec (for example `DancingScript`).
  final String fontFamily;
  final List<String>? fontFamilyFallback;
  final Color color;
  final double fontSize;
  final double height;

  /// Tilt in degrees, turned around [rotationAlignment].
  final double degrees;
  final Alignment rotationAlignment;

  /// Defaults to [color].
  final Color? swooshColor;

  /// A zero width leaves the swoosh out.
  final Size swooshSize;
  final double swooshWidth;

  /// Left offset of the swoosh under the text, and its distance below the text.
  final double swooshIndent;
  final double swooshGap;

  /// Dancing Script 700. A variable font file needs the weight on its `wght` axis as well.
  static TextStyle style({
    required String fontFamily,
    required Color color,
    required double fontSize,
    double height = 1.1,
    List<String>? fontFamilyFallback,
  }) => TextStyle(
    fontFamily: fontFamily,
    fontFamilyFallback: fontFamilyFallback,
    fontWeight: FontWeight.w700,
    fontVariations: const [FontVariation('wght', 700)],
    fontSize: fontSize,
    height: height,
    color: color,
  );

  @override
  Widget build(BuildContext context) {
    final line = Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          text,
          style: style(
            fontFamily: fontFamily,
            fontFamilyFallback: fontFamilyFallback,
            color: color,
            fontSize: fontSize,
            height: height,
          ),
        ),
        if (swooshSize.width > 0)
          Padding(
            padding: EdgeInsets.only(left: swooshIndent, top: swooshGap),
            child: SwooshUnderline(color: swooshColor ?? color, size: swooshSize, strokeWidth: swooshWidth),
          ),
      ],
    );
    if (degrees == 0) return line;
    return Transform.rotate(angle: degrees * math.pi / 180, alignment: rotationAlignment, child: line);
  }
}

/// A heading with one key word on the yellow brush and sparks after it, the way the home writes "What's cooking
/// *today?*" (D181): [text] in [style], then [brushed] on a [BrushHighlight] in [brushInk], then [trailing].
/// The words wrap onto a second line when the width is short (fit by layout, never by shrinking, D183, D195).
/// Screen readers hear one header with the words joined.
class HighlightTitle extends StatelessWidget {
  const HighlightTitle({
    super.key,
    required this.text,
    required this.brushed,
    required this.style,
    this.trailing,
    this.sparkColor = FreshHighlightColors.coral,
    this.sparks = true,
    this.brushColor = FreshHighlightColors.brush,
    this.brushInk = FreshHighlightColors.ink,
    this.header = true,
  });

  /// The words before the brush (may be empty).
  final String text;

  /// The key word on the brush.
  final String brushed;

  /// Words after the brush.
  final String? trailing;
  final TextStyle style;
  final Color sparkColor;
  final bool sparks;
  final Color brushColor;
  final Color brushInk;
  final bool header;

  /// The words as screen readers hear them.
  String get label => [text, brushed, ?trailing].where((w) => w.isNotEmpty).join(' ');

  @override
  Widget build(BuildContext context) {
    final size = style.fontSize ?? 20;
    // The home's sparks are 0.68 × at a 20 dp heading and 1 × at 32 dp.
    final spark = (size / 30).clamp(0.5, 1.0);
    final sparkWidth = Sparks.designSize.width * spark;
    final word = Stack(
      clipBehavior: Clip.none,
      children: [
        BrushHighlight(
          color: brushColor,
          degrees: -2,
          padding: EdgeInsets.symmetric(horizontal: size * 0.16),
          child: Text(brushed, style: style.copyWith(color: brushInk)),
        ),
        if (sparks)
          Positioned(
            right: -sparkWidth - 2,
            top: -Sparks.designSize.height * spark * 0.55,
            child: Sparks(color: sparkColor, scale: spark),
          ),
      ],
    );
    return Semantics(
      header: header,
      label: label,
      excludeSemantics: true,
      child: Padding(
        // Room for the sparks above and after the brushed word.
        padding: EdgeInsets.only(top: sparks ? size * 0.3 : 0, right: sparks ? sparkWidth + 2 : 0),
        child: Wrap(
          crossAxisAlignment: WrapCrossAlignment.center,
          spacing: size * 0.22,
          children: [
            if (text.isNotEmpty) Text(text, style: style),
            word,
            if (trailing != null && trailing!.isNotEmpty) Text(trailing!, style: style),
          ],
        ),
      ),
    );
  }
}
