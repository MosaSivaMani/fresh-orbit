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
  'netWeightNote': 'You pay for net weight after cleaning. Final weight may vary up to 10% above the ordered weight.',
  'isWishlisted': false,
};

ProductDetail productDetail() => ProductDetail.fromJson(productDetailJson());

Map<String, Object?> cartLineJson({
  String id = 'line1',
  int quantity = 500,
  int estimated = 66950,
  int max = 73645,
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
  'maxChargeableQuantity': {'value': quantity * 11 ~/ 10, 'unit': 'GRAM'},
  'unitPricePaise': 129900,
  'compareAtPaise': 149900,
  'priceUnit': 'KG',
  'itemPaise': 64950,
  'preparationChargePaise': 2000,
  'preparationChargeBasis': 'PER_KG',
  'taxRateBp': 0,
  'taxPaise': 0,
  'estimatedLineTotalPaise': estimated,
  'maxLineTotalPaise': max,
  'availability': {'status': availability, 'message': availabilityMessage},
  'isSellable': true,
  'issues': <Object?>[],
};

Map<String, Object?> cartJson({
  List<Map<String, Object?>>? lines,
  int estimatedTotal = 70850,
  int maxTotal = 77545,
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
    'maxTotalPaise': maxTotal,
    'minOrderPaise': 29900,
    'minOrderShortfallPaise': 0,
    'freeDeliveryShortfallPaise': 0,
  },
  'canCheckout': canCheckout,
  'checkoutBlockers': blockers,
  'messages': ['Weight-based items are charged on actual net weight, up to 10% over the ordered weight.'],
};

Cart cart({List<Map<String, Object?>>? lines, int estimatedTotal = 70850, Map<String, Object?>? coupon}) =>
    Cart.fromJson(cartJson(lines: lines, estimatedTotal: estimatedTotal, coupon: coupon));

Map<String, Object?> previewJson({
  int expectedTotal = 70850,
  int maxTotal = 77545,
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
    'maxTotalPaise': maxTotal,
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
      'maxChargeableQuantity': {'value': 550, 'unit': 'GRAM'},
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
