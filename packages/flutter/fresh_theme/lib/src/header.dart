// The frame of every app screen in the home theme (D181): the page header with round paper buttons and a
// title whose key word sits on the yellow brush, the back button and the small count pip.

import 'package:flutter/material.dart';
import 'package:fresh_theme/fresh_theme.dart';

/// The home's round paper button (search, saved, back, filters) in the app's colours.
class PaperCircleButton extends StatelessWidget {
  const PaperCircleButton({super.key, required this.icon, required this.label, required this.onTap, this.badge});

  final IconData icon;
  final String label;
  final VoidCallback? onTap;
  final Widget? badge;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    return FreshCircleButton(
      icon: icon,
      label: label,
      onTap: onTap,
      background: c.paper,
      foreground: c.ink,
      shadowColor: c.softShadow,
      badge: badge,
    );
  }
}

/// Back: pops when there is a page below, else goes home (a screen opened from a link).
class BackCircleButton extends StatelessWidget {
  const BackCircleButton({super.key, this.onTap});

  /// The app's back action (its router pops, or goes home when nothing is below). The app sets it once where it
  /// builds its router; without it the button pops the nearest navigator when it can.
  static void Function(BuildContext context)? defaultBack;

  /// Replaces the default back action.
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) => PaperCircleButton(
    key: const Key('pageBack'),
    icon: Icons.arrow_back_rounded,
    label: 'Back',
    onTap:
        onTap ??
        () {
          final back = defaultBack;
          if (back != null) {
            back(context);
          } else {
            Navigator.maybePop(context);
          }
        },
  );
}

/// A small coral count on a header button (active filters, unread alerts).
class CountPip extends StatelessWidget {
  const CountPip(this.count, {super.key});

  final int count;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    return ExcludeSemantics(
      child: Container(
        constraints: const BoxConstraints(minWidth: 18),
        height: 18,
        padding: const EdgeInsets.symmetric(horizontal: 4),
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: c.coral,
          borderRadius: BorderRadius.circular(FreshLayout.pillRadius),
          border: Border.all(color: c.paper, width: 2),
        ),
        child: Text(
          '$count',
          style: TextStyle(
            fontFamily: AppFonts.body,
            fontSize: 10,
            height: 1,
            fontWeight: FontWeight.w800,
            color: c.onCoral,
          ),
        ),
      ),
    );
  }
}

/// The header of an app screen (use it as `Scaffold.appBar`): a back button unless [back] is false, the title,
/// and [actions] (usually [PaperCircleButton]s). The title is "[text] *[brushed]*" with the key word on the brush,
/// or, with [PageHeader.plain], one plain Baloo line (a product's name).
class PageHeader extends StatelessWidget implements PreferredSizeWidget {
  const PageHeader({
    super.key,
    required String this.text,
    required String this.brushed,
    this.trailing,
    this.back = true,
    this.onBack,
    this.actions = const [],
  }) : plain = null;

  const PageHeader.plain(
    String this.plain, {
    super.key,
    this.back = true,
    this.onBack,
    this.actions = const [],
  }) : text = null,
       brushed = null,
       trailing = null;

  final String? text;
  final String? brushed;
  final String? trailing;
  final String? plain;
  final bool back;
  final VoidCallback? onBack;
  final List<Widget> actions;

  @override
  Size get preferredSize => const Size.fromHeight(FreshLayout.headerHeight);

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final Widget title = plain != null && plain!.isEmpty
        ? const SizedBox.shrink()
        : plain != null
        ? Semantics(
            header: true,
            child: Text(
              plain!,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: AppText.display(c.ink, 20, height: 1.1),
            ),
          )
        : PageTitle(text: text!, brushed: brushed!, trailing: trailing);
    return FreshPageHeader(
      key: const Key('pageHeader'),
      leading: back ? BackCircleButton(onTap: onBack) : null,
      title: title,
      actions: actions,
    );
  }
}
