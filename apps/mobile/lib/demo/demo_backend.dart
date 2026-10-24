// Demo build (--dart-define=DEMO=true): in-memory repositories that answer from fixed fixture data, so the
// owner can click through every screen in a browser without an API. They return what a server would return;
// the app still computes nothing. Two switches show the rarer states: "empty lists" and "offline".
// `demoMode` is a compile-time constant, so a build without DEMO=true tree-shakes all of this away.

import 'package:flutter/foundation.dart' show ValueNotifier;

import '../api/api_error.dart';
import '../api/models/account.dart';
import '../api/models/auth.dart';
import '../api/models/cart.dart';
import '../api/models/catalog.dart';
import '../api/models/checkout.dart';
import '../api/models/common.dart';
import '../api/models/json.dart';
import '../api/models/locations.dart';
import '../api/models/orders.dart';
import '../api/repositories.dart';
import 'demo_data.dart';

const demoMode = bool.fromEnvironment('DEMO');

/// The switches on the demo panel. Screens re-read their providers after a change.
class DemoSwitches {
  final empty = ValueNotifier<bool>(false);
  final offline = ValueNotifier<bool>(false);
}

class DemoBackend {
  DemoBackend();

  final switches = DemoSwitches();
  bool _cartFilled = true;
  final Set<String> _readMessages = {};
  final Set<String> _wishlistRemoved = {};
  List<Json> _preferences = demoPreferences;
  String? _pendingPhone;

  bool get _empty => switches.empty.value;

  Future<T> _answer<T>(T Function() value) async {
    await Future<void>.delayed(const Duration(milliseconds: 250));
    if (switches.offline.value) {
      throw const ApiError(status: 0, code: ApiError.networkError, detail: 'Check your internet connection.');
    }
    return value();
  }

  Never _notFound(String what) =>
      throw ApiError(status: 404, code: 'NOT_FOUND', detail: 'We could not find this $what.', traceId: 'demo-404');

  late final auth = _DemoAuth(this);
  late final locations = _DemoLocations(this);
  late final catalog = _DemoCatalog(this);
  late final cart = _DemoCart(this);
  late final orders = _DemoOrders(this);
  late final account = _DemoAccount(this);
}

class _DemoAuth implements AuthRepository {
  _DemoAuth(this._b);
  final DemoBackend _b;

  @override
  Future<OtpChallenge> requestOtp(String phone) => _b._answer(() {
    _b._pendingPhone = phone;
    return OtpChallenge.fromJson({'challengeId': 'demo', 'resendAfterSeconds': 30, 'codeLength': 6});
  });

  @override
  Future<VerifyOtpResult> verifyOtp({
    required String challengeId,
    required String phone,
    required String code,
    String? guestCartToken,
    String? deviceLabel,
  }) => _b._answer(
    () => VerifyOtpResult.fromJson({
      'accessToken': 'demo-access',
      'accessTokenExpiresAt': '2099-01-01T00:00:00Z',
      'refreshToken': 'demo-refresh',
      'refreshTokenExpiresAt': '2099-01-01T00:00:00Z',
      'user': {'id': 'cust1', 'phone': _b._pendingPhone ?? phone, 'fullName': 'Sample Customer'},
    }),
  );

  @override
  Future<void> logout(String refreshToken) async {}
}

class _DemoLocations implements LocationRepository {
  _DemoLocations(this._b);
  final DemoBackend _b;

  @override
  Future<Serviceability> serviceability(String pincode) =>
      _b._answer(() => Serviceability.fromJson(demoServiceability(pincode)));

  @override
  Future<DeliverySlots> deliverySlots({required String pincode, String? date}) =>
      _b._answer(() => DeliverySlots.fromJson(demoSlots));
}

class _DemoCatalog implements CatalogRepository {
  _DemoCatalog(this._b);
  final DemoBackend _b;

  @override
  Future<HomeData> home() => _b._answer(() => HomeData.fromJson(demoHome(empty: _b._empty)));

