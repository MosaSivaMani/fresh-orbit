// Parts of the home's scrolling sections below the first screen (D292, D293): a section heading with the key word
// on the brush and an optional "See all →", a step or promise tile (a picture popping out of a pastel blob, an
// optional step number, a bold label on one line and a two-line caption, like the onboarding tiles) and the help
// card. Screens use these and never copy them; every value shown is given by the app (rule 4).

import 'package:flutter/material.dart';

import '../fresh_theme.dart';

/// Teal for links and icons on paper and on the light blocks (the home's "See all →").
const Color _teal = Color(0xFF0B7D70);

TextStyle _sans(Color color, double size, FontWeight weight, {double? height}) => TextStyle(
  fontFamily: FreshFonts.sans,
  fontFamilyFallback: FreshFonts.sansFallback,
  fontWeight: weight,
  fontSize: size,
  height: height,
  color: color,
);

/// A section heading in the home's style: [text], then [brushed] on the yellow brush with coral sparks, and an
/// optional [action] ("See all →") on the right. One header for screen readers; the action is its own button.
class FreshSectionHeader extends StatelessWidget {
  const FreshSectionHeader({
    super.key,
    required this.text,
    required this.brushed,
    required this.color,
    this.action,
    this.onAction,
    this.actionColor = _teal,
    this.actionKey,
  });

  final String text;
  final String brushed;
  final Color color;

  /// The action's words; shown only with [onAction].
  final String? action;
  final VoidCallback? onAction;
  final Color actionColor;
  final Key? actionKey;

  @override
  Widget build(BuildContext context) {
    final words = action;
    return Row(
      crossAxisAlignment: CrossAxisAlignment.end,
      children: [
        Expanded(
          child: HighlightTitle(text: text, brushed: brushed, style: AppText.display(color, 20, height: 1)),
        ),
        if (words != null && onAction != null)
          Semantics(
            button: true,
            label: words.replaceAll('→', '').trim(),
            excludeSemantics: true,
            child: InkWell(
              key: actionKey,
              borderRadius: BorderRadius.circular(8),
              onTap: onAction,
              child: ConstrainedBox(
                constraints: const BoxConstraints(minHeight: 32),
                child: Padding(
                  padding: const EdgeInsets.symmetric(vertical: 4),
                  child: Text(words, style: _sans(actionColor, 13, FontWeight.w600)),
                ),
              ),
            ),
          ),
      ],
    );
  }
}

/// A step or promise tile: [picture] popping out of a [tint] blob (the onboarding tiles' shape), an optional
/// [step] number on a coral badge at the blob's corner, [label] in bold on one line (fitted, never cut) and
/// [caption] on two lines at most. Screen readers hear "Step n, label, caption".
class FreshStepTile extends StatelessWidget {
  const FreshStepTile({
    super.key,
    required this.picture,
    required this.label,
    required this.caption,
    required this.tint,
    required this.ink,
    required this.muted,
    this.step,
  });

  /// About [pictureSize] wide and [pictureHeight] tall; the app's illustration with its own fallback.
  final Widget picture;
  final String label;
  final String caption;
  final Color tint;
  final Color ink;
  final Color muted;
  final int? step;

  static const double pictureSize = 88;
  static const double pictureHeight = 76;
  static const double _blobWidth = 92;
  static const double _blobHeight = 60;
  static const double _popUp = 14;

  @override
  Widget build(BuildContext context) {
    final number = step;
    return Semantics(
      container: true,
      label: [if (number != null) 'Step $number', label, caption].join(', '),
      excludeSemantics: true,
      child: Column(
        children: [
          SizedBox(
            height: _blobHeight + _popUp,
            width: pictureSize,
            child: Stack(
              clipBehavior: Clip.none,
              alignment: Alignment.topCenter,
              children: [
                Positioned(
                  top: _popUp,
                  child: Container(
                    width: _blobWidth,
                    height: _blobHeight,
                    decoration: BoxDecoration(
                      color: tint,
                      borderRadius: const BorderRadius.only(
                        topLeft: Radius.elliptical(42, 33),
                        topRight: Radius.elliptical(50, 30),
                        bottomRight: Radius.elliptical(46, 30),
                        bottomLeft: Radius.elliptical(46, 27),
                      ),
                    ),
                  ),
                ),
                Positioned(top: 0, child: SizedBox(width: pictureSize, height: pictureHeight, child: picture)),
                if (number != null)
                  Positioned(
                    top: _popUp - 8,
                    left: -2,
                    child: Container(
                      width: 24,
                      height: 24,
                      alignment: Alignment.center,
                      decoration: const BoxDecoration(color: FreshHighlightColors.coral, shape: BoxShape.circle),
                      child: Text('$number', style: _sans(FreshHighlightColors.paper, 12.5, FontWeight.w800)),
                    ),
                  ),
              ],
            ),
          ),
          const SizedBox(height: 8),
          FreshOneLine(label, minFit: 0.7, style: _sans(ink, 13.5, FontWeight.w800, height: 1.2)),
          const SizedBox(height: 2),
          Text(
            caption,
            textAlign: TextAlign.center,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: _sans(muted, 11.5, FontWeight.w500, height: 1.3),
          ),
        ],
      ),
    );
  }
}

/// The help card: a round [icon] badge, a bold [title] with a muted [body] under it and a paper [action] pill,
/// on [color]. The whole card is one button.
class FreshHelpCard extends StatelessWidget {
  const FreshHelpCard({
    super.key,
    required this.title,
    required this.body,
    required this.action,
    required this.color,
    required this.onTap,
    this.icon = Icons.support_agent_rounded,
  });

  final String title;
  final String body;
  final String action;
  final Color color;
  final VoidCallback onTap;
  final IconData icon;

  @override
  Widget build(BuildContext context) => Semantics(
    button: true,
    label: '$title $body ${action.replaceAll('→', '').trim()}',
    excludeSemantics: true,
    child: FreshCard(
      color: color,
      padding: const EdgeInsets.all(14),
      onTap: onTap,
      child: Row(
        children: [
          Container(
            width: 52,
            height: 52,
            decoration: const BoxDecoration(color: FreshHighlightColors.paper, shape: BoxShape.circle),
            child: Icon(icon, size: 28, color: _teal),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(title, style: AppText.display(FreshHighlightColors.ink, 17, height: 1.1)),
                const SizedBox(height: 2),
                Text(body, style: _sans(FreshHighlightColors.ink, 12.5, FontWeight.w500, height: 1.3)),
                const SizedBox(height: 8),
                FreshPill(text: action, color: FreshHighlightColors.ink, size: 12.5),
              ],
            ),
          ),
        ],
      ),
    ),
  );
}
