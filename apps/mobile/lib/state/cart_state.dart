// Cart: always the server's priced cart (§4.6). Writes return the full priced cart, which replaces the state;
// the app never adjusts a line or a total itself. A failed write keeps the last server cart and rethrows the
// ApiError for the screen to show.

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/api_error.dart';
import '../api/models/cart.dart';
import '../api/models/catalog.dart';
import '../api/models/common.dart';
import '../api/repositories.dart';
import 'core_providers.dart';
import 'location_state.dart';
import 'session.dart';

final cartProvider = AsyncNotifierProvider<CartController, Cart>(CartController.new);

/// Line count for the tab badge (null while unknown).
final cartLineCountProvider = Provider<int?>((ref) => ref.watch(cartProvider).value?.lineCount);

class CartController extends AsyncNotifier<Cart> {
  @override
  Future<Cart> build() async {
    // Signing in or out switches the cart owner (JWT vs X-Cart-Token).
    ref.watch(sessionProvider.select((s) => s.signedIn));
    try {
      return await _repo.get();
    } on ApiError catch (e) {
      if (e.code == 'CART_NOT_FOUND') {
        await ref.read(tokenStoreProvider).clearGuestCartToken();
        return Cart.empty;
      }
      rethrow;
    }
  }

  CartRepository get _repo => ref.read(cartRepositoryProvider);

  Future<Cart> _write(Future<Cart> Function(CartRepository repo) op) async {
    final cart = await op(_repo);
    final tokens = ref.read(tokenStoreProvider);
    final token = cart.cartToken;
    if (token != null && !tokens.hasSession) await tokens.saveGuestCartToken(token);
    state = AsyncData(cart);
    return cart;
  }

  Future<Cart> add(AddCartItem item) => _write((r) => r.add(item));

  Future<Cart> changeQuantity(String lineId, Quantity quantity) => _write((r) => r.changeQuantity(lineId, quantity));

  Future<Cart> remove(String lineId) => _write((r) => r.remove(lineId));

  /// Changes a line's cut; the server re-prices the line and returns the cart.
  Future<Cart> changePreparation(String lineId, String preparationCode) =>
      _write((r) => r.changePreparation(lineId, preparationCode));

  Future<Cart> clear() => _write((r) => r.clear());

  Future<Cart> applyCoupon(String code) => _write((r) => r.applyCoupon(code));

  Future<Cart> removeCoupon() => _write((r) => r.removeCoupon());

  /// Moves the cart to a new pincode. 409 CART_LOCATION_CHANGED is rethrown so the screen can confirm and call
  /// again with `force: true`.
  Future<Cart> changeLocation(String pincode, {bool force = false}) =>
      _write((r) => r.setLocation(pincode, force: force));

  /// The − / + of a cart line's stepper. The cart line carries no order limits, so the product's server
  /// limits (min / max / step / maxOrderableNow) for that variant and preparation are read first; the new
  /// quantity is only a proposal the server validates. Going below the minimum removes the line.
  Future<Cart> stepLine(CartLine line, {required bool up}) async {
    final detail = await ref
        .read(catalogRepositoryProvider)
        .product(line.productSlug, preparation: line.preparation?.code);
    final variant = detail.variants.where((v) => v.id == line.variantId).firstOrNull;
    if (variant == null) {
      throw const ApiError(
        status: 0,
        code: 'VARIANT_NOT_SOLD',
        detail: 'This item is no longer sold. Remove it from the cart.',
      );
    }
    final step = variant.orderStep.value;
    final next = line.quantity.value + (up ? step : -step);
    if (!up && next < variant.minOrder.value) return remove(line.id);
    final max = (variant.availability?.maxOrderableNow ?? variant.maxOrder).value;
    if (up && next > max) {
      throw const ApiError(
        status: 0,
        code: 'QUANTITY_ABOVE_MAX',
        detail: 'You already have the most you can order of this item.',
      );
    }
    return changeQuantity(line.id, line.quantity.withValue(next));
  }

  /// The ADD button on a product card: adds the server's default size of the list item (the size behind the card's
  /// "from" price, MB-2) with its default cut and minimum quantity, with no extra detail call. Without one (older
  /// list shape) it reads the detail and adds the first orderable size. Returns the choice made, or null when
  /// nothing can be added from the card (the caller then opens the product page). The server prices the line.
  Future<QuickAddChoice?> quickAdd(ProductSummary product) async {
    final listed = product.defaultVariant;
    if (listed != null) {
      final cart = await add(
        AddCartItem(variantId: listed.id, preparationCode: listed.defaultPreparationCode, quantity: listed.minOrder),
      );
      final line = cart.lines
          .where(
            (l) =>
                l.variantId == listed.id &&
                (listed.defaultPreparationCode == null || l.preparation?.code == listed.defaultPreparationCode),
          )
          .lastOrNull;
      return QuickAddChoice(
        sizeName: listed.name,
        preparationName: line?.preparation?.name,
        quantity: line?.quantity ?? listed.minOrder,
      );
    }
    final detail = await ref.read(catalogRepositoryProvider).product(product.slug);
    final variant = detail.variants
        .where((v) => v.unitPricePaise != null && (v.availability?.canAdd ?? true))
        .firstOrNull;
    if (variant == null) return null;
    final orderable = variant.preparations.where((p) => p.chargePaise != null).toList();
    final preparation = variant.preparations.isEmpty
        ? null
        : (orderable.where((p) => p.isDefault).firstOrNull ?? orderable.firstOrNull);
    if (variant.preparations.isNotEmpty && preparation == null) return null;
    final choice = QuickAddChoice(
      sizeName: variant.name,
      preparationName: preparation?.name,
      quantity: variant.minOrder,
    );
    await add(AddCartItem(variantId: variant.id, preparationCode: preparation?.code, quantity: variant.minOrder));
    return choice;
  }

  /// A cart returned by another endpoint (reorder).
  void replace(Cart cart) => state = AsyncData(cart);

  Future<void> reload() async {
    ref.invalidateSelf();
    await future;
  }
}

/// What [CartController.quickAdd] put in the cart.
class QuickAddChoice {
  const QuickAddChoice({required this.sizeName, required this.preparationName, required this.quantity});

  /// The size added (the snack bar names it, MB-2).
  final String sizeName;
  final String? preparationName;
  final Quantity quantity;
}

/// Coupons the signed-in customer may use in the current pincode's city. The API client sends the pincode as
/// X-Pincode; without one the server lists none.
final couponsProvider = FutureProvider.autoDispose<List<VisibleCoupon>>((ref) {
  ref.watch(pincodeProvider);
  return ref.read(cartRepositoryProvider).coupons();
});
