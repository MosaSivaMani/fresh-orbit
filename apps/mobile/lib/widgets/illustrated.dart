// Illustrated building blocks (D53): Sticker, PolaroidCard, FeatureCard, HelpHero and ReceiptCard. They lay out
// what they are given; none of them computes a business value (CLAUDE.md rule 4).

import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:fresh_theme/fresh_theme.dart';

import '../illustrations/illustration.dart';
import '../theme/app_theme.dart';
import 'bold.dart';
import 'motion.dart';
import 'states.dart';

enum StickerTone { coral, mango, teal, paper, sand }

/// A sticker on an avatar's edge or a card (the mockup's `.badge`): a pill with a 2 px ink outline, a hard
/// 2 px offset shadow and a slight tilt. Its colours are the same in light and dark, as in the mockup.
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
    final (bg, fg) = switch (tone) {
      StickerTone.coral => (const Color(0xFFF07A5A), BlockInk.ink),
      StickerTone.mango => (const Color(0xFFF5B544), BlockInk.ink),
      StickerTone.teal => (const Color(0xFF0B7D70), BlockInk.paper),
      StickerTone.paper => (BlockInk.paper, BlockInk.ink),
      StickerTone.sand => (ill.sandDeep, ill.ink),
    };
    final outline = tone == StickerTone.sand ? ill.ink : BlockInk.ink;
    return Transform.rotate(
      angle: tilt * 2 * math.pi,
      child: Container(
        padding: const EdgeInsets.fromLTRB(12, 6, 12, 5),
        decoration: BoxDecoration(
          color: bg,
          borderRadius: BorderRadius.circular(FreshRadius.full),
          border: Border.all(color: outline, width: 2),
          boxShadow: [BoxShadow(color: outline, offset: const Offset(2, 2))],
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (icon != null) ...[
              ExcludeSemantics(child: Icon(icon, size: 14, color: fg)),
              const SizedBox(width: FreshSpacing.s1),
            ],
            Flexible(
              child: Text(
                label,
                style: AppText.display(fg, 12.5, weight: FontWeight.w700, height: 1.1),
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

/// "How can we help you today?" next to the support agent in the ringed circle (D88).
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
          const EntranceMotion(child: AvatarCircle(slot: IllustrationSlot.helpSupport, size: 124)),
        ],
      ),
    );
  }
}

/// One line on a receipt. [value] is already formatted from a server value; the receipt never computes it.
@immutable
class ReceiptRow {
  const ReceiptRow(this.label, this.value, {this.detail, this.semanticsLabel});

  final String label;
  final String value;

  /// A small second line under the label (for example "Packed 1.01 kg net").
  final String? detail;

  /// Read instead of "label: value" (for example "Estimated total ₹708.50").
  final String? semanticsLabel;
}

/// A paper receipt with a scalloped torn bottom edge: title, optional subtitle, rows, a teal total band and an
/// optional coral weight note. Shows exactly what it is given.
class ReceiptCard extends StatelessWidget {
  const ReceiptCard({
    super.key,
    required this.title,
    required this.rows,
    this.subtitle,
    this.total,
    this.note,
    this.header,
  });

  final String title;
  final String? subtitle;
  final List<ReceiptRow> rows;

  /// The emphasised last line, on a teal band.
  final ReceiptRow? total;

  /// The weight note on a coral band (for example "The final total is set when your order is weighed").
  final String? note;

  /// Shown above the title (for example an illustration).
  final Widget? header;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final ill = illustratedColors(context);
    final totalColor = theme.brightness == Brightness.dark ? ill.tealText : ill.tealDeep;

    Widget row(ReceiptRow r) => Semantics(
      label: r.semanticsLabel ?? '${[r.label, ?r.detail].join(', ')}: ${r.value}',
      excludeSemantics: true,
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: FreshSpacing.s1),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(r.label, style: theme.textTheme.bodyMedium),
                  if (r.detail != null) Text(r.detail!, style: theme.textTheme.bodySmall?.copyWith(color: ill.muted)),
                ],
              ),
            ),
            const SizedBox(width: FreshSpacing.s3),
            Flexible(
              child: Text(
                r.value,
                textAlign: TextAlign.end,
                style: theme.textTheme.titleSmall?.copyWith(fontFeatures: const [FontFeature.tabularFigures()]),
              ),
            ),
          ],
        ),
      ),
    );

    final t = total;
    return PhysicalShape(
      clipper: const _ScallopEdgeClipper(),
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
            if (subtitle != null)
              Text(
                subtitle!,
                style: theme.textTheme.bodySmall?.copyWith(color: ill.muted),
                textAlign: TextAlign.center,
              ),
            const SizedBox(height: FreshSpacing.s3),
            const _DashedRule(),
            const SizedBox(height: FreshSpacing.s2),
            for (final r in rows) row(r),
            if (t != null) ...[
              const SizedBox(height: FreshSpacing.s2),
              const _DashedRule(),
              const SizedBox(height: FreshSpacing.s3),
              Semantics(
                label: t.semanticsLabel ?? '${t.label}: ${t.value}',
                excludeSemantics: true,
                child: Container(
                  padding: const EdgeInsets.symmetric(horizontal: FreshSpacing.s3, vertical: FreshSpacing.s2),
                  decoration: BoxDecoration(color: ill.tealSoft, borderRadius: BorderRadius.circular(12)),
                  child: Row(
                    children: [
                      Expanded(
                        child: Text(t.label, style: theme.textTheme.titleMedium?.copyWith(color: totalColor)),
                      ),
                      Text(
                        t.value,
                        style: theme.textTheme.titleMedium?.copyWith(
                          color: totalColor,
                          fontFeatures: const [FontFeature.tabularFigures()],
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ],
            if (note != null) ...[
              const SizedBox(height: FreshSpacing.s2),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: FreshSpacing.s3, vertical: FreshSpacing.s2),
                decoration: BoxDecoration(color: ill.coralSoft, borderRadius: BorderRadius.circular(12)),
                child: Text(
                  note!,
                  style: theme.textTheme.bodySmall?.copyWith(color: ill.coralText, fontWeight: FontWeight.w600),
                ),
              ),
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
      child: CustomPaint(painter: _DashPainter(illustratedColors(context).line)),
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
      ..strokeWidth = 2
      ..strokeCap = StrokeCap.round;
    for (var x = 0.0; x < size.width; x += 9) {
      canvas.drawLine(Offset(x, size.height / 2), Offset(math.min(x + 4, size.width), size.height / 2), paint);
    }
  }

  @override
  bool shouldRepaint(_DashPainter old) => old.color != color;
}

/// Rounded top corners and a scalloped bottom edge, as if torn along a perforation.
class _ScallopEdgeClipper extends CustomClipper<Path> {
  const _ScallopEdgeClipper();

  static const _width = 18.0;
  static const _depth = 7.0;

  @override
  Path getClip(Size size) {
    const r = 24.0;
    final bottom = size.height;
    final path = Path()
      ..moveTo(0, r)
      ..quadraticBezierTo(0, 0, r, 0)
      ..lineTo(size.width - r, 0)
      ..quadraticBezierTo(size.width, 0, size.width, r)
      ..lineTo(size.width, bottom);
    final count = math.max(1, (size.width / _width).round());
    final step = size.width / count;
    for (var i = count; i > 0; i--) {
      final x = i * step;
      // A round bite out of the paper between two points of the edge.
      path.quadraticBezierTo(x - step / 2, bottom - 2 * _depth, x - step, bottom);
    }
    return path..close();
  }

  @override
  bool shouldReclip(_ScallopEdgeClipper oldClipper) => false;
}
