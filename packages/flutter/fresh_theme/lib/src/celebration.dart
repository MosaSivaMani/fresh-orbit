// The order-placed look (D288, owner's reference docs/design/mockups/app-order-placed-reference-2026-10-29.png):
// a teal header with a big title, a handwritten key word on a mango brush with coral sparks, a cream status pill
// and a thank-you line; drawn, static scenery behind it (two birds, a pale sun, a palm and two boats at the right,
// white wave curls along the bottom edge) and the character at the right, overlapping the header's bottom; a
// cream ticket card with a scalloped bottom edge that rises over the header (icon circle rows with the values in
// pills, the total on a mint band, a coral note); a pale blue note card with a picture; a pill button with an
// outline. The parts lay out the values they are given (already formatted from the server); nothing here
// computes a business value. Nothing moves.

import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:fresh_theme/fresh_theme.dart';

/// Colours of the order-placed look that stay the same in light and dark (they sit on the teal header or are
/// tints the reference uses on the cream card).
abstract final class FreshCelebrationColors {
  /// The header block.
  static const Color teal = Color(0xFF2C8378);

  /// Scenery silhouettes on the header (palm, boats).
  static const Color silhouette = Color(0x8C1C5A54);

  /// The pale sun behind the palm.
  static const Color sun = Color(0x59FBE3B8);

  /// The lighter water above the header's bottom edge.
  static const Color shallows = Color(0x6690D3C8);

  /// The brush behind the handwritten word.
  static const Color mango = Color(0xFFF5B544);

  /// The status pill on the header.
  static const Color cream = Color(0xFFFFF8EA);

  /// The ticket card and its dashed rule and hairlines.
  static const Color card = Color(0xFFFFFCF5);
  static const Color rule = Color(0xFFE8DCC8);

  /// Mint icon circles, pills and the total band.
  static const Color mint = Color(0xFFE0F1ED);

  /// Coral icon circles and pills.
  static const Color coralSoft = Color(0xFFFCE3DA);

  /// Icons and text on mint.
  static const Color tealInk = Color(0xFF16675E);

  /// Icons on coral and the note text.
  static const Color coralInk = Color(0xFFB4432A);

  /// Text on a coral pill.
  static const Color coralDeep = Color(0xFF6E2414);

  /// The note card under the ticket.
  static const Color noteBlue = Color(0xFFE4EEFA);
  static const Color noteInk = Color(0xFF243F78);
}

/// The teal header of the order-placed page with [child] (the ticket card) rising over its bottom edge.
///
/// [title] in Baloo 2 800 white, [scriptWord] in ink Dancing Script on a mango brush with coral sparks, the
/// [status] in a cream pill (left out when null), the [message] in white, and the character built by
/// [character] for the size the header picks (about 44 % of the width, 140–200 px) at the right, its feet
/// behind the card. The header runs under the status bar. Screen readers hear "[title] [scriptWord]" as one
/// header, then the status and the message.
class FreshCelebrationHeader extends StatelessWidget {
  const FreshCelebrationHeader({
    super.key,
    required this.title,
    required this.scriptWord,
    required this.message,
    required this.character,
    required this.child,
    this.status,
  });

  final String title;
  final String scriptWord;
  final String? status;
  final String message;
  final Widget Function(double size) character;
  final Widget child;

  /// Side margin of the header text and of the card.
  static const double gutter = 20;

  /// How far the card rises over the header.
  static const double overlap = 30;

  /// The transparent strip under the header's wavy edge.
  static const double edge = 14;

  /// The character's size for a page [width] wide.
  static double characterSizeFor(double width) => (width * 0.44).clamp(140.0, 200.0);

