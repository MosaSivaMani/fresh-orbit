// Bold blocks building blocks (D88, docs/design/mockups/app-bold-blocks.html): the colour-block screen with a
// rising paper sheet, the ringed circle avatar, the food-app tab bar, the floating View cart bar, category
// bubbles, filter pills, the compact − n + stepper, section boxes, slot buttons, segmented pills, menu rows and
// order progress steps. They lay out what they are given; none of them computes a business value (rule 4).
// There is no decorative looping motion: the owner removed the orbit halo and the ticker tape (D88).

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:fresh_theme/fresh_theme.dart';

import '../illustrations/illustration.dart';
import '../theme/app_theme.dart';
import 'heading.dart';
import 'motion.dart';

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

/// The part of a full-scene JPEG kept in view inside the circle (the mockup's `object-position`).
Alignment avatarFocus(IllustrationSlot slot) => switch (slot) {
  IllustrationSlot.onboardingFresh => const Alignment(0.1, -0.56),
  IllustrationSlot.onboardingCleaned => const Alignment(0, -0.44),
  IllustrationSlot.onboardingDelivery => const Alignment(-0.1, -0.44),
  IllustrationSlot.locationPick => const Alignment(0, -0.2),
  IllustrationSlot.notServiceable => const Alignment(-0.2, -0.44),
  IllustrationSlot.emptyOrders => const Alignment(0, -0.44),
  IllustrationSlot.emptyCart ||
  IllustrationSlot.emptyWishlist ||
  IllustrationSlot.emptyNotifications ||
  IllustrationSlot.emptyAddresses => const Alignment(0, -0.1),
  _ => const Alignment(0, -0.3),
};

/// A character in a big circle: paper border, thin ink ring outside it, card shadow. A JPEG scene is cropped
/// inside the circle around [focus] (default [avatarFocus]); it is never shown as a rectangle.
class AvatarCircle extends StatelessWidget {
  const AvatarCircle({
    super.key,
    required this.slot,
    this.size = 196,
    this.focus,
    this.semanticLabel,
    this.excludeFromSemantics = true,
  });

  final IllustrationSlot slot;
  final double size;
  final Alignment? focus;
  final String? semanticLabel;

  /// Decorative by default: the screen's title says the same.
  final bool excludeFromSemantics;

  /// Paper border and ink ring scale down for small peeks (bento tiles).
  double get _border => size >= 120 ? 5 : 3;
  double get _ring => size >= 120 ? 3 : 2;

  @override
  Widget build(BuildContext context) {
    final inner = size - 2 * _border;
    return Container(
      key: ValueKey('avatar-${slot.code}'),
      width: size,
      height: size,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        color: illustratedColors(context).paper,
        border: Border.all(color: BlockInk.paper, width: _border),
        boxShadow: [
          BoxShadow(color: BlockInk.ink, spreadRadius: _ring),
          if (size >= 120) ...cardShadow(context),
        ],
      ),
      child: ClipOval(
        child: Illustration(
          slot: slot,
          size: inner,
          inCircle: true,
          focus: focus ?? avatarFocus(slot),
          semanticLabel: semanticLabel,
          excludeFromSemantics: excludeFromSemantics,
        ),
      ),
    );
  }
}

enum AvatarSize { large, small }

/// Where a sticker sits on the avatar's edge.
class StickerSpot {
  const StickerSpot({required this.child, this.left, this.right, this.top = 40});

  final Widget child;
  final double? left;
  final double? right;
  final double top;
}

/// A full colour-block screen: a small caps eyebrow and a big headline on the block, the character in a big
/// ringed circle, and a paper sheet that rises from the bottom over the circle's lower half and holds
/// [children]. The whole screen scrolls when the content (or the keyboard) needs it. Use it as a Scaffold
/// body with `backgroundColor: tone.background(...)`.
class ColorBlockScreen extends StatelessWidget {
  const ColorBlockScreen({
    super.key,
    required this.tone,
    required this.heading,
    required this.slot,
    this.eyebrow,
    this.avatarSize = AvatarSize.large,
    this.sticker,
    this.onBack,
    this.backTooltip = 'Back',
    this.trailing,
    this.children = const [],
    this.sheetCrossAxis = CrossAxisAlignment.center,
  });

  final BlockTone tone;
  final String? eyebrow;

  /// The big heading: one or two lines, optionally a handwritten line and a brush swipe.
  final List<HeadingLine> heading;
  final IllustrationSlot slot;
  final AvatarSize avatarSize;
  final StickerSpot? sticker;

