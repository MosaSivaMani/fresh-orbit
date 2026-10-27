// TEMPORARY: replace with fresh_api generated models after Phase 3
//
// docs/api/README.md §4.6 (GET /cart and every cart write) and §5 (pricing contract). Every money value here is
// computed by the server; the app only formats it.

import 'common.dart';
import 'json.dart';

class CartPreparation {
  const CartPreparation({required this.code, required this.name, required this.yieldBp});

  factory CartPreparation.fromJson(Json j) =>
      CartPreparation(code: reqString(j, 'code'), name: reqString(j, 'name'), yieldBp: reqInt(j, 'yieldBp'));

  final String code;
  final String name;
  final int yieldBp;
}

class CartLine {
  const CartLine({
    required this.id,
    required this.productId,
    required this.productSlug,
    required this.productName,
    required this.variantId,
    required this.variantName,
    required this.sellUnit,
    this.imageUrl,
    this.preparation,
    required this.quantity,
    required this.estimatedNetQuantity,
    required this.maxChargeableQuantity,
    required this.unitPricePaise,
    this.compareAtPaise,
    required this.priceUnit,
    required this.itemPaise,
    required this.preparationChargePaise,
    this.preparationChargeBasis,
    required this.taxPaise,
    required this.estimatedLineTotalPaise,
    required this.maxLineTotalPaise,
    this.availability,
    required this.isSellable,
    required this.issues,
  });

  factory CartLine.fromJson(Json j) {
    final prep = asJsonOrNull(j['preparation'], 'preparation');
    return CartLine(
      id: reqString(j, 'id'),
      productId: reqString(j, 'productId'),
      productSlug: reqString(j, 'productSlug'),
      productName: reqString(j, 'productName'),
      variantId: reqString(j, 'variantId'),
      variantName: reqString(j, 'variantName'),
      sellUnit: reqString(j, 'sellUnit'),
      imageUrl: optString(j, 'imageUrl'),
      preparation: prep == null ? null : CartPreparation.fromJson(prep),
      quantity: reqQuantity(j, 'quantity'),
      estimatedNetQuantity: reqQuantity(j, 'estimatedNetQuantity'),
      maxChargeableQuantity: reqQuantity(j, 'maxChargeableQuantity'),
      unitPricePaise: reqInt(j, 'unitPricePaise'),
      compareAtPaise: optInt(j, 'compareAtPaise'),
      priceUnit: reqString(j, 'priceUnit'),
      itemPaise: reqInt(j, 'itemPaise'),
      preparationChargePaise: optInt(j, 'preparationChargePaise') ?? 0,
      preparationChargeBasis: optString(j, 'preparationChargeBasis'),
      taxPaise: optInt(j, 'taxPaise') ?? 0,
      estimatedLineTotalPaise: reqInt(j, 'estimatedLineTotalPaise'),
      maxLineTotalPaise: reqInt(j, 'maxLineTotalPaise'),
      availability: optAvailability(j, 'availability'),
      isSellable: optBool(j, 'isSellable') ?? true,
      issues: listOrEmpty(j, 'issues', CodedMessage.fromJson),
    );
  }

  final String id;
  final String productId;
  final String productSlug;
  final String productName;
  final String variantId;
  final String variantName;
  final String sellUnit;
  final String? imageUrl;
  final CartPreparation? preparation;
  final Quantity quantity;
  final Quantity estimatedNetQuantity;
  final Quantity maxChargeableQuantity;
  final int unitPricePaise;
  final int? compareAtPaise;
  final String priceUnit;
  final int itemPaise;
  final int preparationChargePaise;
  final String? preparationChargeBasis;
  final int taxPaise;
  final int estimatedLineTotalPaise;
  final int maxLineTotalPaise;
  final Availability? availability;
  final bool isSellable;
  final List<CodedMessage> issues;
}

/// Coupon status APPLIED, NOT_APPLICABLE or INVALID (§5.3).
class CartCoupon {
  const CartCoupon({required this.code, required this.status, required this.discountPaise, this.message});

  factory CartCoupon.fromJson(Json j) => CartCoupon(
    code: reqString(j, 'code'),
    status: reqString(j, 'status'),
    discountPaise: optInt(j, 'discountPaise') ?? 0,
    message: optString(j, 'message'),
  );

  final String code;
  final String status;
  final int discountPaise;
  final String? message;
}

/// A coupon the customer may use (GET /coupons, for the pincode's city): the code, the owner's description and
/// the minimum order, all as the server sends them. The app never works out a discount from it.
class VisibleCoupon {
  const VisibleCoupon({
    required this.code,
    this.description,
    required this.minOrderPaise,
    this.validTo,
    required this.firstOrderOnly,
  });