  @override
  Widget build(BuildContext context) {
    final top = MediaQuery.paddingOf(context).top;
    return LayoutBuilder(
      builder: (context, box) {
        final size = characterSizeFor(box.maxWidth);
        final header = Stack(
          clipBehavior: Clip.none,
          children: [
            const Positioned.fill(
              child: ExcludeSemantics(child: CustomPaint(painter: FreshCelebrationBackdropPainter(edge: edge))),
            ),
            Positioned(
              right: 0,
              bottom: overlap - 12,
              child: SizedBox.square(dimension: size, child: character(size)),
            ),
            Padding(
              padding: EdgeInsets.fromLTRB(gutter, top + 18, size * 0.72 + 6, overlap + 20),
              child: ConstrainedBox(
                constraints: BoxConstraints(minHeight: math.max(0.0, size - 46)),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    FreshCelebrationTitle(title: title, scriptWord: scriptWord),
                    if (status != null) ...[
                      const SizedBox(height: 12),
                      FreshStatusPill(label: status!),
                    ],
                    const SizedBox(height: 12),
                    Text(
                      message,
                      style: const TextStyle(
                        fontFamily: AppFonts.body,
                        fontFamilyFallback: FreshFonts.sansFallback,
                        fontSize: 16,
                        height: 1.3,
                        fontWeight: FontWeight.w500,
                        color: BlockInk.paper,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ],
        );
        return AnnotatedRegion<SystemUiOverlayStyle>(
          value: SystemUiOverlayStyle.light,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              header,
              FreshOverlapUp(
                amount: overlap,
                child: Padding(
                  padding: const EdgeInsets.symmetric(horizontal: gutter),
                  child: child,
                ),
              ),
            ],
          ),
        );
      },
    );
  }
}

/// "Order" over "*placed*": the title in white Baloo 2 800, the word in ink Dancing Script on a mango brush
/// tilted −3°, coral sparks after it. One header for screen readers, announced when it appears.
class FreshCelebrationTitle extends StatelessWidget {
  const FreshCelebrationTitle({super.key, required this.title, required this.scriptWord, this.size = 44});

  final String title;
  final String scriptWord;
  final double size;

  @override
  Widget build(BuildContext context) => Semantics(
    header: true,
    liveRegion: true,
    label: '$title $scriptWord',
    excludeSemantics: true,
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(title, style: AppText.display(BlockInk.paper, size, height: 1)),
        Stack(
          clipBehavior: Clip.none,
          children: [
            Padding(
              padding: const EdgeInsets.only(right: 34),
              child: Transform.rotate(
                angle: -3 * math.pi / 180,
                alignment: Alignment.centerLeft,
                child: BrushHighlight(
                  color: FreshCelebrationColors.mango,
                  padding: const EdgeInsets.fromLTRB(10, 0, 14, 4),
                  child: Text(scriptWord, style: scriptStyle(BlockInk.ink, size * 0.88)),
                ),
              ),
            ),
            const Positioned(
              right: 0,
              top: -6,
              child: Sparks(color: FreshHighlightColors.coral, degrees: 20, scale: 0.85),
            ),
          ],
        ),
      ],
    ),
  );
}

/// A cream pill with teal text (the order status on the header).
class FreshStatusPill extends StatelessWidget {
  const FreshStatusPill({super.key, required this.label});

  final String label;

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 5),
    decoration: const ShapeDecoration(color: FreshCelebrationColors.cream, shape: StadiumBorder()),
    child: Text(label, style: AppText.display(FreshCelebrationColors.tealInk, 16, weight: FontWeight.w700)),
  );
}

/// Paints the teal header: the block with a wavy bottom edge [edge] above the box's bottom (the page shows
/// below it), lighter water and white wave curls along that edge, a pale sun, a palm and two boats at the right
/// and two birds. Static; fills its box.
class FreshCelebrationBackdropPainter extends CustomPainter {
  const FreshCelebrationBackdropPainter({required this.edge, this.color = FreshCelebrationColors.teal});

  final double edge;
  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final w = size.width;
    final base = size.height - edge;
    if (w <= 0 || base <= 0) return;

    // The block, its bottom edge a gentle wave.
    const segment = 72.0;
    const amp = 5.0;
    final block = Path()
      ..moveTo(0, 0)
      ..lineTo(w, 0)
      ..lineTo(w, base);
    var x = w;
    var up = true;
    while (x > 0) {
      final next = x - segment / 2;
      block.quadraticBezierTo(x - segment / 4, base + (up ? -amp : amp), next, base);
      x = next;
      up = !up;
    }
    block.close();
    canvas
      ..drawPath(block, Paint()..color = color)
      ..save()
      ..clipPath(block);

