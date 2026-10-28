// First run after onboarding (D277, D278): where a customer goes once the first-run sign-in is done.
// "Tell us about you" (D290): a sign-in whose account has no name opens the details step over the page it lands on.

import 'dart:async';

import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

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

/// The "Tell us about you" step (D290).
const aboutYouPath = '/about-you';

/// Pages the details step never opens over: the sign-in itself (it is still leaving), the step, and the launch
/// pages before it.
const _notOver = {'/login', aboutYouPath, '/splash', '/onboarding'};

/// Opens "Tell us about you" (D290) once after a sign-in whose account has no name: when [isDue] says so, the
/// first page the router shows after leaving the sign-in gets the step pushed over it, and [onAsked] clears the
/// flag. Pushing over the page (instead of changing where the sign-in goes) covers every way out of the sign-in
/// without touching it: the first-run destination (home or the pincode screen, D278), the page that needed
/// sign-in (`?from=/checkout`, D267) and a pop back to the screen that pushed the login. Saving the name pops the
/// step, so the customer is where the sign-in would have taken them; Back pops it too, never traps. Returns the
/// function that stops listening.
VoidCallback askAboutYouAfterSignIn(
  GoRouter router, {
  required bool Function() isDue,
  required VoidCallback onAsked,
}) {
  void check() {
    if (!isDue() || router.routerDelegate.currentConfiguration.isEmpty) return;
    if (_notOver.contains(router.state.uri.path)) return;
    onAsked();
    // After this navigation has built its page: the step goes on top of it.
    WidgetsBinding.instance.addPostFrameCallback((_) => unawaited(router.push<void>(aboutYouPath)));
    WidgetsBinding.instance.ensureVisualUpdate();
  }

  router.routerDelegate.addListener(check);
  return () => router.routerDelegate.removeListener(check);
}
