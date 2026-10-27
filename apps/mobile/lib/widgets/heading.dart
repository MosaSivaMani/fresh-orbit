// Headings in the home theme (D181): heavy Baloo 2 lines, one key word on the yellow brush with coral sparks
// after it ([PageTitle], the home's "What's cooking *today?*"), and on the colour-block screens a big heading whose
// handwritten Dancing Script line (tilted −3°, 1.12×) can sit on the brush, with sparks at its top corner
// ([Heading]). The brush and the sparks are the theme package's ([BrushHighlight], [Sparks]).

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

  /// The sparks at the top right corner.
  final bool doodle;

  /// Brush paint; the home's yellow by default.
  final Color? brushColor;

  /// Script colour when not on a brush (defaults to [color]).
  final Color? scriptColor;

  String get label => lines.map((l) => l.text).join(' ');

  @override
  Widget build(BuildContext context) {
    final brush = brushColor ?? FreshHighlightColors.brush;
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
              top: -22,
              child: Sparks(color: color.withValues(alpha: 0.85), scale: 0.85),
            ),
        ],
      ),
    );
  }
}

/// A screen title in the home's style: [text] in Baloo 2 800 and the key word [brushed] on the yellow brush with
/// coral sparks: "Shop *fresh*", "Your *cart*". One header for screen readers.
class PageTitle extends StatelessWidget {
  const PageTitle({super.key, required this.text, required this.brushed, this.trailing, this.size = 24, this.color});

  final String text;
  final String brushed;
  final String? trailing;
  final double size;

  /// Defaults to ink.
  final Color? color;

  @override
  Widget build(BuildContext context) => HighlightTitle(
    text: text,
    brushed: brushed,
    trailing: trailing,
    style: AppText.display(color ?? illustratedColors(context).ink, size, height: 1.1),
  );
}

/// A painted swipe behind its child (the mockup's `.brush`): the theme package's [BrushHighlight], upright, in the
/// home's yellow by default.
class BrushSwipe extends StatelessWidget {
  const BrushSwipe({super.key, required this.child, this.color = FreshHighlightColors.brush});

  final Widget child;
  final Color color;

  @override
  Widget build(BuildContext context) => BrushHighlight(color: color, child: child);
}
