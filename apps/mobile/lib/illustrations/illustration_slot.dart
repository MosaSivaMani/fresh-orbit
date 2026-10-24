// Illustration slots (D53). Each slot names one place in the app that shows a character illustration. The
// slot codes match `packages/illustrations/manifest.json`; its `sync-mobile` script copies the final images to
// `assets/illustrations/<slot_lower_snake>.webp`. Images contain no text: every label is real UI text.
// ADMIN_* slots exist in the manifest but are not used by the mobile app.

enum IllustrationSlot {
  onboardingFresh('ONBOARDING_FRESH', 'A smiling fishmonger holding up a fresh fish on ice'),
  onboardingCleaned('ONBOARDING_CLEANED', 'A fishmonger cutting fish into neat steaks'),
  onboardingDelivery('ONBOARDING_DELIVERY', 'A delivery rider carrying a cold box to a front door'),
  locationPick('LOCATION_PICK', 'A home cook placing a map pin on a small town map'),
  notServiceable('NOT_SERVICEABLE', 'A rider looking at a map with a question mark'),
  emptyCart('EMPTY_CART', 'An empty shopping basket with a small fish peeking in'),
  emptyWishlist('EMPTY_WISHLIST', 'A small fish holding a heart'),
  emptyOrders('EMPTY_ORDERS', 'A home cook with an empty plate, waiting for a first order'),
  emptyNotifications('EMPTY_NOTIFICATIONS', 'A quiet bell resting next to a sleeping small fish'),
  emptyAddresses('EMPTY_ADDRESSES', 'A small house with an empty doormat'),
  noSearchResults('NO_SEARCH_RESULTS', 'A small fish looking through a magnifying glass'),
  outOfStock('OUT_OF_STOCK', 'A fishmonger showing an empty ice tray'),
  loginOtp('LOGIN_OTP', 'A home cook holding a phone with a message bubble'),
  orderPlaced('ORDER_PLACED', 'A fishmonger giving a cheerful thumbs up next to a wrapped parcel'),
  orderPacking('ORDER_PACKING', 'A fishmonger packing fish into a cold box'),
  orderOnTheWay('ORDER_ON_THE_WAY', 'A delivery rider on a scooter with a cold box'),
  orderDelivered('ORDER_DELIVERED', 'A home cook receiving a cold box at the door'),
  orderCancelled('ORDER_CANCELLED', 'A rider turning back with a gentle wave'),
  complaintReceived('COMPLAINT_RECEIVED', 'A support agent writing a note on a clipboard'),
  helpSupport('HELP_SUPPORT', 'A friendly support agent wearing a headset'),
  errorGeneric('ERROR_GENERIC', 'A small fish looking puzzled at a tangled fishing line'),
  offline('OFFLINE', 'A rider holding a phone with no signal'),
  notFound('NOT_FOUND', 'A small fish lost among sea rocks'),
  homeHero('HOME_HERO', 'A fishmonger and a delivery rider waving beside a cold box of fresh fish'),
  featureHonestWeight('FEATURE_HONEST_WEIGHT', 'A fish on a kitchen scale'),
  featureCleanedYourWay('FEATURE_CLEANED_YOUR_WAY', 'Fish steaks and fillets on a chopping board'),
  featureColdChain('FEATURE_COLD_CHAIN', 'A cold box with ice and a thermometer'),
  featureSameDay('FEATURE_SAME_DAY', 'A rider with a clock and a cold box');

  const IllustrationSlot(this.code, this.semanticLabel);

  /// UPPER_SNAKE code, as in the manifest.
  final String code;

  /// Default alt text, read by screen readers when the illustration is not excluded from semantics.
  final String semanticLabel;

  /// `assets/illustrations/onboarding_fresh.webp` for ONBOARDING_FRESH.
  String get assetPath => 'assets/illustrations/${code.toLowerCase()}.webp';
}

/// The tracking illustration for an order status code. Unknown codes get the placed illustration.
IllustrationSlot slotForOrderStatus(String status) => switch (status) {
  'PREPARING' || 'PACKED' => IllustrationSlot.orderPacking,
  'OUT_FOR_DELIVERY' => IllustrationSlot.orderOnTheWay,
  'DELIVERED' => IllustrationSlot.orderDelivered,
  'CANCELLED' || 'PAYMENT_FAILED' || 'RETURNED' => IllustrationSlot.orderCancelled,
  'DELIVERY_FAILED' => IllustrationSlot.errorGeneric,
  _ => IllustrationSlot.orderPlaced,
};
