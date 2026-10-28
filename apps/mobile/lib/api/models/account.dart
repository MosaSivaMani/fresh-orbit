// TEMPORARY: replace with fresh_api generated models after Phase 3
//
// docs/api/README.md §4.2 (/me, /me/addresses, /me/wishlist) and §4.10 (notifications, device tokens,
// preferences). Field names follow the Customers and Notifications contracts already merged on `main`
// (`addressId`, `customerId`, `messageId`) because docs/api does not write these bodies out.

import 'catalog.dart';
import 'json.dart';

class Profile {
  const Profile({
    required this.customerId,
    this.phone,
    this.fullName,
    this.email,
    required this.marketingOptIn,
    required this.status,
    required this.version,
  });

  factory Profile.fromJson(Json j) => Profile(
    customerId: optString(j, 'customerId') ?? reqString(j, 'id'),
    phone: optString(j, 'phone'),
    fullName: optString(j, 'fullName'),
    email: optString(j, 'email'),
    marketingOptIn: optBool(j, 'marketingOptIn') ?? false,
    status: optString(j, 'status') ?? 'ACTIVE',
    version: reqString(j, 'version'),
  );

  final String customerId;
  final String? phone;
  final String? fullName;
  final String? email;
  final bool marketingOptIn;
  final String status;

  /// Row version for `If-Match`.
  final String version;
}

class Address {
  const Address({
    required this.id,
    required this.label,
    required this.recipientName,
    required this.phone,
    required this.line1,
    this.line2,
    this.landmark,
    required this.locality,
    required this.cityName,
    required this.stateCode,
    required this.pincode,
    required this.isDefault,
    this.isServiceable,
    required this.version,
  });

  factory Address.fromJson(Json j) => Address(
    id: optString(j, 'addressId') ?? reqString(j, 'id'),
    label: reqString(j, 'label'),
    recipientName: reqString(j, 'recipientName'),
    phone: reqString(j, 'phone'),
    line1: reqString(j, 'line1'),
    line2: optString(j, 'line2'),
    landmark: optString(j, 'landmark'),
    locality: reqString(j, 'locality'),
    cityName: reqString(j, 'cityName'),
    stateCode: reqString(j, 'stateCode'),
    pincode: reqString(j, 'pincode'),
    isDefault: optBool(j, 'isDefault') ?? false,
    isServiceable: optBool(j, 'isServiceable'),
    version: optString(j, 'version') ?? '',
  );

  final String id;

  /// HOME, WORK, OTHER.
  final String label;
  final String recipientName;
  final String phone;
  final String line1;
  final String? line2;
  final String? landmark;
  final String locality;
  final String cityName;
  final String stateCode;
  final String pincode;
  final bool isDefault;

  /// Null until Customers can ask Locations (CUS-R5); the checkout preview is the authority.
  final bool? isServiceable;
  final String version;

  String get oneLine =>
      [line1, line2, landmark, locality, cityName, pincode].whereType<String>().where((s) => s.isNotEmpty).join(', ');
}

/// Body of POST and PUT `/me/addresses`.
class AddressInput {
  const AddressInput({
    required this.label,
    required this.recipientName,
    required this.phone,
    required this.line1,
    this.line2,
    this.landmark,
    required this.locality,
    required this.cityName,
    required this.stateCode,
    required this.pincode,
    this.isDefault,
  });

  factory AddressInput.from(Address a) => AddressInput(
    label: a.label,
    recipientName: a.recipientName,
    phone: a.phone,
    line1: a.line1,
    line2: a.line2,
    landmark: a.landmark,
    locality: a.locality,
    cityName: a.cityName,
    stateCode: a.stateCode,
    pincode: a.pincode,
    isDefault: a.isDefault,
  );

  final String label;
  final String recipientName;
  final String phone;
  final String line1;
  final String? line2;
  final String? landmark;
  final String locality;
  final String cityName;
  final String stateCode;
  final String pincode;
  final bool? isDefault;

