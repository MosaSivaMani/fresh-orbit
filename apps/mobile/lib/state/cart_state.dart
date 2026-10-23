// Cart: always the server's priced cart (§4.6). Writes return the full priced cart, which replaces the state;
// the app never adjusts a line or a total itself. A failed write keeps the last server cart and rethrows the
// ApiError for the screen to show.

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/api_error.dart';
import '../api/models/cart.dart';
import '../api/models/common.dart';
import '../api/repositories.dart';
import 'core_providers.dart';
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

  Future<Cart> clear() => _write((r) => r.clear());

  Future<Cart> applyCoupon(String code) => _write((r) => r.applyCoupon(code));

  Future<Cart> removeCoupon() => _write((r) => r.removeCoupon());

  /// Moves the cart to a new pincode. 409 CART_LOCATION_CHANGED is rethrown so the screen can confirm and call
  /// again with `force: true`.
  Future<Cart> changeLocation(String pincode, {bool force = false}) =>
      _write((r) => r.setLocation(pincode, force: force));

  /// A cart returned by another endpoint (reorder).
  void replace(Cart cart) => state = AsyncData(cart);

  Future<void> reload() async {
    ref.invalidateSelf();
    await future;
  }
}
