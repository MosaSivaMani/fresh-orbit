// Bold blocks parts that still live in the app (D88, docs/design/mockups/app-bold-blocks.html): the colour-block
// screen and the ringed circle avatar (they show the app's illustrations), the home's tab bar, View cart bar and
// category bubbles, and the compact − n + stepper (it reads the API's Quantity). The other shared parts live in
// `fresh_theme`. They lay out what they are given; none of them computes a business value (rule 4).
// There is no decorative looping motion: the owner removed the orbit halo and the ticker tape (D88).

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:fresh_theme/fresh_theme.dart';

import '../illustrations/illustration.dart';

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
    this.seaFloor = false,
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

  /// Ends the sheet with the sea floor ([FreshSeaFloor], D289): at the bottom of the screen when the sheet is
  /// short, after the content when it scrolls.
  final bool seaFloor;

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
                            padding: EdgeInsets.fromLTRB(20, 14, 20, seaFloor ? 18 : 18 + bottomInset),
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
                        if (seaFloor) ...[const Spacer(), const FreshSeaFloor()],
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
                    : FreshNetworkImage(
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
