// Checkout (docs/api §4.8): choose address, slot, payment → server preview (blockers, totals,
// expectedTotalPaise) → place order with one Idempotency-Key per user action.
//
// Idempotency: the key is created when the customer taps "Place order" and reused only while the outcome is
// unknown (network error, 5xx, IDEMPOTENCY_IN_PROGRESS, CONCURRENT_SAVE). A definitive refusal (4xx) is
// replayed by the server for the same key, so the next attempt gets a new key. The key belongs to one request
// body (D269): any change of selection, notes or expected total (a refreshed preview, an accepted new price)
// starts a new key, because the same key with a different body is refused (422 IDEMPOTENCY_KEY_REUSED).
//
// PRICE_CHANGED (409): the problem's `preview` member is shown to the customer; accepting places the order
// again with the new `expectedTotalPaise` from that preview. The app never computes the difference itself.

import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/api_client.dart';
import '../api/api_error.dart';
import '../api/models/checkout.dart';
import '../api/models/json.dart';
import '../api/models/orders.dart';
import 'cart_state.dart';
import 'core_providers.dart';
import 'location_state.dart';
import 'orders_state.dart';

class CheckoutState {
  const CheckoutState({
    this.addressId,
    this.addressPincode,
    this.slotId,
    this.paymentMethod = 'COD',
    this.substitutionPreference = 'CALL_ME',
    this.notes = '',
    this.preview,
    this.placing = false,
    this.placeError,
    this.priceChanged,
    this.placedOrder,
  });

  final String? addressId;
  final String? addressPincode;
  final String? slotId;
  final String paymentMethod;
  final String substitutionPreference;
  final String notes;

  /// Null until an address and a slot are chosen.
  final AsyncValue<CheckoutPreview>? preview;
  final bool placing;
  final ApiError? placeError;

  /// Set when the server answered PRICE_CHANGED: the new preview to show and confirm.
  final CheckoutPreview? priceChanged;
  final OrderDetail? placedOrder;

  CheckoutSelection? get selection => addressId == null || slotId == null
      ? null
      : CheckoutSelection(
          addressId: addressId!,
          deliverySlotId: slotId!,
          paymentMethod: paymentMethod,
          substitutionPreference: substitutionPreference,
        );

  bool get canPlace {
    final p = preview?.value;
    return p != null && p.canPlaceOrder && !placing && preview is AsyncData<CheckoutPreview>;
  }

  CheckoutState copyWith({
    String? addressId,
    String? addressPincode,
    String? slotId,
    bool clearSlot = false,
    String? paymentMethod,
    String? substitutionPreference,
    String? notes,
    AsyncValue<CheckoutPreview>? preview,
    bool clearPreview = false,
    bool? placing,
    ApiError? placeError,
    bool clearPlaceError = false,
    CheckoutPreview? priceChanged,
    bool clearPriceChanged = false,
    OrderDetail? placedOrder,
  }) => CheckoutState(
    addressId: addressId ?? this.addressId,
    addressPincode: addressPincode ?? this.addressPincode,
    slotId: clearSlot ? null : (slotId ?? this.slotId),
    paymentMethod: paymentMethod ?? this.paymentMethod,
    substitutionPreference: substitutionPreference ?? this.substitutionPreference,
    notes: notes ?? this.notes,
    preview: clearPreview ? null : (preview ?? this.preview),
    placing: placing ?? this.placing,
    placeError: clearPlaceError ? null : (placeError ?? this.placeError),
    priceChanged: clearPriceChanged ? null : (priceChanged ?? this.priceChanged),
    placedOrder: placedOrder ?? this.placedOrder,
  );
}

/// The delivery slot the customer tapped in the cart (D88). Checkout starts with it when the chosen address
/// offers the same slot; the server checks it again in the preview. Cleared when an order is placed.
final preferredSlotProvider = NotifierProvider<PreferredSlotController, String?>(PreferredSlotController.new);

class PreferredSlotController extends Notifier<String?> {
  @override
  String? build() => null;

  void choose(String? slotId) => state = slotId;
}

