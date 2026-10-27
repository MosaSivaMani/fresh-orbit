// Saved fish (D88): the coral block when nothing is saved; otherwise the grid of product cards with ADD.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../api/models/account.dart';
import '../../state/account_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/bold.dart';
import '../../widgets/heading.dart';
import '../../widgets/screen.dart';
import '../../widgets/states.dart';
import 'product_grid.dart';

class WishlistScreen extends ConsumerWidget {
  const WishlistScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final wishlist = ref.watch(wishlistProvider);
    if (wishlist.value?.isEmpty ?? false) {
      return Scaffold(
        backgroundColor: BlockTone.coral.background(illustratedColors(context)),
        body: ColorBlockScreen(
          tone: BlockTone.coral,
          eyebrow: 'Saved',
          heading: const [HeadingLine('Nothing', script: true), HeadingLine('saved yet', brush: true)],
          slot: IllustrationSlot.emptyWishlist,
          onBack: context.canPop() ? () => context.pop() : null,
          children: [
            const SheetTitle('Tap the ♥ on any fish', header: true),
            const SheetNote('It waits here for next time.'),
            SizedBox(
              width: double.infinity,
              child: FilledButton(onPressed: () => context.go('/shop'), child: const Text('Browse fish')),
            ),
          ],
        ),
      );
    }
    return Scaffold(
      appBar: const PageHeader(text: 'Saved', brushed: 'fish'),
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
                    padding: FreshLayout.list,
                    sliver: SliverGrid.builder(
                      gridDelegate: productGridDelegate,
                      itemCount: withProduct.length,
                      itemBuilder: (context, i) {
                        return ShopProductCard(product: withProduct[i].product!, index: i);
                      },
                    ),
                  ),
                // The server sends no product for an entry that is no longer on the storefront; it can be removed.
                SliverPadding(
                  padding: FreshLayout.page,
                  sliver: SliverList.list(
                  children: [
                    for (final e in idsOnly)
                      ListTile(
                        key: ValueKey('wishlistGone-${e.productId}'),
                        leading: const Icon(Icons.favorite),
                        title: const Text('No longer sold'),
                        subtitle: const Text('This fish is not on the shop right now.'),
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
                ),
              ],
            );
          },
        ),
      ),
    );
  }
}
