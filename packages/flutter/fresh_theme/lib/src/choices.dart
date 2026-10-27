// Choice parts: an option tile for picking one of a few (a cut, a size). The chosen one is teal; a disabled one
// (no tap) is muted. Values arrive formatted.

import 'package:flutter/material.dart';
import 'package:fresh_theme/fresh_theme.dart';

/// One option: its [title] and a short [subtitle] (for example the store's charge for a cut). Radius 16 on the
/// grid; one button for screen readers, marked selected when [selected].
class OptionTile extends StatelessWidget {
  const OptionTile({super.key, required this.title, required this.subtitle, required this.selected, this.onTap, this.subtitleLines = 1});

  final String title;
  final String subtitle;
  final bool selected;

  /// Null = cannot be chosen here.
  final VoidCallback? onTap;

  /// Lines the subtitle may take (1 in a grid of tiles, more in a list).
  final int subtitleLines;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    return Semantics(
      button: true,
      selected: selected,
      enabled: onTap != null,
      label: '$title, $subtitle',
      excludeSemantics: true,
      child: Material(
        color: selected ? c.tealSoft : c.paper,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(FreshLayout.tileRadius),
          side: BorderSide(color: selected ? c.tealText : c.line, width: selected ? 2 : 1.5),
        ),
        child: InkWell(
          borderRadius: BorderRadius.circular(FreshLayout.tileRadius),
          onTap: onTap,
          child: Align(
            alignment: Alignment.centerLeft,
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: FreshLayout.cardGap, vertical: 6),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    title,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      fontFamily: AppFonts.body,
                      fontSize: 12.5,
                      fontWeight: FontWeight.w700,
                      color: onTap == null ? c.muted : c.ink,
                    ),
                  ),
                  Text(
                    subtitle,
                    maxLines: subtitleLines,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(fontFamily: AppFonts.body, fontSize: 10.5, fontWeight: FontWeight.w500, color: c.muted),
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