/// Orders placed in this run of the app, by id (MO-4): the order-placed screen shows the `POST /orders` answer at
/// once and keeps showing it when the read that follows fails (a cold server, offline).
final placedOrdersProvider = NotifierProvider<PlacedOrdersController, Map<String, OrderDetail>>(
  PlacedOrdersController.new,
);

class PlacedOrdersController extends Notifier<Map<String, OrderDetail>> {
  @override
  Map<String, OrderDetail> build() => const {};

  void remember(OrderDetail order) => state = {...state, order.id: order};
}

/// A place-order attempt whose outcome is unknown (network error, 5xx), kept outside the checkout screen (MO-3):
/// coming back to checkout with the same order body reuses its Idempotency-Key, so the server answers with the
/// order it may already have made instead of making a second one.
class PendingPlace {
  const PendingPlace({required this.key, required this.body});

  final String key;
  final String body;
}

final pendingPlaceProvider = NotifierProvider<PendingPlaceController, PendingPlace?>(PendingPlaceController.new);

class PendingPlaceController extends Notifier<PendingPlace?> {
  @override
  PendingPlace? build() => null;

  void set(PendingPlace? pending) => state = pending;
}

final checkoutProvider = NotifierProvider.autoDispose<CheckoutController, CheckoutState>(CheckoutController.new);

/// Refusals about the chosen slot: the slot list is read again so the customer sees what is still open.
const _slotRefusals = {'SLOT_FULL', 'SLOT_CUTOFF_PASSED', 'SLOT_NOT_AVAILABLE'};

class CheckoutController extends Notifier<CheckoutState> {
  String? _idempotencyKey;

  /// The request body [_idempotencyKey] was made for.
  String? _keyBody;
  int _previewSeq = 0;

  /// The key of the current place-order action (exposed for tests and diagnostics).
  String? get idempotencyKey => _idempotencyKey;

  @override
  CheckoutState build() => const CheckoutState();

  Future<void> selectAddress(String addressId, {required String pincode}) async {
    if (addressId == state.addressId) return;
    _idempotencyKey = null;
    final samePincode = pincode == state.addressPincode;
    state = state.copyWith(
      addressId: addressId,
      addressPincode: pincode,
      clearSlot: !samePincode, // slots belong to the address's location
      clearPreview: true,
      clearPlaceError: true,
    );
    await refreshPreview();
  }

  /// The chosen address was edited (from checkout): its pincode may have moved to another store, so the slot is
  /// kept only for the same pincode, and the server prices the order again.
  Future<void> addressEdited(String addressId, {required String pincode}) async {
    if (addressId != state.addressId) return;
    _idempotencyKey = null;
    final samePincode = pincode == state.addressPincode;
    state = state.copyWith(addressPincode: pincode, clearSlot: !samePincode, clearPreview: true, clearPlaceError: true);
    await refreshPreview();
  }

  Future<void> selectSlot(String slotId) async {
    if (slotId == state.slotId) return;
    _idempotencyKey = null;
    state = state.copyWith(slotId: slotId, clearPlaceError: true);
    await refreshPreview();
  }

  Future<void> selectPayment(String method) async {
    if (method == state.paymentMethod) return;
    _idempotencyKey = null;
    state = state.copyWith(paymentMethod: method, clearPlaceError: true);
    await refreshPreview();
  }

  void setSubstitutionPreference(String value) {
    _idempotencyKey = null;
    state = state.copyWith(substitutionPreference: value);
  }

  void setNotes(String notes) {
    if (notes == state.notes) return;
    _idempotencyKey = null;
    state = state.copyWith(notes: notes);
  }

  Future<void> refreshPreview() async {
    final selection = state.selection;
    if (selection == null) return;
    final seq = ++_previewSeq;
    state = state.copyWith(preview: const AsyncLoading<CheckoutPreview>());
    try {
      final preview = await ref.read(orderRepositoryProvider).preview(selection);
      if (seq != _previewSeq || !ref.mounted) return;
      state = state.copyWith(preview: AsyncData(preview));
    } catch (e, st) {
      if (seq != _previewSeq || !ref.mounted) return;
      state = state.copyWith(preview: AsyncError<CheckoutPreview>(ApiError.from(e), st));
    }
  }

