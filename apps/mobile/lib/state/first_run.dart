// First run after onboarding (D277, D278): where a customer goes once the first-run sign-in is done.

import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'account_state.dart';
import 'cart_state.dart';
import 'core_providers.dart';
import 'location_state.dart';

/// The page after the first-run sign-in (D278): `/home` when a delivery pincode is known, else `/location`.
///
/// A pincode already on the device wins. Otherwise the customer's saved addresses are tried, the default one
/// first: the first pincode the server says is serviceable becomes the delivery pincode (the same store the
/// Location screen writes). The app never decides serviceability itself; an address the server already marked
/// not serviceable is skipped. When the cart holds lines for another pincode, the Location screen asks
/// instead, because moving the cart may remove items. Any failure falls back to asking for the pincode.
Future<String> pageAfterFirstSignIn(WidgetRef ref) async {
  if (ref.read(pincodeProvider) != null) return '/home';
  try {
    final addresses = await ref.read(addressesProvider.future);
    final ordered = [...addresses.where((a) => a.isDefault), ...addresses.where((a) => !a.isDefault)];
    final pincodes = <String>{
      for (final a in ordered)
        if (a.isServiceable != false) a.pincode,
    };
    final locations = ref.read(locationRepositoryProvider);
    for (final pincode in pincodes) {
      final check = await locations.serviceability(pincode);
      if (!check.serviceable) continue;
      final cart = await ref.read(cartProvider.future);
      if (!cart.isEmpty && cart.pincode != null && cart.pincode != check.pincode) return '/location';
      await ref.read(pincodeProvider.notifier).set(check.pincode);
      return '/home';
    }
  } catch (_) {
    // Could not read the addresses or the serviceability: ask for the pincode instead of guessing.
  }
  return '/location';
}
