// Fixture data for the demo build (--dart-define=DEMO=true). These are fixed answers in the API's documented
// shapes, standing in for the server: every price, total and status is written here as the server would send
// it, and nothing in the app computes them. Names are samples, no real city or store.

import '../api/models/json.dart';

const demoPincode = '600041';

/// Typing this pincode on the location screen shows the not-serviceable state.
const demoUnservedPincode = '999999';

const _products = <Json>[
  {
    'id': 'p1',
    'slug': 'seer-fish',
    'name': 'Seer Fish',
    'localNames': ['Vanjaram', 'Surmai'],
    'category': 'fish',
    'price': 129900,
    'compareAt': 149900,
    'status': 'IN_STOCK',
    'description': 'Firm, mildly sweet flesh; ideal for fry and curry.',
  },
  {
    'id': 'p2',
    'slug': 'tiger-prawns',
    'name': 'Tiger Prawns',
    'localNames': ['Eral', 'Jhinga'],
    'category': 'prawns',
    'price': 89900,
    'compareAt': null,
    'status': 'IN_STOCK',
    'description': 'Large, sweet prawns with a firm bite. Great for roast and biryani.',
  },
  {
    'id': 'p3',
    'slug': 'mud-crab',
    'name': 'Mud Crab',
    'localNames': ['Nandu', 'Kekda'],
    'category': 'crabs',
    'price': 74900,
    'compareAt': null,
    'status': 'LOW_STOCK',
    'description': 'Meaty claws and rich flavour; best in pepper masala.',
  },
  {
    'id': 'p4',
    'slug': 'white-pomfret',
    'name': 'White Pomfret',
    'localNames': ['Vavval', 'Paplet'],
    'category': 'fish',
    'price': 109900,
    'compareAt': 119900,
    'status': 'IN_STOCK',
    'description': 'Soft, delicate flesh with few bones. A family favourite for tawa fry.',
  },
  {
    'id': 'p5',
    'slug': 'sardine',
    'name': 'Sardine',
    'localNames': ['Mathi', 'Chaala'],
    'category': 'fish',
    'price': 24900,
    'compareAt': null,
    'status': 'LOW_STOCK',
    'description': 'Small, oily and full of flavour. Fry crisp or cook in a tangy curry.',
  },
  {
    'id': 'p6',
    'slug': 'squid',
    'name': 'Squid',
    'localNames': ['Kanava'],
    'category': 'shellfish',
    'price': 54900,
    'compareAt': null,
    'status': 'OUT_OF_STOCK',
    'description': 'Tender rings and tentacles for a quick stir fry.',
  },
];

Json _summary(Json p) => {
  'id': p['id'],
  'slug': p['slug'],
  'name': p['name'],
  'localNames': p['localNames'],
  'sourceType': 'WILD',
  'imageUrl': null,
  'fromPrice': {'unitPricePaise': p['price'], 'compareAtPaise': p['compareAt'], 'perUnit': 'KG'},
  'availability': {'status': p['status'], 'message': null},
  'locationRequired': false,
};

List<Json> demoProducts({String? category, String? q}) => [
  for (final p in _products)
    if ((category == null || p['category'] == category) && (q == null || _matches(p, q))) _summary(p),
];

bool _matches(Json p, String q) {
  final needle = q.trim().toLowerCase();
  if (needle.isEmpty) return true;
  final names = [p['name'] as String, ...(p['localNames']! as List<Object?>).cast<String>()];
  return names.any((n) => n.toLowerCase().contains(needle));
}

List<Json> demoSuggestions(String q) => [
  for (final p in _products)
    if (_matches(p, q))
      {'name': p['name'], 'slug': p['slug'], 'imageUrl': null, 'matchedLocalName': (p['localNames']! as List).first},
];

const demoCategories = <Json>[
  {'id': 'c1', 'slug': 'fish', 'name': 'Fish'},
  {'id': 'c2', 'slug': 'prawns', 'name': 'Prawns'},
  {'id': 'c3', 'slug': 'crabs', 'name': 'Crabs'},
  {'id': 'c4', 'slug': 'shellfish', 'name': 'Squid and shellfish'},
];

