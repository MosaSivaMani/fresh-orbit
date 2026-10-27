// Repositories: one interface per area (so tests and widget tests use fakes) and one HTTP implementation
// over [ApiClient]. Paths are relative to `/api/v1/` (docs/api/README.md §4).

import 'package:dio/dio.dart';

import 'api_client.dart';
import 'models/account.dart';
import 'models/auth.dart';
import 'models/cart.dart';
import 'models/catalog.dart';
import 'models/checkout.dart';
import 'models/common.dart';
import 'models/json.dart';
import 'models/locations.dart';
import 'models/orders.dart';

String _seg(String value) => Uri.encodeComponent(value);

// ---------------------------------------------------------------- auth

abstract class AuthRepository {
  Future<OtpChallenge> requestOtp(String phone);
  Future<VerifyOtpResult> verifyOtp({
    required String challengeId,
    required String phone,
    required String code,
    String? guestCartToken,
    String? deviceLabel,
  });
  Future<void> logout(String refreshToken);
}

class HttpAuthRepository implements AuthRepository {
  HttpAuthRepository(this._api);
  final ApiClient _api;

  @override
  Future<OtpChallenge> requestOtp(String phone) async => parseObject(
    await _api.post('auth/otp/request', body: {'phone': phone, 'purpose': 'LOGIN'}, skipAuth: true),
    OtpChallenge.fromJson,
  );

  @override
  Future<VerifyOtpResult> verifyOtp({
    required String challengeId,
    required String phone,
    required String code,
    String? guestCartToken,
    String? deviceLabel,
  }) async => parseObject(
    await _api.post(
      'auth/otp/verify',
      body: {
        'challengeId': challengeId,
        'phone': phone,
        'code': code,
        'deviceLabel': deviceLabel,
        'guestCartToken': guestCartToken,
      },
      skipAuth: true,
    ),
    VerifyOtpResult.fromJson,
  );

  @override
  Future<void> logout(String refreshToken) async {
    await _api.post('auth/logout', body: {'refreshToken': refreshToken, 'allDevices': false});
  }
}

// ---------------------------------------------------------------- locations

abstract class LocationRepository {
  Future<Serviceability> serviceability(String pincode);

  /// Slots for the pincode's location, today … today + 6 (optionally one [date]).
  Future<DeliverySlots> deliverySlots({required String pincode, String? date});
}

class HttpLocationRepository implements LocationRepository {
  HttpLocationRepository(this._api);
  final ApiClient _api;

  @override
  Future<Serviceability> serviceability(String pincode) async =>
      parseObject(await _api.get('serviceability', query: {'pincode': pincode}), Serviceability.fromJson);

  @override
  Future<DeliverySlots> deliverySlots({required String pincode, String? date}) async =>
      parseObject(await _api.get('delivery-slots', query: {'pincode': pincode, 'date': ?date}), DeliverySlots.fromJson);
}

// ---------------------------------------------------------------- catalog

abstract class CatalogRepository {
  Future<HomeData> home();
  Future<List<Category>> categories();
  Future<Paged<ProductSummary>> products(ProductQuery query);
  Future<ProductDetail> product(String slug, {String? preparation});
  Future<List<SearchSuggestion>> suggest(String q);
}

class HttpCatalogRepository implements CatalogRepository {
  HttpCatalogRepository(this._api);
  final ApiClient _api;

  @override
  Future<HomeData> home() async => parseObject(await _api.get('home'), HomeData.fromJson);

  @override
  Future<List<Category>> categories() async => parseArray(await _api.get('categories'), Category.fromJson);

  @override
  Future<Paged<ProductSummary>> products(ProductQuery query) async => parseObject(
    await _api.get('products', query: query.toQuery()),
    (j) => Paged.fromJson(j, ProductSummary.fromJson),
  );

  @override
  Future<ProductDetail> product(String slug, {String? preparation}) async => parseObject(
    await _api.get('products/${_seg(slug)}', query: {'preparation': ?preparation}),
    ProductDetail.fromJson,
  );

