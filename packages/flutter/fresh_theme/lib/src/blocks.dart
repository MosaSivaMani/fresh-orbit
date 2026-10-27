// The Bold blocks parts shared by the screens (D88, D181): block tones, sheet titles and notes, round icon
// buttons, page dots, filter pills, section boxes and titles, menu cards and rows, slot buttons, segmented
// pills, progress steps and the sticky action bar with its teal button. Values arrive formatted; nothing
// here computes a business value.

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:fresh_theme/fresh_theme.dart';

/// The four block colours. Coral = welcome, cart, saved; teal = home, tracking; mango = orders, sign-in;
/// ice = location, alerts.
enum BlockTone { coral, teal, mango, ice }

extension BlockToneColors on BlockTone {
  Color background(IllustratedColors c) => switch (this) {
    BlockTone.coral => c.coral,
    BlockTone.teal => c.tealText,
    BlockTone.mango => c.mango,
    BlockTone.ice => c.ice,
  };

  /// Text on the block: ink on coral, mango and ice; paper on teal (dark-ink in dark mode, for contrast).
  Color foreground(IllustratedColors c) => this == BlockTone.teal ? c.onTeal : BlockInk.ink;

  /// Status-bar icons that stay readable on the block.
  SystemUiOverlayStyle overlay(IllustratedColors c) =>
      foreground(c).computeLuminance() > 0.5 ? SystemUiOverlayStyle.light : SystemUiOverlayStyle.dark;
}

/// Where a sticker sits on the avatar's edge.
class StickerSpot {
  const StickerSpot({required this.child, this.left, this.right, this.top = 40});

  final Widget child;
  final double? left;
  final double? right;
  final double top;
}

/// The sheet heading under the avatar (Baloo 2 800, 24).
class SheetTitle extends StatelessWidget {
  const SheetTitle(this.text, {super.key, this.header = false});

  final String text;
  final bool header;

  @override
  Widget build(BuildContext context) => Semantics(
    header: header,
    child: Text(
      text,
      textAlign: TextAlign.center,
      style: AppText.display(illustratedColors(context).ink, 24, height: 1),
    ),
  );
}

/// Muted sheet line (13 px).
class SheetNote extends StatelessWidget {
  const SheetNote(this.text, {super.key, this.align = TextAlign.center});

  final String text;
  final TextAlign align;

  @override
  Widget build(BuildContext context) => Text(
    text,
    textAlign: align,
    style: Theme.of(context).textTheme.bodyMedium?.copyWith(color: illustratedColors(context).muted),
  );
}

/// A 36 px paper circle with a line icon, inside a 48 px tap target.
class RoundIconButton extends StatelessWidget {
  const RoundIconButton({super.key, required this.icon, required this.tooltip, required this.onPressed});