  Json toJson() => {
    'label': label,
    'recipientName': recipientName,
    'phone': phone,
    'line1': line1,
    'line2': line2,
    'landmark': landmark,
    'locality': locality,
    'cityName': cityName,
    'stateCode': stateCode,
    'pincode': pincode,
    'latitude': null,
    'longitude': null,
    'isDefault': isDefault,
  };
}

/// Wishlist entry. docs/api says the list returns products (priced with a pincode); the Customers module on
/// `main` returns `{ productId, addedAt }` only. Both are read: [product] is null for the short shape.
class WishlistEntry {
  const WishlistEntry({required this.productId, this.product});

  /// `GET /me/wishlist` items are `{ productId, addedAt, product: { slug, name, imageUrl, price, isAvailable } }`
  /// (product null when it is no longer on the storefront). A flat product summary is read too.
  factory WishlistEntry.fromJson(Json j) {
    final productId = optString(j, 'productId') ?? reqString(j, 'id');
    final nested = asJsonOrNull(j['product'], 'product');
    if (nested != null) {
      final price = asJsonOrNull(nested['price'], 'price');
      final available = optBool(nested, 'isAvailable') ?? false;
      return WishlistEntry(
        productId: productId,
        product: ProductSummary.fromJson({
          'id': productId,
          'slug': nested['slug'],
          'name': nested['name'],
          'imageUrl': nested['imageUrl'],
          'fromPrice': price,
          // The server's flags as availability codes: sold and in stock, not sold at this location, or sold out.
          'availability': {
            'status': available
                ? 'IN_STOCK'
                : price == null
                ? 'NOT_SOLD_HERE'
                : 'OUT_OF_STOCK',
            'message': null,
          },
        }),
      );
    }
    final hasProduct = j['slug'] is String && j['name'] is String;
    return WishlistEntry(
      productId: productId,
      product: hasProduct ? ProductSummary.fromJson({...j, 'id': optString(j, 'id') ?? j['productId']}) : null,
    );
  }

  final String productId;
  final ProductSummary? product;
}

class InboxMessage {
  const InboxMessage({
    required this.messageId,
    this.title,
    required this.body,
    required this.isRead,
    required this.createdAt,
  });

  factory InboxMessage.fromJson(Json j) => InboxMessage(
    messageId: reqString(j, 'messageId'),
    title: optString(j, 'title'),
    body: reqString(j, 'body'),
    isRead: reqBool(j, 'isRead'),
    createdAt: reqInstant(j, 'createdAt'),
  );

  final String messageId;
  final String? title;
  final String body;
  final bool isRead;
  final DateTime createdAt;
}

class InboxPage {
  const InboxPage({required this.items, required this.unreadCount, required this.totalCount});

  /// Signed out: nothing to show.
  static const empty = InboxPage(items: [], unreadCount: 0, totalCount: 0);

  factory InboxPage.fromJson(Json j) => InboxPage(
    items: reqList(j, 'items', InboxMessage.fromJson),
    unreadCount: optInt(j, 'unreadCount') ?? 0,
    totalCount: optInt(j, 'totalCount') ?? 0,
  );

  final List<InboxMessage> items;
  final int unreadCount;
  final int totalCount;
}

/// One preference row: category ORDER_UPDATES / OFFERS × channel SMS / EMAIL / PUSH / IN_APP.
class NotificationPreference {
  const NotificationPreference({required this.category, required this.channel, required this.enabled});

  factory NotificationPreference.fromJson(Json j) => NotificationPreference(
    category: reqString(j, 'category'),
    channel: reqString(j, 'channel'),
    enabled: reqBool(j, 'enabled'),
  );

  final String category;
  final String channel;
  final bool enabled;

  NotificationPreference copyWith({required bool enabled}) =>
      NotificationPreference(category: category, channel: channel, enabled: enabled);

  Json toJson() => {'category': category, 'channel': channel, 'enabled': enabled};
}