  @override
  Future<List<Category>> categories() => _b._answer(() => demoCategories.map(Category.fromJson).toList());

  @override
  Future<Paged<ProductSummary>> products(ProductQuery query) => _b._answer(() {
    final items = _b._empty ? <Json>[] : demoProducts(category: query.category, q: query.q);
    return Paged.fromJson({
      'items': items,
      'page': 1,
      'pageSize': 20,
      'totalCount': items.length,
    }, ProductSummary.fromJson);
  });

  @override
  Future<ProductDetail> product(String slug, {String? preparation}) => _b._answer(() {
    final json = demoProductDetail(slug);
    if (json == null) _b._notFound('product');
    return ProductDetail.fromJson(json);
  });

  @override
  Future<List<SearchSuggestion>> suggest(String q) =>
      _b._answer(() => _b._empty ? <SearchSuggestion>[] : demoSuggestions(q).map(SearchSuggestion.fromJson).toList());
}

class _DemoCart implements CartRepository {
  _DemoCart(this._b);
  final DemoBackend _b;

  Cart _current() => _b._cartFilled && !_b._empty ? Cart.fromJson(demoCart()) : Cart.empty;

  Future<Cart> _fill() => _b._answer(() {
    _b._cartFilled = true;
    return _current();
  });

  @override
  Future<Cart> get() => _b._answer(_current);

  // The demo cart is fixed: adding or changing anything shows the same server-priced cart.
  @override
  Future<Cart> add(AddCartItem item) => _fill();

  @override
  Future<Cart> changeQuantity(String lineId, Quantity quantity) => _fill();

  @override
  Future<Cart> changePreparation(String lineId, String preparationCode) => _fill();

  @override
  Future<Cart> remove(String lineId) => clear();

  @override
  Future<Cart> clear() => _b._answer(() {
    _b._cartFilled = false;
    return Cart.empty;
  });

  @override
  Future<Cart> setLocation(String pincode, {bool force = false}) => _b._answer(_current);

  @override
  Future<Cart> applyCoupon(String code) => _b._answer(
    () => throw const ApiError(
      status: 422,
      code: 'COUPON_NOT_FOUND',
      detail: 'Coupons are not set up in the demo.',
      traceId: 'demo-coupon',
    ),
  );

  @override
  Future<Cart> removeCoupon() => _b._answer(_current);
}

class _DemoOrders implements OrderRepository {
  _DemoOrders(this._b);
  final DemoBackend _b;

  @override
  Future<CheckoutPreview> preview(CheckoutSelection selection) => _b._answer(
    () => CheckoutPreview.fromJson(demoPreview(selection.addressId, selection.deliverySlotId, selection.paymentMethod)),
  );

  @override
  Future<OrderDetail> placeOrder(PlaceOrderRequest request, {required String idempotencyKey}) => _b._answer(() {
    _b._cartFilled = false;
    return OrderDetail.fromJson(demoOrder('o1')!);
  });

  @override
  Future<Paged<OrderSummary>> orders({int page = 1, int pageSize = 20}) => _b._answer(() {
    final items = _b._empty ? <Json>[] : [for (final id in demoOrderStatuses.keys) demoOrderSummary(id)];
    return Paged.fromJson({
      'items': items,
      'page': 1,
      'pageSize': pageSize,
      'totalCount': items.length,
    }, OrderSummary.fromJson);
  });

  @override
  Future<OrderDetail> order(String orderId) => _b._answer(() {
    final json = demoOrder(orderId);
    if (json == null) _b._notFound('order');
    return OrderDetail.fromJson(json);
  });

  @override
  Future<OrderTracking> tracking(String orderId) => _b._answer(() {
    final json = demoTracking(orderId);
    if (json == null) _b._notFound('order');
    return OrderTracking.fromJson(json);
  });