Json demoHome({required bool empty}) => {
  'banners': [
    {'title': 'Weekend seer fish', 'subtitle': 'Steaks cut the way you like them.', 'badge': 'Offer'},
    {'title': 'Prawns for biryani', 'subtitle': 'Cleaned and deveined, ready to cook.', 'badge': 'New'},
  ],
  'featuredCategories': demoCategories,
  'featuredProducts': empty ? <Json>[] : demoProducts(),
  'locationRequired': false,
};

Json? demoProductDetail(String slug) {
  final p = _products.where((e) => e['slug'] == slug).firstOrNull;
  if (p == null) return null;
  final price = p['price']! as int;
  return {
    'id': p['id'],
    'slug': p['slug'],
    'name': p['name'],
    'localNames': p['localNames'],
    'description': p['description'],
    'sourceType': 'WILD',
    'sourceRegion': 'Sample harbour',
    'storageInstructions': 'Keep at 0-4 °C; cook within 24 hours.',
    'category': {'slug': p['category'], 'name': p['category']},
    'images': <Object?>[],
    'variants': [
      {
        'id': 'v-${p['id']}',
        'name': 'Medium',
        'sellUnit': 'KG',
        'isWeightVariable': true,
        'minOrder': {'value': 500, 'unit': 'GRAM'},
        'maxOrder': {'value': 3000, 'unit': 'GRAM'},
        'orderStep': {'value': 250, 'unit': 'GRAM'},
        'unitPricePaise': price,
        'compareAtPaise': p['compareAt'],
        'preparations': [
          {'code': 'WHOLE', 'name': 'Whole, uncleaned', 'yieldBp': 10000, 'chargePaise': 0, 'chargeBasis': 'FLAT'},
          {
            'code': 'CLEANED',
            'name': 'Cleaned',
            'yieldBp': 8000,
            'isDefault': true,
            'chargePaise': 3000,
            'chargeBasis': 'PER_KG',
          },
          {'code': 'STEAK', 'name': 'Steaks (1 cm)', 'yieldBp': 7000, 'chargePaise': 4000, 'chargeBasis': 'PER_KG'},
        ],
        'availability': {
          'status': p['status'],
          'maxOrderableNow': {'value': p['status'] == 'OUT_OF_STOCK' ? 0 : 3000, 'unit': 'GRAM'},
          'message': null,
        },
      },
    ],
    'netWeightNote': 'You pay for net weight after cleaning. Final weight may vary up to 10% above the ordered weight.',
    'isWishlisted': false,
  };
}

Json demoServiceability(String pincode) => pincode == demoUnservedPincode
    ? {'pincode': pincode, 'serviceable': false, 'reasonCode': 'PINCODE_NOT_SERVICEABLE'}
    : {
        'pincode': pincode,
        'serviceable': true,
        'city': {'id': 'c1', 'code': 'SAMPLE_CITY', 'name': 'Sample City'},
        'fulfillmentLocation': {'id': 'l1', 'code': 'SAMPLE_STORE', 'name': 'Sample Store'},
        'serviceArea': {'deliveryFeePaise': 3900, 'freeDeliveryAbovePaise': 49900, 'minOrderPaise': 29900},
        'nextSlot': {'id': 's1', 'startAt': '2026-10-24T08:30:00Z', 'endAt': '2026-10-24T10:30:00Z'},
      };

const demoSlots = <String, Object?>{
  'fulfillmentLocationId': 'l1',
  'items': [
    {
      'id': 's1',
      'slotDate': '2026-10-24',
      'startAt': '2026-10-24T08:30:00Z',
      'endAt': '2026-10-24T10:30:00Z',
      'cutoffAt': '2026-10-24T06:30:00Z',
      'status': 'AVAILABLE',
    },
    {
      'id': 's2',
      'slotDate': '2026-10-24',
      'startAt': '2026-10-24T12:30:00Z',
      'endAt': '2026-10-24T14:30:00Z',
      'cutoffAt': '2026-10-24T10:30:00Z',
      'status': 'FULL',
    },
  ],
};