  /// Shows a round back button above the eyebrow.
  final VoidCallback? onBack;
  final String backTooltip;

  /// Shown at the top right (for example "Skip").
  final Widget? trailing;
  final List<Widget> children;
  final CrossAxisAlignment sheetCrossAxis;

  static const _stageTop = 14.0;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final fg = tone.foreground(c);
    final size = avatarSize == AvatarSize.large ? 196.0 : 150.0;
    final overlap = avatarSize == AvatarSize.large ? 86.0 : 64.0;
    final sheetTop = _stageTop + size - overlap;
    final bottomInset = MediaQuery.paddingOf(context).bottom;

    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: tone.overlay(c),
      child: ColoredBox(
        color: tone.background(c),
        child: SafeArea(
          bottom: false,
          child: CustomScrollView(
            slivers: [
              SliverToBoxAdapter(
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(20, 8, 20, 0),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      if (onBack != null || trailing != null)
                        Padding(
                          padding: const EdgeInsets.only(bottom: FreshSpacing.s2),
                          child: Row(
                            children: [
                              if (onBack != null)
                                RoundIconButton(
                                  icon: Icons.arrow_back_rounded,
                                  tooltip: backTooltip,
                                  onPressed: onBack,
                                ),
                              const Spacer(),
                              ?trailing,
                            ],
                          ),
                        ),
                      if (eyebrow != null)
                        Text(eyebrow!.toUpperCase(), style: AppText.eyebrow(fg.withValues(alpha: 0.8))),
                      const SizedBox(height: 6),
                      Heading(lines: heading, color: fg, brushColor: tone == BlockTone.mango ? BlockInk.paper : null),
                    ],
                  ),
                ),
              ),
              SliverFillRemaining(
                hasScrollBody: false,
                child: Stack(
                  children: [
                    // The sheet rises once into place (instantly with reduced motion).
                    Positioned.fill(
                      top: sheetTop,
                      child: EntranceMotion(
                        rise: 48,
                        child: DecoratedBox(
                          decoration: BoxDecoration(
                            color: c.paper,
                            borderRadius: const BorderRadius.vertical(top: Radius.circular(AppRadius.sheet)),
                          ),
                        ),
                      ),
                    ),
                    Column(
                      children: [
                        SizedBox(
                          height: _stageTop + size,
                          child: Stack(
                            clipBehavior: Clip.none,
                            children: [
                              Positioned(
                                top: _stageTop,
                                left: 0,
                                right: 0,
                                child: Center(
                                  child: AvatarCircle(slot: slot, size: size),
                                ),
                              ),
                              if (sticker != null)
                                Positioned(
                                  top: sticker!.top,
                                  left: sticker!.left,
                                  right: sticker!.right,
                                  child: sticker!.child,
                                ),
                            ],
                          ),
                        ),
                        EntranceMotion(
                          rise: 48,
                          child: Padding(
                            padding: EdgeInsets.fromLTRB(20, 14, 20, 18 + bottomInset),
                            child: Column(
                              crossAxisAlignment: sheetCrossAxis,
                              children: [
                                for (var i = 0; i < children.length; i++) ...[
                                  if (i > 0) const SizedBox(height: 12),
                                  children[i],
                                ],
                              ],
                            ),
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
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

/// One tab of [FoodTabBar].
class FoodTab {
  const FoodTab(this.label, this.icon, {this.badge});

  final String label;
  final IconData icon;

  /// A count shown on a coral pip (the cart).
  final int? badge;
}

/// The floating bottom menu (D176, home preview `.nav`): a rounded paper bar 58 tall, [FreshLayout.gutter] from
/// both screen edges, icon over label; the active tab has a pale teal pill behind its icon and a teal label; the
/// cart shows its line count on a coral pip. The space around the bar is the page colour.
class FoodTabBar extends StatelessWidget {
  const FoodTabBar({super.key, required this.tabs, required this.currentIndex, required this.onSelected});

  final List<FoodTab> tabs;
  final int currentIndex;
  final ValueChanged<int> onSelected;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final inset = MediaQuery.paddingOf(context).bottom;
    // The bar floats over the home indicator area the way the preview's does, never closer than 8 to the edge.
    final bottom = inset > 28 ? inset - 20 : 8.0;
    return Padding(
      padding: EdgeInsets.fromLTRB(FreshLayout.gutter, 0, FreshLayout.gutter, bottom),
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: c.paper,
          borderRadius: BorderRadius.circular(FreshLayout.navRadius),
          boxShadow: [BoxShadow(color: c.softShadow, blurRadius: 16, offset: const Offset(0, 4))],
        ),
        child: ConstrainedBox(
          constraints: const BoxConstraints(minHeight: 58),
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 4),
            child: Row(
              children: [
                for (var i = 0; i < tabs.length; i++)
                  Expanded(
                    child: _FoodTabItem(tab: tabs[i], active: i == currentIndex, onTap: () => onSelected(i)),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _FoodTabItem extends StatelessWidget {
  const _FoodTabItem({required this.tab, required this.active, required this.onTap});

  final FoodTab tab;
  final bool active;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final color = active ? c.tealText : c.muted;
    final badge = tab.badge ?? 0;
    final label = badge > 0 ? '${tab.label}, $badge ${badge == 1 ? 'item' : 'items'}' : tab.label;
    return Semantics(
      key: ValueKey('tab-${tab.label}'),
      button: true,
      selected: active,
      label: label,
      excludeSemantics: true,
      child: InkWell(
        borderRadius: BorderRadius.circular(16),
        onTap: onTap,
        child: ConstrainedBox(
          constraints: const BoxConstraints(minHeight: 50),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              SizedBox(
                width: 48,
                height: 28,
                child: Stack(
                  clipBehavior: Clip.none,
                  alignment: Alignment.center,
                  children: [
                    if (active)
                      Container(
                        width: 48,
                        height: 28,
                        decoration: BoxDecoration(color: c.tealSoft, borderRadius: BorderRadius.circular(14)),
                      ),
                    Icon(tab.icon, size: 22, color: color),
                    if (badge > 0)
                      Positioned(
                        top: -4,
                        right: 2,
                        child: Container(
                          constraints: const BoxConstraints(minWidth: 18),
                          height: 18,
                          padding: const EdgeInsets.symmetric(horizontal: 4),
                          decoration: BoxDecoration(
                            color: c.coral,
                            borderRadius: BorderRadius.circular(FreshLayout.pillRadius),
                            border: Border.all(color: c.paper, width: 2),
                          ),
                          alignment: Alignment.center,
                          child: Text(
                            '$badge',
                            style: TextStyle(
                              fontFamily: AppFonts.body,
                              fontSize: 10,
                              height: 1,
                              fontWeight: FontWeight.w800,
                              color: c.onCoral,
                            ),
                          ),
                        ),
                      ),
                  ],
                ),
              ),
              const SizedBox(height: 1),
              Text(
                tab.label,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  fontFamily: AppFonts.body,
                  fontSize: 11,
                  fontWeight: active ? FontWeight.w700 : FontWeight.w600,
                  color: color,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// The floating dark-teal cart bar above the menu while the cart has lines (D176, home preview `.cart`): 52 tall,
/// [FreshLayout.gutter] from both edges, "2 ITEMS ADDED", the server's amount, up to two line pictures and
/// "View cart". It shows only what it is given (rule 4).
class ViewCartBar extends StatelessWidget {
  const ViewCartBar({
    super.key,
    required this.count,
    required this.amount,
    required this.onTap,
    this.thumbnails = const [],
  });

  final int count;

  /// Already formatted from a server value (the cart's subtotal).
  final String amount;
  final VoidCallback onTap;

  /// Picture URLs of the first cart lines (null for a line without one); at most two are shown.
  final List<String?> thumbnails;

  /// The bar's colour (`#0C4A43`), the same in light and dark.
  static const Color background = Color(0xFF0C4A43);
  static const Color _caption = Color(0xFFBFE3E0);

  @override
  Widget build(BuildContext context) {
    final items = '$count ${count == 1 ? 'item' : 'items'}';
    final thumbs = thumbnails.take(2).toList();
    return Padding(
      padding: const EdgeInsets.fromLTRB(FreshLayout.gutter, 0, FreshLayout.gutter, 8),
      child: Semantics(
        button: true,
        label: '$items added, $amount. View cart',
        excludeSemantics: true,
        child: Material(
          key: const Key('viewCartBar'),
          color: background,
          borderRadius: BorderRadius.circular(FreshLayout.barRadius),
          elevation: 6,
          shadowColor: background.withValues(alpha: 0.6),
          child: InkWell(
            borderRadius: BorderRadius.circular(FreshLayout.barRadius),
            onTap: onTap,
            child: ConstrainedBox(
              constraints: const BoxConstraints(minHeight: 52),
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 6),
                child: Row(
                  children: [
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text(
                            '$items added'.toUpperCase(),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: AppText.eyebrow(_caption, size: 10.5).copyWith(letterSpacing: 0.84, height: 1.15),
                          ),
                          Text(
                            amount,
                            maxLines: 1,
                            style: const TextStyle(
                              fontFamily: AppFonts.body,
                              fontFamilyFallback: FreshFonts.sansFallback,
                              fontWeight: FontWeight.w800,
                              fontSize: 17,
                              height: 1.15,
                              color: BlockInk.paper,
                              fontFeatures: [FontFeature.tabularFigures()],
                            ),
                          ),
                        ],
                      ),
                    ),
                    if (thumbs.isNotEmpty) ...[
                      _Thumbs(urls: thumbs),
                      const SizedBox(width: 10),
                    ],
                    Container(
                      padding: const EdgeInsets.only(left: 12),
                      decoration: BoxDecoration(
                        border: Border(left: BorderSide(color: BlockInk.paper.withValues(alpha: 0.3))),
                      ),
                      child: const Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text(
                            'View cart',
                            style: TextStyle(
                              fontFamily: AppFonts.body,
                              fontFamilyFallback: FreshFonts.sansFallback,
                              fontWeight: FontWeight.w700,
                              fontSize: 15,
                              color: BlockInk.paper,
                            ),
                          ),
                          Icon(Icons.chevron_right_rounded, color: BlockInk.paper, size: 20),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// Up to two overlapping round line pictures with a paper ring; a soft circle when a line has no picture or it
/// fails to load.
class _Thumbs extends StatelessWidget {
  const _Thumbs({required this.urls});

  final List<String?> urls;

  @override
  Widget build(BuildContext context) {
    const size = 32.0;
    const soft = Color(0xFFFBE6DC);
    const blank = ColoredBox(color: soft);
    return ExcludeSemantics(
      child: SizedBox(
        width: size + (urls.length - 1) * (size - 8),
        height: size,
        child: Stack(
          children: [
            for (var i = 0; i < urls.length; i++)
              Positioned(
                left: i * (size - 8),
                child: Container(
                  width: size,
                  height: size,
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    color: soft,
                    border: Border.all(color: BlockInk.paper, width: 2),
                  ),
                  clipBehavior: Clip.antiAlias,
                  child: urls[i] == null
                      ? blank
                      : Image.network(
                          urls[i]!,
                          fit: BoxFit.cover,
                          excludeFromSemantics: true,
                          errorBuilder: (_, _, _) => blank,
                        ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

/// The soft tints used behind product pictures and category bubbles, in turn.
List<Color> softTints(IllustratedColors c) => [c.tealSoft, c.coralSoft, c.mangoSoft, c.iceSoft];

/// A round category bubble with a label under it; the selected one has an ink ring and an offset shadow.
class CategoryBubble extends StatelessWidget {
  const CategoryBubble({
    super.key,
    required this.label,
    required this.tint,
    required this.selected,
    required this.onTap,
    this.icon = Icons.set_meal_rounded,
    this.imageUrl,
  });

  final String label;
  final Color tint;
  final bool selected;
  final VoidCallback onTap;
  final IconData icon;
  final String? imageUrl;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    return Semantics(
      button: true,
      selected: selected,
      label: label,
      excludeSemantics: true,
      child: InkWell(
        borderRadius: BorderRadius.circular(16),
        onTap: onTap,
        child: SizedBox(
          width: 64,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(
                width: 54,
                height: 54,
                decoration: BoxDecoration(
                  color: tint,
                  shape: BoxShape.circle,
                  border: Border.all(color: selected ? c.ink : Colors.transparent, width: 2),
                  boxShadow: selected ? [BoxShadow(color: c.ink, offset: const Offset(2, 2))] : null,
                ),
                clipBehavior: Clip.antiAlias,
                child: imageUrl == null
                    ? Icon(icon, color: c.tealDeep, size: 28)
                    : Image.network(
                        imageUrl!,
                        fit: BoxFit.cover,
                        excludeFromSemantics: true,
                        errorBuilder: (_, _, _) => Icon(icon, color: c.tealDeep, size: 28),
                      ),
              ),
              const SizedBox(height: 4),
              Text(
                label,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                textAlign: TextAlign.center,
                style: TextStyle(fontFamily: AppFonts.body, fontSize: 11, fontWeight: FontWeight.w700, color: c.ink),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// A small square-cornered filter pill (Sort, Wild caught, In stock). Selected pills turn ink.
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
          borderRadius: BorderRadius.circular(10),
          side: BorderSide(color: selected ? c.ink : c.line),
        ),
        child: InkWell(
          borderRadius: BorderRadius.circular(10),
          onTap: onTap,
          child: ConstrainedBox(
            constraints: const BoxConstraints(minHeight: 36),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
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

/// A compact teal-outlined − n + stepper. The label is the server's quantity, formatted; − and + only ask
/// the caller to propose a new quantity (the server answers). [filled] is the teal version used on product
/// pictures.
class CompactStepper extends StatelessWidget {
  const CompactStepper({
    super.key,
    required this.label,
    required this.itemName,
    required this.onDecrease,
    required this.onIncrease,
    this.pending = false,
    this.filled = false,
    this.large = false,
  });

  final String label;
  final String itemName;
  final VoidCallback? onDecrease;
  final VoidCallback? onIncrease;
  final bool pending;
  final bool filled;
  final bool large;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final fg = filled ? c.onTeal : c.tealDeep;
    final textStyle = TextStyle(
      fontFamily: AppFonts.body,
      fontWeight: FontWeight.w800,
      fontSize: large ? 14 : 12.5,
      color: fg,
      fontFeatures: const [FontFeature.tabularFigures()],
    );
    Widget button(IconData icon, String tooltip, VoidCallback? onPressed) => Tooltip(
      message: tooltip,
      child: Semantics(
        button: true,
        enabled: onPressed != null && !pending,
        label: tooltip,
        excludeSemantics: true,
        child: InkResponse(
          onTap: pending ? null : onPressed,
          radius: 22,
          child: SizedBox(
            width: large ? 44 : 34,
            height: large ? 44 : 34,
            child: Icon(icon, size: large ? 17 : 15, color: onPressed == null ? fg.withValues(alpha: 0.4) : fg),
          ),
        ),
      ),
    );
    return Semantics(
      container: true,
      label: 'Quantity of $itemName',
      child: Container(
        decoration: BoxDecoration(
          color: filled ? c.tealText : c.paper,
          border: Border.all(color: c.tealText, width: 1.5),
          borderRadius: BorderRadius.circular(large ? 14 : 10),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            button(Icons.remove_rounded, 'Decrease quantity of $itemName', onDecrease),
            Semantics(
              liveRegion: true,
              child: pending
                  ? SizedBox(
                      width: 16,
                      height: 16,
                      child: CircularProgressIndicator(strokeWidth: 2, color: fg, semanticsLabel: 'Updating'),
                    )
                  : Text(label, style: textStyle),
            ),
            button(Icons.add_rounded, 'Increase quantity of $itemName', onIncrease),
          ],
        ),
      ),
    );
  }
}

/// A paper box with the card shadow (`.box`).
class SectionBox extends StatelessWidget {
  const SectionBox({super.key, required this.child, this.padding = const EdgeInsets.all(12), this.color});

  final Widget child;
  final EdgeInsetsGeometry padding;
  final Color? color;

  @override
  Widget build(BuildContext context) => Container(
    width: double.infinity,
    // The shadow only; the colour is a Material so list tiles and ink splashes inside paint on it.
    decoration: BoxDecoration(
      borderRadius: BorderRadius.circular(18),
      boxShadow: color == null ? cardShadow(context) : null,
    ),
    child: Material(
      color: color ?? illustratedColors(context).paper,
      borderRadius: BorderRadius.circular(18),
      child: Padding(padding: padding, child: child),
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
    padding: const EdgeInsets.fromLTRB(16, 14, 16, 6),
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
          borderRadius: BorderRadius.circular(11),
          side: BorderSide(color: selected ? c.ink : c.line, width: 1.5),
        ),
        child: InkWell(
          borderRadius: BorderRadius.circular(11),
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

/// A paper card of menu rows separated by hairlines (`.menu`).
class MenuCard extends StatelessWidget {
  const MenuCard({super.key, required this.children});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    return Container(
      margin: const EdgeInsets.symmetric(horizontal: 12),
      decoration: BoxDecoration(
        color: c.paper,
        borderRadius: BorderRadius.circular(18),
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
          padding: const EdgeInsets.fromLTRB(12, 10, 12, 12),
          child: Row(
            children: [
              if (leading != null) ...[leading!, const SizedBox(width: 10)],
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
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
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
