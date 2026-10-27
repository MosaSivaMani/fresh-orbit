// Loading and notice parts: skeleton boxes and lists, the product grid layout, info banners, the info
// snack bar, badges, the primary button and the product photo frame.

import 'package:flutter/material.dart';
import 'package:fresh_theme/fresh_theme.dart';

/// Grey placeholder block. Hidden from screen readers; the container announces "Loading".
class SkeletonBox extends StatelessWidget {
  const SkeletonBox({super.key, this.width, required this.height, this.radius = 12});

  final double? width;
  final double height;
  final double radius;

  @override
  Widget build(BuildContext context) => Container(
    width: width,
    height: height,
    decoration: BoxDecoration(color: freshColors(context).muted, borderRadius: BorderRadius.circular(radius)),
  );
}

class LoadingSemantics extends StatelessWidget {
  const LoadingSemantics({super.key, required this.child, this.label = 'Loading'});

  final Widget child;
  final String label;

  @override
  Widget build(BuildContext context) => Semantics(
    label: label,
    liveRegion: true,
    container: true,
    child: ExcludeSemantics(child: child),
  );
}

class ListSkeleton extends StatelessWidget {
  const ListSkeleton({super.key, this.rows = 6, this.rowHeight = 72});

  final int rows;
  final double rowHeight;

  @override
  Widget build(BuildContext context) => LoadingSemantics(
    child: ListView.separated(
      physics: const NeverScrollableScrollPhysics(),
      padding: FreshLayout.list,
      itemCount: rows,
      separatorBuilder: (_, _) => const SizedBox(height: FreshLayout.cardGap),
      itemBuilder: (_, _) => Row(
        children: [
          SkeletonBox(width: rowHeight, height: rowHeight, radius: FreshLayout.tileRadius),
          const SizedBox(width: FreshSpacing.s3),
          const Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                SkeletonBox(height: 16),
                SizedBox(height: FreshSpacing.s2),
                SkeletonBox(width: 120, height: 14),
              ],
            ),
          ),
        ],
      ),
    ),
  );
}

class GridSkeleton extends StatelessWidget {
  const GridSkeleton({super.key, this.items = 6, this.shrinkWrap = false});

  final int items;
  final bool shrinkWrap;

  @override
  Widget build(BuildContext context) => LoadingSemantics(
    child: GridView.builder(
      shrinkWrap: shrinkWrap,
      physics: const NeverScrollableScrollPhysics(),
      padding: FreshLayout.list,
      gridDelegate: productGridDelegate,
      itemCount: items,
      itemBuilder: (_, _) => const Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            child: SkeletonBox(height: double.infinity, radius: FreshLayout.cardRadius),
          ),
          SizedBox(height: FreshSpacing.s2),
          SkeletonBox(height: 16),
          SizedBox(height: FreshSpacing.s1),
          SkeletonBox(width: 80, height: 14),
        ],
      ),
    ),
  );
}

/// Two columns on phones; more on wide screens. Tall enough for text scaling.
const productGridDelegate = SliverGridDelegateWithMaxCrossAxisExtent(
  maxCrossAxisExtent: 240,
  mainAxisSpacing: FreshLayout.cardGap,
  crossAxisSpacing: FreshLayout.cardGap,
  childAspectRatio: 0.72,
);

/// Inline banner for blockers, availability notes and server messages. Tone is shown with an icon and text,
/// never colour alone.
enum BannerTone { info, warning, danger, success }

class InfoBanner extends StatelessWidget {
  const InfoBanner({super.key, required this.text, this.tone = BannerTone.info, this.title});

  final String text;
  final String? title;
  final BannerTone tone;