Json _line({
  required String id,
  required String productId,
  required String slug,
  required String name,
  required int unitPrice,
  required int itemPaise,
  required int prepPaise,
  required int estimated,
  required int max,
}) => {
  'id': id,
  'productId': productId,
  'productSlug': slug,
  'productName': name,
  'variantId': 'v-$productId',
  'variantName': 'Medium',
  'sellUnit': 'KG',
  'imageUrl': null,
  'preparation': {'code': 'CLEANED', 'name': 'Cleaned', 'yieldBp': 8000},
  'quantity': {'value': 500, 'unit': 'GRAM'},
  'estimatedNetQuantity': {'value': 500, 'unit': 'GRAM'},
  'maxChargeableQuantity': {'value': 550, 'unit': 'GRAM'},
  'unitPricePaise': unitPrice,
  'priceUnit': 'KG',
  'itemPaise': itemPaise,
  'preparationChargePaise': prepPaise,
  'preparationChargeBasis': 'PER_KG',
  'taxRateBp': 0,
  'taxPaise': 0,
  'estimatedLineTotalPaise': estimated,
  'maxLineTotalPaise': max,
  'availability': {'status': 'IN_STOCK', 'message': null},
  'isSellable': true,
  'issues': <Object?>[],
};

final _cartLines = [
  _line(
    id: 'line1',
    productId: 'p1',
    slug: 'seer-fish',
    name: 'Seer Fish',
    unitPrice: 129900,
    itemPaise: 64950,
    prepPaise: 1500,
    estimated: 66450,
    max: 73095,
  ),
  _line(
    id: 'line2',
    productId: 'p2',
    slug: 'tiger-prawns',
    name: 'Tiger Prawns',
    unitPrice: 89900,
    itemPaise: 44950,
    prepPaise: 1500,
    estimated: 46450,
    max: 51095,
  ),
];

const _cartTotals = <String, Object?>{
  'itemsPaise': 109900,
  'preparationPaise': 3000,
  'subtotalPaise': 112900,
  'discountPaise': 0,
  'deliveryFeePaise': 0,
  'deliveryFeeWaived': true,
  'taxPaise': 0,
  'estimatedTotalPaise': 112900,
  'maxTotalPaise': 124190,
  'minOrderPaise': 29900,
  'minOrderShortfallPaise': 0,
  'freeDeliveryShortfallPaise': 0,
};

Json demoCart() => {
  'id': 'cart1',
  'cartToken': null,
  'pincode': demoPincode,
  'fulfillmentLocationCode': 'SAMPLE_STORE',
  'lines': _cartLines,
  'coupon': null,
  'totals': _cartTotals,
  'canCheckout': true,
  'checkoutBlockers': <Object?>[],
  'messages': ['Weight-based items are charged on actual net weight, up to 10% over the ordered weight.'],
};

Json demoPreview(String addressId, String slotId, String paymentMethod) => {
  'cartId': 'cart1',
  'address': {
    'id': addressId,
    'label': 'HOME',
    'line1': '12, 3rd Cross Street',
    'locality': 'Sample Nagar',
    'pincode': demoPincode,
    'isServiceable': true,
  },
  'fulfillmentLocationCode': 'SAMPLE_STORE',
  'slot': {
    'id': slotId,
    'startAt': '2026-10-24T08:30:00Z',
    'endAt': '2026-10-24T10:30:00Z',
    'cutoffAt': '2026-10-24T06:30:00Z',
    'status': 'AVAILABLE',
  },
  'lines': _cartLines,
  'coupon': null,
  'totals': _cartTotals,
  'paymentMethods': [
    {'method': 'COD', 'enabled': true, 'reasonCode': null},
    {'method': 'UPI_ON_DELIVERY', 'enabled': true, 'reasonCode': null},
    {'method': 'ONLINE', 'enabled': false, 'reasonCode': 'PAYMENT_METHOD_NOT_ALLOWED'},
  ],
  'canPlaceOrder': true,
  'blockers': <Object?>[],
  'expectedTotalPaise': 112900,
};

