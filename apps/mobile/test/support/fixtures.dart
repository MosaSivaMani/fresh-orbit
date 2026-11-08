// JSON fixtures copied from the examples in docs/api/README.md, parsed with the app's models (so a model that
// drifts from the documented shape fails here).

import 'package:fresh_orbit_app/api/models/cart.dart';
import 'package:fresh_orbit_app/api/models/catalog.dart';
import 'package:fresh_orbit_app/api/models/checkout.dart';
import 'package:fresh_orbit_app/api/models/locations.dart';
import 'package:fresh_orbit_app/api/models/orders.dart';

Map<String, Object?> serviceableJson() => {
  'pincode': '600041',
  'serviceable': true,
  'reasonCode': null,
  'city': {'id': 'c1', 'code': 'CITY1', 'name': 'Sample City'},
  'fulfillmentLocation': {'id': 'l1', 'code': 'LOC1', 'name': 'Sample Store', 'hasColdStorage': true},
  'serviceArea': {
    'id': 'sa1',
    'name': 'Sample Area',
    'deliveryFeePaise': 3900,
    'freeDeliveryAbovePaise': 49900,
    'minOrderPaise': 29900,
  },
  'nextSlot': {'id': 's1', 'startAt': '2026-10-23T08:30:00Z', 'endAt': '2026-10-23T10:30:00Z'},
};

Serviceability serviceable() => Serviceability.fromJson(serviceableJson());

Serviceability notServiceable(String pincode) => Serviceability.fromJson({
  'pincode': pincode,
  'serviceable': false,
  'reasonCode': 'PINCODE_NOT_SERVICEABLE',
  'city': null,
  'fulfillmentLocation': null,
  'serviceArea': null,
  'nextSlot': null,
});

Map<String, Object?> productDetailJson() => {
  'id': 'p1',
  'slug': 'seer-fish-vanjaram',
  'name': 'Seer Fish',
  'localNames': ['Vanjaram', 'Neymeen', 'Surmai'],
  'speciesScientificName': 'Scomberomorus commerson',
  'description': 'Firm, mildly sweet flesh; ideal for fry and curry.',
  'sourceType': 'WILD',
  'sourceRegion': 'Sample harbour',
  'storageInstructions': 'Keep at 0-4 °C; cook within 24 hours.',
  'category': {'slug': 'fish', 'name': 'Fish'},
  'images': <Object?>[],
  'location': {'fulfillmentLocationCode': 'LOC1', 'pincode': '600041'},
  'variants': [
    {
      'id': 'v1',
      'sku': 'SEER-KG',
      'name': 'Medium (3-5 kg fish)',
      'sellUnit': 'KG',
      'isWeightVariable': true,
      'minOrder': {'value': 500, 'unit': 'GRAM'},
      'maxOrder': {'value': 5000, 'unit': 'GRAM'},
      'orderStep': {'value': 250, 'unit': 'GRAM'},
      'unitPricePaise': 129900,
      'compareAtPaise': 149900,
      'preparations': [
        {
          'code': 'WHOLE',
          'name': 'Whole, uncleaned',
          'yieldBp': 10000,
          'isDefault': false,
          'chargePaise': 0,
          'chargeBasis': 'FLAT',
        },
        {
          'code': 'STEAK',
          'name': 'Steaks (1 cm)',
          'yieldBp': 7000,
          'isDefault': true,
          'chargePaise': 4000,
          'chargeBasis': 'PER_KG',
        },
        {
          'code': 'FILLET',
          'name': 'Boneless fillet',
          'yieldBp': 5500,
          'isDefault': false,
          'chargePaise': 8000,
          'chargeBasis': 'PER_KG',
        },
      ],
      'availability': {
        'status': 'IN_STOCK',
        'maxOrderableNow': {'value': 5000, 'unit': 'GRAM'},
        'message': null,
      },
    },
  ],
  'netWeightNote': 'Prices are for net weight after cleaning. If your pack weighs less, you pay less.',
  'isWishlisted': false,
};

ProductDetail productDetail() => ProductDetail.fromJson(productDetailJson());

/// D348: a fixed net-weight pack of seer steaks, one price per pack (`sellUnit` PACK).
Map<String, Object?> packVariantJson() => {
  'id': 'v2',
  'sku': 'SEER-STEAK-500',
  'name': 'Steaks, 500 g net',
  'sellUnit': 'PACK',
  'isWeightVariable': false,
  'packSizeGrams': 500,
  'avgPieceGrams': null,
  'minOrder': {'value': 1, 'unit': 'PACK'},
  'maxOrder': {'value': 10, 'unit': 'PACK'},
  'orderStep': {'value': 1, 'unit': 'PACK'},
  'unitPricePaise': 74900,
  'compareAtPaise': null,
  'preparations': [
    {
      'code': 'STEAK',
      'name': 'Steaks (1 cm)',
      'yieldBp': 10000,
      'isDefault': true,
      'chargePaise': 0,
      'chargeBasis': 'FLAT',
    },
  ],
  'availability': {
    'status': 'IN_STOCK',
    'maxOrderableNow': {'value': 10, 'unit': 'PACK'},
    'message': null,
  },
  'serving': {
    'basis': {'value': 1, 'unit': 'PACK'},
    'netWeightGrams': 500,
    'piecesMin': 5,
    'piecesMax': 7,
    'serves': 3,
  },
};

