// Checkout (docs/api §4.8): choose address, slot, payment → server preview (blockers, totals,
// expectedTotalPaise) → place order with one Idempotency-Key per user action.
//
// Idempotency: the key is created when the customer taps "Place order" and reused only while the outcome is
// unknown (network error, 5xx, IDEMPOTENCY_IN_PROGRESS, CONCURRENT_SAVE). A definitive refusal (4xx) is
// replayed by the server for the same key, so the next attempt gets a new key. Any change of selection or an
// accepted new price also starts a new key (same key with a different body → IDEMPOTENCY_KEY_REUSED).
//
// PRICE_CHANGED (409): the problem's `preview` member is shown to the customer; accepting places the order
// again with the new `expectedTotalPaise` from that preview. The app never computes the difference itself.

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/api_client.dart';
import '../api/api_error.dart';
import '../api/models/checkout.dart';
import '../api/models/json.dart';
import '../api/models/orders.dart';
import 'cart_state.dart';
import 'core_providers.dart';
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
  }) =>
      CheckoutState(
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

final checkoutProvider = NotifierProvider.autoDispose<CheckoutController, CheckoutState>(CheckoutController.new);

class CheckoutController extends Notifier<CheckoutState> {
  String? _idempotencyKey;
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
    _idempotencyKey ??= newIdempotencyKey();
    state = state.copyWith(placing: true, clearPlaceError: true);
    try {
      final order = await ref.read(orderRepositoryProvider).placeOrder(
            PlaceOrderRequest(
              selection: selection,
              expectedTotalPaise: expectedTotalPaise,
              customerNotes: state.notes.trim().isEmpty ? null : state.notes.trim(),
            ),
            idempotencyKey: _idempotencyKey!,
          );
      _idempotencyKey = null;
      if (!ref.mounted) return order;
      state = state.copyWith(placing: false, placedOrder: order);
      ref.invalidate(cartProvider);
      ref.invalidate(ordersProvider);
      return order;
    } on ApiError catch (e) {
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
      state = state.copyWith(placing: false, placeError: e);
      if (!e.isTransient) await refreshPreview(); // show the server's current blockers
      return null;
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
