// Illustrated building blocks (D53): Sticker, PolaroidCard, FeatureCard, HelpHero and ReceiptCard. They lay out
// what they are given; none of them computes a business value (CLAUDE.md rule 4).

import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:fresh_theme/fresh_theme.dart';

import '../illustrations/illustration.dart';
import '../theme/app_theme.dart';
import 'motion.dart';
import 'states.dart';

enum StickerTone { coral, teal, sand }

/// A pill label with a thick outline and a hard offset shadow, slightly tilted, like a sticker on paper.
class Sticker extends StatelessWidget {
  const Sticker({super.key, required this.label, this.tone = StickerTone.coral, this.tilt = -0.012, this.icon});

  final String label;
  final StickerTone tone;

  /// Rotation in turns.
  final double tilt;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final ill = illustratedColors(context);
    final scheme = Theme.of(context).colorScheme;
    final (bg, fg) = switch (tone) {
      StickerTone.coral => (ill.coral, ill.onCoral),
      StickerTone.teal => (scheme.primary, scheme.onPrimary),
      StickerTone.sand => (ill.sandDeep, scheme.onSurface),
    };
    return Transform.rotate(
      angle: tilt * 2 * math.pi,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: FreshSpacing.s3, vertical: FreshSpacing.s1),
        decoration: BoxDecoration(
          color: bg,
          borderRadius: BorderRadius.circular(FreshRadius.full),
          border: Border.all(color: ill.ink, width: 2),
          boxShadow: [BoxShadow(color: ill.ink, offset: const Offset(2, 2.5))],
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (icon != null) ...[
              ExcludeSemantics(child: Icon(icon, size: 16, color: fg)),
              const SizedBox(width: FreshSpacing.s1),
            ],
            Flexible(
              child: Text(
                label,
                style: Theme.of(context).textTheme.labelLarge?.copyWith(color: fg, fontWeight: FontWeight.w700),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// A tilted instant-photo card: thick dark outline, hard offset shadow, a picture area, an optional caption and
/// stickers on the top edge.
class PolaroidCard extends StatelessWidget {
  const PolaroidCard({
    super.key,
    required this.picture,
    this.caption,
    this.stickers = const [],
    this.tilt = -0.01,
    this.pictureColor,
  });

  final Widget picture;
  final String? caption;
  final List<Widget> stickers;

  /// Rotation in turns.
  final double tilt;
  final Color? pictureColor;

  @override
  Widget build(BuildContext context) {
    final ill = illustratedColors(context);
    final card = Container(
      padding: const EdgeInsets.fromLTRB(FreshSpacing.s3, FreshSpacing.s3, FreshSpacing.s3, FreshSpacing.s4),
      decoration: BoxDecoration(
        color: ill.paper,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: ill.ink, width: 2.5),
        boxShadow: [BoxShadow(color: ill.ink, offset: const Offset(4, 5))],
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          ClipRRect(
            borderRadius: BorderRadius.circular(12),
            child: ColoredBox(
              color: pictureColor ?? ill.tealSoft,
              child: Center(child: picture),
            ),
          ),
          if (caption != null) ...[
            const SizedBox(height: FreshSpacing.s3),
            Text(caption!, style: Theme.of(context).textTheme.titleMedium, textAlign: TextAlign.center),
          ],
        ],
      ),
    );
    return EntranceMotion(
      fromTurns: tilt * 1.5,
      child: Transform.rotate(
        angle: tilt * 2 * math.pi,
        child: Stack(
          clipBehavior: Clip.none,
          children: [
            Padding(
              padding: const EdgeInsets.only(top: FreshSpacing.s3),
              child: card,
            ),
            if (stickers.isNotEmpty)
              Positioned(
                top: 0,
                left: FreshSpacing.s4,
                right: FreshSpacing.s4,
                child: Wrap(spacing: FreshSpacing.s2, runSpacing: FreshSpacing.s1, children: stickers),
              ),
          ],
        ),
      ),
    );
  }
}

/// Feature card: big illustration, a number, a chunky rounded headline and one short line.
class FeatureCard extends StatelessWidget {
  const FeatureCard({
    super.key,
    required this.slot,
    required this.number,
    required this.title,
    required this.message,
    this.width = 220,
  });

  final IllustrationSlot slot;
  final int number;
  final String title;
  final String message;
  final double width;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final ill = illustratedColors(context);
    return SizedBox(
      width: width,
      child: Card(
        child: Padding(
          padding: const EdgeInsets.all(FreshSpacing.s4),
          child: MergeSemantics(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Center(child: Illustration(slot: slot, size: 120, excludeFromSemantics: true)),
                const SizedBox(height: FreshSpacing.s2),
                ExcludeSemantics(
                  child: Text(
                    number.toString().padLeft(2, '0'),
                    style: theme.textTheme.headlineMedium?.copyWith(color: ill.tealText, height: 1),
                  ),
                ),
                Text(title, style: theme.textTheme.titleLarge),
                const SizedBox(height: FreshSpacing.s1),
                Text(message, style: theme.textTheme.bodyMedium?.copyWith(color: freshColors(context).textMuted)),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// "How can we help you today?" next to the support agent on a coloured circle.
class HelpHero extends StatelessWidget {
  const HelpHero({super.key, this.title = 'How can we help you today?', this.message});

  final String title;
  final String? message;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final ill = illustratedColors(context);
    return Container(
      padding: const EdgeInsets.all(FreshSpacing.s5),
      decoration: BoxDecoration(color: ill.tealSoft, borderRadius: BorderRadius.circular(AppRadius.hero)),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Semantics(header: true, child: Text(title, style: theme.textTheme.headlineMedium)),
                if (message != null) ...[
                  const SizedBox(height: FreshSpacing.s2),
                  Text(message!, style: theme.textTheme.bodyMedium),
                ],
              ],
            ),
          ),
          const SizedBox(width: FreshSpacing.s3),
          EntranceMotion(
            child: Container(
              width: 132,
              height: 132,
              decoration: BoxDecoration(color: ill.coral, shape: BoxShape.circle),
              alignment: Alignment.center,
              child: const Illustration(slot: IllustrationSlot.helpSupport, size: 124),
            ),
          ),
        ],
      ),
    );
  }
}

/// One line on a receipt. [value] is already formatted from a server value; the receipt never computes it.
@immutable
class ReceiptRow {
  const ReceiptRow(this.label, this.value, {this.semanticsLabel});