  /// Places the order with the last preview's expected total. Returns the order, or null when refused,
  /// failed, or a price change needs the customer's confirmation ([CheckoutState.priceChanged]).
  Future<OrderDetail?> placeOrder() async {
    final preview = state.preview?.value;
    if (preview == null || !state.canPlace) return null;
    return _place(preview.expectedTotalPaise);
  }

  /// The customer accepted the new price from PRICE_CHANGED: place again with that preview's expected total.
  Future<OrderDetail?> acceptNewPrice() async {
    final changed = state.priceChanged;
    if (changed == null) return null;
    _idempotencyKey = null;
    state = state.copyWith(preview: AsyncData(changed), clearPriceChanged: true);
    if (!changed.canPlaceOrder) return null;
    return _place(changed.expectedTotalPaise);
  }

  /// The customer declined the new price: keep the new preview on screen without placing.
  void declineNewPrice() {
    final changed = state.priceChanged;
    if (changed == null) return;
    _idempotencyKey = null;
    state = state.copyWith(preview: AsyncData(changed), clearPriceChanged: true);
  }

  Future<OrderDetail?> _place(int expectedTotalPaise) async {
    final selection = state.selection;
    if (selection == null || state.placing) return null;
    final request = PlaceOrderRequest(
      selection: selection,
      expectedTotalPaise: expectedTotalPaise,
      customerNotes: state.notes.trim().isEmpty ? null : state.notes.trim(),
    );
    final body = jsonEncode(request.toJson());
    // An earlier attempt with this same body ended with an unknown outcome (possibly on a checkout screen that was
    // closed since): reuse its key so the server replays the order it may have made (MO-3).
    final pending = ref.read(pendingPlaceProvider);
    if (pending != null && pending.body == body) {
      _idempotencyKey = pending.key;
      _keyBody = body;
    }
    if (_idempotencyKey == null || _keyBody != body) {
      _idempotencyKey = newIdempotencyKey();
      _keyBody = body;
    }
    final pendingPlace = ref.read(pendingPlaceProvider.notifier);
    pendingPlace.set(PendingPlace(key: _idempotencyKey!, body: body));
    // The controller stays alive until the answer arrives, even if the screen goes away, so the cart, the orders
    // and the preferred slot are always refreshed after an order is placed (MO-3).
    final keepAlive = ref.keepAlive();
    state = state.copyWith(placing: true, clearPlaceError: true);
    try {
      final order = await ref.read(orderRepositoryProvider).placeOrder(request, idempotencyKey: _idempotencyKey!);
      _idempotencyKey = null;
      pendingPlace.set(null);
      ref.read(placedOrdersProvider.notifier).remember(order);
      ref.invalidate(cartProvider);
      ref.invalidate(ordersProvider);
      ref.read(preferredSlotProvider.notifier).choose(null);
      state = state.copyWith(placing: false, placedOrder: order);
      return order;
    } on ApiError catch (e) {
      if (!e.isTransient) pendingPlace.set(null);
      if (!ref.mounted) return null;
      if (e.code == 'PRICE_CHANGED') {
        _idempotencyKey = null;
        final newPreview = _previewFrom(e);
        if (newPreview != null) {
          state = state.copyWith(placing: false, priceChanged: newPreview);
        } else {
          state = state.copyWith(placing: false, placeError: e);
          await refreshPreview();
        }
        return null;
      }
      if (!e.isTransient) _idempotencyKey = null;
      if (_slotRefusals.contains(e.code) && state.addressPincode != null) {
        ref.invalidate(deliverySlotsProvider(state.addressPincode!));
      }
      state = state.copyWith(placing: false, placeError: e);
      if (!e.isTransient) await refreshPreview(); // show the server's current blockers
      return null;
    } finally {
      keepAlive.close();
    }
  }

  static CheckoutPreview? _previewFrom(ApiError e) {
    final raw = e.extensions['preview'];
    if (raw == null) return null;
    try {
      return parseObject(raw, (Json j) => CheckoutPreview.fromJson(j));
    } on ApiError {
      return null;
    }
  }
}
