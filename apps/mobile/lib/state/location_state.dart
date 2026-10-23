// The customer's delivery pincode (stored on the device) and its serviceability from the server.

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/models/locations.dart';
import 'core_providers.dart';

final pincodeProvider = NotifierProvider<PincodeController, String?>(PincodeController.new);

class PincodeController extends Notifier<String?> {
  @override
  String? build() => ref.read(appPrefsProvider).pincode;

  Future<void> set(String pincode) async {
    await ref.read(appPrefsProvider).setPincode(pincode);
    state = pincode;
  }
}

/// Serviceability of any pincode (Location screen check). Always 200 from the server; `serviceable` decides.
final serviceabilityProvider = FutureProvider.autoDispose.family<Serviceability, String>(
  (ref, pincode) => ref.read(locationRepositoryProvider).serviceability(pincode),
);

/// Serviceability of the stored pincode (home banner: location, fees, next slot). Null without a pincode.
final currentServiceabilityProvider = FutureProvider<Serviceability?>((ref) async {
  final pincode = ref.watch(pincodeProvider);
  if (pincode == null) return null;
  return ref.read(locationRepositoryProvider).serviceability(pincode);
});

/// Delivery slots for a delivery address's pincode (checkout).
final deliverySlotsProvider = FutureProvider.autoDispose.family<DeliverySlots, String>(
  (ref, pincode) => ref.read(locationRepositoryProvider).deliverySlots(pincode: pincode),
);