  final String label;
  final String value;

  /// Read instead of "label value" (for example "Estimated total ₹708.50").
  final String? semanticsLabel;
}

/// A paper receipt with a torn bottom edge. Shows exactly the rows it is given.
class ReceiptCard extends StatelessWidget {
  const ReceiptCard({super.key, required this.title, required this.rows, this.total, this.footnote, this.header});

  final String title;
  final List<ReceiptRow> rows;

  /// The emphasised last line, below a dashed rule.
  final ReceiptRow? total;
  final String? footnote;

  /// Shown above the title (for example an illustration).
  final Widget? header;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final ill = illustratedColors(context);
    final muted = freshColors(context).textMuted;
    Widget row(ReceiptRow r, {bool strong = false}) => Semantics(
      label: r.semanticsLabel ?? '${r.label}: ${r.value}',
      excludeSemantics: true,
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: FreshSpacing.s1),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: Text(
                r.label,
                style: strong ? theme.textTheme.titleMedium : theme.textTheme.bodyMedium?.copyWith(color: muted),
              ),
            ),
            const SizedBox(width: FreshSpacing.s3),
            Flexible(
              flex: 2,
              child: Text(
                r.value,
                textAlign: TextAlign.end,
                style: strong ? theme.textTheme.titleMedium : theme.textTheme.titleSmall,
              ),
            ),
          ],
        ),
      ),
    );

    return PhysicalShape(
      clipper: const _TornEdgeClipper(),
      color: ill.paper,
      elevation: 4,
      shadowColor: ill.softShadow,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(FreshSpacing.s5, FreshSpacing.s5, FreshSpacing.s5, FreshSpacing.s8),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          mainAxisSize: MainAxisSize.min,
          children: [
            ?header,
            Semantics(
              header: true,
              child: Text(title, style: theme.textTheme.titleLarge, textAlign: TextAlign.center),
            ),
            const SizedBox(height: FreshSpacing.s3),
            const _DashedRule(),
            const SizedBox(height: FreshSpacing.s2),
            for (final r in rows) row(r),
            if (total != null) ...[
              const SizedBox(height: FreshSpacing.s2),
              const _DashedRule(),
              const SizedBox(height: FreshSpacing.s2),
              row(total!, strong: true),
            ],
            if (footnote != null) ...[
              const SizedBox(height: FreshSpacing.s2),
              Text(footnote!, style: theme.textTheme.bodySmall?.copyWith(color: muted)),
            ],
          ],
        ),
      ),
    );
  }
}

class _DashedRule extends StatelessWidget {
  const _DashedRule();

  @override
  Widget build(BuildContext context) => ExcludeSemantics(
    child: SizedBox(
      height: 2,
      width: double.infinity,
      child: CustomPaint(painter: _DashPainter(freshColors(context).borderStrong)),
    ),
  );
}

class _DashPainter extends CustomPainter {
  _DashPainter(this.color);

  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = color
      ..strokeWidth = 1.5
      ..strokeCap = StrokeCap.round;
    for (var x = 0.0; x < size.width; x += 9) {
      canvas.drawLine(Offset(x, size.height / 2), Offset(math.min(x + 4, size.width), size.height / 2), paint);
    }
  }

  @override
  bool shouldRepaint(_DashPainter old) => old.color != color;
}

/// Rounded top corners, a zig-zag torn bottom edge.
class _TornEdgeClipper extends CustomClipper<Path> {
  const _TornEdgeClipper();

  static const _tooth = 14.0;
  static const _depth = 8.0;

  @override
  Path getClip(Size size) {
    const r = 20.0;
    final path = Path()
      ..moveTo(0, r)
      ..quadraticBezierTo(0, 0, r, 0)
      ..lineTo(size.width - r, 0)
      ..quadraticBezierTo(size.width, 0, size.width, r)
      ..lineTo(size.width, size.height - _depth);
    final teeth = math.max(1, (size.width / _tooth).round());
    final step = size.width / teeth;
    for (var i = teeth; i > 0; i--) {
      final x = i * step;
      path
        ..lineTo(x - step / 2, size.height)
        ..lineTo(x - step, size.height - _depth);
    }
    return path..close();
  }

  @override
  bool shouldReclip(_TornEdgeClipper oldClipper) => false;
}
