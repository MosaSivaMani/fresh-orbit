// In-memory repositories for provider and widget tests. Each method delegates to an overridable handler and
// records its calls; unset handlers throw so a test notices calls it did not expect.

import 'package:fresh_orbit_app/api/models/account.dart';
import 'package:fresh_orbit_app/api/models/auth.dart';
import 'package:fresh_orbit_app/api/models/cart.dart';
import 'package:fresh_orbit_app/api/models/catalog.dart';
import 'package:fresh_orbit_app/api/models/checkout.dart';
import 'package:fresh_orbit_app/api/models/common.dart';
import 'package:fresh_orbit_app/api/models/locations.dart';
import 'package:fresh_orbit_app/api/models/orders.dart';
import 'package:fresh_orbit_app/api/repositories.dart';

Never _unexpected(String name) => throw StateError('Unexpected call: $name');

class FakeLocationRepository implements LocationRepository {
  Future<Serviceability> Function(String pincode)? onServiceability;
  Future<DeliverySlots> Function(String pincode)? onSlots;
  final List<String> checked = [];

  @override
  Future<Serviceability> serviceability(String pincode) {
    checked.add(pincode);
    return (onServiceability ?? (_) => _unexpected('serviceability'))(pincode);
  }

  @override
  Future<DeliverySlots> deliverySlots({required String pincode, String? date}) =>
      (onSlots ?? (_) => _unexpected('deliverySlots'))(pincode);
}

class FakeCatalogRepository implements CatalogRepository {
  Future<HomeData> Function()? onHome;
  Future<List<Category>> Function()? onCategories;
  Future<Paged<ProductSummary>> Function(ProductQuery q)? onProducts;
  Future<ProductDetail> Function(String slug, String? preparation)? onProduct;
  Future<List<SearchSuggestion>> Function(String q)? onSuggest;

  @override
  Future<HomeData> home() => (onHome ?? () => _unexpected('home'))();

  @override
  Future<List<Category>> categories() => (onCategories ?? () async => const <Category>[])();

  @override
  Future<Paged<ProductSummary>> products(ProductQuery query) => (onProducts ?? (_) => _unexpected('products'))(query);

  @override
  Future<ProductDetail> product(String slug, {String? preparation}) =>
      (onProduct ?? (_, _) => _unexpected('product'))(slug, preparation);

  @override
  Future<List<SearchSuggestion>> suggest(String q) => (onSuggest ?? (_) async => const <SearchSuggestion>[])(q);
}

class FakeCartRepository implements CartRepository {
  FakeCartRepository([this.current = Cart.empty]);

  Cart current;
  final List<String> calls = [];
  final List<AddCartItem> added = [];
  final List<Quantity> quantities = [];

  /// When set, the next write throws it (and is cleared).
  Object? failNext;

  Future<Cart> _write(String name, Cart Function() next) async {
    calls.add(name);
    final f = failNext;
    if (f != null) {
      failNext = null;
      throw f;
    }
    current = next();
    return current;
  }

  /// What the "server" returns after a write. Defaults to the current cart.
  Cart Function()? afterWrite;

  /// When set, the next GET throws it (and is cleared).
  Object? failGet;

  @override
  Future<Cart> get() async {
    calls.add('get');
    final f = failGet;
    if (f != null) {
      failGet = null;
      throw f;
    }
    return current;
  }

  @override
  Future<Cart> add(AddCartItem item) {
    added.add(item);
    return _write('add', afterWrite ?? () => current);
  }

  @override
  Future<Cart> changeQuantity(String lineId, Quantity quantity) {
    quantities.add(quantity);
    return _write('changeQuantity:$lineId', afterWrite ?? () => current);
  }

  @override
  Future<Cart> changePreparation(String lineId, String preparationCode) =>
      _write('changePreparation', afterWrite ?? () => current);

  @override
  Future<Cart> remove(String lineId) => _write('remove:$lineId', afterWrite ?? () => Cart.empty);

  @override
  Future<Cart> clear() => _write('clear', () => Cart.empty);

  @override
  Future<Cart> setLocation(String pincode, {bool force = false}) =>
      _write('setLocation:$pincode:$force', afterWrite ?? () => current);

  @override
  Future<Cart> applyCoupon(String code) => _write('applyCoupon:$code', afterWrite ?? () => current);

  @override
  Future<Cart> removeCoupon() => _write('removeCoupon', afterWrite ?? () => current);
}

class PlaceCall {
  PlaceCall(this.request, this.key);
  final PlaceOrderRequest request;
  final String key;
}

class FakeOrderRepository implements OrderRepository {
  Future<CheckoutPreview> Function(CheckoutSelection s)? onPreview;
  Future<OrderDetail> Function(PlaceOrderRequest r, String key)? onPlace;
  Future<Paged<OrderSummary>> Function(int page)? onOrders;
  Future<OrderDetail> Function(String id)? onOrder;
  Future<OrderTracking> Function(String id)? onTracking;
  final List<CheckoutSelection> previews = [];
  final List<PlaceCall> placed = [];
  final List<String> cancelled = [];