  final IconData icon;
  final String tooltip;
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    return Tooltip(
      message: tooltip,
      child: Semantics(
        button: true,
        label: tooltip,
        excludeSemantics: true,
        child: InkResponse(
          onTap: onPressed,
          radius: 24,
          child: SizedBox.square(
            dimension: 48,
            child: Center(
              child: Container(
                width: 36,
                height: 36,
                decoration: BoxDecoration(color: c.paper, shape: BoxShape.circle, boxShadow: cardShadow(context)),
                child: Icon(icon, size: 19, color: c.ink),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// Page dots: the current one is a 24 px ink pill.
class PageDots extends StatelessWidget {
  const PageDots({super.key, required this.count, required this.index});

  final int count;
  final int index;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    return Semantics(
      label: 'Page ${index + 1} of $count',
      child: ExcludeSemantics(
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            for (var i = 0; i < count; i++)
              Container(
                margin: const EdgeInsets.symmetric(horizontal: 3),
                width: i == index ? 24 : 7,
                height: 7,
                decoration: BoxDecoration(
                  color: i == index ? c.ink : c.line,
                  borderRadius: BorderRadius.circular(FreshRadius.full),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

/// A small round filter pill (Sort, Wild caught, In stock), the home's pill shape. Selected pills turn ink.
class FilterPill extends StatelessWidget {
  const FilterPill({super.key, required this.label, required this.onTap, this.selected = false, this.icon});

  final String label;
  final VoidCallback onTap;
  final bool selected;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final fg = selected ? c.onInk : c.ink;
    return Semantics(
      button: true,
      selected: selected,
      label: label,
      excludeSemantics: true,
      child: Material(
        color: selected ? c.ink : c.paper,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(FreshLayout.pillRadius),
          side: BorderSide(color: selected ? c.ink : c.line),
        ),
        child: InkWell(
          borderRadius: BorderRadius.circular(FreshLayout.pillRadius),
          onTap: onTap,
          child: ConstrainedBox(
            constraints: const BoxConstraints(minHeight: 36),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  if (icon != null) ...[Icon(icon, size: 15, color: fg), const SizedBox(width: 4)],
                  Text(
                    label,
                    style: TextStyle(fontFamily: AppFonts.body, fontSize: 12, fontWeight: FontWeight.w600, color: fg),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// A paper box with the card shadow (`.box`), the theme's [FreshCard].
class SectionBox extends StatelessWidget {
  const SectionBox({super.key, required this.child, this.padding = const EdgeInsets.all(12), this.color});

  final Widget child;
  final EdgeInsetsGeometry padding;
  final Color? color;

  @override
  Widget build(BuildContext context) => SizedBox(
    width: double.infinity,
    // The theme's small card (radius 20, D189); its colour is a Material, so list tiles and ink splashes paint on it.
    child: FreshCard(
      color: color ?? illustratedColors(context).paper,
      shadow: color == null ? cardShadow(context) : const [],
      padding: padding,
      child: child,
    ),
  );
}

/// A section heading inside a box (`.sect`: Baloo 2 800, 14).
class SectionTitle extends StatelessWidget {
  const SectionTitle(this.text, {super.key});

  final String text;

  @override
  Widget build(BuildContext context) =>
      Semantics(header: true, child: Text(text, style: AppText.display(illustratedColors(context).ink, 14.5)));
}

/// Small caps label above a menu (`.lbl`).
class MenuLabel extends StatelessWidget {
  const MenuLabel(this.text, {super.key});

  final String text;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.fromLTRB(FreshLayout.gutter + 4, 14, FreshLayout.gutter, 6),
    child: Semantics(
      header: true,
      child: Text(text.toUpperCase(), style: AppText.eyebrow(illustratedColors(context).muted, size: 11)),
    ),
  );
}

/// A delivery-slot button: day or time on top, a small second line; the chosen one is ink.
class SlotButton extends StatelessWidget {
  const SlotButton({
    super.key,
    required this.title,
    required this.subtitle,
    required this.selected,
    required this.onTap,
  });

  final String title;
  final String subtitle;
  final bool selected;

  /// Null when the slot cannot be booked.
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final enabled = onTap != null;
    final fg = selected ? c.onInk : (enabled ? c.ink : c.muted);
    return Semantics(
      button: true,
      selected: selected,
      enabled: enabled,
      label: '$title, $subtitle',
      excludeSemantics: true,
      child: Material(
        color: selected ? c.ink : (enabled ? c.paper : c.sand),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(FreshLayout.tileRadius),
          side: BorderSide(color: selected ? c.ink : c.line, width: 1.5),
        ),
        child: InkWell(
          borderRadius: BorderRadius.circular(FreshLayout.tileRadius),
          onTap: onTap,
          child: ConstrainedBox(
            constraints: const BoxConstraints(minHeight: 48, minWidth: 92),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    title,
                    textAlign: TextAlign.center,
                    style: TextStyle(fontFamily: AppFonts.body, fontSize: 12, fontWeight: FontWeight.w700, color: fg),
                  ),
                  Text(
                    subtitle,
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      fontFamily: AppFonts.body,
                      fontSize: 10.5,
                      fontWeight: FontWeight.w500,
                      color: selected ? c.onInk.withValues(alpha: 0.75) : c.muted,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// A segmented control of small pills on a page-tone track (`.seg`): EN / తెలుగు / हिं, Light / Auto / Dark.
class SegmentedPills<T> extends StatelessWidget {
  const SegmentedPills({
    super.key,
    required this.options,
    required this.selected,
    required this.onSelected,
    required this.semanticLabel,
  });

  final Map<T, String> options;
  final T selected;
  final ValueChanged<T> onSelected;
  final String semanticLabel;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    return Semantics(
      container: true,
      label: semanticLabel,
      child: Container(
        padding: const EdgeInsets.all(2),
        decoration: BoxDecoration(color: c.sand, borderRadius: BorderRadius.circular(10)),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            for (final e in options.entries)
              Semantics(
                button: true,
                selected: e.key == selected,
                label: e.value,
                excludeSemantics: true,
                child: InkWell(
                  borderRadius: BorderRadius.circular(8),
                  onTap: () => onSelected(e.key),
                  child: Container(
                    constraints: const BoxConstraints(minHeight: 36, minWidth: 40),
                    alignment: Alignment.center,
                    padding: const EdgeInsets.symmetric(horizontal: 9),
                    decoration: e.key == selected
                        ? BoxDecoration(
                            color: c.paper,
                            borderRadius: BorderRadius.circular(8),
                            boxShadow: const [BoxShadow(color: Color(0x1F000000), blurRadius: 3, offset: Offset(0, 1))],
                          )
                        : null,
                    child: Text(
                      e.value,
                      style: TextStyle(
                        fontFamily: AppFonts.body,
                        fontSize: 11.5,
                        fontWeight: FontWeight.w700,
                        color: c.ink,
                      ),
                    ),
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

/// A paper card of menu rows separated by hairlines (`.menu`), inside the gutters with the small-card radius.
class MenuCard extends StatelessWidget {
  const MenuCard({super.key, required this.children});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    return Container(
      margin: FreshLayout.page,
      decoration: BoxDecoration(
        color: c.paper,
        borderRadius: BorderRadius.circular(FreshLayout.cardRadius),
        boxShadow: cardShadow(context),
      ),
      clipBehavior: Clip.antiAlias,
      child: Column(
        children: [
          for (var i = 0; i < children.length; i++) ...[if (i > 0) Divider(height: 1, color: c.line), children[i]],
        ],
      ),
    );
  }
}

/// One menu row: teal line icon, title with an optional muted second line, and a trailing widget (chevron by
/// default when tappable).
class MenuRow extends StatelessWidget {
  const MenuRow({
    super.key,
    required this.icon,
    required this.title,
    this.subtitle,
    this.trailing,
    this.onTap,
    this.danger = false,
  });

  final IconData icon;
  final String title;
  final String? subtitle;
  final Widget? trailing;
  final VoidCallback? onTap;
  final bool danger;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final color = danger ? c.coralText : c.ink;
    final row = ConstrainedBox(
      constraints: const BoxConstraints(minHeight: 52),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
        child: Row(
          children: [
            Icon(icon, size: 19, color: danger ? c.coralText : c.tealText),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    title,
                    style: TextStyle(
                      fontFamily: AppFonts.body,
                      fontSize: 13.5,
                      fontWeight: FontWeight.w600,
                      color: color,
                    ),
                  ),
                  if (subtitle != null)
                    Text(
                      subtitle!,
                      style: TextStyle(
                        fontFamily: AppFonts.body,
                        fontSize: 11.5,
                        fontWeight: FontWeight.w500,
                        color: c.muted,
                      ),
                    ),
                ],
              ),
            ),
            const SizedBox(width: 8),
            trailing ??
                (onTap == null ? const SizedBox.shrink() : Icon(Icons.chevron_right_rounded, size: 18, color: c.muted)),
          ],
        ),
      ),
    );
    if (onTap == null) return MergeSemantics(child: row);
    return MergeSemantics(
      child: InkWell(onTap: onTap, child: row),
    );
  }
}

/// Order progress in four bars (Placed, Packed, On the way, Delivered); [reached] bars are teal.
class ProgressSteps extends StatelessWidget {
  const ProgressSteps({super.key, required this.labels, required this.reached});

  final List<String> labels;

  /// How many steps are done (0 to labels.length).
  final int reached;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    return Semantics(
      label: reached == 0 ? 'Not started' : 'Step $reached of ${labels.length}: ${labels[reached - 1]}',
      child: ExcludeSemantics(
        child: Row(
          children: [
            for (var i = 0; i < labels.length; i++)
              Expanded(
                child: Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 2),
                  child: Column(
                    children: [
                      Container(
                        height: 6,
                        decoration: BoxDecoration(
                          color: i < reached ? c.tealText : c.line,
                          borderRadius: BorderRadius.circular(999),
                        ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        labels[i],
                        textAlign: TextAlign.center,
                        style: TextStyle(
                          fontFamily: AppFonts.body,
                          fontSize: 10.5,
                          fontWeight: FontWeight.w600,
                          color: i < reached ? c.ink : c.muted,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

/// The sticky bottom bar of cart, checkout and product (`.paybar`): an optional left part and a big action.
class StickyActionBar extends StatelessWidget {
  const StickyActionBar({super.key, required this.action, this.leading});

  final Widget action;
  final Widget? leading;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    return DecoratedBox(
      decoration: BoxDecoration(
        color: c.paper,
        border: Border(top: BorderSide(color: c.line)),
      ),
      child: SafeArea(
        top: false,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(FreshLayout.gutter, 10, FreshLayout.gutter, 12),
          child: Row(
            children: [
              if (leading != null) ...[leading!, const SizedBox(width: FreshLayout.cardGap)],
              Expanded(child: action),
            ],
          ),
        ),
      ),
    );
  }
}

/// The teal action of the sticky bar: label on the left and, when given, an amount on the right (both from the
/// caller, already formatted from server values). Wraps a [FilledButton] so it is a real button.
class TealActionButton extends StatelessWidget {
  const TealActionButton({
    super.key,
    required this.label,
    required this.onPressed,
    this.amount,
    this.loading = false,
    this.semanticLabel,
  });

  final String label;
  final String? amount;
  final VoidCallback? onPressed;
  final bool loading;
  final String? semanticLabel;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final enabled = onPressed != null && !loading;
    final fg = enabled ? c.onTeal : c.muted;
    return Semantics(
      button: true,
      label: semanticLabel ?? (loading ? '$label, working' : null),
      excludeSemantics: semanticLabel != null || loading,
      child: FilledButton(
        style: FilledButton.styleFrom(
          backgroundColor: c.tealText,
          foregroundColor: c.onTeal,
          disabledBackgroundColor: c.line,
          disabledForegroundColor: c.muted,
          minimumSize: const Size(48, 50),
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 11),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(FreshLayout.barRadius)),
        ),
        onPressed: enabled ? onPressed : null,
        child: loading
            ? SizedBox(
                width: 20,
                height: 20,
                child: CircularProgressIndicator(strokeWidth: 2, color: fg, semanticsLabel: 'Working'),
              )
            : Row(
                children: [
                  Expanded(
                    child: Text(
                      label,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(fontFamily: AppFonts.body, fontWeight: FontWeight.w800, fontSize: 14, color: fg),
                    ),
                  ),
                  if (amount != null) Text(amount!, style: AppText.price(fg, size: 16)),
                ],
              ),
      ),
    );
  }
}