    // The pale sun, the boats and the palm at the right, behind the character.
    final silhouette = Paint()..color = FreshCelebrationColors.silhouette;
    canvas.drawCircle(Offset(w - 46, base - 92), 48, Paint()..color = FreshCelebrationColors.sun);
    for (final (left, scale) in [(w - 128, 0.3), (w - 74, 0.24)]) {
      canvas
        ..save()
        ..translate(left, base - 16 - 364 * scale)
        ..scale(scale);
      FreshScenery.trawler(canvas, silhouette.color, window: const Color(0x00000000));
      canvas.restore();
    }
    FreshScenery.palm(canvas, Offset(w - 24, base - 132), scale: 0.8, color: silhouette.color);

    // Two birds above the title's right end.
    for (final (fx, y, span) in const [(0.5, 0.2, 16.0), (0.44, 0.3, 12.0)]) {
      FreshScenery.bird(
        canvas,
        Offset(w * fx, base * y),
        span: span,
        color: const Color(0xCC17423E),
        strokeWidth: 1.8,
      );
    }

    // Lighter water along the bottom, then the white curls.
    final shallows = Path()..moveTo(0, base - 22);
    x = 0;
    up = true;
    while (x < w) {
      final next = x + 44;
      shallows.quadraticBezierTo(x + 22, base - 22 + (up ? -4 : 4), next, base - 22);
      x = next;
      up = !up;
    }
    shallows
      ..lineTo(w, size.height)
      ..lineTo(0, size.height)
      ..close();
    canvas.drawPath(shallows, Paint()..color = FreshCelebrationColors.shallows);

    final curl = Paint()
      ..color = BlockInk.paper.withValues(alpha: 0.85)
      ..style = PaintingStyle.stroke
      ..strokeWidth = 2.4
      ..strokeCap = StrokeCap.round;
    for (var cx = 8.0; cx < w; cx += 52) {
      canvas.drawPath(
        Path()
          ..moveTo(cx, base - 6)
          ..quadraticBezierTo(cx + 10, base - 18, cx + 22, base - 12)
          ..quadraticBezierTo(cx + 26, base - 9, cx + 22, base - 7),
        curl,
      );
    }
    canvas.restore();
  }

  @override
  bool shouldRepaint(FreshCelebrationBackdropPainter old) => old.edge != edge || old.color != color;
}

/// Lays [child] out at its full height but takes [amount] less, and paints and hit-tests it [amount] higher, so
/// it rises over whatever is above it (the ticket card over the header).
class FreshOverlapUp extends SingleChildRenderObjectWidget {
  const FreshOverlapUp({super.key, required this.amount, super.child});

  final double amount;

  @override
  RenderObject createRenderObject(BuildContext context) => RenderFreshOverlapUp(amount);

  @override
  void updateRenderObject(BuildContext context, RenderFreshOverlapUp renderObject) => renderObject.amount = amount;
}

/// The render object of [FreshOverlapUp].
class RenderFreshOverlapUp extends RenderProxyBox {
  RenderFreshOverlapUp(this._amount);

  double _amount;

  set amount(double value) {
    if (value == _amount) return;
    _amount = value;
    markNeedsLayout();
  }

  Offset get _shift => Offset(0, -_amount);

  BoxConstraints _childConstraints(BoxConstraints c) => c.copyWith(
    minHeight: c.minHeight == 0 ? 0 : c.minHeight + _amount,
    maxHeight: c.maxHeight.isFinite ? c.maxHeight + _amount : double.infinity,
  );

  Size _sizeFor(BoxConstraints c, Size child) =>
      c.constrain(Size(child.width, math.max(0.0, child.height - _amount)));

  @override
  void performLayout() {
    final c = child;
    if (c == null) {
      size = constraints.smallest;
      return;
    }
    c.layout(_childConstraints(constraints), parentUsesSize: true);
    size = _sizeFor(constraints, c.size);
  }

  @override
  Size computeDryLayout(BoxConstraints constraints) {
    final c = child;
    if (c == null) return constraints.smallest;
    return _sizeFor(constraints, c.getDryLayout(_childConstraints(constraints)));
  }

  @override
  double computeMinIntrinsicHeight(double width) => math.max(0.0, super.computeMinIntrinsicHeight(width) - _amount);

  @override
  double computeMaxIntrinsicHeight(double width) => math.max(0.0, super.computeMaxIntrinsicHeight(width) - _amount);

  @override
  void paint(PaintingContext context, Offset offset) {
    final c = child;
    if (c != null) context.paintChild(c, offset + _shift);
  }