/// Demo orders: one per tracking state, so every illustration can be seen.
const demoOrderStatuses = <String, String>{
  'o1': 'CONFIRMED',
  'o2': 'PACKED',
  'o3': 'OUT_FOR_DELIVERY',
  'o4': 'DELIVERED',
  'o5': 'CANCELLED',
};

const _timelines = <String, List<String>>{
  'CONFIRMED': ['CONFIRMED'],
  'PACKED': ['CONFIRMED', 'PREPARING', 'PACKED'],
  'OUT_FOR_DELIVERY': ['CONFIRMED', 'PREPARING', 'PACKED', 'OUT_FOR_DELIVERY'],
  'DELIVERED': ['CONFIRMED', 'PREPARING', 'PACKED', 'OUT_FOR_DELIVERY', 'DELIVERED'],
  'CANCELLED': ['CONFIRMED', 'CANCELLED'],
};

String _number(String id) => 'FO-26${id.substring(1).padLeft(4, '0')}';

Json? demoOrder(String id) {
  final status = demoOrderStatuses[id];
  if (status == null) return null;
  final delivered = status == 'DELIVERED';
  return {
    'id': id,
    'orderNumber': _number(id),
    'version': '1',
    'status': status,
    'paymentMethod': id == 'o2' ? 'UPI_ON_DELIVERY' : 'COD',
    'placedAt': '2026-10-24T04:${id.substring(1)}0:00Z',
    'slot': {'startAt': '2026-10-24T08:30:00Z', 'endAt': '2026-10-24T10:30:00Z'},
    'address': {
      'recipientName': 'Sample Customer',
      'phone': '+919876543210',
      'line1': '12, 3rd Cross Street',
      'locality': 'Sample Nagar',
      'cityName': 'Sample City',
      'stateCode': 'TN',
      'pincode': demoPincode,
    },
    'substitutionPreference': 'CALL_ME',
    'items': [
      {
        'id': 'oi1',
        'lineNo': 1,
        'productName': 'Seer Fish',
        'variantName': 'Medium',
        'preparationName': 'Cleaned',
        'orderedQuantity': {'value': 500, 'unit': 'GRAM'},
        'maxChargeableQuantity': {'value': 550, 'unit': 'GRAM'},
        'packedQuantity': delivered ? {'value': 520, 'unit': 'GRAM'} : null,
        'chargedQuantity': delivered ? {'value': 520, 'unit': 'GRAM'} : null,
        'unitPricePaise': 129900,
        'preparationChargePaise': 1500,
        'estimatedTotalPaise': 66450,
        'finalTotalPaise': delivered ? 69108 : null,
        'status': 'ACTIVE',
      },
      {
        'id': 'oi2',
        'lineNo': 2,
        'productName': 'Tiger Prawns',
        'variantName': 'Medium',
        'preparationName': 'Cleaned',
        'orderedQuantity': {'value': 500, 'unit': 'GRAM'},
        'maxChargeableQuantity': {'value': 550, 'unit': 'GRAM'},
        'packedQuantity': delivered ? {'value': 495, 'unit': 'GRAM'} : null,
        'chargedQuantity': delivered ? {'value': 495, 'unit': 'GRAM'} : null,
        'unitPricePaise': 89900,
        'preparationChargePaise': 1500,
        'estimatedTotalPaise': 46450,
        'finalTotalPaise': delivered ? 45985 : null,
        'status': 'ACTIVE',
      },
    ],
    'estimate': {
      'itemsPaise': 109900,
      'preparationPaise': 3000,
      'discountPaise': 0,
      'deliveryFeePaise': 0,
      'taxPaise': 0,
      'totalPaise': 112900,
    },
    'final': delivered
        ? {
            'itemsPaise': 112048,
            'preparationPaise': 3045,
            'discountPaise': 0,
            'deliveryFeePaise': 0,
            'taxPaise': 0,
            'totalPaise': 115093,
          }
        : null,
    'amountDuePaise': delivered || status == 'CANCELLED' ? 0 : 112900,
    'amountPaidPaise': delivered ? 115093 : 0,
    'amountRefundedPaise': 0,
    'couponCode': null,
    'substitutions': <Object?>[],
    'payment': {'method': 'COD', 'status': delivered ? 'CAPTURED' : 'PENDING', 'nextAction': null},
    'invoice': null,
    'allowedActions': {
      'cancel': status == 'CONFIRMED',
      'raiseComplaint': delivered,
      'downloadInvoice': delivered,
      'reorder': delivered || status == 'CANCELLED',
    },
  };
}