/// The seer fish product with the pack size as its only size.
Map<String, Object?> packProductDetailJson() => {
  ...productDetailJson(),
  'variants': [packVariantJson()],
};

/// D348: one line of 1 pack; the price is the pack price and never changes.
Map<String, Object?> packCartLineJson({String id = 'line2', int packs = 1}) => {
  'id': id,
  'productId': 'p1',
  'productSlug': 'seer-fish-vanjaram',
  'productName': 'Seer Fish (Vanjaram)',
  'variantId': 'v2',
  'variantName': 'Steaks, 500 g net',
  'sellUnit': 'PACK',
  'imageUrl': null,
  'preparation': {'code': 'STEAK', 'name': 'Steaks (1 cm)', 'yieldBp': 10000},
  'quantity': {'value': packs, 'unit': 'PACK'},
  'estimatedNetQuantity': {'value': packs, 'unit': 'PACK'},
  'maxChargeableQuantity': {'value': packs, 'unit': 'PACK'},
  'unitPricePaise': 74900,
  'compareAtPaise': null,
  'priceUnit': 'PACK',
  'itemPaise': 74900 * packs,
  'preparationChargePaise': 0,
  'preparationChargeBasis': null,
  'taxRateBp': 0,
  'taxPaise': 0,
  'estimatedLineTotalPaise': 74900 * packs,
  'maxLineTotalPaise': 74900 * packs,
  'packNetGrams': 500,
  'piecesMin': 5,
  'piecesMax': 7,
  'availability': {'status': 'IN_STOCK', 'message': null},
  'isSellable': true,
  'issues': <Object?>[],
};

Map<String, Object?> cartLineJson({
  String id = 'line1',
  int quantity = 500,
  int estimated = 66950,
  // D347: no over-weight charge by default, so the maximum equals the estimate. A test may pass a larger
  // [max] / [maxQuantity] to stand for an operator-configured over-weight allowance.
  int? max,
  int? maxQuantity,
  String availability = 'IN_STOCK',
  String? availabilityMessage,
}) => {
  'id': id,
  'productId': 'p1',
  'productSlug': 'seer-fish-vanjaram',
  'productName': 'Seer Fish (Vanjaram)',
  'variantId': 'v1',
  'variantName': 'Medium (3-5 kg fish)',
  'sellUnit': 'KG',
  'imageUrl': null,
  'preparation': {'code': 'STEAK', 'name': 'Steaks (1 cm)', 'yieldBp': 7000},
  'quantity': {'value': quantity, 'unit': 'GRAM'},
  'estimatedNetQuantity': {'value': quantity, 'unit': 'GRAM'},
  'maxChargeableQuantity': {'value': maxQuantity ?? quantity, 'unit': 'GRAM'},
  'unitPricePaise': 129900,
  'compareAtPaise': 149900,
  'priceUnit': 'KG',
  'itemPaise': 64950,
  'preparationChargePaise': 2000,
  'preparationChargeBasis': 'PER_KG',
  'taxRateBp': 0,
  'taxPaise': 0,
  'estimatedLineTotalPaise': estimated,
  'maxLineTotalPaise': max ?? estimated,
  'availability': {'status': availability, 'message': availabilityMessage},
  'isSellable': true,
  'issues': <Object?>[],
};

Map<String, Object?> cartJson({
  List<Map<String, Object?>>? lines,
  int estimatedTotal = 70850,
  int? maxTotal,
  bool canCheckout = true,
  List<Map<String, Object?>> blockers = const [],
  Map<String, Object?>? coupon,
}) => {
  'id': 'cart1',
  'cartToken': null,
  'pincode': '600041',
  'fulfillmentLocationCode': 'LOC1',
  'lines': lines ?? [cartLineJson()],
  'coupon': coupon,
  'totals': {
    'itemsPaise': 64950,
    'preparationPaise': 2000,
    'subtotalPaise': 66950,
    'discountPaise': 0,
    'deliveryFeePaise': 3900,
    'deliveryFeeWaived': false,
    'taxPaise': 0,
    'estimatedTotalPaise': estimatedTotal,
    'maxTotalPaise': maxTotal ?? estimatedTotal,
    'minOrderPaise': 29900,
    'minOrderShortfallPaise': 0,
    'freeDeliveryShortfallPaise': 0,
  },
  'canCheckout': canCheckout,
  'checkoutBlockers': blockers,
  'messages': <String>[],
};

Cart cart({List<Map<String, Object?>>? lines, int estimatedTotal = 70850, Map<String, Object?>? coupon}) =>
    Cart.fromJson(cartJson(lines: lines, estimatedTotal: estimatedTotal, coupon: coupon));

