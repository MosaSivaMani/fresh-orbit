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
      appBar: AppBar(
        title: const ScriptTitle(text: 'Saved', script: 'fish'),
      ),
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
                        return ShopProductCard(product: withProduct[i].product!, index: i);
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
