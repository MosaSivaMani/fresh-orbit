import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../api/models/catalog.dart';
import '../../state/catalog_state.dart';
import '../../widgets/commerce.dart';
import '../../widgets/states.dart';

/// Paged product grid for a query: skeleton while loading, error with retry, empty state, load more on scroll.
class ProductGrid extends ConsumerWidget {
  const ProductGrid({super.key, required this.query, this.empty});

  final ProductQuery query;
  final Widget? empty;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final provider = productListProvider(query);
    return AsyncValueView<ProductListState>(
      value: ref.watch(provider),
      onRetry: () => ref.invalidate(provider),
      loading: const GridSkeleton(),
      data: (list) {
        if (list.items.isEmpty) {
          return empty ??
              const EmptyState(
                slot: IllustrationSlot.noSearchResults,
                title: 'No products found',
                message: 'Try another category or fewer filters.',
              );
        }
        return NotificationListener<ScrollNotification>(
          onNotification: (n) {
            if (n.metrics.extentAfter < 400 && list.hasMore && !list.loadingMore) {
              ref.read(provider.notifier).loadMore().catchError((Object e) {
                if (context.mounted) showErrorSnack(context, e);
              });
            }
            return false;
          },
          child: RefreshIndicator(
            onRefresh: () => ref.refresh(provider.future),
            child: CustomScrollView(
              slivers: [
                SliverPadding(
                  padding: const EdgeInsets.all(FreshSpacing.s4),
                  sliver: SliverGrid.builder(
                    gridDelegate: productGridDelegate,
                    itemCount: list.items.length,
                    itemBuilder: (context, i) {
                      final p = list.items[i];
                      return ProductCard(product: p, onTap: () => context.push('/product/${p.slug}'));
                    },
                  ),
                ),
                if (list.loadingMore)
                  const SliverToBoxAdapter(
                    child: Padding(
                      padding: EdgeInsets.all(FreshSpacing.s4),
                      child: Center(child: CircularProgressIndicator(semanticsLabel: 'Loading more')),
                    ),
                  ),
              ],
            ),
          ),
        );
      },
    );
  }
}
