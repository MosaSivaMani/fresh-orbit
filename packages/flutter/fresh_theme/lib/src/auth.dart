// The sign-in look (D281, owner's reference docs/design/mockups/app-login-reference-2026-10-29.jpg): a coral
// header with a round back button, a spaced caps eyebrow, a handwritten line with mango sparks and a key word on
// a mango brush, a drawn coastal scene at the right (birds, clouds, a pale sun, palms, two fishing boats, the
// sea); a cream card with large rounded top corners rising over it; a ringed avatar on the card's top edge with
// a teal chat badge; a phone field with the India flag and +91; a dark pill button with an arrow.
// The parts lay out what they are given; nothing here computes a business value. Nothing moves.

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:fresh_theme/fresh_theme.dart';

/// Colours of the sign-in look that stay the same in light and dark (they sit on the coral header).
abstract final class FreshAuthColors {
  /// The header block.
  static const Color coral = Color(0xFFE5806A);

  /// The brush behind the key word and the sparks after the handwritten line.
  static const Color mango = Color(0xFFF5B544);

  /// The avatar's backdrop inside the ring.
  static const Color avatarBackdrop = Color(0xFFE3F2EF);

  /// The chat badge and the first ring dot.
  static const Color teal = Color(0xFF2E7F78);

  /// The second ring dot.
  static const Color dotCoral = Color(0xFFEE7A5C);
}

/// The whole sign-in page: [FreshAuthHeader] at the top and [children] on the cream card under it. It scrolls,
/// so the keyboard never covers the field. Use it as a Scaffold body (the Scaffold's background should be the
/// paper colour, so the card runs to the bottom of the screen).
class FreshAuthPage extends StatelessWidget {
  const FreshAuthPage({
    super.key,
    required this.eyebrow,
    required this.scriptLine,
    required this.brushLine,
    required this.avatar,
    this.onBack,
    this.backTooltip = 'Back',
    this.children = const [],
  });

  final String eyebrow;
  final String scriptLine;
  final String brushLine;