  @override
  Future<List<SearchSuggestion>> suggest(String q) async =>
      parseArray(await _api.get('search/suggest', query: {'q': q}), SearchSuggestion.fromJson);
}

// ---------------------------------------------------------------- cart

abstract class CartRepository {
  Future<Cart> get();
  Future<Cart> add(AddCartItem item);
  Future<Cart> changeQuantity(String lineId, Quantity quantity);
  Future<Cart> changePreparation(String lineId, String preparationCode);
  Future<Cart> remove(String lineId);
  Future<Cart> clear();
  Future<Cart> setLocation(String pincode, {bool force = false});
  Future<Cart> applyCoupon(String code);
  Future<Cart> removeCoupon();
}

class HttpCartRepository implements CartRepository {
  HttpCartRepository(this._api);
  final ApiClient _api;

  Cart _cart(Object? data) => parseObject(data, Cart.fromJson);

  @override
  Future<Cart> get() async => _cart(await _api.get('cart'));

  @override
  Future<Cart> add(AddCartItem item) async => _cart(await _api.post('cart/items', body: item.toJson()));

  @override
  Future<Cart> changeQuantity(String lineId, Quantity quantity) async =>
      _cart(await _api.patch('cart/items/${_seg(lineId)}', body: {'quantity': quantity.toJson()}));

  @override
  Future<Cart> changePreparation(String lineId, String preparationCode) async =>
      _cart(await _api.patch('cart/items/${_seg(lineId)}', body: {'preparationCode': preparationCode}));

  @override
  Future<Cart> remove(String lineId) async => _cart(await _api.delete('cart/items/${_seg(lineId)}'));

  @override
  Future<Cart> clear() async {
    final data = await _api.delete('cart');
    return data == null ? Cart.empty : _cart(data);
  }

  @override
  Future<Cart> setLocation(String pincode, {bool force = false}) async =>
      _cart(await _api.put('cart/location', body: {'pincode': pincode}, query: {if (force) 'force': true}));

  @override
  Future<Cart> applyCoupon(String code) async => _cart(await _api.put('cart/coupon', body: {'code': code}));

  @override
  Future<Cart> removeCoupon() async => _cart(await _api.delete('cart/coupon'));
}

// ---------------------------------------------------------------- checkout and orders

abstract class OrderRepository {
  Future<CheckoutPreview> preview(CheckoutSelection selection);

  /// `POST /orders` with `Idempotency-Key` (reuse [idempotencyKey] when retrying the same action).
  Future<OrderDetail> placeOrder(PlaceOrderRequest request, {required String idempotencyKey});
  Future<Paged<OrderSummary>> orders({int page = 1, int pageSize = 20});
  Future<OrderDetail> order(String orderId);
  Future<OrderTracking> tracking(String orderId);
  Future<OrderDetail> cancel(String orderId, {required String reasonCode, required String idempotencyKey});
  Future<Cart> reorder(String orderId);

  /// `POST /orders/{id}/complaints` (JSON) with `Idempotency-Key`; returns the created complaint.
  Future<Complaint> raiseComplaint(
    String orderId, {
    String? orderItemId,
    required String kind,
    required String description,
    required String idempotencyKey,
  });

  /// `POST /orders/{id}/complaints/{cid}/photos`: one photo per request, multipart field `file`.
  Future<void> addComplaintPhoto(
    String orderId,
    String complaintId, {
    required List<int> bytes,
    required String fileName,
  });
  Future<List<Complaint>> complaints(String orderId);
  Future<InvoiceLink> invoice(String orderId);

  /// `POST /orders/{id}/substitutions/{sid}/accept` or `/reject`; returns the updated order.
  Future<OrderDetail> answerSubstitution(String orderId, String substitutionId, {required bool accept});
}