  @override
  Future<OrderDetail> cancel(String orderId, {required String reasonCode, required String idempotencyKey}) =>
      order(orderId);

  @override
  Future<Cart> reorder(String orderId) => _b._answer(() {
    _b._cartFilled = true;
    return Cart.fromJson(demoCart());
  });

  @override
  Future<void> raiseComplaint(
    String orderId, {
    String? orderItemId,
    required String kind,
    required String description,
    required String idempotencyKey,
  }) => _b._answer(() {});

  @override
  Future<List<Complaint>> complaints(String orderId) => _b._answer(() => const <Complaint>[]);

  @override
  Future<InvoiceLink> invoice(String orderId) => _b._answer(() => const InvoiceLink(invoiceNumber: 'INV/26-27/000001'));
}

class _DemoAccount implements AccountRepository {
  _DemoAccount(this._b);
  final DemoBackend _b;

  @override
  Future<Profile> profile() => _b._answer(() => Profile.fromJson(demoProfile));

  @override
  Future<Profile> updateProfile({String? fullName, String? email, bool? marketingOptIn, required String version}) =>
      _b._answer(
        () => Profile.fromJson({
          ...demoProfile,
          'fullName': ?fullName,
          'email': ?email,
          'marketingOptIn': ?marketingOptIn,
        }),
      );

  @override
  Future<void> deleteAccount({required String version}) => _b._answer(() {});

  @override
  Future<List<Address>> addresses() => _b._answer(() => _b._empty ? <Address>[] : [Address.fromJson(demoAddress)]);

  @override
  Future<Address> addAddress(AddressInput input) => _b._answer(() => Address.fromJson(demoAddress));

  @override
  Future<Address> replaceAddress(String addressId, AddressInput input, {required String version}) =>
      _b._answer(() => Address.fromJson(demoAddress));

  @override
  Future<void> deleteAddress(String addressId) => _b._answer(() {});

  @override
  Future<Address> makeDefault(String addressId) => _b._answer(() => Address.fromJson(demoAddress));

  @override
  Future<List<WishlistEntry>> wishlist() => _b._answer(
    () => _b._empty
        ? <WishlistEntry>[]
        : [
            for (final e in demoWishlist())
              if (!_b._wishlistRemoved.contains(e['id'])) WishlistEntry.fromJson(e),
          ],
  );

  @override
  Future<void> addToWishlist(String productId) => _b._answer(() => _b._wishlistRemoved.remove(productId));

  @override
  Future<void> removeFromWishlist(String productId) => _b._answer(() => _b._wishlistRemoved.add(productId));

  @override
  Future<InboxPage> notifications({bool unreadOnly = false, int page = 1}) => _b._answer(() {
    if (_b._empty) return InboxPage.fromJson({'items': <Json>[], 'unreadCount': 0, 'totalCount': 0});
    final items = [
      for (final m in (demoInbox['items']! as List).cast<Json>())
        {...m, 'isRead': (m['isRead']! as bool) || _b._readMessages.contains(m['messageId'])},
    ];
    return InboxPage.fromJson({
      'items': items,
      'unreadCount': items.where((m) => m['isRead'] == false).length,
      'totalCount': items.length,
    });
  });

  @override
  Future<void> markRead(String messageId) => _b._answer(() => _b._readMessages.add(messageId));

  @override
  Future<void> markAllRead() => _b._answer(() => _b._readMessages.addAll(['m1', 'm2']));

  @override
  Future<List<NotificationPreference>> preferences() =>
      _b._answer(() => _b._preferences.map(NotificationPreference.fromJson).toList());

  @override
  Future<List<NotificationPreference>> replacePreferences(List<NotificationPreference> preferences) => _b._answer(() {
    _b._preferences = [for (final p in preferences) p.toJson()];
    return preferences;
  });

  @override
  Future<void> registerDeviceToken({required String pushToken, required String platform}) async {}

  @override
  Future<void> unregisterDeviceToken(String pushToken) async {}
}