  /// The picture inside the avatar ring (sized by [FreshAuthHeader.avatarSizeFor]).
  final Widget avatar;
  final VoidCallback? onBack;
  final String backTooltip;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    final bottomInset = MediaQuery.paddingOf(context).bottom;
    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.dark,
      child: SingleChildScrollView(
        keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            FreshAuthHeader(
              eyebrow: eyebrow,
              scriptLine: scriptLine,
              brushLine: brushLine,
              avatar: avatar,
              onBack: onBack,
              backTooltip: backTooltip,
            ),
            Padding(
              padding: EdgeInsets.fromLTRB(FreshAuthHeader.gutter, 14, FreshAuthHeader.gutter, 24 + bottomInset),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  for (var i = 0; i < children.length; i++) ...[
                    if (i > 0) const SizedBox(height: 14),
                    children[i],
                  ],
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// The coral header of [FreshAuthPage]: back button (left out when [onBack] is null), eyebrow, the two-line
/// title, the coastal scene at the right and the top of the cream card with the avatar on its edge.
class FreshAuthHeader extends StatelessWidget {
  const FreshAuthHeader({
    super.key,
    required this.eyebrow,
    required this.scriptLine,
    required this.brushLine,
    required this.avatar,
    this.onBack,
    this.backTooltip = 'Back',
  });

  final String eyebrow;
  final String scriptLine;
  final String brushLine;
  final Widget avatar;
  final VoidCallback? onBack;
  final String backTooltip;

  /// Side margin of the header text and the card content.
  static const double gutter = 24;

  /// The card's top corners.
  static const double cardRadius = 34;

  /// The avatar diameter for a screen: about 38 % of the width, smaller on short screens (112–156).
  static double avatarSizeFor(Size screen) {
    final byWidth = screen.width * 0.38;
    final byHeight = screen.height * 0.22;
    return (byWidth < byHeight ? byWidth : byHeight).clamp(112.0, 156.0);
  }

  @override
  Widget build(BuildContext context) {
    final screen = MediaQuery.sizeOf(context);
    final top = MediaQuery.paddingOf(context).top;
    final avatarSize = avatarSizeFor(screen);
    final cap = avatarSize / 2;
    final sceneWidth = (screen.width * 0.42).clamp(140.0, 240.0);
    final sceneHeight = sceneWidth * FreshCoastScenePainter.designSize.height / FreshCoastScenePainter.designSize.width;
    // The scene's sea runs under the card's rounded corner.
    final sceneBelowCardTop = sceneWidth * FreshCoastScenePainter.underCard / FreshCoastScenePainter.designSize.width;

    return ColoredBox(
      color: FreshAuthColors.coral,
      child: Stack(
        clipBehavior: Clip.hardEdge,
        children: [
          Positioned(
            right: 0,
            bottom: cap - sceneBelowCardTop,
            width: sceneWidth,
            height: sceneHeight,
            child: const FreshCoastScene(),
          ),
          Positioned(
            left: 0,
            right: 0,
            bottom: 0,
            height: cap,
            child: DecoratedBox(
              decoration: BoxDecoration(
                color: illustratedColors(context).paper,
                borderRadius: const BorderRadius.vertical(top: Radius.circular(cardRadius)),
              ),
            ),
          ),
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              SizedBox(height: top + 8),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: gutter - 6),
                child: onBack == null
                    ? const SizedBox(height: 8)
                    : RoundIconButton(icon: Icons.arrow_back_rounded, tooltip: backTooltip, onPressed: onBack),
              ),
              Padding(
                padding: const EdgeInsets.fromLTRB(gutter, 8, gutter, 0),
                child: FreshAuthTitle(eyebrow: eyebrow, scriptLine: scriptLine, brushLine: brushLine),
              ),
              const SizedBox(height: 12),
              Center(
                child: FreshAvatarBadge(size: avatarSize, child: avatar),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

/// The header text: a white spaced caps [eyebrow], a white handwritten [scriptLine] with mango sparks after it,
/// and [brushLine] in heavy ink on a mango brush. Screen readers hear one header with the two lines joined.
class FreshAuthTitle extends StatelessWidget {
  const FreshAuthTitle({super.key, required this.eyebrow, required this.scriptLine, required this.brushLine});

  final String eyebrow;
  final String scriptLine;
  final String brushLine;

  @override
  Widget build(BuildContext context) {
    final eyebrowStyle = AppText.eyebrow(BlockInk.paper, size: 13).copyWith(letterSpacing: 2.2);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(eyebrow.toUpperCase(), style: eyebrowStyle),
        const SizedBox(height: 6),
        Semantics(
          header: true,
          label: '$scriptLine $brushLine',
          excludeSemantics: true,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Stack(
                clipBehavior: Clip.none,
                children: [
                  Padding(
                    padding: const EdgeInsets.only(right: 30),
                    child: Text(scriptLine, style: scriptStyle(BlockInk.paper, 40)),
                  ),
                  const Positioned(
                    right: 0,
                    top: -14,
                    child: Sparks(color: FreshAuthColors.mango, degrees: 10, scale: 0.9),
                  ),
                ],
              ),
              const SizedBox(height: 4),
              BrushHighlight(
                color: FreshAuthColors.mango,
                degrees: -2,
                padding: const EdgeInsets.fromLTRB(12, 2, 14, 5),
                child: Text(brushLine, style: AppText.display(BlockInk.ink, 38, height: 1)),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

/// A picture in a big circle (the sign-in avatar): a pale mint backdrop, a white ring and a thin dark ink
/// outline, a small teal chat badge at the bottom right and two coloured dots near the ring. [child] fills the
/// inner circle and is clipped to it. Decorative: wrap the child's semantics as needed.
class FreshAvatarBadge extends StatelessWidget {
  const FreshAvatarBadge({
    super.key,
    required this.size,
    required this.child,
    this.badgeIcon = Icons.sms_rounded,
    this.showBadge = true,
  });

  final double size;
  final Widget child;
  final IconData badgeIcon;
  final bool showBadge;

  /// The white ring's width.
  double get ring => size >= 120 ? 6 : 4;

  /// The picture's diameter inside the ring.
  double get innerSize => size - 2 * ring;

  @override
  Widget build(BuildContext context) {
    final badge = size * 0.26;
    final dot = (size * 0.05).clamp(5.0, 9.0);
    return SizedBox.square(
      dimension: size,
      child: Stack(
        clipBehavior: Clip.none,
        children: [
          Container(
            width: size,
            height: size,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: FreshAuthColors.avatarBackdrop,
              border: Border.all(color: BlockInk.paper, width: ring),
              boxShadow: [
                const BoxShadow(color: BlockInk.ink, spreadRadius: 2.5),
                BoxShadow(color: BlockInk.ink.withValues(alpha: 0.18), blurRadius: 18, offset: const Offset(0, 8)),
              ],
            ),
            child: ClipOval(child: SizedBox.square(dimension: innerSize, child: child)),
          ),
          Positioned(
            left: size * 0.14,
            top: size * 0.30,
            child: _Dot(size: dot, color: FreshAuthColors.teal),
          ),
          Positioned(
            right: size * 0.13,
            top: size * 0.15,
            child: _Dot(size: dot * 1.25, color: FreshAuthColors.dotCoral),
          ),
          if (showBadge)
            Positioned(
              right: -badge * 0.08,
              bottom: size * 0.02,
              child: ExcludeSemantics(
                child: Container(
                  width: badge,
                  height: badge,
                  decoration: BoxDecoration(
                    color: BlockInk.paper,
                    shape: BoxShape.circle,
                    border: Border.all(color: BlockInk.ink, width: 2),
                  ),
                  alignment: Alignment.center,
                  child: Icon(badgeIcon, size: badge * 0.56, color: FreshAuthColors.teal),
                ),
              ),
            ),
        ],
      ),
    );
  }
}

class _Dot extends StatelessWidget {
  const _Dot({required this.size, required this.color});

  final double size;
  final Color color;

  @override
  Widget build(BuildContext context) => Container(
    width: size,
    height: size,
    decoration: BoxDecoration(color: color, shape: BoxShape.circle),
  );
}

/// An Indian mobile number field: a rounded outline with the India flag, "+91" and a chevron, a thin divider and
/// the number. The prefix is fixed (the store sells in India only); the chevron only completes the look. The
/// caller passes the formatters and reads [controller].
class FreshPhoneField extends StatelessWidget {
  const FreshPhoneField({
    super.key,
    required this.controller,
    this.fieldKey,
    this.hint = 'Mobile number',
    this.countryCode = '+91',
    this.errorText,
    this.inputFormatters,
    this.autofillHints,
    this.onSubmitted,
    this.enabled = true,
  });

  final TextEditingController controller;
  final Key? fieldKey;
  final String hint;
  final String countryCode;
  final String? errorText;
  final List<TextInputFormatter>? inputFormatters;
  final Iterable<String>? autofillHints;
  final ValueChanged<String>? onSubmitted;
  final bool enabled;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final danger = freshColors(context).dangerText;
    final hasError = errorText != null;
    final textStyle = Theme.of(context).textTheme.titleMedium?.copyWith(color: c.ink, fontSize: 17);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          constraints: const BoxConstraints(minHeight: 56),
          padding: const EdgeInsets.only(left: 16, right: 8),
          decoration: BoxDecoration(
            color: c.paper,
            borderRadius: BorderRadius.circular(18),
            border: Border.all(color: hasError ? danger : c.line, width: 1.5),
          ),
          child: Row(
            children: [
              const ExcludeSemantics(child: FreshIndiaFlag()),
              const SizedBox(width: 10),
              Text(countryCode, style: AppText.price(c.ink, size: 17)),
              ExcludeSemantics(child: Icon(Icons.expand_more_rounded, size: 18, color: c.muted)),
              const SizedBox(width: 10),
              Container(width: 1.5, height: 28, color: c.line),
              const SizedBox(width: 12),
              Expanded(
                child: TextField(
                  key: fieldKey,
                  controller: controller,
                  enabled: enabled,
                  keyboardType: TextInputType.phone,
                  autofillHints: autofillHints,
                  inputFormatters: inputFormatters,
                  style: textStyle,
                  decoration: InputDecoration(
                    hintText: hint,
                    hintStyle: textStyle?.copyWith(color: c.muted, fontWeight: FontWeight.w400),
                    border: InputBorder.none,
                    enabledBorder: InputBorder.none,
                    focusedBorder: InputBorder.none,
                    disabledBorder: InputBorder.none,
                    errorBorder: InputBorder.none,
                    focusedErrorBorder: InputBorder.none,
                    filled: false,
                    isDense: true,
                    contentPadding: const EdgeInsets.symmetric(vertical: 16),
                  ),
                  onSubmitted: onSubmitted,
                ),
              ),
            ],
          ),
        ),
        if (hasError)
          Padding(
            padding: const EdgeInsets.only(top: 6, left: 4),
            child: Semantics(
              liveRegion: true,
              child: Text(errorText!, style: Theme.of(context).textTheme.bodySmall?.copyWith(color: danger)),
            ),
          ),
      ],
    );
  }
}

/// The India flag, drawn (saffron, white with the navy wheel, green), 26 × 18 with rounded corners.
class FreshIndiaFlag extends StatelessWidget {
  const FreshIndiaFlag({super.key, this.width = 26});

  final double width;

  @override
  Widget build(BuildContext context) => ClipRRect(
    borderRadius: BorderRadius.circular(3),
    child: CustomPaint(size: Size(width, width * 18 / 26), painter: const _IndiaFlagPainter()),
  );
}

class _IndiaFlagPainter extends CustomPainter {
  const _IndiaFlagPainter();

  @override
  void paint(Canvas canvas, Size size) {
    final band = size.height / 3;
    canvas
      ..drawRect(Rect.fromLTWH(0, 0, size.width, band), Paint()..color = const Color(0xFFFF9933))
      ..drawRect(Rect.fromLTWH(0, band, size.width, band), Paint()..color = const Color(0xFFFFFFFF))
      ..drawRect(Rect.fromLTWH(0, band * 2, size.width, band), Paint()..color = const Color(0xFF138808))
      ..drawCircle(
        Offset(size.width / 2, size.height / 2),
        band * 0.42,
        Paint()
          ..color = const Color(0xFF000080)
          ..style = PaintingStyle.stroke
          ..strokeWidth = size.height * 0.05,
      )
      ..drawCircle(Offset(size.width / 2, size.height / 2), band * 0.1, Paint()..color = const Color(0xFF000080));
  }

  @override
  bool shouldRepaint(_IndiaFlagPainter old) => false;
}

/// A full-width dark pill button with the label in the middle and an arrow at the right ("Send code →"). It
/// shows a spinner and ignores taps while [loading].
class FreshArrowButton extends StatelessWidget {
  const FreshArrowButton({
    super.key,
    required this.label,
    required this.onPressed,
    this.loading = false,
    this.icon = Icons.arrow_forward_rounded,
  });

  final String label;
  final VoidCallback? onPressed;
  final bool loading;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    return SizedBox(
      width: double.infinity,
      child: Semantics(
        button: true,
        label: loading ? '$label, working' : null,
        child: FilledButton(
          onPressed: loading ? null : onPressed,
          style: FilledButton.styleFrom(
            backgroundColor: c.ink,
            foregroundColor: c.onInk,
            minimumSize: const Size.fromHeight(56),
            padding: const EdgeInsets.symmetric(horizontal: 20),
            shape: const StadiumBorder(),
          ),
          child: loading
              ? const SizedBox(
                  width: 20,
                  height: 20,
                  child: CircularProgressIndicator(strokeWidth: 2, semanticsLabel: 'Working'),
                )
              : Row(
                  children: [
                    const SizedBox(width: 24),
                    Expanded(
                      child: Text(label, textAlign: TextAlign.center, maxLines: 1, overflow: TextOverflow.ellipsis),
                    ),
                    ExcludeSemantics(child: Icon(icon, size: 24)),
                  ],
                ),
        ),
      ),
    );
  }
}

/// The drawn coast at the right of the sign-in header, excluded from semantics. It fills its box (keep the
/// [FreshCoastScenePainter.designSize] ratio). Static.
class FreshCoastScene extends StatelessWidget {
  const FreshCoastScene({super.key});

  @override
  Widget build(BuildContext context) =>
      const ExcludeSemantics(child: CustomPaint(painter: FreshCoastScenePainter(), size: Size.infinite));
}

/// Two gulls, soft peach clouds, a large pale sun, two palms on a spit of land, two fishing boats and the teal
/// sea, in a 160 × 200 design box scaled to the canvas width. The bottom [underCard] units of the sea run under
/// the card that rises over the header.
class FreshCoastScenePainter extends CustomPainter {
  const FreshCoastScenePainter();

  static const Size designSize = Size(160, 200);

  /// Design units of the scene below the card's top edge.
  static const double underCard = 30;

  static const Color sun = Color(0xFFF8D6C0);
  static const Color cloud = Color(0xFFF2B49B);
  static const Color cloudFront = Color(0xFFF6C6B0);
  static const Color haze = Color(0xFFEC9C83);
  static const Color bird = Color(0xFF3B4B49);
  static const Color palm = Color(0xFF1F5653);
  static const Color boat = Color(0xFF285E60);
  static const Color seaDeep = Color(0xFF3E918C);
  static const Color seaLight = Color(0xFF79C1B9);

  @override
  void paint(Canvas canvas, Size size) {
    final k = size.width / designSize.width;
    canvas
      ..save()
      ..scale(k);

    // The pale sun, half behind the clouds.
    canvas.drawCircle(const Offset(122, 116), 50, Paint()..color = sun);

    // Clouds: a big one at the top right and a small one lower at the left.
    _cloud(canvas, const [(Offset(112, 58), 20.0), (Offset(136, 46), 26.0), (Offset(162, 58), 22.0)], 70, cloud);
    _cloud(canvas, const [(Offset(26, 104), 12.0), (Offset(44, 96), 16.0), (Offset(62, 104), 11.0)], 112, cloudFront);

    // A soft far shore behind the boats.
    canvas.drawPath(
      Path()
        ..moveTo(0, 152)
        ..quadraticBezierTo(30, 132, 64, 140)
        ..quadraticBezierTo(90, 146, 104, 152)
        ..close(),
      Paint()..color = haze.withValues(alpha: 0.7),
    );

    // Gulls.
    FreshScenery.bird(canvas, const Offset(78, 10), span: 15, color: bird, strokeWidth: 2, rise: 0.32);
    FreshScenery.bird(canvas, const Offset(30, 40), span: 17, color: bird, strokeWidth: 2, rise: 0.32);

    // A spit of land with two palms at the right edge.
    final land = Paint()..color = palm;
    canvas
      ..drawPath(
        Path()
          ..moveTo(108, 160)
          ..quadraticBezierTo(126, 146, 150, 146)
          ..quadraticBezierTo(162, 146, 170, 150)
          ..lineTo(170, 166)
          ..lineTo(108, 166)
          ..close(),
        land,
      )
      ..drawCircle(const Offset(122, 154), 7, land)
      ..drawCircle(const Offset(150, 146), 8, land);
    FreshScenery.palm(canvas, const Offset(146, 72), scale: 0.62, color: palm);
    FreshScenery.palm(canvas, const Offset(120, 104), scale: 0.44, color: palm, mirrored: true);

    // Two fishing boats: the larger at the left, a smaller one beside it.
    _boat(canvas, left: 40, bottom: 164, scale: 0.46, color: boat);
    _boat(canvas, left: 84, bottom: 162, scale: 0.36, color: boat.withValues(alpha: 0.92));

    // The sea: deep teal with a light band and a wave line, running under the card.
    canvas
      ..drawPath(
        Path()
          ..moveTo(0, 162)
          ..quadraticBezierTo(40, 156, 80, 162)
          ..quadraticBezierTo(120, 168, 170, 158)
          ..lineTo(170, 200)
          ..lineTo(0, 200)
          ..close(),
        Paint()..color = seaDeep,
      )
      ..drawPath(
        Path()
          ..moveTo(0, 176)
          ..quadraticBezierTo(40, 170, 80, 176)
          ..quadraticBezierTo(120, 182, 170, 172)
          ..lineTo(170, 200)
          ..lineTo(0, 200)
          ..close(),
        Paint()..color = seaLight,
      )
      ..drawPath(
        Path()
          ..moveTo(14, 169)
          ..quadraticBezierTo(34, 165, 54, 169)
          ..moveTo(96, 170)
          ..quadraticBezierTo(116, 166, 136, 169),
        Paint()
          ..color = const Color(0xCCFFFDF8)
          ..style = PaintingStyle.stroke
          ..strokeWidth = 1.6
          ..strokeCap = StrokeCap.round,
      )
      ..restore();
  }

  /// A flat-bottomed cloud from overlapping circles, cut at [base].
  static void _cloud(Canvas canvas, List<(Offset, double)> puffs, double base, Color color) {
    final path = Path();
    for (final (centre, radius) in puffs) {
      path.addOval(Rect.fromCircle(center: centre, radius: radius));
    }
    final first = puffs.first;
    final last = puffs.last;
    path.addRect(Rect.fromLTRB(first.$1.dx, base - 10, last.$1.dx, base));
    canvas
      ..save()
      ..clipRect(Rect.fromLTRB(-50, -50, 400, base))
      ..drawPath(path, Paint()..color = color)
      ..restore();
  }

  /// The theme's trawler (its hull spans x 2–120, mast top y 250 to keel y 364) placed with its left edge at
  /// [left] and its keel at [bottom].
  static void _boat(
    Canvas canvas, {
    required double left,
    required double bottom,
    required double scale,
    required Color color,
  }) {
    canvas
      ..save()
      ..translate(left - 2 * scale, bottom - 364 * scale)
      ..scale(scale);
    FreshScenery.trawler(canvas, color);
    canvas.restore();
  }

  @override
  bool shouldRepaint(FreshCoastScenePainter old) => false;
}