class HttpOrderRepository implements OrderRepository {
  HttpOrderRepository(this._api);
  final ApiClient _api;

  @override
  Future<CheckoutPreview> preview(CheckoutSelection selection) async =>
      parseObject(await _api.post('checkout/preview', body: selection.toJson()), CheckoutPreview.fromJson);

  @override
  Future<OrderDetail> placeOrder(PlaceOrderRequest request, {required String idempotencyKey}) async => parseObject(
    await _api.post('orders', body: request.toJson(), headers: {'Idempotency-Key': idempotencyKey}),
    OrderDetail.fromJson,
  );

  @override
  Future<Paged<OrderSummary>> orders({int page = 1, int pageSize = 20}) async => parseObject(
    await _api.get('orders', query: {'page': page, 'pageSize': pageSize, 'sort': '-placedAt'}),
    (j) => Paged.fromJson(j, OrderSummary.fromJson),
  );

  @override
  Future<OrderDetail> order(String orderId) async =>
      parseObject(await _api.get('orders/${_seg(orderId)}'), OrderDetail.fromJson);

  @override
  Future<OrderTracking> tracking(String orderId) async =>
      parseObject(await _api.get('orders/${_seg(orderId)}/tracking'), OrderTracking.fromJson);

  @override
  Future<OrderDetail> cancel(String orderId, {required String reasonCode, required String idempotencyKey}) async =>
      parseObject(
        await _api.post(
          'orders/${_seg(orderId)}/cancel',
          body: {'reasonCode': reasonCode},
          headers: {'Idempotency-Key': idempotencyKey},
        ),
        OrderDetail.fromJson,
      );

  @override
  Future<Cart> reorder(String orderId) async =>
      parseObject(await _api.post('orders/${_seg(orderId)}/reorder'), Cart.fromJson);

  @override
  Future<Complaint> raiseComplaint(
    String orderId, {
    String? orderItemId,
    required String kind,
    required String description,
    required String idempotencyKey,
  }) async => parseObject(
    await _api.post(
      'orders/${_seg(orderId)}/complaints',
      body: {'orderItemId': ?orderItemId, 'kind': kind, 'description': description},
      headers: {'Idempotency-Key': idempotencyKey},
    ),
    Complaint.fromJson,
  );

  @override
  Future<void> addComplaintPhoto(
    String orderId,
    String complaintId, {
    required List<int> bytes,
    required String fileName,
  }) async {
    await _api.post(
      'orders/${_seg(orderId)}/complaints/${_seg(complaintId)}/photos',
      body: FormData.fromMap({'file': MultipartFile.fromBytes(bytes, filename: fileName)}),
    );
  }

  @override
  Future<OrderDetail> answerSubstitution(String orderId, String substitutionId, {required bool accept}) async =>
      parseObject(
        await _api.post('orders/${_seg(orderId)}/substitutions/${_seg(substitutionId)}/${accept ? 'accept' : 'reject'}'),
        OrderDetail.fromJson,
      );

  @override
  Future<List<Complaint>> complaints(String orderId) async =>
      parseArray(await _api.get('orders/${_seg(orderId)}/complaints'), Complaint.fromJson);

  @override
  Future<InvoiceLink> invoice(String orderId) async =>
      parseObject(await _api.get('orders/${_seg(orderId)}/invoice'), InvoiceLink.fromJson);
}

// ---------------------------------------------------------------- account

abstract class AccountRepository {
  Future<Profile> profile();
  Future<Profile> updateProfile({String? fullName, String? email, bool? marketingOptIn, required String version});
  Future<void> deleteAccount({required String version});

  Future<List<Address>> addresses();
  Future<Address> addAddress(AddressInput input);
  Future<Address> replaceAddress(String addressId, AddressInput input, {required String version});
  Future<void> deleteAddress(String addressId);
  Future<Address> makeDefault(String addressId);

  Future<List<WishlistEntry>> wishlist();
  Future<void> addToWishlist(String productId);
  Future<void> removeFromWishlist(String productId);