  factory VisibleCoupon.fromJson(Json j) => VisibleCoupon(
    code: reqString(j, 'code'),
    description: optString(j, 'description'),
    minOrderPaise: optInt(j, 'minOrderPaise') ?? 0,
    validTo: optInstant(j, 'validTo'),
    firstOrderOnly: optBool(j, 'firstOrderOnly') ?? false,
  );

  final String code;
  final String? description;
  final int minOrderPaise;
  final DateTime? validTo;
  final bool firstOrderOnly;
}

CartCoupon? optCoupon(Json j) {
  final v = asJsonOrNull(j['coupon'], 'coupon');
  return v == null ? null : CartCoupon.fromJson(v);
}

/// Totals (§5.2). Fields that only the cart sends are nullable so the same class serves preview totals.
class CartTotals {
  const CartTotals({
    required this.itemsPaise,
    required this.preparationPaise,
    required this.subtotalPaise,
    required this.discountPaise,
    required this.deliveryFeePaise,
    required this.deliveryFeeWaived,
    required this.taxPaise,
    required this.estimatedTotalPaise,
    required this.maxTotalPaise,
    this.minOrderPaise,
    this.minOrderShortfallPaise,
    this.freeDeliveryShortfallPaise,
  });

  factory CartTotals.fromJson(Json j) => CartTotals(
    itemsPaise: reqInt(j, 'itemsPaise'),
    preparationPaise: optInt(j, 'preparationPaise') ?? 0,
    subtotalPaise: reqInt(j, 'subtotalPaise'),
    discountPaise: optInt(j, 'discountPaise') ?? 0,
    deliveryFeePaise: optInt(j, 'deliveryFeePaise') ?? 0,
    deliveryFeeWaived: optBool(j, 'deliveryFeeWaived') ?? false,
    taxPaise: optInt(j, 'taxPaise') ?? 0,
    estimatedTotalPaise: reqInt(j, 'estimatedTotalPaise'),
    maxTotalPaise: reqInt(j, 'maxTotalPaise'),
    minOrderPaise: optInt(j, 'minOrderPaise'),
    minOrderShortfallPaise: optInt(j, 'minOrderShortfallPaise'),
    freeDeliveryShortfallPaise: optInt(j, 'freeDeliveryShortfallPaise'),
  );

  final int itemsPaise;
  final int preparationPaise;
  final int subtotalPaise;
  final int discountPaise;
  final int deliveryFeePaise;
  final bool deliveryFeeWaived;
  final int taxPaise;
  final int estimatedTotalPaise;
  final int maxTotalPaise;
  final int? minOrderPaise;
  final int? minOrderShortfallPaise;
  final int? freeDeliveryShortfallPaise;
}

class Cart {
  const Cart({
    this.id,
    this.cartToken,
    this.pincode,
    required this.lines,
    this.coupon,
    this.totals,
    required this.canCheckout,
    required this.checkoutBlockers,
    required this.messages,
  });

  factory Cart.fromJson(Json j) {
    final totals = asJsonOrNull(j['totals'], 'totals');
    return Cart(
      id: optString(j, 'id'),
      cartToken: optString(j, 'cartToken'),
      pincode: optString(j, 'pincode'),
      lines: listOrEmpty(j, 'lines', CartLine.fromJson),
      coupon: optCoupon(j),
      totals: totals == null ? null : CartTotals.fromJson(totals),
      canCheckout: optBool(j, 'canCheckout') ?? false,
      checkoutBlockers: listOrEmpty(j, 'checkoutBlockers', CodedMessage.fromJson),
      messages: stringList(j, 'messages'),
    );
  }

  static const empty = Cart(lines: [], canCheckout: false, checkoutBlockers: [], messages: []);

  final String? id;
  final String? cartToken;
  final String? pincode;
  final List<CartLine> lines;
  final CartCoupon? coupon;
  final CartTotals? totals;
  final bool canCheckout;
  final List<CodedMessage> checkoutBlockers;
  final List<String> messages;

  bool get isEmpty => lines.isEmpty;

  /// Count of lines for the tab badge (a count of rows, not a business value).
  int get lineCount => lines.length;
}

class AddCartItem {
  const AddCartItem({required this.variantId, this.preparationCode, required this.quantity});

  final String variantId;
  final String? preparationCode;
  final Quantity quantity;

  Map<String, dynamic> toJson() => {
    'variantId': variantId,
    'preparationCode': preparationCode,
    'quantity': quantity.toJson(),
  };
}