  @override
  Widget build(BuildContext context) {
    final c = freshColors(context);
    final (bg, fg, icon) = switch (tone) {
      BannerTone.info => (c.infoSubtle, c.infoText, Icons.info_outline),
      BannerTone.warning => (c.warningSubtle, c.warningText, Icons.warning_amber_outlined),
      BannerTone.danger => (c.dangerSubtle, c.dangerText, Icons.error_outline),
      BannerTone.success => (c.successSubtle, c.successText, Icons.check_circle_outline),
    };
    final theme = Theme.of(context);
    return Semantics(
      container: true,
      child: Container(
        width: double.infinity,
        padding: const EdgeInsets.all(FreshLayout.cardGap),
        decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(FreshLayout.tileRadius)),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(icon, color: fg, size: 20),
            const SizedBox(width: FreshSpacing.s2),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  if (title != null) Text(title!, style: theme.textTheme.titleSmall?.copyWith(color: fg)),
                  Text(text, style: theme.textTheme.bodyMedium?.copyWith(color: fg)),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

void showInfoSnack(BuildContext context, String text) {
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(text)));
}

/// Small text badge. Tone is decoration; the text carries the meaning.
class FreshBadge extends StatelessWidget {
  const FreshBadge({super.key, required this.label, this.tone = BannerTone.info, this.highlight = false});

  final String label;
  final BannerTone tone;

  /// Warm sand (offers, bestsellers).
  final bool highlight;

  @override
  Widget build(BuildContext context) {
    final c = freshColors(context);
    final (bg, fg) = highlight
        ? (c.highlightSubtle, c.highlightText)
        : switch (tone) {
            BannerTone.info => (c.infoSubtle, c.infoText),
            BannerTone.warning => (c.warningSubtle, c.warningText),
            BannerTone.danger => (c.dangerSubtle, c.dangerText),
            BannerTone.success => (c.successSubtle, c.successText),
          };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: FreshSpacing.s2, vertical: FreshSpacing.s0_5),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(FreshRadius.full)),
      child: Text(label, style: Theme.of(context).textTheme.labelMedium?.copyWith(color: fg)),
    );
  }
}

/// Network image with a calm placeholder; decorative unless [semanticLabel] is given.
class ProductPhoto extends StatelessWidget {
  const ProductPhoto({
    super.key,
    required this.url,
    this.semanticLabel,
    this.aspectRatio = 1,
    this.radius = AppRadius.field,
    this.transparent = false,
  });

  final String? url;
  final String? semanticLabel;
  final double aspectRatio;
  final double radius;

  /// No placeholder fill: the caller's tint shows behind the fish icon.
  final bool transparent;

  @override
  Widget build(BuildContext context) {
    final ill = illustratedColors(context);
    final placeholder = Container(
      color: transparent ? null : ill.tealSoft,
      alignment: Alignment.center,
      child: Icon(Icons.set_meal_rounded, color: ill.tealDeep.withValues(alpha: 0.75), size: 44),
    );
    return AspectRatio(
      aspectRatio: aspectRatio,
      child: ClipRRect(
        borderRadius: BorderRadius.circular(radius),
        child: url == null
            ? ExcludeSemantics(child: placeholder)
            : Image.network(
                url!,
                fit: BoxFit.cover,
                semanticLabel: semanticLabel,
                excludeFromSemantics: semanticLabel == null,
                errorBuilder: (_, _, _) => placeholder,
                loadingBuilder: (context, child, progress) => progress == null ? child : placeholder,
              ),
      ),
    );
  }
}

/// A full-width primary button with a loading state that disables it (one action per tap).
class PrimaryButton extends StatelessWidget {
  const PrimaryButton({super.key, required this.label, required this.onPressed, this.loading = false, this.icon});

  final String label;
  final VoidCallback? onPressed;
  final bool loading;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final child = loading
        ? const SizedBox(
            width: 20,
            height: 20,
            child: CircularProgressIndicator(strokeWidth: 2, semanticsLabel: 'Working'),
          )
        : Text(label);
    return SizedBox(
      width: double.infinity,
      child: Semantics(
        button: true,
        label: loading ? '$label, working' : null,
        child: icon == null || loading
            ? FilledButton(onPressed: loading ? null : onPressed, child: child)
            : FilledButton.icon(onPressed: onPressed, icon: Icon(icon), label: child),
      ),
    );
  }
}
