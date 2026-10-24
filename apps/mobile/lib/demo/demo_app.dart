// Demo entry point and the demo panel on the Account tab. Only reachable when built with --dart-define=DEMO=true
// (see main.dart); the panel lets the owner switch on empty lists or offline and jump to the rarer screens.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../api/token_store.dart';
import '../app.dart';
import '../core/app_prefs.dart';
import '../state/account_state.dart';
import '../state/cart_state.dart';
import '../state/catalog_state.dart';
import '../state/core_providers.dart';
import '../state/location_state.dart';
import '../state/orders_state.dart';
import '../widgets/illustrated.dart';
import 'demo_backend.dart';
import 'demo_data.dart';

/// The demo backend, or null in a normal build.
final demoBackendProvider = Provider<DemoBackend?>((ref) => null);

Future<void> runDemoApp() async {
  final backend = DemoBackend();
  // Starts at onboarding, signed in, so every screen is one tap away; sign out to see the OTP login.
  final tokens = TokenStore(MemorySecretStore({'fo.refreshToken': 'demo-refresh'}));
  await tokens.load();
  runApp(
    ProviderScope(
      overrides: [
        appPrefsProvider.overrideWithValue(MemoryAppPrefs()),
        tokenStoreProvider.overrideWithValue(tokens),
        apiBaseUrlProvider.overrideWithValue('demo'),
        authRepositoryProvider.overrideWithValue(backend.auth),
        locationRepositoryProvider.overrideWithValue(backend.locations),
        catalogRepositoryProvider.overrideWithValue(backend.catalog),
        cartRepositoryProvider.overrideWithValue(backend.cart),
        orderRepositoryProvider.overrideWithValue(backend.orders),
        accountRepositoryProvider.overrideWithValue(backend.account),
        demoBackendProvider.overrideWithValue(backend),
      ],
      retry: (_, _) => null,
      child: const FreshOrbitApp(),
    ),
  );
}

class DemoPanel extends ConsumerWidget {
  const DemoPanel({super.key});

  void _reload(WidgetRef ref) {
    ref
      ..invalidate(homeProvider)
      ..invalidate(categoriesProvider)
      ..invalidate(productListProvider)
      ..invalidate(productDetailProvider)
      ..invalidate(searchSuggestionsProvider)
      ..invalidate(cartProvider)
      ..invalidate(ordersProvider)
      ..invalidate(orderDetailProvider)
      ..invalidate(orderTrackingProvider)
      ..invalidate(wishlistProvider)
      ..invalidate(inboxProvider)
      ..invalidate(addressesProvider)
      ..invalidate(profileProvider)
      ..invalidate(currentServiceabilityProvider);
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final backend = ref.watch(demoBackendProvider);
    if (backend == null) return const SizedBox.shrink();
    final theme = Theme.of(context);
    Widget toggle(String title, String subtitle, ValueNotifier<bool> value) => ValueListenableBuilder<bool>(
      valueListenable: value,
      builder: (context, on, _) => SwitchListTile(
        title: Text(title),
        subtitle: Text(subtitle),
        value: on,
        onChanged: (v) {
          value.value = v;
          _reload(ref);
        },
      ),
    );
    Widget jump(String label, String path) => ActionChip(label: Text(label), onPressed: () => context.push(path));
    return Padding(
      padding: const EdgeInsets.all(FreshSpacing.s4),
      child: Card(
        child: Padding(
          padding: const EdgeInsets.all(FreshSpacing.s4),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Sticker(label: 'Demo', tone: StickerTone.mango),
              const SizedBox(height: FreshSpacing.s2),
              Text(
                'Sample data, no server. Pincode $demoUnservedPincode shows the not-serviceable state; any '
                'other 6-digit pincode is served.',
                style: theme.textTheme.bodyMedium,
              ),
              toggle(
                'Show empty states',
                'Empty cart, orders, wishlist, notifications, addresses, search',
                backend.switches.empty,
              ),
              toggle('Pretend to be offline', 'Every load fails with the offline state', backend.switches.offline),
              const SizedBox(height: FreshSpacing.s2),
              Wrap(
                spacing: FreshSpacing.s2,
                runSpacing: FreshSpacing.s2,
                children: [
                  jump('Onboarding', '/onboarding'),
                  jump('Location', '/location?change=1'),
                  jump('Order placed', '/order-success/o1'),
                  jump('Packing', '/orders/o2'),
                  jump('On the way', '/orders/o3'),
                  jump('Delivered', '/orders/o4'),
                  jump('Cancelled', '/orders/o5'),
                  jump('Sold out product', '/product/squid'),
                  jump('Help', '/account/help'),
                  jump('Page not found', '/no-such-page'),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}