  @override
  Future<CheckoutPreview> preview(CheckoutSelection selection) {
    previews.add(selection);
    return (onPreview ?? (_) => _unexpected('preview'))(selection);
  }

  @override
  Future<OrderDetail> placeOrder(PlaceOrderRequest request, {required String idempotencyKey}) {
    placed.add(PlaceCall(request, idempotencyKey));
    return (onPlace ?? (_, _) => _unexpected('placeOrder'))(request, idempotencyKey);
  }

  @override
  Future<Paged<OrderSummary>> orders({int page = 1, int pageSize = 20}) =>
      (onOrders ?? (_) async => const Paged<OrderSummary>(items: [], page: 1, pageSize: 20, totalCount: 0))(page);

  @override
  Future<OrderDetail> order(String orderId) => (onOrder ?? (_) => _unexpected('order'))(orderId);

  @override
  Future<OrderTracking> tracking(String orderId) => (onTracking ?? (_) => _unexpected('tracking'))(orderId);

  @override
  Future<OrderDetail> cancel(String orderId, {required String reasonCode, required String idempotencyKey}) async {
    cancelled.add('$orderId:$reasonCode:$idempotencyKey');
    return (onOrder ?? (_) => _unexpected('order'))(orderId);
  }

  @override
  Future<Cart> reorder(String orderId) async => Cart.empty;

  @override
  Future<void> raiseComplaint(
    String orderId, {
    String? orderItemId,
    required String kind,
    required String description,
    required String idempotencyKey,
  }) async {}

  @override
  Future<List<Complaint>> complaints(String orderId) async => const [];
}

class FakeAccountRepository implements AccountRepository {
  List<Address> addressList = [];
  Profile? me;
  List<WishlistEntry> wish = [];
  List<NotificationPreference> prefs = [];

  @override
  Future<Profile> profile() async => me ?? _unexpected('profile');

  @override
  Future<Profile> updateProfile({String? fullName, String? email, bool? marketingOptIn, required String version}) async =>
      me ?? _unexpected('updateProfile');

  @override
  Future<void> deleteAccount({required String version}) async {}

  @override
  Future<List<Address>> addresses() async => addressList;

  @override
  Future<Address> addAddress(AddressInput input) async => _unexpected('addAddress');

  @override
  Future<Address> replaceAddress(String addressId, AddressInput input, {required String version}) async =>
      _unexpected('replaceAddress');

  @override
  Future<void> deleteAddress(String addressId) async {}

  @override
  Future<Address> makeDefault(String addressId) async => addressList.firstWhere((a) => a.id == addressId);

  @override
  Future<List<WishlistEntry>> wishlist() async => wish;

  @override
  Future<void> addToWishlist(String productId) async => wish = [...wish, WishlistEntry(productId: productId)];

  @override
  Future<void> removeFromWishlist(String productId) async =>
      wish = wish.where((w) => w.productId != productId).toList();

  @override
  Future<InboxPage> notifications({bool unreadOnly = false, int page = 1}) async =>
      const InboxPage(items: [], unreadCount: 0, totalCount: 0);

  @override
  Future<void> markRead(String messageId) async {}

  @override
  Future<void> markAllRead() async {}

  @override
  Future<List<NotificationPreference>> preferences() async => prefs;

  @override
  Future<List<NotificationPreference>> replacePreferences(List<NotificationPreference> preferences) async =>
      prefs = preferences;

  @override
  Future<void> registerDeviceToken({required String pushToken, required String platform}) async {}

  @override
  Future<void> unregisterDeviceToken(String pushToken) async {}
}

class FakeAuthRepository implements AuthRepository {
  Future<OtpChallenge> Function(String phone)? onRequest;
  Future<VerifyOtpResult> Function(String code, String? guestCartToken)? onVerify;

  @override
  Future<OtpChallenge> requestOtp(String phone) => (onRequest ?? (_) => _unexpected('requestOtp'))(phone);

  @override
  Future<VerifyOtpResult> verifyOtp({
    required String challengeId,
    required String phone,
    required String code,
    String? guestCartToken,
    String? deviceLabel,
  }) =>
      (onVerify ?? (_, _) => _unexpected('verifyOtp'))(code, guestCartToken);

  @override
  Future<void> logout(String refreshToken) async {}
}

Address address({String id = 'a1', String pincode = '600041', bool isDefault = true}) => Address.fromJson({
      'addressId': id,
      'label': 'HOME',
      'recipientName': 'Sample Customer',
      'phone': '+919876543210',
      'line1': '12, 3rd Cross Street',
      'line2': null,
      'landmark': null,
      'locality': 'Sample Nagar',
      'cityName': 'Sample City',
      'stateCode': 'TN',
      'pincode': pincode,
      'latitude': null,
      'longitude': null,
      'isDefault': isDefault,
      'isServiceable': null,
      'version': '1',
    });
