import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../api/models/account.dart';
import '../../state/account_state.dart';
import '../../widgets/commerce.dart';
import '../../widgets/states.dart';

class WishlistScreen extends ConsumerWidget {
  const WishlistScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final wishlist = ref.watch(wishlistProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Wishlist')),
      body: RefreshIndicator(
        onRefresh: () => ref.refresh(wishlistProvider.future),
        child: AsyncValueView<List<WishlistEntry>>(
          value: wishlist,
          onRetry: () => ref.invalidate(wishlistProvider),
          loading: const GridSkeleton(),
          data: (items) {
            if (items.isEmpty) {
              return EmptyState(
                slot: IllustrationSlot.emptyWishlist,
                title: 'Your wishlist is empty',
                message: 'Tap the heart on a product to save it for later.',
                action: FilledButton(onPressed: () => context.go('/shop'), child: const Text('Browse fish')),
              );
            }
            final withProduct = items.where((e) => e.product != null).toList();
            final idsOnly = items.where((e) => e.product == null).toList();
            return CustomScrollView(
              slivers: [
                if (withProduct.isNotEmpty)
                  SliverPadding(
                    padding: const EdgeInsets.all(FreshSpacing.s4),
                    sliver: SliverGrid.builder(
                      gridDelegate: productGridDelegate,
                      itemCount: withProduct.length,
                      itemBuilder: (context, i) {
                        final p = withProduct[i].product!;
                        return ProductCard(product: p, onTap: () => context.push('/product/${p.slug}'));
                      },
                    ),
                  ),
                // The Customers module currently returns product ids only; show them with a remove action
                // until the wishlist endpoint returns product summaries (docs/api §4.2).
                SliverList.list(
                  children: [
                    for (final e in idsOnly)
                      ListTile(
                        leading: const Icon(Icons.favorite),
                        title: const Text('Saved product'),
                        subtitle: const Text('Details will appear when the catalog returns them.'),
                        trailing: IconButton(
                          tooltip: 'Remove from wishlist',
                          icon: const Icon(Icons.delete_outline),
                          onPressed: () =>
                              ref.read(wishlistProvider.notifier).toggle(e.productId).catchError((Object err) {
                                if (context.mounted) showErrorSnack(context, err);
                              }),
                        ),
                      ),
                  ],
                ),
              ],
            );
          },
        ),
      ),
    );
  }
}