  Future<InboxPage> notifications({bool unreadOnly = false, int page = 1});
  Future<void> markRead(String messageId);
  Future<void> markAllRead();
  Future<List<NotificationPreference>> preferences();
  Future<List<NotificationPreference>> replacePreferences(List<NotificationPreference> preferences);

  Future<void> registerDeviceToken({required String pushToken, required String platform});
  Future<void> unregisterDeviceToken(String pushToken);
}

class HttpAccountRepository implements AccountRepository {
  HttpAccountRepository(this._api);
  final ApiClient _api;

  @override
  Future<Profile> profile() async => parseObject(await _api.get('me'), Profile.fromJson);

  @override
  Future<Profile> updateProfile({
    String? fullName,
    String? email,
    bool? marketingOptIn,
    required String version,
  }) async => parseObject(
    await _api.patch(
      'me',
      body: {'fullName': fullName, 'email': email, 'marketingOptIn': marketingOptIn},
      headers: ifMatch(version),
    ),
    Profile.fromJson,
  );

  @override
  Future<void> deleteAccount({required String version}) async {
    await _api.delete('me', headers: ifMatch(version));
  }

  @override
  Future<List<Address>> addresses() async => parseArray(await _api.get('me/addresses'), Address.fromJson);

  @override
  Future<Address> addAddress(AddressInput input) async =>
      parseObject(await _api.post('me/addresses', body: input.toJson()), Address.fromJson);

  @override
  Future<Address> replaceAddress(String addressId, AddressInput input, {required String version}) async => parseObject(
    await _api.put('me/addresses/${_seg(addressId)}', body: input.toJson(), headers: ifMatch(version)),
    Address.fromJson,
  );

  @override
  Future<void> deleteAddress(String addressId) async {
    await _api.delete('me/addresses/${_seg(addressId)}');
  }

  @override
  Future<Address> makeDefault(String addressId) async =>
      parseObject(await _api.post('me/addresses/${_seg(addressId)}/default'), Address.fromJson);

  @override
  Future<List<WishlistEntry>> wishlist() async => parseArray(await _api.get('me/wishlist'), WishlistEntry.fromJson);

  @override
  Future<void> addToWishlist(String productId) async {
    await _api.put('me/wishlist/${_seg(productId)}');
  }

  @override
  Future<void> removeFromWishlist(String productId) async {
    await _api.delete('me/wishlist/${_seg(productId)}');
  }

  @override
  Future<InboxPage> notifications({bool unreadOnly = false, int page = 1}) async => parseObject(
    await _api.get('me/notifications', query: {'unreadOnly': unreadOnly, 'page': page}),
    InboxPage.fromJson,
  );

  @override
  Future<void> markRead(String messageId) async {
    await _api.post('me/notifications/${_seg(messageId)}/read');
  }

  @override
  Future<void> markAllRead() async {
    await _api.post('me/notifications/read-all');
  }

  @override
  Future<List<NotificationPreference>> preferences() async =>
      _readPreferences(await _api.get('me/notification-preferences'));

  @override
  Future<List<NotificationPreference>> replacePreferences(List<NotificationPreference> preferences) async =>
      _readPreferences(
        await _api.put(
          'me/notification-preferences',
          body: {
            'preferences': [for (final p in preferences) p.toJson()],
          },
        ),
      );

  List<NotificationPreference> _readPreferences(Object? data) =>
      parseObject(data, (Json j) => reqList(j, 'preferences', NotificationPreference.fromJson));

  @override
  Future<void> registerDeviceToken({required String pushToken, required String platform}) async {
    await _api.put('me/device-tokens', body: {'pushToken': pushToken, 'provider': 'FCM', 'platform': platform});
  }

  @override
  Future<void> unregisterDeviceToken(String pushToken) async {
    await _api.delete('me/device-tokens/${_seg(pushToken)}');
  }
}
