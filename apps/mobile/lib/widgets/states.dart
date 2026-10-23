// Loading skeletons, empty and error states, and an AsyncValue view that uses them.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';

import '../api/api_error.dart';

FreshColors freshColors(BuildContext context) => Theme.of(context).extension<FreshColors>()!;

class EmptyState extends StatelessWidget {
  const EmptyState({super.key, required this.icon, required this.title, this.message, this.action});

  final IconData icon;
  final String title;
  final String? message;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(FreshSpacing.s6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ExcludeSemantics(child: Icon(icon, size: 56, color: freshColors(context).textMuted)),
            const SizedBox(height: FreshSpacing.s4),
            Text(title, style: theme.textTheme.titleMedium, textAlign: TextAlign.center),
            if (message != null) ...[
              const SizedBox(height: FreshSpacing.s2),
              Text(message!, style: theme.textTheme.bodyMedium, textAlign: TextAlign.center),
            ],
            if (action != null) ...[const SizedBox(height: FreshSpacing.s5), action!],
          ],
        ),
      ),
    );
  }
}

/// Failed load: the server's sentence, its trace id ("Reference: …") and a retry button. Announced to
/// screen readers as a live region.
class ErrorState extends StatelessWidget {
  const ErrorState({super.key, required this.error, this.onRetry, this.compact = false});

  final Object error;
  final VoidCallback? onRetry;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    final apiError = ApiError.from(error);
    final theme = Theme.of(context);
    final colors = freshColors(context);
    final body = Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        if (!compact) ExcludeSemantics(child: Icon(Icons.cloud_off_outlined, size: 56, color: colors.dangerText)),
        if (!compact) const SizedBox(height: FreshSpacing.s4),
        Text(
          apiError.status == 0 && apiError.code == ApiError.networkError ? 'You are offline' : 'Could not load this',
          style: theme.textTheme.titleMedium,
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: FreshSpacing.s2),
        Text(apiError.message, style: theme.textTheme.bodyMedium, textAlign: TextAlign.center),
        if (apiError.traceId != null) ...[
          const SizedBox(height: FreshSpacing.s2),
          SelectableText(
            'Reference: ${apiError.traceId}',
            style: theme.textTheme.bodySmall?.copyWith(color: colors.textMuted),
            textAlign: TextAlign.center,
          ),
        ],
        if (onRetry != null) ...[
          const SizedBox(height: FreshSpacing.s4),
          OutlinedButton.icon(onPressed: onRetry, icon: const Icon(Icons.refresh), label: const Text('Try again')),
        ],
      ],
    );
    return Semantics(
      liveRegion: true,
      container: true,
      child: Center(child: SingleChildScrollView(padding: const EdgeInsets.all(FreshSpacing.s6), child: body)),
    );
  }
}

/// Grey placeholder block. Hidden from screen readers; the container announces "Loading".
class SkeletonBox extends StatelessWidget {
  const SkeletonBox({super.key, this.width, required this.height, this.radius = FreshRadius.md});

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
  Widget build(BuildContext context) =>
      Semantics(label: label, liveRegion: true, container: true, child: ExcludeSemantics(child: child));
}

class ListSkeleton extends StatelessWidget {
  const ListSkeleton({super.key, this.rows = 6, this.rowHeight = 72});

  final int rows;
  final double rowHeight;

  @override
  Widget build(BuildContext context) => LoadingSemantics(
        child: ListView.separated(
          physics: const NeverScrollableScrollPhysics(),
          padding: const EdgeInsets.all(FreshSpacing.s4),
          itemCount: rows,
          separatorBuilder: (_, _) => const SizedBox(height: FreshSpacing.s3),
          itemBuilder: (_, _) => Row(
            children: [
              SkeletonBox(width: rowHeight, height: rowHeight),
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
          padding: const EdgeInsets.all(FreshSpacing.s4),
          gridDelegate: productGridDelegate,
          itemCount: items,
          itemBuilder: (_, _) => const Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(child: SkeletonBox(height: double.infinity, radius: FreshRadius.lg)),
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
  mainAxisSpacing: FreshSpacing.s3,
  crossAxisSpacing: FreshSpacing.s3,
  childAspectRatio: 0.58,
);

/// Renders an AsyncValue with the standard loading, error (with retry) and data states. While refreshing with
/// data present, the data stays on screen.
class AsyncValueView<T> extends StatelessWidget {
  const AsyncValueView({
    super.key,
    required this.value,
    required this.data,
    this.loading,
    this.onRetry,
  });

  final AsyncValue<T> value;
  final Widget Function(T data) data;
  final Widget? loading;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    final v = value;
    if (v.hasValue && !(v.hasError && !v.isLoading && !v.isRefreshing)) return data(v.requireValue);
    if (v.hasError && !v.isLoading) return ErrorState(error: v.error!, onRetry: onRetry);
    return loading ?? const ListSkeleton();
  }
}

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
        padding: const EdgeInsets.all(FreshSpacing.s3),
        decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(FreshRadius.md)),
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

/// Shows an ApiError (or any error) as a snack bar with the server's sentence.
void showErrorSnack(BuildContext context, Object error) {
  final e = ApiError.from(error);
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(e.message)));
}

void showInfoSnack(BuildContext context, String text) {
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(text)));
}