Map<String, Object?> previewJson({
  int expectedTotal = 70850,
  int? maxTotal,
  bool canPlaceOrder = true,
  List<Map<String, Object?>> blockers = const [],
}) => {
  'cartId': 'cart1',
  'address': {
    'id': 'a1',
    'label': 'HOME',
    'line1': '12, 3rd Cross Street',
    'locality': 'Sample Nagar',
    'pincode': '600041',
    'isServiceable': true,
  },
  'fulfillmentLocationCode': 'LOC1',
  'slot': {
    'id': 's1',
    'startAt': '2026-10-23T08:30:00Z',
    'endAt': '2026-10-23T10:30:00Z',
    'cutoffAt': '2026-10-23T06:30:00Z',
    'status': 'AVAILABLE',
  },
  'lines': [cartLineJson()],
  'coupon': null,
  'totals': {
    'itemsPaise': 64950,
    'preparationPaise': 2000,
    'subtotalPaise': 66950,
    'discountPaise': 0,
    'deliveryFeePaise': 3900,
    'deliveryFeeWaived': false,
    'taxPaise': 0,
    'estimatedTotalPaise': expectedTotal,
    'maxTotalPaise': maxTotal ?? expectedTotal,
  },
  'paymentMethods': [
    {'method': 'COD', 'enabled': true, 'reasonCode': null},
    {'method': 'UPI_ON_DELIVERY', 'enabled': true, 'reasonCode': null},
    {'method': 'ONLINE', 'enabled': false, 'reasonCode': 'PAYMENT_METHOD_NOT_ALLOWED'},
  ],
  'canPlaceOrder': canPlaceOrder,
  'blockers': blockers,
  'expectedTotalPaise': expectedTotal,
};

CheckoutPreview preview({
  int expectedTotal = 70850,
  bool canPlaceOrder = true,
  List<Map<String, Object?>> blockers = const [],
}) => CheckoutPreview.fromJson(
  previewJson(expectedTotal: expectedTotal, canPlaceOrder: canPlaceOrder, blockers: blockers),
);

Map<String, Object?> orderJson({String status = 'CONFIRMED', Map<String, Object?>? allowedActions}) => {
  'id': 'o1',
  'orderNumber': 'FO-260001',
  'version': '918273',
  'status': status,
  'paymentMethod': 'COD',
  'placedAt': '2026-10-23T05:41:12Z',
  'slot': {'startAt': '2026-10-23T08:30:00Z', 'endAt': '2026-10-23T10:30:00Z'},
  'address': {
    'recipientName': 'Sample Customer',
    'phone': '+919876543210',
    'line1': '12, 3rd Cross Street',
    'line2': null,
    'landmark': null,
    'locality': 'Sample Nagar',
    'cityName': 'Sample City',
    'stateCode': 'TN',
    'pincode': '600041',
  },
  'substitutionPreference': 'CALL_ME',
  'items': [
    {
      'id': 'oi1',
      'lineNo': 1,
      'productName': 'Seer Fish (Vanjaram)',
      'variantName': 'Medium (3-5 kg fish)',
      'preparationName': 'Steaks (1 cm)',
      'orderedQuantity': {'value': 500, 'unit': 'GRAM'},
      'maxChargeableQuantity': {'value': 500, 'unit': 'GRAM'},
      'packedQuantity': null,
      'chargedQuantity': null,
      'unitPricePaise': 129900,
      'preparationChargePaise': 2000,
      'estimatedTotalPaise': 66950,
      'finalTotalPaise': null,
      'status': 'ACTIVE',
    },
  ],
  'estimate': {
    'itemsPaise': 64950,
    'preparationPaise': 2000,
    'discountPaise': 0,
    'deliveryFeePaise': 3900,
    'taxPaise': 0,
    'totalPaise': 70850,
  },
  'final': null,
  'amountDuePaise': 70850,
  'amountPaidPaise': 0,
  'amountRefundedPaise': 0,
  'couponCode': null,
  'substitutions': <Object?>[],
  'payment': {'method': 'COD', 'status': 'PENDING', 'nextAction': null},
  'invoice': null,
  'allowedActions':
      allowedActions ?? {'cancel': true, 'raiseComplaint': false, 'downloadInvoice': false, 'reorder': true},
};

OrderDetail order({String status = 'CONFIRMED', Map<String, Object?>? allowedActions}) =>
    OrderDetail.fromJson(orderJson(status: status, allowedActions: allowedActions));

DeliverySlots slots() => DeliverySlots.fromJson({
  'fulfillmentLocationId': 'l1',
  'items': [
    {
      'id': 's1',
      'slotDate': '2026-10-23',
      'startAt': '2026-10-23T08:30:00Z',
      'endAt': '2026-10-23T10:30:00Z',
      'cutoffAt': '2026-10-23T06:30:00Z',
      'status': 'AVAILABLE',
    },
    {
      'id': 's2',
      'slotDate': '2026-10-23',
      'startAt': '2026-10-23T12:30:00Z',
      'endAt': '2026-10-23T14:30:00Z',
      'cutoffAt': '2026-10-23T10:30:00Z',
      'status': 'FULL',
    },
  ],
});
