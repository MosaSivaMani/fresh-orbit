// Illustrated empty and error states and an AsyncValue view that uses them. They stay in the app because they
// show the app's illustrations and read its API errors; loading skeletons and banners live in `fresh_theme`.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';

import '../api/api_error.dart';
import '../illustrations/illustration.dart';
import 'bold.dart';

export '../illustrations/illustration.dart' show IllustrationSlot;

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

/// Shows an ApiError (or any error) as a snack bar with the server's sentence.
void showErrorSnack(BuildContext context, Object error) {
  final e = ApiError.from(error);
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(e.message)));
}