Json? demoTracking(String id) {
  final status = demoOrderStatuses[id];
  if (status == null) return null;
  final steps = _timelines[status]!;
  return {
    'orderNumber': _number(id),
    'status': status,
    'timeline': [
      for (var i = 0; i < steps.length; i++) {'status': steps[i], 'at': '2026-10-24T0${4 + i}:15:00Z'},
    ],
    'slot': {'startAt': '2026-10-24T08:30:00Z', 'endAt': '2026-10-24T10:30:00Z'},
    'shipment': status == 'OUT_FOR_DELIVERY' ? {'status': 'DISPATCHED', 'riderName': 'Sample Rider'} : null,
    'deliveryCode': status == 'OUT_FOR_DELIVERY' ? '4821' : null,
    'amountDuePaise': status == 'OUT_FOR_DELIVERY' ? 112900 : null,
  };
}

Json demoOrderSummary(String id) {
  final o = demoOrder(id)!;
  return {
    'id': id,
    'orderNumber': o['orderNumber'],
    'status': o['status'],
    'placedAt': o['placedAt'],
    'slot': o['slot'],
    'estimate': o['estimate'],
    'final': o['final'],
    'itemCount': 2,
  };
}

const demoProfile = <String, Object?>{
  'customerId': 'cust1',
  'phone': '+919876543210',
  'fullName': 'Sample Customer',
  'email': null,
  'marketingOptIn': false,
  'status': 'ACTIVE',
  'version': '1',
};

const demoAddress = <String, Object?>{
  'id': 'a1',
  'label': 'HOME',
  'recipientName': 'Sample Customer',
  'phone': '+919876543210',
  'line1': '12, 3rd Cross Street',
  'locality': 'Sample Nagar',
  'cityName': 'Sample City',
  'stateCode': 'TN',
  'pincode': demoPincode,
  'isDefault': true,
  'isServiceable': true,
  'version': '1',
};

List<Json> demoWishlist() => [
  for (final p in demoProducts().where((p) => p['id'] == 'p4' || p['id'] == 'p6')) {...p, 'productId': p['id']},
];

const demoInbox = <String, Object?>{
  'items': [
    {
      'messageId': 'm1',
      'title': 'Your order is on the way',
      'body': 'Order FO-260003 is out for delivery. Share the delivery code when it arrives.',
      'isRead': false,
      'createdAt': '2026-10-24T07:15:00Z',
    },
    {
      'messageId': 'm2',
      'title': 'Order delivered',
      'body': 'Order FO-260004 was delivered. Enjoy your meal!',
      'isRead': true,
      'createdAt': '2026-10-23T09:40:00Z',
    },
  ],
  'unreadCount': 1,
  'totalCount': 2,
};

const demoPreferences = <Json>[
  {'category': 'ORDER_UPDATES', 'channel': 'SMS', 'enabled': true},
  {'category': 'ORDER_UPDATES', 'channel': 'PUSH', 'enabled': true},
  {'category': 'OFFERS', 'channel': 'PUSH', 'enabled': false},
];
