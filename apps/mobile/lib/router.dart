// Navigation map (docs/design/mobile.md): bottom navigation Home · Shop · Cart · Orders · Account as a
// StatefulShellRoute (each tab keeps its stack); full-screen routes for search, product, login, checkout and
// order success. Checkout and account pages that need a customer redirect to login and come back.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'features/catalog/home_screen.dart';
import 'features/catalog/product_screen.dart';
import 'features/catalog/search_screen.dart';
import 'features/catalog/shop_screen.dart';
import 'features/catalog/wishlist_screen.dart';
import 'features/onboarding/location_screen.dart';
import 'features/onboarding/onboarding_screen.dart';
import 'features/onboarding/splash_screen.dart';
import 'state/cart_state.dart';
import 'state/session.dart';

/// Paths that need a signed-in customer.
bool _needsLogin(String path) =>
    path.startsWith('/checkout') ||
    path.startsWith('/orders/') ||
    path.startsWith('/account/') && !path.startsWith('/account/help') ||
    path.startsWith('/wishlist');

final routerProvider = Provider<GoRouter>((ref) {
  final sessionChanges = ValueNotifier<bool>(ref.read(sessionProvider).signedIn);
  ref.listen(sessionProvider, (_, next) => sessionChanges.value = next.signedIn);
  ref.onDispose(sessionChanges.dispose);

  return GoRouter(
    initialLocation: '/splash',
    refreshListenable: sessionChanges,
    redirect: (context, state) {
      final signedIn = ref.read(sessionProvider).signedIn;
      final path = state.uri.path;
      if (!signedIn && _needsLogin(path)) {
        return Uri(path: '/login', queryParameters: {'from': state.uri.toString()}).toString();
      }
      return null;
    },
    routes: [
      GoRoute(path: '/splash', builder: (_, _) => const SplashScreen()),
      GoRoute(path: '/onboarding', builder: (_, _) => const OnboardingScreen()),
      GoRoute(
        path: '/location',
        builder: (_, state) => LocationScreen(changing: state.uri.queryParameters['change'] == '1'),
      ),
      GoRoute(path: '/search', builder: (_, state) => SearchScreen(initialQuery: state.uri.queryParameters['q'])),
      GoRoute(path: '/product/:slug', builder: (_, state) => ProductScreen(slug: state.pathParameters['slug']!)),
      GoRoute(path: '/wishlist', builder: (_, _) => const WishlistScreen()),
      StatefulShellRoute.indexedStack(
        builder: (context, state, shell) => AppShell(shell: shell),
        branches: [
          StatefulShellBranch(routes: [GoRoute(path: '/home', builder: (_, _) => const HomeScreen())]),
          StatefulShellBranch(
            routes: [
              GoRoute(path: '/shop', builder: (_, state) => ShopScreen(category: state.uri.queryParameters['category'])),
            ],
          ),
          for (final tab in const ['/cart', '/orders', '/account'])
            StatefulShellBranch(routes: [GoRoute(path: tab, builder: (_, _) => _Pending(tab))]),
        ],
      ),
    ],
  );
});

class AppShell extends ConsumerWidget {
  const AppShell({super.key, required this.shell});

  final StatefulNavigationShell shell;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final cartCount = ref.watch(cartLineCountProvider) ?? 0;
    return Scaffold(
      body: shell,
      bottomNavigationBar: NavigationBar(
        selectedIndex: shell.currentIndex,
        onDestinationSelected: (i) => shell.goBranch(i, initialLocation: i == shell.currentIndex),
        destinations: [
          const NavigationDestination(icon: Icon(Icons.home_outlined), selectedIcon: Icon(Icons.home), label: 'Home'),
          const NavigationDestination(
            icon: Icon(Icons.storefront_outlined),
            selectedIcon: Icon(Icons.storefront),
            label: 'Shop',
          ),
          NavigationDestination(
            icon: Badge(
              isLabelVisible: cartCount > 0,
              label: Text('$cartCount'),
              child: const Icon(Icons.shopping_basket_outlined),
            ),
            selectedIcon: Badge(
              isLabelVisible: cartCount > 0,
              label: Text('$cartCount'),
              child: const Icon(Icons.shopping_basket),
            ),
            label: 'Cart',
            tooltip: cartCount > 0 ? 'Cart, $cartCount items' : 'Cart',
          ),
          const NavigationDestination(
            icon: Icon(Icons.receipt_long_outlined),
            selectedIcon: Icon(Icons.receipt_long),
            label: 'Orders',
          ),
          const NavigationDestination(icon: Icon(Icons.person_outline), selectedIcon: Icon(Icons.person), label: 'Account'),
        ],
      ),
    );
  }
}

/// Temporary tab body until the screen group lands (removed in the same series of commits).
class _Pending extends StatelessWidget {
  const _Pending(this.path);
  final String path;

  @override
  Widget build(BuildContext context) => Scaffold(body: Center(child: Text(path)));
}