  @override
  bool hitTest(BoxHitTestResult result, {required Offset position}) {
    if (hitTestChildren(result, position: position)) {
      result.add(BoxHitTestEntry(this, position));
      return true;
    }
    return false;
  }

  @override
  bool hitTestChildren(BoxHitTestResult result, {required Offset position}) {
    final c = child;
    if (c == null) return false;
    return result.addWithPaintOffset(
      offset: _shift,
      position: position,
      hitTest: (result, transformed) => c.hitTest(result, position: transformed),
    );
  }

  @override
  void applyPaintTransform(RenderBox child, Matrix4 transform) =>
      transform.multiply(Matrix4.translationValues(0, -_amount, 0));
}

/// The colour of a [FreshTicketRow]'s icon circle and value pill.
enum FreshTicketTone {
  mint,
  coral;

  Color get soft => this == mint ? FreshCelebrationColors.mint : FreshCelebrationColors.coralSoft;
  Color get icon => this == mint ? FreshCelebrationColors.tealInk : FreshCelebrationColors.coralInk;
  Color get text => this == mint ? BlockInk.ink : FreshCelebrationColors.coralDeep;
}

/// One row of a [FreshTicketCard]: an icon circle, the [label] and the [value] (already formatted from a server
/// value) in a pill. [strong] is a word of the value shown bold ("Cash on **delivery**").
@immutable
class FreshTicketRow {
  const FreshTicketRow({
    required this.icon,
    required this.label,
    required this.value,
    this.tone = FreshTicketTone.mint,
    this.strong,
    this.semanticsLabel,
  });

  final IconData icon;
  final String label;
  final String value;
  final FreshTicketTone tone;
  final String? strong;

  /// Read instead of "label: value".
  final String? semanticsLabel;
}

/// The cream ticket of the order-placed page: an icon circle and the [title], a dashed rule, [rows] split by
/// hairlines, the [total] on a mint band (teal ₹ circle, coral sparks) and the [note] on a coral strip with a
/// scale icon. The bottom edge is scalloped. Shows exactly what it is given.
class FreshTicketCard extends StatelessWidget {
  const FreshTicketCard({
    super.key,
    required this.title,
    required this.rows,
    this.titleIcon = Icons.assignment_outlined,
    this.totalLabel,
    this.totalValue,
    this.totalSemanticsLabel,
    this.note,
    this.noteIcon = Icons.scale_outlined,
  });

  final String title;
  final IconData titleIcon;
  final List<FreshTicketRow> rows;
  final String? totalLabel;

  /// Already formatted ("₹708.50").
  final String? totalValue;
  final String? totalSemanticsLabel;
  final String? note;
  final IconData noteIcon;

  static const double _radius = 26;

