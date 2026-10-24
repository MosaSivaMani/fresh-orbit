import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../api/api_error.dart';
import '../../api/models/cart.dart';
import '../../api/models/catalog.dart';
import '../../core/format.dart';
import '../../state/cart_state.dart';
import '../../state/catalog_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/bold.dart';
import '../../widgets/commerce.dart';
import '../../widgets/states.dart';

/// Paged product grid for a query: skeleton while loading, error with retry, empty state, load more on scroll.
class ProductGrid extends ConsumerWidget {
  const ProductGrid({super.key, required this.query, this.empty, this.header});

  final ProductQuery query;
  final Widget? empty;

  /// Scrolls with the grid (search bar, category bubbles, filter pills).
  final Widget? header;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final provider = productListProvider(query);
    final head = header;
    Widget withHeader(Widget body) => head == null
        ? body
        : CustomScrollView(
            slivers: [
              SliverToBoxAdapter(child: head),
              SliverFillRemaining(hasScrollBody: false, child: body),
            ],
          );
    return AsyncValueView<ProductListState>(
      value: ref.watch(provider),
      onRetry: () => ref.invalidate(provider),
      loading: withHeader(const GridSkeleton(shrinkWrap: true)),
      data: (list) {
        if (list.items.isEmpty) {
          return withHeader(
            empty ??
                const EmptyState(
                  slot: IllustrationSlot.noSearchResults,
                  title: 'No products found',
                  message: 'Try another category or fewer filters.',
                ),
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
                if (head != null) SliverToBoxAdapter(child: head),
                SliverPadding(
                  padding: const EdgeInsets.fromLTRB(12, 10, 12, FreshSpacing.s4),
                  sliver: SliverGrid.builder(
                    gridDelegate: productGridDelegate,
                    itemCount: list.items.length,
                    itemBuilder: (context, i) => ShopProductCard(product: list.items[i], index: i),
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

/// A [ProductCard] wired to the cart: ADD adds the first orderable size with its default cut at the server's
/// minimum; once the product has one cart line, the button is that line's − n + stepper; with several lines
/// (different cuts) it opens the cart. Every amount stays the server's.
class ShopProductCard extends ConsumerStatefulWidget {
  const ShopProductCard({super.key, required this.product, this.index = 0});

  final ProductSummary product;

  /// Position in the grid, for the picture tint.
  final int index;

  @override
  ConsumerState<ShopProductCard> createState() => _ShopProductCardState();
}

class _ShopProductCardState extends ConsumerState<ShopProductCard> {
  bool _pending = false;

  String get _path => '/product/${widget.product.slug}';

  Future<void> _run(Future<void> Function() op) async {
    if (_pending) return;
    setState(() => _pending = true);
    try {
      await op();
    } on ApiError catch (e) {
      if (!mounted) return;
      if (e.code == 'LOCATION_REQUIRED' || e.code == 'PINCODE_NOT_SERVICEABLE') {
        await context.push('/location?change=1');
      } else {
        showErrorSnack(context, e);
      }
    } catch (e) {
      if (mounted) showErrorSnack(context, e);
    } finally {
      if (mounted) setState(() => _pending = false);
    }
  }

  Future<void> _add() => _run(() async {
    final choice = await ref.read(cartProvider.notifier).quickAdd(widget.product.slug);
    if (!mounted) return;
    if (choice == null) {
      await context.push(_path);
      return;
    }
    final cut = choice.preparation == null ? '' : ', ${choice.preparation!.name}';
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          content: Text('Added ${formatQuantity(choice.quantity)}$cut.'),
          action: SnackBarAction(label: 'Change cut', onPressed: () => context.push(_path)),
        ),
      );
  });

  Future<void> _step(CartLine line, {required bool up}) =>
      _run(() => ref.read(cartProvider.notifier).stepLine(line, up: up));

  @override
  Widget build(BuildContext context) {
    final p = widget.product;
    final lines = ref.watch(cartProvider).value?.lines.where((l) => l.productId == p.id).toList() ?? const <CartLine>[];
    final canAdd = p.fromPrice != null && !p.locationRequired && (p.availability?.canAdd ?? true);
    final CardCartControl control = switch (lines.length) {
      0 => CardAdd(canAdd ? _add : null),
      1 => CardStepper(
        label: formatQuantity(lines.single.quantity),
        onDecrease: () => _step(lines.single, up: false),
        onIncrease: () => _step(lines.single, up: true),
      ),
      _ => CardInCart(lines: lines.length, onOpen: () => context.go('/cart')),
    };
    final tints = softTints(illustratedColors(context));
    return ProductCard(
      product: p,
      onTap: () => context.push(_path),
      control: control,
      pending: _pending,
      tint: tints[widget.index % tints.length],
    );
  }
}
