// Browse state: home, categories, product lists with paging, product detail, search suggestions.
// Everything re-reads when the pincode changes, because prices and availability depend on it.

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/models/catalog.dart';
import 'core_providers.dart';
import 'location_state.dart';

final homeProvider = FutureProvider<HomeData>((ref) {
  ref.watch(pincodeProvider);
  return ref.read(catalogRepositoryProvider).home();
});

final categoriesProvider = FutureProvider<List<Category>>((ref) => ref.read(catalogRepositoryProvider).categories());

class ProductListState {
  const ProductListState({required this.items, required this.totalCount, required this.page, this.loadingMore = false});

  final List<ProductSummary> items;
  final int totalCount;
  final int page;
  final bool loadingMore;

  bool get hasMore => items.length < totalCount;
}

/// Product list for a query (page is managed here; the query's own page is ignored).
final productListProvider =
    AsyncNotifierProvider.autoDispose.family<ProductListController, ProductListState, ProductQuery>(
  ProductListController.new,
);

class ProductListController extends AsyncNotifier<ProductListState> {
  ProductListController(this.query);

  final ProductQuery query;

  @override
  Future<ProductListState> build() async {
    ref.watch(pincodeProvider);
    final page = await ref.read(catalogRepositoryProvider).products(query.copyWith(page: 1));
    return ProductListState(items: page.items, totalCount: page.totalCount, page: 1);
  }

  Future<void> loadMore() async {
    final current = state.value;
    if (current == null || current.loadingMore || !current.hasMore) return;
    state = AsyncData(
      ProductListState(items: current.items, totalCount: current.totalCount, page: current.page, loadingMore: true),
    );
    try {
      final next = await ref.read(catalogRepositoryProvider).products(query.copyWith(page: current.page + 1));
      state = AsyncData(
        ProductListState(items: [...current.items, ...next.items], totalCount: next.totalCount, page: current.page + 1),
      );
    } catch (_) {
      state = AsyncData(current);
      rethrow;
    }
  }
}

class ProductRequest {
  const ProductRequest(this.slug, [this.preparation]);

  final String slug;
  final String? preparation;

  @override
  bool operator ==(Object other) => other is ProductRequest && other.slug == slug && other.preparation == preparation;

  @override
  int get hashCode => Object.hash(slug, preparation);
}

/// Product detail; `preparation` asks the server for `maxOrderableNow` for that preparation.
final productDetailProvider = FutureProvider.autoDispose.family<ProductDetail, ProductRequest>((ref, request) {
  ref.watch(pincodeProvider);
  return ref.read(catalogRepositoryProvider).product(request.slug, preparation: request.preparation);
});

final searchSuggestionsProvider = FutureProvider.autoDispose.family<List<SearchSuggestion>, String>((ref, q) async {
  if (q.trim().length < 2) return const [];
  // Debounce typing: a newer query disposes this provider before the delay ends.
  var disposed = false;
  ref.onDispose(() => disposed = true);
  await Future<void>.delayed(const Duration(milliseconds: 250));
  if (disposed) return const [];
  return ref.read(catalogRepositoryProvider).suggest(q.trim());
});
