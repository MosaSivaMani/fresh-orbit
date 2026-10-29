// Inline links inside a sentence (D299): "By continuing you agree to our *terms* and *privacy policy*." The links
// are real text spans, so they wrap with the sentence, follow the text size and are read as links by screen
// readers. The app decides what a link opens; this part only draws the sentence and reports the tap.

import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';

import 'look.dart';

/// One piece of a [FreshLinkText]: plain words, or a link with its own tap.
@immutable
class FreshTextPart {
  /// Plain words.
  const FreshTextPart(this.text) : onTap = null;

  /// Words drawn as a link (teal, underlined); [onTap] runs when they are tapped.
  const FreshTextPart.link(this.text, {required VoidCallback this.onTap});

  final String text;
  final VoidCallback? onTap;

  bool get isLink => onTap != null;
}

/// A sentence with tappable words in it. Links take the theme's teal text colour (or [linkColor]), a semibold
/// weight and an underline, so they read as links without colour alone.
class FreshLinkText extends StatefulWidget {
  const FreshLinkText({super.key, required this.parts, this.style, this.linkColor, this.textAlign = TextAlign.start});

  final List<FreshTextPart> parts;

  /// The sentence's style; the surrounding [DefaultTextStyle] when null.
  final TextStyle? style;
  final Color? linkColor;
  final TextAlign textAlign;

  @override
  State<FreshLinkText> createState() => _FreshLinkTextState();
}

class _FreshLinkTextState extends State<FreshLinkText> {
  List<TapGestureRecognizer?> _recognizers = const [];

  @override
  void initState() {
    super.initState();
    _makeRecognizers();
  }

  @override
  void didUpdateWidget(FreshLinkText oldWidget) {
    super.didUpdateWidget(oldWidget);
    _disposeRecognizers();
    _makeRecognizers();
  }

  @override
  void dispose() {
    _disposeRecognizers();
    super.dispose();
  }

  void _makeRecognizers() {
    _recognizers = [
      for (final part in widget.parts) part.isLink ? (TapGestureRecognizer()..onTap = part.onTap) : null,
    ];
  }

  void _disposeRecognizers() {
    for (final r in _recognizers) {
      r?.dispose();
    }
  }

  @override
  Widget build(BuildContext context) {
    final base = DefaultTextStyle.of(context).style.merge(widget.style);
    final color = widget.linkColor ?? illustratedColors(context).tealText;
    final linkStyle = TextStyle(
      color: color,
      fontWeight: FontWeight.w600,
      decoration: TextDecoration.underline,
      decorationColor: color,
    );
    return Text.rich(
      TextSpan(
        children: [
          for (var i = 0; i < widget.parts.length; i++)
            widget.parts[i].isLink
                ? TextSpan(text: widget.parts[i].text, style: linkStyle, recognizer: _recognizers[i])
                : TextSpan(text: widget.parts[i].text),
        ],
      ),
      style: base,
      textAlign: widget.textAlign,
    );
  }
}