  @override
  Widget build(BuildContext context) {
    const rule = FreshCelebrationColors.rule;
    final total = totalValue;
    return PhysicalShape(
      clipper: const _TicketClipper(radius: _radius),
      color: FreshCelebrationColors.card,
      elevation: 6,
      shadowColor: const Color(0x40281E0A),
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 30),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          mainAxisSize: MainAxisSize.min,
          children: [
            Row(
              children: [
                FreshIconCircle(icon: titleIcon, size: 52),
                const SizedBox(width: 14),
                Expanded(
                  child: Semantics(
                    header: true,
                    child: Text(title, style: AppText.display(BlockInk.ink, 26, height: 1.1)),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            const ExcludeSemantics(
              child: SizedBox(height: 2, child: CustomPaint(painter: _DashPainter(rule))),
            ),
            const SizedBox(height: 4),
            for (var i = 0; i < rows.length; i++) ...[
              if (i > 0) const Divider(height: 1, thickness: 1, color: rule),
              _TicketRowView(row: rows[i]),
            ],
            if (total != null) ...[
              const SizedBox(height: 8),
              _TotalBand(label: totalLabel ?? '', value: total, semanticsLabel: totalSemanticsLabel),
            ],
            if (note != null) ...[
              const SizedBox(height: 8),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
                decoration: BoxDecoration(
                  color: FreshCelebrationColors.coralSoft,
                  borderRadius: BorderRadius.circular(16),
                ),
                child: Row(
                  children: [
                    ExcludeSemantics(child: Icon(noteIcon, size: 28, color: FreshCelebrationColors.coralInk)),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Text(
                        note!,
                        style: const TextStyle(
                          fontFamily: AppFonts.body,
                          fontFamilyFallback: FreshFonts.sansFallback,
                          fontSize: 14,
                          height: 1.35,
                          color: FreshCelebrationColors.coralInk,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

/// A soft circle with a line icon (the ticket's row icons). Decorative.
class FreshIconCircle extends StatelessWidget {
  const FreshIconCircle({super.key, required this.icon, this.tone = FreshTicketTone.mint, this.size = 44});

  final IconData icon;
  final FreshTicketTone tone;
  final double size;

  @override
  Widget build(BuildContext context) => ExcludeSemantics(
    child: Container(
      width: size,
      height: size,
      decoration: BoxDecoration(color: tone.soft, shape: BoxShape.circle),
      child: Icon(icon, size: size * 0.5, color: tone.icon),
    ),
  );
}

class _TicketRowView extends StatelessWidget {
  const _TicketRowView({required this.row});

  final FreshTicketRow row;

  @override
  Widget build(BuildContext context) {
    const valueStyle = TextStyle(
      fontFamily: AppFonts.body,
      fontFamilyFallback: FreshFonts.sansFallback,
      fontSize: 15,
      height: 1.3,
      fontWeight: FontWeight.w600,
    );
    final style = valueStyle.copyWith(color: row.tone.text);
    final strong = row.strong;
    final at = strong == null ? -1 : row.value.lastIndexOf(strong);
    final value = at < 0
        ? TextSpan(text: row.value, style: style)
        : TextSpan(
            style: style,
            children: [
              TextSpan(text: row.value.substring(0, at)),
              TextSpan(text: strong, style: const TextStyle(fontWeight: FontWeight.w800)),
              TextSpan(text: row.value.substring(at + strong!.length)),
            ],
          );
    return Semantics(
      label: row.semanticsLabel ?? '${row.label}: ${row.value}',
      excludeSemantics: true,
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 10),
        child: LayoutBuilder(
          builder: (context, box) => Row(
            children: [
              FreshIconCircle(icon: row.icon, tone: row.tone),
              const SizedBox(width: 12),
              ConstrainedBox(
                constraints: BoxConstraints(maxWidth: box.maxWidth * 0.32),
                child: Text(row.label, style: AppText.display(BlockInk.ink, 17, weight: FontWeight.w700)),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Align(
                  alignment: Alignment.centerRight,
                  child: Container(
                    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                    decoration: BoxDecoration(color: row.tone.soft, borderRadius: BorderRadius.circular(14)),
                    child: Text.rich(value),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _TotalBand extends StatelessWidget {
  const _TotalBand({required this.label, required this.value, this.semanticsLabel});

  final String label;
  final String value;
  final String? semanticsLabel;

  @override
  Widget build(BuildContext context) => Semantics(
    label: semanticsLabel ?? '$label: $value',
    excludeSemantics: true,
    child: Container(
      padding: const EdgeInsets.fromLTRB(12, 12, 16, 12),
      decoration: BoxDecoration(color: FreshCelebrationColors.mint, borderRadius: BorderRadius.circular(18)),
      child: Row(
        children: [
          Container(
            width: 46,
            height: 46,
            decoration: const BoxDecoration(color: FreshCelebrationColors.tealInk, shape: BoxShape.circle),
            child: const Icon(Icons.currency_rupee_rounded, color: BlockInk.paper, size: 26),
          ),
          const SizedBox(width: 12),
          Expanded(child: Text(label, style: AppText.display(BlockInk.ink, 18))),
          Container(
            width: 1,
            height: 34,
            margin: const EdgeInsets.symmetric(horizontal: 10),
            color: FreshCelebrationColors.tealInk.withValues(alpha: 0.2),
          ),
          Stack(
            clipBehavior: Clip.none,
            children: [
              Text(
                value,
                style: AppText.price(BlockInk.ink, size: 28),
              ),
              const Positioned(
                right: -16,
                top: -14,
                child: Sparks(color: FreshHighlightColors.coral, degrees: 10, scale: 0.6),
              ),
            ],
          ),
        ],
      ),
    ),
  );
}

class _DashPainter extends CustomPainter {
  const _DashPainter(this.color);

  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = color
      ..strokeWidth = 1.6
      ..strokeCap = StrokeCap.round;
    for (var x = 0.0; x < size.width; x += 9) {
      canvas.drawLine(Offset(x, size.height / 2), Offset(math.min(x + 4, size.width), size.height / 2), paint);
    }
  }

  @override
  bool shouldRepaint(_DashPainter old) => old.color != color;
}

/// Rounded corners, the bottom edge a row of small round bumps (a wavy ticket edge).
class _TicketClipper extends CustomClipper<Path> {
  const _TicketClipper({required this.radius});

  final double radius;

  static const double _bump = 16;
  static const double _depth = 5;

  @override
  Path getClip(Size size) {
    final r = radius;
    final edge = size.height - _depth;
    final path = Path()
      ..moveTo(0, r)
      ..quadraticBezierTo(0, 0, r, 0)
      ..lineTo(size.width - r, 0)
      ..quadraticBezierTo(size.width, 0, size.width, r)
      ..lineTo(size.width, edge - r / 2)
      ..quadraticBezierTo(size.width, edge, size.width - r / 2, edge);
    final span = size.width - r;
    final count = math.max(1, (span / _bump).round());
    final step = span / count;
    for (var i = 0; i < count; i++) {
      final x = size.width - r / 2 - i * step;
      path.quadraticBezierTo(x - step / 2, size.height + _depth, x - step, edge);
    }
    return path
      ..quadraticBezierTo(0, edge, 0, edge - r / 2)
      ..close();
  }

  @override
  bool shouldReclip(_TicketClipper oldClipper) => oldClipper.radius != radius;
}

/// A pale blue note with an info icon, the [text] and an optional [picture] at the right (the rider).
class FreshNoteCard extends StatelessWidget {
  const FreshNoteCard({
    super.key,
    required this.text,
    this.picture,
    this.pictureSize = 88,
    this.icon = Icons.info_outline_rounded,
  });

  final String text;
  final Widget? picture;
  final double pictureSize;
  final IconData icon;

  @override
  Widget build(BuildContext context) => Container(
    padding: EdgeInsets.fromLTRB(14, 14, picture == null ? 14 : 6, 14),
    decoration: BoxDecoration(color: FreshCelebrationColors.noteBlue, borderRadius: BorderRadius.circular(20)),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: [
        ExcludeSemantics(child: Icon(icon, size: 26, color: FreshCelebrationColors.noteInk)),
        const SizedBox(width: 10),
        Expanded(
          child: Text(
            text,
            style: const TextStyle(
              fontFamily: AppFonts.body,
              fontFamilyFallback: FreshFonts.sansFallback,
              fontSize: 14,
              height: 1.4,
              color: FreshCelebrationColors.noteInk,
            ),
          ),
        ),
        if (picture != null) ...[
          const SizedBox(width: 6),
          SizedBox.square(dimension: pictureSize, child: picture),
        ],
      ],
    ),
  );
}

/// A full-width outlined pill (ink outline, 56 px) with an optional [icon] before the centred label and an arrow
/// at the right ("Continue shopping →"). The filled twin is [FreshArrowButton].
class FreshOutlineArrowButton extends StatelessWidget {
  const FreshOutlineArrowButton({super.key, required this.label, required this.onPressed, this.icon});

  final String label;
  final VoidCallback? onPressed;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final ink = illustratedColors(context).ink;
    return SizedBox(
      width: double.infinity,
      child: OutlinedButton(
        onPressed: onPressed,
        style: OutlinedButton.styleFrom(
          foregroundColor: ink,
          minimumSize: const Size.fromHeight(56),
          padding: const EdgeInsets.symmetric(horizontal: 20),
          shape: const StadiumBorder(),
          side: BorderSide(color: ink, width: 1.6),
        ),
        child: Row(
          children: [
            SizedBox(
              width: 24,
              child: icon == null ? null : ExcludeSemantics(child: Icon(icon, size: 24)),
            ),
            Expanded(
              child: Text(label, textAlign: TextAlign.center, maxLines: 1, overflow: TextOverflow.ellipsis),
            ),
            const ExcludeSemantics(child: Icon(Icons.arrow_forward_rounded, size: 24)),
          ],
        ),
      ),
    );
  }
}
