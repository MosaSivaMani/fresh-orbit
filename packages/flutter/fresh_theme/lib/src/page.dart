// The parts every app screen shares with the home (D181): the scroll behaviour that never bounces (D182), the
// screen header with round paper buttons, the paper card with the small-card radius (D189) and the 3:2 picture
// frame (D193). Colours, shadows and text styles are passed in by the app; nothing here computes a business value.

import 'package:flutter/material.dart';

import 'layout.dart';

/// Scrolling that stands still at the ends on every platform (D182): clamping physics, no bounce. Set it once on
/// the app (`MaterialApp.scrollBehavior`) so every list and page follows it.
class FreshScrollBehavior extends MaterialScrollBehavior {
  const FreshScrollBehavior();

  @override
  ScrollPhysics getScrollPhysics(BuildContext context) => FreshLayout.physics;
}

/// A round paper button with a line icon and a soft shadow (the home's search and wishlist buttons):
/// [FreshLayout.iconButton] wide, named for screen readers and given a tooltip.
class FreshCircleButton extends StatelessWidget {
  const FreshCircleButton({
    super.key,
    required this.icon,
    required this.label,
    required this.onTap,
    required this.background,
    required this.foreground,
    this.shadowColor,
    this.size = FreshLayout.iconButton,
    this.badge,
  });

  final IconData icon;
  final String label;

  /// Null shows the button disabled.
  final VoidCallback? onTap;
  final Color background;
  final Color foreground;
  final Color? shadowColor;
  final double size;

  /// A small marker on the top-right edge (for example an active-filter count).
  final Widget? badge;

  @override
  Widget build(BuildContext context) {
    final button = Material(
      color: background,
      shape: const CircleBorder(),
      elevation: shadowColor == null ? 0 : 2,
      shadowColor: shadowColor,
      child: InkWell(
        customBorder: const CircleBorder(),
        onTap: onTap,
        child: SizedBox.square(
          dimension: size,
          child: Icon(icon, size: size * 0.46, color: onTap == null ? foreground.withValues(alpha: 0.4) : foreground),
        ),
      ),
    );
    return Semantics(
      button: true,
      enabled: onTap != null,
      label: label,
      excludeSemantics: true,
      child: Tooltip(
        message: label,
        child: badge == null
            ? button
            : Stack(
                clipBehavior: Clip.none,
                children: [
                  button,
                  Positioned(top: -2, right: -2, child: badge!),
                ],
              ),
      ),
    );
  }
}

/// The header of an app screen: an optional [leading] button (back), the [title] and [actions], on one row
/// [FreshLayout.headerHeight] high inside the gutters, below the status bar. When very large system text makes the
/// row taller than the header, the row is drawn smaller at the full width ([FreshFitWidth]), never cut off. Use it
/// as a `Scaffold.appBar`.
class FreshPageHeader extends StatelessWidget implements PreferredSizeWidget {
  const FreshPageHeader({
    super.key,
    required this.title,
    this.leading,
    this.actions = const [],
    this.background,
  });

  final Widget title;
  final Widget? leading;
  final List<Widget> actions;

  /// Defaults to the page colour (the scaffold background).
  final Color? background;

  @override
  Size get preferredSize => const Size.fromHeight(FreshLayout.headerHeight);

  @override
  Widget build(BuildContext context) {
    final row = Row(
      children: [
        if (leading != null) ...[leading!, const SizedBox(width: FreshLayout.cardGap)],
        Expanded(
          child: Align(alignment: Alignment.centerLeft, child: title),
        ),
        for (final action in actions) ...[const SizedBox(width: 8), action],
      ],
    );
    return ColoredBox(
      color: background ?? Theme.of(context).scaffoldBackgroundColor,
      child: SafeArea(
        bottom: false,
        child: SizedBox(
          height: FreshLayout.headerHeight,
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: FreshLayout.gutter, vertical: 6),
            child: Align(
              alignment: Alignment.centerLeft,
              child: FreshFitWidth(child: row),
            ),
          ),
        ),
      ),
    );
  }
}

/// A card with the small-card radius ([FreshLayout.cardRadius], D189), a colour, an optional shadow and padding.
/// With [onTap] the whole card is one ink-splashing tap target.
class FreshCard extends StatelessWidget {
  const FreshCard({
    super.key,
    required this.child,
    required this.color,
    this.shadow = const [],
    this.padding = const EdgeInsets.all(FreshLayout.cardGap),
    this.radius = FreshLayout.cardRadius,
    this.border,
    this.onTap,
  });

  final Widget child;
  final Color color;
  final List<BoxShadow> shadow;
  final EdgeInsetsGeometry padding;
  final double radius;
  final BorderSide? border;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final shape = RoundedRectangleBorder(
      borderRadius: BorderRadius.circular(radius),
      side: border ?? BorderSide.none,
    );
    final content = Padding(padding: padding, child: child);
    return DecoratedBox(
      decoration: BoxDecoration(borderRadius: BorderRadius.circular(radius), boxShadow: shadow),
      child: Material(
        color: color,
        shape: shape,
        clipBehavior: Clip.antiAlias,
        child: onTap == null ? content : InkWell(onTap: onTap, child: content),
      ),
    );
  }
}

/// A picture in the 3:2 frame of cards and banners (D193), rounded, on [color]. The child fills the frame; give
/// it a cover or contain fit so it is cropped or padded, never stretched.
class FreshPictureFrame extends StatelessWidget {
  const FreshPictureFrame({
    super.key,
    required this.child,
    this.color,
    this.borderRadius = const BorderRadius.all(Radius.circular(FreshLayout.cardRadius)),
  });

  final Widget child;
  final Color? color;
  final BorderRadius borderRadius;

  @override
  Widget build(BuildContext context) => AspectRatio(
    aspectRatio: FreshLayout.pictureAspect,
    child: ClipRRect(
      borderRadius: borderRadius,
      child: ColoredBox(
        color: color ?? Colors.transparent,
        child: SizedBox.expand(child: child),
      ),
    ),
  );
}
