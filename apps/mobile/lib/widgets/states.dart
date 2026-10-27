// Loading skeletons, illustrated empty and error states, and an AsyncValue view that uses them.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';

import '../api/api_error.dart';
import '../illustrations/illustration.dart';
import '../theme/app_theme.dart';
import 'bold.dart';
import 'motion.dart';
import 'screen.dart';

export '../illustrations/illustration.dart' show IllustrationSlot;

FreshColors freshColors(BuildContext context) => Theme.of(context).extension<FreshColors>()!;

/// Empty and error states sit inside the gutters, a section gap from the top and bottom.
const _statePadding = EdgeInsets.symmetric(horizontal: FreshLayout.gutter, vertical: FreshLayout.sectionGap);

/// Inline empty state: the slot's character in a ringed circle avatar, a title, an optional line and an
/// optional action. The illustration is decorative here because the title says the same thing. Full-screen
/// empty states use [ColorBlockScreen] instead.
class EmptyState extends StatelessWidget {
  const EmptyState({
    super.key,
    required this.slot,
    required this.title,
    this.message,
    this.action,
    this.illustrationSize = 180,
  });

  final IllustrationSlot slot;
  final String title;
  final String? message;
  final Widget? action;
  final double illustrationSize;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Center(
      child: SingleChildScrollView(
        padding: _statePadding,
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 420),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              EntranceMotion(
                child: AvatarCircle(slot: slot, size: illustrationSize),
              ),
              const SizedBox(height: FreshSpacing.s4),
              Semantics(
                header: true,
                child: Text(title, style: AppText.display(illustratedColors(context).ink, 24), textAlign: TextAlign.center),
              ),
              if (message != null) ...[
                const SizedBox(height: FreshSpacing.s2),
                Text(
                  message!,
                  style: theme.textTheme.bodyLarge?.copyWith(color: freshColors(context).textMuted),
                  textAlign: TextAlign.center,
                ),
              ],
              if (action != null) ...[const SizedBox(height: FreshSpacing.s5), action!],
            ],
          ),
        ),
      ),
    );
  }
}

/// The error illustration for an API error: offline for network failures, not found for 404, else generic.
IllustrationSlot slotForError(ApiError e) {
  if (e.status == 0 && (e.code == ApiError.networkError || e.code == 'TIMEOUT')) return IllustrationSlot.offline;
  if (e.status == 404) return IllustrationSlot.notFound;
  return IllustrationSlot.errorGeneric;
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
    final slot = slotForError(apiError);
    final title = switch (slot) {
      IllustrationSlot.offline => 'You are offline',
      IllustrationSlot.notFound => 'We could not find this',
      _ => 'Could not load this',
    };
    final body = Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        if (!compact) ...[
          EntranceMotion(child: AvatarCircle(slot: slot, size: 160)),
          const SizedBox(height: FreshSpacing.s4),
        ],
        Text(
          title,
          style: AppText.display(illustratedColors(context).ink, compact ? 17 : 24),
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
      child: Center(
        child: SingleChildScrollView(padding: _statePadding, child: body),
      ),
    );
  }
}

/// Unknown route (go_router errorBuilder).
class NotFoundScreen extends StatelessWidget {
  const NotFoundScreen({super.key, this.onHome});

  final VoidCallback? onHome;

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: PageHeader(text: 'Page', brushed: 'not found', onBack: onHome),
    body: SafeArea(
      child: EmptyState(
        slot: IllustrationSlot.notFound,
        title: 'This page swam away',
        message: 'The link may be old or mistyped.',
        action: onHome == null ? null : FilledButton(onPressed: onHome, child: const Text('Go to home')),
      ),
    ),
  );
}

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

/// Renders an AsyncValue with the standard loading, error (with retry) and data states. While refreshing with
/// data present, the data stays on screen.
class AsyncValueView<T> extends StatelessWidget {
  const AsyncValueView({super.key, required this.value, required this.data, this.loading, this.onRetry});

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
